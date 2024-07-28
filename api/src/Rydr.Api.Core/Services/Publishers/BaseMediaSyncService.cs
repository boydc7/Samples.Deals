using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Enums;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Publishers;

public abstract class BaseMediaSyncService : IPublisherMediaSyncService
{
    private static readonly IPublisherAccountService _publisherAccountService = RydrEnvironment.Container.Resolve<IPublisherAccountService>();

    protected readonly ILog _log;

    static BaseMediaSyncService()
    {
        var minutes = RydrEnvironment.GetAppSetting("PublisherAccount.SyncIntervalMinutes", 60);

        if (minutes > 59)
        {
            minutes = ((int)Math.Round(minutes / 60.0, MidpointRounding.ToEven)) * 60;

            if (minutes > 1380)
            {
                minutes = 1380;
            }
        }

        PublisherAccountSyncIntervalMinutes = minutes;
    }

    public static int PublisherAccountSyncIntervalMinutes { get; }

    protected readonly IDeferRequestsService _deferRequestsService;
    protected readonly IPocoDynamo _dynamoDb;

    protected BaseMediaSyncService(IDeferRequestsService deferRequestsService, IPocoDynamo dynamoDb)
    {
        _deferRequestsService = deferRequestsService;
        _dynamoDb = dynamoDb;

        _log = LogManager.GetLogger(GetType());
    }

    public abstract PublisherType PublisherType { get; }

    protected abstract Task<List<long>> DoSyncMediaAsync(IEnumerable<string> fbMediaIds, DynPublisherAppAccount publisherAppAccount, bool isCompletionMedia = false);

    public abstract Task SyncRecentMediaAsync(SyncPublisherAppAccountInfo appAccount);
    public abstract Task SyncUserDataAsync(SyncPublisherAppAccountInfo appAccount);

    public async Task<List<long>> SyncMediaAsync(IEnumerable<string> publisherMediaIds, long publisherAccountId,
                                                 bool isCompletionMedia = false, long publisherAppId = 0)
    {
        var dynPublisherAppAccount = await _dynamoDb.GetPublisherAppAccountOrDefaultAsync(publisherAccountId, publisherAppId);

        var results = await DoSyncMediaAsync(publisherMediaIds, dynPublisherAppAccount, isCompletionMedia);

        return results;
    }

    public async Task AddOrUpdateMediaSyncAsync(long publisherAccountId)
    {
        var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

        if (publisherAccount == null || publisherAccount.IsDeleted())
        {
            return;
        }

        // The FB/IG/etc sync job - these run from a dedicated q.
        _deferRequestsService.PublishMessageRecurring(new PostSyncRecentPublisherAccountMedia
                                                      {
                                                          PublisherAccountId = publisherAccountId
                                                      }.WithAdminRequestInfo(),
                                                      PublisherAccountSyncIntervalMinutes > 59
                                                          ? CronBuilder.Hourly(PublisherAccountSyncIntervalMinutes / 60, RandomProvider.GetRandomIntBeween(1, 59))
                                                          : CronBuilder.Minutely(PublisherAccountSyncIntervalMinutes),
                                                      PostSyncRecentPublisherAccountMedia.GetRecurringJobId(publisherAccountId));

        // Media analysis - non-dedicated q, only inf accounts do this
        if (!publisherAccount.IsTokenAccount() && publisherAccount.RydrAccountType.IsInfluencer())
        {
            _deferRequestsService.PublishMessageRecurring(new PostDeferredLowPriMessage
                                                          {
                                                              Dto = new PostAnalyzePublisherMedia
                                                                  {
                                                                      PublisherAccountId = publisherAccountId
                                                                  }.WithAdminRequestInfo()
                                                                   .ToJsv(),
                                                              Type = typeof(PostAnalyzePublisherMedia).FullName
                                                          }.WithAdminRequestInfo(),
                                                          CronBuilder.Hourly(1, RandomProvider.GetRandomIntBeween(1, 59)),
                                                          PostAnalyzePublisherMedia.GetRecurringJobId(publisherAccountId));
        }

        // Creator?
        if (publisherAccount.RydrAccountType.IsInfluencer())
        { // Creator metrics update
            _deferRequestsService.PublishMessageRecurring(new PostDeferredLowPriMessage
                                                          {
                                                              Dto = new PostUpdateCreatorMetrics
                                                                  {
                                                                      PublisherIdentifier = publisherAccountId.ToStringInvariant()
                                                                  }.WithAdminRequestInfo()
                                                                   .ToJsv(),
                                                              Type = typeof(PostUpdateCreatorMetrics).FullName
                                                          }.WithAdminRequestInfo(),
                                                          CronBuilder.Hourly(6, RandomProvider.GetRandomIntBeween(1, 59)),
                                                          PostUpdateCreatorMetrics.GetRecurringJobId(publisherAccountId));
        }
    }

