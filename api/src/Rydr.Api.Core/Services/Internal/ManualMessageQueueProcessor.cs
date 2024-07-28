using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.Messaging;

namespace Rydr.Api.Core.Services.Internal;

public class ManualMessageQueueProcessor<T> : IMessageQueueProcessor
{
    private readonly ILog _log = LogManager.GetLogger("ManualMessageQueueProcessor");
    private readonly IMessageFactory _messageFactory;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IOpsNotificationService _opsNotificationService;
    private readonly IPersistentCounterAndListService _counterAndListService;
    private readonly IReadOnlyList<Func<IMessage<T>, long, MqProcessType>> _dlqProcessPredicates;

    public ManualMessageQueueProcessor(IMessageFactory messageFactory,
                                       IFileStorageProvider fileStorageProvider,
                                       IOpsNotificationService opsNotificationService,
                                       IPersistentCounterAndListService counterAndListService,
                                       IEnumerable<Func<IMessage<T>, long, MqProcessType>> dlqProcessPredicates = null)
    {
        _messageFactory = messageFactory;
        _fileStorageProvider = fileStorageProvider;
        _opsNotificationService = opsNotificationService;
        _counterAndListService = counterAndListService;
        _dlqProcessPredicates = dlqProcessPredicates?.AsListReadOnly().NullIfEmpty();
    }

    public async Task<MqRetryResponse> ReprocessDlqAsync(MqRetry request)
    {
        var ignored = 0;
        var archived = 0;
        var alert = 0;
        var mostRetryAttempts = 0L;

        var (attempted, failed) = await ProcessAsync(QueueNames<T>.Dlq,
                                                     async (mqc, msg) =>
                                                     {
                                                         if (msg.Tag.IsNullOrEmpty())
                                                         {
                                                             msg.Tag = msg.Id.ToStringId();
                                                         }

                                                         var msgCountKey = string.Concat(typeof(T).FullName, "_DlqReprocess_", msg.Tag).ToShaBase64();

                                                         var dlqMessageRetryCount = _counterAndListService.GetCounter(msgCountKey);

                                                         if (dlqMessageRetryCount > mostRetryAttempts)
                                                         {
                                                             mostRetryAttempts = dlqMessageRetryCount;
                                                         }

                                                         if (_dlqProcessPredicates != null)
                                                         {
                                                             var action = _dlqProcessPredicates.Max(p =>
                                                                                                    {
                                                                                                        var mqProcessType = p(msg, dlqMessageRetryCount);

                                                                                                        if (mqProcessType == MqProcessType.Alert)
                                                                                                        {
                                                                                                            alert++;
                                                                                                        }

                                                                                                        return (int)mqProcessType;
                                                                                                    });

                                                             switch ((MqProcessType)action)
                                                             {
                                                                 case MqProcessType.Alert:
                                                                     _log.InfoFormat("Alerting DLQ message for type [{0}] message [{1}]", typeof(T).Name, msg.Body.ToJsv().Left(250));

                                                                     alert++;

                                                                     // Alert - put it back from wence it came (returning false Naks the message right back onto the q)
                                                                     return false;

                                                                 case MqProcessType.Ignore:
                                                                     // Ignore, Ack and do not process
                                                                     ignored++;

                                                                     _counterAndListService.Clear(msgCountKey);

                                                                     return true;

                                                                 case MqProcessType.Archive:
                                                                     var fmd = new FileMetaData(Path.Combine(RydrFileStoragePaths.DlqDropPath, typeof(T).Name,
                                                                                                             msg.CreatedDate.ToString("yyyy-MM-dd"),
                                                                                                             msg.Error?.ErrorCode?.Left(25) ?? "Unknown"),
                                                                                                string.Concat(msg.CreatedDate.ToUnixTimestamp(), "-", msg.Id.ToStringId(), ".json"))
                                                                               {
                                                                                   Bytes = msg.ToJson().ToUtf8Bytes()
                                                                               };

                                                                     _log.InfoFormat("Archiving DLQ message for type [{0}] at [{1}]", typeof(T).Name, fmd.FullName);

                                                                     await _fileStorageProvider.StoreAsync(fmd, new FileStorageOptions
                                                                                                                {
                                                                                                                    ContentType = "application/json",
                                                                                                                    Encrypt = true,
                                                                                                                    StorageClass = FileStorageClass.Intelligent
                                                                                                                });

                                                                     archived++;

                                                                     _counterAndListService.Clear(msgCountKey);

                                                                     // Archived, Ack and do not process
                                                                     return true;

                                                                 default:
                                                                 case MqProcessType.Unspecified:
                                                                 case MqProcessType.Reprocess:
                                                                     // Break, normal processing...
                                                                     break;
                                                             }
                                                         }

                                                         // If we haven't returned by here, re-process away
                                                         _counterAndListService.Increment(msgCountKey);

                                                         mqc.Publish(msg);

                                                         return true;
                                                     },
                                                     request.Limit);

        if (alert > 0)
        {
            await _opsNotificationService.TrySendApiNotificationAsync($"DLQ Reprocessing for [{typeof(T).Name}] alerted [{alert} times",
                                                                      $"Most retry attempts: [{mostRetryAttempts}] \n Archived: [{archived}] \n <https://app.datadoghq.com/logs?live=true&query=\"DLQ%20message\"|DLQ Logs>");
        }
        else if (archived > 0)
        {
            await _opsNotificationService.TrySendApiNotificationAsync($"DLQ Reprocessing for [{typeof(T).Name}] archived [{archived} messages",
                                                                      $"Archived: [{archived}] \n Most retry attempts: [{mostRetryAttempts}] \n <https://app.datadoghq.com/logs?live=true&query=\"DLQ%20message\"|DLQ Logs>");
        }

        return new MqRetryResponse
               {
                   IgnoredCount = ignored,
                   ArchivedCount = archived,
                   AttemptCount = attempted,
                   FailCount = failed
               };
    }

