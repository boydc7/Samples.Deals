using System.Reflection;
using Hangfire;
using Rydr.ActiveCampaign;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.Messaging;
using StringExtensions = ServiceStack.StringExtensions;

namespace Rydr.Api.Core.Services.Internal;

public class GenericDeferredRequestService : IDeferredRequestProcessingService, IDeferRequestsService
{
    private static readonly Assembly _dtoAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWithIgnoreCase("Rydr.Api.Dto"));

    private static readonly MethodInfo _fromJsvMethod = typeof(StringExtensions).GetMethod("FromJsv", new[]
                                                                                                      {
                                                                                                          typeof(string)
                                                                                                      });

    private readonly ILog _log;
    private readonly IMessageFactory _msgFactory;
    private readonly IRequestStateManager _requestStateManager;
    private readonly ITaskExecuter _taskExecuter;

    public GenericDeferredRequestService(IMessageFactory msgFactory, IRequestStateManager requestStateManager, ITaskExecuter taskExecuter)
    {
        _msgFactory = msgFactory;
        _requestStateManager = requestStateManager;
        _taskExecuter = taskExecuter;
        _log = LogManager.GetLogger(nameof(GenericDeferredRequestService));
    }

    public object DeserializeDto<T>(T message)
        where T : PostDeferredBase
    {
        try
        {
            var dtoType = _dtoAssembly.GetType(message.Type, true, false);

            var fromJsvGeneric = _fromJsvMethod.MakeGenericMethod(dtoType);

            return fromJsvGeneric.Invoke(null, new object[]
                                               {
                                                   message.Dto
                                               });
        }
        catch(Exception ex)
        {
            _log.Exception(ex, $"Could not DeserializeDto for type [{message.Type}], DTO [{message.Dto.ToJsv()}]");

            throw;
        }
    }

    /// <summary>
    ///     Defers a request via a DEDICATED queue for the type in question
    ///     Type/DTO must register a message handler for use in MessagingConfiguration
    /// </summary>
    public void PublishMessage<T>(T dto)
        where T : RequestBase
    {
        var state = _requestStateManager.GetState();

        UpdateMessage(dto, state);

        DoPublishMessageToQueue(dto);
    }

    /// <summary>
    ///     Defers a request via the SHARED message queue - shared across all DTOs that publish to this queue
    ///     priority
    ///     No type/DTO registration required, uses a shared queue
    /// </summary>
    public void DeferRequest<T>(T request)
        where T : RequestBase
    {
        var deferredMessage = GetDeferredMessage(request);

        DoPublishMessageToQueue(deferredMessage);
    }

    public void DeferLowPriRequest<T>(T request)
        where T : RequestBase
    {
        var deferredMessage = GetDeferredLowMessage(request);

        DoPublishMessageToQueue(deferredMessage);
    }

    public void DeferFifoRequest<T>(T request)
        where T : RequestBase
    {
        var deferredMessage = GetDeferredFifoMessage(request);

        DoPublishMessageToQueue(deferredMessage);
    }

    public void DeferDealRequest<T>(T request)
        where T : RequestBase
    {
        var deferredMessage = GetDeferredDealMessage(request);

        DoPublishMessageToQueue(deferredMessage);
    }

    public void DeferPrimaryDealRequest<T>(T request)
        where T : RequestBase
    {
        var deferredMessage = GetDeferredPrimaryDealMessage(request);

        DoPublishMessageToQueue(deferredMessage);
    }

    public void DeferRequestScheduled<T>(T request, DateTime runAt)
        where T : RequestBase
    {
        Guard.AgainstArgumentOutOfRange(runAt < DateTimeHelper.UtcNow || runAt > DateTimeHelper.MaxApplicationDate, nameof(runAt));

        var deferredMessage = GetDeferredMessage(request);

        BackgroundJob.Schedule(() => DoPublishMessageToQueue(deferredMessage), runAt);
    }

    public void DeferRequestScheduled<T>(T request, TimeSpan delay)
        where T : RequestBase
    {
        Guard.AgainstArgumentOutOfRange(delay.TotalDays > 750, nameof(delay));

        var deferredMessage = GetDeferredMessage(request);

        BackgroundJob.Schedule(() => DoPublishMessageToQueue(deferredMessage), delay);
    }

    public void PublishMessageScheduled<T>(T dto, DateTime runAt)
        where T : RequestBase
    {
        Guard.AgainstArgumentOutOfRange(runAt < DateTimeHelper.UtcNow || runAt > DateTimeHelper.MaxApplicationDate, nameof(runAt));

        var state = _requestStateManager.GetState();

        UpdateMessage(dto, state);

        BackgroundJob.Schedule(() => DoPublishMessageToQueue(dto), runAt);
    }

    public void PublishMessageScheduled<T>(T dto, TimeSpan delay)
        where T : RequestBase
    {
        Guard.AgainstArgumentOutOfRange(delay.TotalDays > 750, nameof(delay));

        var state = _requestStateManager.GetState();

        UpdateMessage(dto, state);

        BackgroundJob.Schedule(() => DoPublishMessageToQueue(dto), delay);
    }

    public void DeferRequestRecurring<T>(T request, string cronString, string jobId = null)
        where T : RequestBase
    {
        Guard.AgainstNullArgument(cronString.IsNullOrEmpty(), nameof(cronString));

        var deferredMessage = GetDeferredMessage(request);

        RecurringJob.AddOrUpdate(jobId.ToNullIfEmpty(), () => DoPublishMessageToQueue(deferredMessage), cronString);
    }

    public void PublishMessageRecurring<T>(T dto, string cronString, string jobId = null)
        where T : RequestBase
    {
        Guard.AgainstNullArgument(cronString.IsNullOrEmpty(), nameof(cronString));

        var state = _requestStateManager.GetState();

        UpdateMessage(dto, state);

        RecurringJob.AddOrUpdate(jobId.ToNullIfEmpty(), () => DoPublishMessageToQueue(dto), cronString);
    }

    public void RemoveRecurringJob(string jobId)
        => RecurringJob.RemoveIfExists(jobId);

    private void UpdateMessage<T>(T request, IRequestState state)
        where T : RequestBase
    {
        request.UserId = request.UserId.Gz(state.UserId).Gz(UserAuthInfo.AdminUserId);
        request.WorkspaceId = request.WorkspaceId.Gz(state.WorkspaceId).Gz(UserAuthInfo.AdminWorkspaceId);
        request.RoleId = request.RoleId.Gz(state.RoleId);
        request.RequestPublisherAccountId = request.RequestPublisherAccountId.Gz(state.RequestPublisherAccountId);
        request.IsSystemRequest = request.IsSystemRequest || state.IsSystemRequest;
    }

    // NOTE: Keep this method public...for Hangfire use...it is not part of the interface...
    // ReSharper disable once MemberCanBePrivate.Global
    public void DoPublishMessageToQueue<T>(T msg)
        where T : RequestBase
        => DoPublishMessageToQueue(msg, false);

    // NOTE: Keep this method public...for Hangfire use...it is not part of the interface...
    // ReSharper disable once MemberCanBePrivate.Global
    public void DoPublishMessageToQueue<T>(T msg, bool keepThisForSerializationPurposesButItIsNotUsed)
        where T : RequestBase
    {
        var msgToQueue = new Message<T>(msg)
                         {
                             RetryAttempts = MessagingConfiguration.DefaultRetryCount
                         };

        try
        {
            DoPublishWrappedMessageAsync(msgToQueue).GetAwaiter().GetResult();

            return;
        }
        catch(Exception x)
        {
            _log.Warn($"Could not publish to MQ, deferring locally. Message [{msg.ToJsv().Left(500)}]", x);
        }

        // Could not defer the message to the distributed producer, defer it locally to try and get it processed
        _taskExecuter.ExecAsync(msgToQueue, DoPublishWrappedMessageAsync, true, maxAttempts: 35);
    }

    private async Task DoPublishWrappedMessageAsync<T>(Message<T> msgToPublish)
    {
        Exception lastEx = null;
        var publishAttempts = 0;

        do
        {
            try
            {
                using(var msgProducer = _msgFactory.CreateMessageProducer())
                {
                    msgProducer.Publish(msgToPublish);

                    // Success, all done
                    return;
                }
            }
            catch(Exception x) when(_log.LogExceptionReturnTrue(x))
            {
                lastEx = x;

                // Purposely not using a backoff here, just pause for short delay on each iteration
                await Task.Delay(250);
            }

            publishAttempts++;
        } while (publishAttempts < 3);

        throw lastEx ?? new ApplicationException("Unknown error inDoPublishWrappedMessageAsync");
    }

    private PostDeferredLowPriMessage GetDeferredLowMessage<T>(T request)
        where T : RequestBase
    {
        var state = _requestStateManager.GetState();

        UpdateMessage(request, state);

        var deferredMessage = new PostDeferredLowPriMessage
                              {
                                  UserId = request.UserId,
                                  WorkspaceId = request.WorkspaceId,
                                  RoleId = request.RoleId,
                                  RequestPublisherAccountId = request.RequestPublisherAccountId,
                                  Dto = request.ToJsv(),
                                  Type = request.GetType().FullName,
                                  OriginatingRequestId = state.RequestId
                              };

        return deferredMessage;
    }

    private PostDeferredMessage GetDeferredMessage<T>(T request)
        where T : RequestBase
    {
        var state = _requestStateManager.GetState();

        UpdateMessage(request, state);

        var deferredMessage = new PostDeferredMessage
                              {
                                  UserId = request.UserId,
                                  WorkspaceId = request.WorkspaceId,
                                  RoleId = request.RoleId,
                                  RequestPublisherAccountId = request.RequestPublisherAccountId,
                                  Dto = request.ToJsv(),
                                  Type = request.GetType().FullName,
                                  OriginatingRequestId = state.RequestId
                              };

        return deferredMessage;
    }

    private PostDeferredPrimaryDealMessage GetDeferredPrimaryDealMessage<T>(T request)
        where T : RequestBase
    {
        var state = _requestStateManager.GetState();

        UpdateMessage(request, state);

        var deferredMessage = new PostDeferredPrimaryDealMessage
                              {
                                  UserId = request.UserId,
                                  WorkspaceId = request.WorkspaceId,
                                  RoleId = request.RoleId,
                                  RequestPublisherAccountId = request.RequestPublisherAccountId,
                                  Dto = request.ToJsv(),
                                  Type = request.GetType().FullName,
                                  OriginatingRequestId = state.RequestId
                              };

        return deferredMessage;
    }

    private PostDeferredDealMessage GetDeferredDealMessage<T>(T request)
        where T : RequestBase
    {
        var state = _requestStateManager.GetState();

        UpdateMessage(request, state);

        var deferredMessage = new PostDeferredDealMessage
                              {
                                  UserId = request.UserId,
                                  WorkspaceId = request.WorkspaceId,
                                  RoleId = request.RoleId,
                                  RequestPublisherAccountId = request.RequestPublisherAccountId,
                                  Dto = request.ToJsv(),
                                  Type = request.GetType().FullName,
                                  OriginatingRequestId = state.RequestId
                              };

        return deferredMessage;
    }

    private PostDeferredFifoMessage GetDeferredFifoMessage<T>(T request)
        where T : RequestBase
    {
        var state = _requestStateManager.GetState();

        UpdateMessage(request, state);

        var deferredMessage = new PostDeferredFifoMessage
                              {
                                  UserId = request.UserId,
                                  WorkspaceId = request.WorkspaceId,
                                  RoleId = request.RoleId,
                                  RequestPublisherAccountId = request.RequestPublisherAccountId,
                                  Dto = request.ToJsv(),
                                  Type = request.GetType().FullName,
                                  OriginatingRequestId = state.RequestId
                              };

        return deferredMessage;
    }
}