    protected bool SyncStepShouldTrySync(SyncPublisherAppAccountInfo syncPublisherAppAccountInfo, string stepName)
    {
        if (syncPublisherAppAccountInfo?.SyncStepsLastFailedOn == null ||
            stepName.IsNullOrEmpty() ||
            !syncPublisherAppAccountInfo.SyncStepsLastFailedOn.ContainsKey(stepName))
        {
            return true;
        }

        // If the last failure tracked for the given step is earlier than the time the token was updated, can try sync - otherwise we've failed
        // since the token was updated for this combo, so do not try sync
        var lastFailedOn = syncPublisherAppAccountInfo.SyncStepsLastFailedOn[stepName];

        if (lastFailedOn <= syncPublisherAppAccountInfo.TokenLastUpdated.Gz(long.MaxValue))
        {
            return true;
        }

        // Skip sync generally, but try infrequently...backing off based on how many times this step has failed...
        var failCount = (syncPublisherAppAccountInfo.SyncStepsFailCount.IsNullOrEmptyRydr() ||
                         !syncPublisherAppAccountInfo.SyncStepsFailCount.ContainsKey(stepName)
                             ? 0
                             : syncPublisherAppAccountInfo.SyncStepsFailCount[stepName]) + 1;

        if (failCount >= 16)
        {
            return false;
        }

        var tryIfOlderThan = DateTimeHelper.UtcNowTs - ((failCount / 3) * failCount * 7500);

        return lastFailedOn <= tryIfOlderThan;
    }

    protected void UpdateSyncStepLastFailed(SyncPublisherAppAccountInfo syncPublisherAppAccountInfo, string stepName, bool success)
    {
        syncPublisherAppAccountInfo.SyncStepsLastFailedOn ??= new Dictionary<string, long>();

        syncPublisherAppAccountInfo.SyncStepsFailCount ??= new Dictionary<string, long>();

        syncPublisherAppAccountInfo.SyncStepsLastFailedOn[stepName] = success
                                                                          ? 0
                                                                          : DateTimeHelper.UtcNowTs;

        var xValue = success
                         ? 0
                         : syncPublisherAppAccountInfo.SyncStepsFailCount.ContainsKey(stepName)
                             ? syncPublisherAppAccountInfo.SyncStepsFailCount[stepName]
                             : 0;

        syncPublisherAppAccountInfo.SyncStepsFailCount[stepName] = success
                                                                       ? 0
                                                                       : xValue + 1;
    }

    protected async Task<T> TrySyncStep<T>(DynPublisherAccount publisherAccount, SyncPublisherAppAccountInfo syncPublisherAppAccountInfo,
                                           Func<Task<T>> step, string stepName)
    {
        if (!SyncStepShouldTrySync(syncPublisherAppAccountInfo, stepName))
        {
            _log.DebugInfoFormat($"FacebookSyncStep being skipped at step [{stepName ?? "Unknown"}] for PublisherAccount [{publisherAccount.DisplayName()}], due to step failure since token update");

            return default;
        }

        var success = false;

        try
        {
            var result = await step();

            success = true;

            return result;
        }
        catch(FbApiException fbx) when(fbx.IsApiStepPermissionError) // Non-apiStep errors should just bubble up and out
        { // For these, log and continue syncing other operations...
            _log.Warn($"FacebookSyncStep failed at step [{stepName ?? "Unknown"}] for PublisherAccount [{publisherAccount.DisplayName()}], will continue syncing other data", fbx);
        }
        catch(FbApiAggregateException fbx) when(fbx.IsApiStepPermissionError) // Non-apiStep errors should just bubble up and out
        { // For these, log and continue syncing other operations...
            _log.Warn($"FacebookSyncStep failed at step [{stepName ?? "Unknown"}] for PublisherAccount [{publisherAccount.DisplayName()}], will continue syncing other data", fbx);
        }
        finally
        {
            UpdateSyncStepLastFailed(syncPublisherAppAccountInfo, stepName, success);
        }

        return default;
    }
}