    public async Task<MqRetryResponse> ProcessInqAsync(MqRetry request)
    {
        var (attempted, failed) = await ProcessAsync(QueueNames<T>.In, (c, m) =>
                                                                       {
                                                                           ServiceStackHost.Instance.ExecuteMessage(m);

                                                                           return Task.FromResult(true);
                                                                       }, request.Limit);

        return new MqRetryResponse
               {
                   AttemptCount = attempted,
                   FailCount = failed
               };
    }

    private async Task<(int Atempted, int Failed)> ProcessAsync(string queueName, Func<IMessageQueueClient, IMessage<T>, Task<bool>> handler, int limit = 0)
    {
        var attempted = 0;
        var failed = 0;

        if (limit <= 0)
        {
            limit = int.MaxValue;
        }

        using(var mqClient = _messageFactory.CreateMessageQueueClient())
        {
            do
            {
                IMessage<T> msg = null;

                try
                {
                    msg = mqClient.Get<T>(queueName, TimeSpan.FromSeconds(2));

                    if (msg == null)
                    {
                        break;
                    }

                    var ack = await handler(mqClient, msg);

                    if (ack)
                    {
                        mqClient.Ack(msg);
                    }
                    else
                    {
                        mqClient.Nak(msg, false);
                    }
                }
                catch(TimeoutException)
                {
                    // Nothing to do, all done
                    break;
                }
                catch(Exception x)
                {
                    _log.Exception(x, $"Manual message queue processing failed for Queue [{queueName}], message [{msg?.GetBody().ToJsv().Left(250) ?? "NO-BODY"}]");

                    if (msg != null)
                    {
                        mqClient.Nak(msg, false);
                    }

                    failed++;
                }

                attempted++;
            } while (attempted < limit);
        }

        return (attempted, failed);
    }
}
