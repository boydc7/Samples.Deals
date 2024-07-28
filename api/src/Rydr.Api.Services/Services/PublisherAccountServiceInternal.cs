using Hangfire;
using Nest;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Dto.Users;
using Rydr.FbSdk.Enums;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite;

namespace Rydr.Api.Services.Services;

public class PublisherAccountServiceInternal : BaseInternalOnlyApiService
{
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IAssociationService _associationService;
    private readonly IAuthorizeService _authorizeService;
    private readonly IRydrDataService _rydrDataService;
    private readonly IServiceCacheInvalidator _serviceCacheInvalidator;
    private readonly IWorkspaceService _workspaceService;
    private readonly IOpsNotificationService _opsNotificationService;
    private readonly IWorkspacePublisherSubscriptionService _workspacePublisherSubscriptionService;
    private readonly IMapItemService _mapItemService;
    private readonly IElasticClient _esClient;

    public PublisherAccountServiceInternal(IDeferRequestsService deferRequestsService,
                                           IPublisherAccountService publisherAccountService,
                                           IAssociationService associationService,
                                           IAuthorizeService authorizeService,
                                           IRydrDataService rydrDataService,
                                           IServiceCacheInvalidator serviceCacheInvalidator,
                                           IWorkspaceService workspaceService,
                                           IOpsNotificationService opsNotificationService,
                                           IWorkspacePublisherSubscriptionService workspacePublisherSubscriptionService,
                                           IMapItemService mapItemService,
                                           IElasticClient esClient)
    {
        _deferRequestsService = deferRequestsService;
        _publisherAccountService = publisherAccountService;
        _associationService = associationService;
        _authorizeService = authorizeService;
        _rydrDataService = rydrDataService;
        _serviceCacheInvalidator = serviceCacheInvalidator;
        _workspaceService = workspaceService;
        _opsNotificationService = opsNotificationService;
        _workspacePublisherSubscriptionService = workspacePublisherSubscriptionService;
        _mapItemService = mapItemService;
        _esClient = esClient;
    }

    public async Task Post(PublisherAccountDownConvert request)
    {
        var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

        if (publisherAccount == null || publisherAccount.IsDeleted() || publisherAccount.IsBasicLink || publisherAccount.IsSoftLinked ||
            !publisherAccount.PublisherType.IsWritablePublisherType())
        {
            // Nothing to do if the account is invalid, already basic or lower, or not a writable publisher type already
            return;
        }

        var nonWritableAlternateType = publisherAccount.PublisherType.NonWritableAlternateAccountType();

        if (nonWritableAlternateType == PublisherType.Unknown)
        {
            return;
        }

        var removedExistingAccount = false;

        var existingPublisherAccount = publisherAccount.CreateCopy();

        publisherAccount.PublisherType = nonWritableAlternateType;
        publisherAccount.AccountId = publisherAccount.AlternateAccountId.Coalesce(publisherAccount.AccountId);
        publisherAccount.AlternateAccountId = existingPublisherAccount.AccountId;
        publisherAccount.EdgeId = DynPublisherAccount.BuildEdgeId(publisherAccount.PublisherType, publisherAccount.AccountId);

        _log.InfoFormat("  Down converting PublisherAccount [{0}] to type [{1}], edge [{2}]", publisherAccount.DisplayName(), publisherAccount.PublisherType, publisherAccount.EdgeId);

        try
        {
            removedExistingAccount = await _publisherAccountService.HardDeletePublisherAccountForReplacementOnlyAsync(existingPublisherAccount.PublisherAccountId);

            await _publisherAccountService.PutPublisherAccount(publisherAccount);
        }
        catch
        {
            if (removedExistingAccount)
            {
                await _publisherAccountService.PutPublisherAccount(existingPublisherAccount);
            }

            throw;
        }
    }

    public async Task Post(PublisherAccountUpConvertFacebook request)
    {
        var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

        if (publisherAccount == null || publisherAccount.IsDeleted() || publisherAccount.PublisherType.IsWritablePublisherType() ||
            (!publisherAccount.IsBasicLink && !publisherAccount.IsSoftLinked))
        {
            // Nothing to do if the account is invalid, already non-basic or writable already
            return;
        }

        var existingPublisherAccount = publisherAccount.CreateCopy();

        publisherAccount.PopulateIgAccountWithFbInfo(request.WithFacebookAccount);

        var removedExistingAccount = false;

        _log.InfoFormat("  Up converting PublisherAccount [{0}] to type [{1}], edge [{2}]", publisherAccount.DisplayName(), publisherAccount.PublisherType, publisherAccount.EdgeId);

        try
        {
            removedExistingAccount = await _publisherAccountService.HardDeletePublisherAccountForReplacementOnlyAsync(existingPublisherAccount.PublisherAccountId);

            await _publisherAccountService.PutPublisherAccount(publisherAccount);
        }
        catch
        {
            if (removedExistingAccount)
            {
                await _publisherAccountService.PutPublisherAccount(existingPublisherAccount);
            }

            throw;
        }
    }

    public async Task Post(PublisherAccountRecentDealStatsUpdate request)
    {
        var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

        if (publisherAccount == null || publisherAccount.IsDeleted())
        {
            _deferRequestsService.RemoveRecurringJob(PublisherAccountRecentDealStatsUpdate.GetRecurringJobId(request.PublisherAccountId, request.InWorkspaceId));

            return;
        }

        // Update the CompletedThisWeek and CompletedLastWeek stats for the given publisherAccount in the given workspace
        var now = _dateTimeProvider.UtcNow;
        var tomorrowUtc = now.AddDays(1).Date.ToUnixTimestamp();
        var startOfWeek = now.StartOfWeek().ToUnixTimestamp();
        var startOfLastWeek = now.StartOfWeek().AddDays(-7).Date.ToUnixTimestamp();
        var thisWeekCompleted = 0;
        var lastWeekCompleted = 0;

        Task putPublisherAccountStatAsync(DealStatType dealStatType, long contextWorkspaceId, long value, long workspaceId)
            => _dynamoDb.PutItemAsync(new DynPublisherAccountStat
                                      {
                                          Id = publisherAccount.PublisherAccountId,
                                          EdgeId = DynPublisherAccountStat.BuildEdgeId(DynItemType.DealStat, contextWorkspaceId, dealStatType),
                                          Cnt = value,
                                          PublisherAccountId = publisherAccount.PublisherAccountId,
                                          StatType = dealStatType,
                                          TypeId = (int)DynItemType.PublisherAccountStat,
                                          ReferenceId = publisherAccount.PublisherAccountId.ToStringInvariant(),
                                          WorkspaceId = workspaceId,
                                          ModifiedBy = request.UserId,
                                          ModifiedOn = now,
                                          CreatedBy = request.UserId,
                                          CreatedOn = now,
                                          CreatedWorkspaceId = request.WorkspaceId
                                      });

        if (publisherAccount.IsInfluencer())
        { // For an influencer, update the number of deals they've completed recently
            var typeReferenceBeginsAt = string.Concat((int)DynItemType.DealRequest, "|", startOfLastWeek);
            var typeReferenceEndsAt = string.Concat((int)DynItemType.DealRequest, "|", tomorrowUtc);

            await _dynamoDb.FromQueryIndex<DynItemEdgeIdGlobalIndex>(dr => dr.EdgeId == publisherAccount.PublisherAccountId.ToEdgeId() &&
                                                                           Dynamo.Between(dr.TypeReference, typeReferenceBeginsAt, typeReferenceEndsAt))
                           .Filter(dr => dr.StatusId == DealRequestStatus.Completed.ToString())
                           .Select(i => new
                                        {
                                            i.TypeReference
                                        })
                           .QueryAsync(_dynamoDb)
                           .EachWhileAsync((item, index) =>
                                           {
                                               var statusUpdatedAt = DynItem.GetFinalEdgeSegment(item.TypeReference).ToLong(0);

                                               if (statusUpdatedAt >= startOfWeek)
                                               {
                                                   thisWeekCompleted++;
                                               }
                                               else if (statusUpdatedAt >= startOfLastWeek)
                                               {
                                                   lastWeekCompleted++;
                                               }

                                               return index < 500;
                                           });

            var contextWorkspaceId = publisherAccount.GetContextWorkspaceId(request.InWorkspaceId);

            // Influencers use 0 contextual workspace here correctly
            await putPublisherAccountStatAsync(DealStatType.CompletedThisWeek, contextWorkspaceId, thisWeekCompleted, 0);
            await putPublisherAccountStatAsync(DealStatType.CompletedLastWeek, contextWorkspaceId, lastWeekCompleted, 0);
        }
        else if (publisherAccount.IsBusiness())
        { // For a business, update the number of requests that influencers have completed for them in the last weeks, within the correct context workspace
            var inWorkspace = await _workspaceService.GetWorkspaceAsync(request.InWorkspaceId);
            var contextWorkspaceId = inWorkspace.GetContextWorkspaceId();

            // Just a performance fork - if the contextWorkspaceId > 0, we know we are filtering to a single workspaceId, and do not need to go fetch all the actual request
            // objects to inspect for a correct contextWorkspace
            var requestStatusUpdatedAt = contextWorkspaceId > 0
                                             ? _dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(dr => dr.TypeOwnerSpace == DynItem.BuildTypeOwnerSpaceHash(DynItemType.DealRequest, publisherAccount.PublisherAccountId) &&
                                                                                                                         Dynamo.Between(dr.ReferenceId, startOfLastWeek.ToStringInvariant(), tomorrowUtc.ToStringInvariant()))
                                                        .Filter(dr => dr.StatusId == DealRequestStatus.Completed.ToString() &&
                                                                      dr.WorkspaceId == contextWorkspaceId)
                                                        .Select(i => new
                                                                     {
                                                                         i.ReferenceId
                                                                     })
                                                        .QueryAsync(_dynamoDb)
                                                        .Select(i => i.ReferenceId.ToLong(0))
                                                        .Take(1000)
                                             : _dynamoDb.GetItemsFromAsync<DynDealRequest, DynItemTypeOwnerSpaceReferenceGlobalIndex>(_dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(dr => dr.TypeOwnerSpace == DynItem.BuildTypeOwnerSpaceHash(DynItemType.DealRequest,
                                                                                                                                                                                                                                                                     publisherAccount.PublisherAccountId) &&
                                                                                                                                                                                                                Dynamo.Between(dr.ReferenceId,
                                                                                                                                                                                                                               startOfLastWeek.ToStringInvariant(),
                                                                                                                                                                                                                               tomorrowUtc.ToStringInvariant()))
                                                                                                                                               .Filter(dr => dr.StatusId == DealRequestStatus.Completed.ToString())
                                                                                                                                               .Select(i => new
                                                                                                                                                            {
                                                                                                                                                                i.Id,
                                                                                                                                                                i.EdgeId
                                                                                                                                                            })
                                                                                                                                               .QueryAsync(_dynamoDb))
                                                        .Where(dr => dr.DealContextWorkspaceId == contextWorkspaceId)
                                                        .Select(dr => dr.ReferenceId.ToLong(0))
                                                        .Take(1000);

            await requestStatusUpdatedAt.Each(updatedAt =>
                                              {
                                                  if (updatedAt >= startOfWeek)
                                                  {
                                                      thisWeekCompleted++;
                                                  }
                                                  else if (updatedAt >= startOfLastWeek)
                                                  {
                                                      lastWeekCompleted++;
                                                  }
                                              });

            await putPublisherAccountStatAsync(DealStatType.CompletedThisWeek, contextWorkspaceId, thisWeekCompleted, inWorkspace.Id);
            await putPublisherAccountStatAsync(DealStatType.CompletedLastWeek, contextWorkspaceId, lastWeekCompleted, inWorkspace.Id);
        }
    }

    public async Task Post(LinkPublisherAccount request)
    {
        try
        {
            // Authorize the workspace to the newly linked account
            await _authorizeService.AuthorizeAsync(request.ToWorkspaceId, request.ToPublisherAccountId);

            // Associate the newly linked account to the token'd account AND to the workspace
            if (request.FromPublisherAccountId > 0)
            {
                await _associationService.AssociateAsync(RecordType.PublisherAccount, request.FromPublisherAccountId,
                                                         RecordType.PublisherAccount, request.ToPublisherAccountId);
            }

            await _associationService.AssociateAsync(RecordType.Workspace, request.ToWorkspaceId,
                                                     RecordType.PublisherAccount, request.ToPublisherAccountId);

            _deferRequestsService.DeferRequest(new PublisherAccountLinked
                                               {
                                                   ToPublisherAccountId = request.ToPublisherAccountId,
                                                   FromPublisherAccountId = request.FromPublisherAccountId,
                                                   FromWorkspaceId = request.ToWorkspaceId,
                                                   IsValidateRequest = request.IsValidateRequest
                                               });
        }
        catch
        {
            if (!request.IsValidateRequest)
            {
                _deferRequestsService.DeferRequest(new DelinkPublisherAccount
                                                   {
                                                       FromWorkspaceId = request.ToWorkspaceId,
                                                       FromPublisherAccountId = request.FromPublisherAccountId,
                                                       ToPublisherAccountId = request.ToPublisherAccountId
                                                   }.WithAdminRequestInfo());
            }

            throw;
        }
    }

    public async Task Post(DelinkPublisherAccount request)
    {
        // If a specific workspace is included, delink the workspace from the TO account specified
        if (request.FromWorkspaceId > 0)
        {
            await _authorizeService.DeAuthorizeAsync(request.FromWorkspaceId, request.ToPublisherAccountId);

            // Disassociate the workspace and publisher account
            await _associationService.TryDeleteAssociationAsync(RecordType.Workspace, request.FromWorkspaceId, RecordType.PublisherAccount, request.ToPublisherAccountId);

            // Unlinked this specific workspace form a given TO account
            _deferRequestsService.DeferRequest(new PublisherAccountUnlinked
                                               {
                                                   FromPublisherAccountId = request.FromPublisherAccountId,
                                                   ToPublisherAccountId = request.ToPublisherAccountId,
                                                   FromWorkspaceId = request.FromWorkspaceId
                                               }.PopulateWithRequestInfo(request));
        }
        else
        { // No specific workspace included, delink the FROM (token) account from the TO account specified in ALL workspaces
            // Get all workspaces the FROM (token) account is linked up to, and delink for each of them
            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.FromPublisherAccountId);

            await foreach (var associatedWorkspaceId in _workspaceService.GetAssociatedWorkspaceIdsAsync(publisherAccount))
            {
                _deferRequestsService.DeferRequest(new DelinkPublisherAccount
                                                   {
                                                       FromWorkspaceId = associatedWorkspaceId,
                                                       FromPublisherAccountId = request.FromPublisherAccountId,
                                                       ToPublisherAccountId = request.ToPublisherAccountId
                                                   }.PopulateWithRequestInfo(request));
            }

            // Disassociate the publisher account IF this delink request isn't for a specific workspaceId
            if (request.FromPublisherAccountId > 0 && request.FromWorkspaceId <= 0)
            {
                await _associationService.TryDeleteAssociationAsync(RecordType.PublisherAccount, request.FromPublisherAccountId, RecordType.PublisherAccount, request.ToPublisherAccountId);
            }
        }
    }

    public async Task Post(PublisherAccountUnlinked request)
    {
        await _rydrDataService.SaveIgnoreConflictAsync(new RydrPublisherAccountLink
                                                       {
                                                           WorkspaceId = request.FromWorkspaceId,
                                                           FromPublisherAccountId = request.FromPublisherAccountId,
                                                           ToPublisherAccountId = request.ToPublisherAccountId,
                                                           DeletedOn = DateTimeHelper.UtcNow
                                                       },
                                                       r => r.Id);

        await _serviceCacheInvalidator.InvalidateWorkspaceAsync(request.FromWorkspaceId, "facebook", "workspaces");
        await _serviceCacheInvalidator.InvalidatePublisherAccountAsync(request.ToPublisherAccountId, "facebook", "workspaces");

        var workspace = await _workspaceService.GetWorkspaceAsync(request.FromWorkspaceId);
        var fromPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.FromPublisherAccountId);
        var toPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.ToPublisherAccountId);

        // Cancel any active subscription(s)
        await _workspacePublisherSubscriptionService.CancelSubscriptionAsync(request.FromWorkspaceId, request.ToPublisherAccountId);

        // Delink all workspace users from the publisher account
        await foreach (var workspaceUser in _workspaceService.GetWorkspaceUsersAsync(workspace.Id))
        {
            await _workspaceService.DelinkUserFromPublisherAccountAsync(workspace.Id, workspaceUser.UserId, request.ToPublisherAccountId);
        }

        // A publisher account unlinked is fired when a token account delinks another account from it...workspaces are basically wrapped over a token account
        // where the owner of the workspace is the root user.  So, fire a delinked message for the owner of the workspace from this account...
        _deferRequestsService.DeferLowPriRequest(new WorkspaceUserPublisherAccountDelinked
                                                 {
                                                     RydrUserId = workspace.OwnerId,
                                                     WorkspaceUserId = workspace.OwnerId,
                                                     InWorkspaceId = workspace.Id,
                                                     FromPublisherAccountId = request.ToPublisherAccountId
                                                 });

        if (fromPublisherAccount.IsRydrSystemPublisherAccount() && toPublisherAccount != null)
        { // If this is a rydr system account publisher, and we just properly delinked an account, remove any soft-linked association map that exists for that connection
            var map = new DynItemMap
                      {
                          Id = fromPublisherAccount.PublisherAccountId,
                          EdgeId = toPublisherAccount.ToRydrSoftLinkedAssociationId()
                      };

            if (await _mapItemService.MapExistsAsync(map.Id, map.EdgeId))
            {
                await _mapItemService.DeleteMapAsync(map.Id, map.EdgeId);
            }
        }

        _log.InfoFormat("Delinked PublisherAccount [{0}] from [{1}]", toPublisherAccount.DisplayName(),
                        fromPublisherAccount?.DisplayName() ?? string.Concat("BasicIg delinked from workspace [", workspace.Id, "]"));

        if (toPublisherAccount != null && toPublisherAccount.RydrAccountType.HasExternalCrmIntegration())
        {
            await _workspaceService.TrackEventNotificationAsync(request.FromWorkspaceId, nameof(PublisherAccountUnlinked), toPublisherAccount.RydrAccountType.ToString(),
                                                                new ExternalCrmUpdateItem
                                                                {
                                                                    FieldName = "LinkedProfiles",
                                                                    FieldValue = toPublisherAccount.DisplayName(),
                                                                    Remove = true
                                                                });
        }
    }

    public async Task Post(PublisherAccountLinked request)
    {
        await _rydrDataService.SaveIgnoreConflictAsync(new RydrPublisherAccountLink
                                                       {
                                                           WorkspaceId = request.FromWorkspaceId,
                                                           FromPublisherAccountId = request.FromPublisherAccountId,
                                                           ToPublisherAccountId = request.ToPublisherAccountId,
                                                           DeletedOn = null
                                                       },
                                                       r => r.Id);

        var workspace = await _workspaceService.TryGetWorkspaceAsync(request.FromWorkspaceId);

        // Owner of the workspace linked to the account
        _deferRequestsService.DeferLowPriRequest(new WorkspaceUserPublisherAccountLinked
                                                 {
                                                     RydrUserId = workspace.OwnerId,
                                                     WorkspaceUserId = workspace.OwnerId,
                                                     InWorkspaceId = workspace.Id,
                                                     ToPublisherAccountId = request.ToPublisherAccountId,
                                                     IsValidateRequest = request.IsValidateRequest
                                                 });

        var fromPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.FromPublisherAccountId);
        var toPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.ToPublisherAccountId);

        _log.InfoFormat("Linked PublisherAccount [{0}] from [{1}]", toPublisherAccount.DisplayName(),
                        fromPublisherAccount?.DisplayName() ?? string.Concat("BasicIg linked to workspace [", workspace.Id, "]"));

        if (!request.IsValidateRequest)
        {
            _deferRequestsService.PublishMessage(new PostSyncRecentPublisherAccountMedia
                                                 {
                                                     PublisherAccountId = request.ToPublisherAccountId,
                                                     WithWorkspaceId = request.FromWorkspaceId
                                                 });

            await _opsNotificationService.TrySendAppNotificationAsync("Publisher Account Linked", $@"
From WorkspaceId : {workspace.Name} ({workspace.Id})
From Account :     {fromPublisherAccount?.DisplayName() ?? "BasicIg account"}
To Account :       {(toPublisherAccount.IsTokenAccount()
                         ? toPublisherAccount.UserName
                         : $"<https://instagram.com/{toPublisherAccount.UserName}|IG: {toPublisherAccount.UserName}>")} ({toPublisherAccount.PublisherAccountId})
  Type:           {(toPublisherAccount.IsInfluencer() ? "Influencer" : "Business")}
");

            if (toPublisherAccount.RydrAccountType.HasExternalCrmIntegration())
            {
                await _workspaceService.TrackEventNotificationAsync(workspace.Id, nameof(PublisherAccountLinked), toPublisherAccount.RydrAccountType.ToString(),
                                                                    new ExternalCrmUpdateItem
                                                                    {
                                                                        FieldName = "LinkedProfiles",
                                                                        FieldValue = toPublisherAccount.DisplayName()
                                                                    });
            }
        }

        _deferRequestsService.DeferLowPriRequest(request.ConvertTo<PublisherAccountLinkedComplete>());
    }

    public async Task Post(PublisherAccountLinkedComplete request)
    {
        var fromPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.FromPublisherAccountId);
        var linkedPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.ToPublisherAccountId);

        var contextWorkspaceId = linkedPublisherAccount.GetContextWorkspaceId(request.FromWorkspaceId);

        await _serviceCacheInvalidator.InvalidateWorkspaceAsync(request.FromWorkspaceId, "facebook", "workspaces");
        await _serviceCacheInvalidator.InvalidatePublisherAccountAsync(request.ToPublisherAccountId, "facebook", "workspaces");

        if (fromPublisherAccount.IsRydrSystemPublisherAccount())
        { // If this is a rydr system account publisher, and we just properly linked an account, remove any soft-linked association map that exists for that connection
            var map = new DynItemMap
                      {
                          Id = fromPublisherAccount.PublisherAccountId,
                          EdgeId = linkedPublisherAccount.ToRydrSoftLinkedAssociationId()
                      };

            if (await _mapItemService.MapExistsAsync(map.Id, map.EdgeId))
            {
                await _mapItemService.DeleteMapAsync(map.Id, map.EdgeId);
            }
        }

        // Ensure daily sync stats sync job is updated for the given workspace
        _deferRequestsService.PublishMessageRecurring(new PostDeferredLowPriMessage
                                                      {
                                                          Dto = new PublisherAccountRecentDealStatsUpdate
                                                              {
                                                                  PublisherAccountId = linkedPublisherAccount.PublisherAccountId,
                                                                  InWorkspaceId = request.FromWorkspaceId
                                                              }.PopulateWithRequestInfo(request)
                                                               .ToJsv(),
                                                          Type = typeof(PublisherAccountRecentDealStatsUpdate).FullName
                                                      }.WithAdminRequestInfo(),
                                                      CronBuilder.Daily(RandomProvider.GetRandomIntBeween(7, 11),
                                                                        RandomProvider.GetRandomIntBeween(1, 59)),
                                                      PublisherAccountRecentDealStatsUpdate.GetRecurringJobId(linkedPublisherAccount.PublisherAccountId, contextWorkspaceId));

        // Schedule any media syncs, kick off creator metrics update immediately on link (to index the creator basic info at least)
        var publisherMediaSyncService = RydrEnvironment.Container.ResolveNamed<IPublisherMediaSyncService>(linkedPublisherAccount.PublisherType.ToString());

        await publisherMediaSyncService.AddOrUpdateMediaSyncAsync(linkedPublisherAccount.PublisherAccountId);

        if (!request.IsValidateRequest)
        {
            RecurringJob.Trigger(PostUpdateCreatorMetrics.GetRecurringJobId(linkedPublisherAccount.PublisherAccountId));
        }

        // Try to install our app on the publishers facebook page
        if (linkedPublisherAccount.PageId.HasValue() && linkedPublisherAccount.PublisherType == PublisherType.Facebook)
        {
            var publisherAppAccount = await _dynamoDb.GetPublisherAppAccountOrDefaultAsync(linkedPublisherAccount.PublisherAccountId, workspaceId: request.FromWorkspaceId);

            if (publisherAppAccount.HasManagePagesScope())
            {
                var fbClient = await publisherAppAccount.GetOrCreateFbClientAsync();

                try
                {
                    await fbClient.InstallAppOnFacebookPageAsync(linkedPublisherAccount.PageId);
                }
                catch(FbApiException fbx) when(!fbx.IsTransient)
                {
                    _log.Warn("InstallAppOnFacebookPageAsync failed to process", fbx);
                }
            }
        }

        // Categorize the new business/creator
        var needsCategories = !request.IsValidateRequest && (linkedPublisherAccount.Tags.IsNullOrEmpty() || !linkedPublisherAccount.Tags.Any(t => t.Key.EqualsOrdinalCi(Tag.TagRydrCategory)));

        if (needsCategories && linkedPublisherAccount.IsBusiness())
        {
            _deferRequestsService.DeferLowPriRequest(new PostHumanCategorizeBusiness
                                                     {
                                                         PublisherAccountId = linkedPublisherAccount.PublisherAccountId
                                                     }.WithAdminRequestInfo());
        }

        if (needsCategories && linkedPublisherAccount.IsInfluencer())
        {
            _deferRequestsService.DeferLowPriRequest(new PostHumanCategorizeCreator
                                                     {
                                                         PublisherAccountId = linkedPublisherAccount.PublisherAccountId
                                                     }.WithAdminRequestInfo());
        }
    }

    public async Task Post(DeletePublisherAccountInternal request)
    {
        // Remove any account links/associations (to and from)
        var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

        if (publisherAccount == null)
        {
            _deferRequestsService.DeferRequest(new PublisherAccountDeleted
                                               {
                                                   PublisherAccountId = request.PublisherAccountId
                                               });

            return;
        }

        var publiserAccountLinks = await _publisherAccountService.GetLinkedPublisherAccountsAsync(publisherAccount.PublisherAccountId)
                                                                 .ToHashSetAsync(DynPublisherAccount.DefaultComparer);

        // Delink all linkages to/from this account in all workspaces the account is linked in
        await foreach (var associatedWorkspaceId in _workspaceService.GetAssociatedWorkspaceIdsAsync(publisherAccount))
        {
            var workspace = await _workspaceService.TryGetWorkspaceAsync(associatedWorkspaceId);

            var worksapceLinkedPublisherAccounts = await _workspaceService.GetWorkspaceUserPublisherAccountsAsync(workspace.Id, workspace.OwnerId, true)
                                                                          .ToHashSetAsync(DynPublisherAccount.DefaultComparer);

            // Workspace linked is the intersection of accounts linked to the given publisher account AND the workspace
            worksapceLinkedPublisherAccounts.IntersectWith(publiserAccountLinks);

            foreach (var worksapceLinkedPublisherAccount in worksapceLinkedPublisherAccounts.Where(wpa => wpa.PublisherAccountId != publisherAccount.PublisherAccountId))
            { // Delink all non-token accounts linked to this workspace AND this token account
                var delinkRequest = publisherAccount.IsTokenAccount()
                                        ? new DelinkPublisherAccount
                                          {
                                              FromWorkspaceId = workspace.Id,
                                              FromPublisherAccountId = publisherAccount.PublisherAccountId,
                                              ToPublisherAccountId = worksapceLinkedPublisherAccount.PublisherAccountId
                                          }
                                        : new DelinkPublisherAccount
                                          {
                                              FromWorkspaceId = workspace.Id,
                                              FromPublisherAccountId = worksapceLinkedPublisherAccount.PublisherAccountId,
                                              ToPublisherAccountId = publisherAccount.PublisherAccountId
                                          };

                _deferRequestsService.DeferRequest(delinkRequest.WithAdminRequestInfo());
            }

            // If a token account, delink the token account from the worksapce itself
            if (publisherAccount.IsTokenAccount())
            {
                await _workspaceService.DelinkTokenAccountAsync(workspace, publisherAccount.PublisherAccountId);
            }
        }

        // Delete the publisher account itself
        await _dynamoDb.SoftDeleteAsync(publisherAccount, request, true);

        // Ensure all associations are deleted
        await _associationService.DeleteAllAssociationsAsync(RecordType.PublisherAccount, publisherAccount.PublisherAccountId);

        // De-authorize
        await _authorizeService.DeAuthorizeAllToFromAsync(publisherAccount.PublisherAccountId);

        _publisherAccountService.FlushModel(publisherAccount.PublisherAccountId);

        _deferRequestsService.DeferRequest(new PublisherAccountDeleted
                                           {
                                               PublisherAccountId = publisherAccount.PublisherAccountId
                                           });
    }

    public async Task<LongIdResponse> Post(PostPublisherAccountUpsert request)
    {
        var existingModel = await _publisherAccountService.GetPublisherAccountAsync(request.Model.Id)
                            ??
                            await _publisherAccountService.TryGetPublisherAccountAsync(request.Model.Type, request.Model.AccountId);

        request.Model.Id = existingModel?.Id ?? request.Model.Id;

        if (request.Model.Id > 0)
        {
            request.Model.AccountId = null;
        }

        await PutOrPostModelAsync<PutPublisherAccount, PostPublisherAccount, PublisherAccount>(request.Model, request);

        return request.Model.Id.ToLongIdResponse();
    }

    public async Task Post(PublisherAccountUpdated request)
    {
        var dynPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

        if (dynPublisherAccount.IsBusiness() || request.FromRydrAccountType.IsBusiness())
        {
            var esBusiness = await dynPublisherAccount.ToEsBusinessAsync();

            var response = await _esClient.IndexAsync(esBusiness, idx => idx.Index(ElasticIndexes.BusinessesAlias)
                                                                            .Id(esBusiness.PublisherAccountId));

            if (!response.SuccessfulOnly())
            {
                throw response.ToException();
            }
        }

        await ProcessChangedAccountMetricsAsync(dynPublisherAccount);

        await ProcessChangedAccountTypeAsync(request, dynPublisherAccount);

        _deferRequestsService.DeferLowPriRequest(new ProcessPublisherAccountTags
                                                 {
                                                     PublisherAccountId = dynPublisherAccount.PublisherAccountId
                                                 }.WithAdminRequestInfo());

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 Ids = new List<long>
                                                       {
                                                           dynPublisherAccount.PublisherAccountId
                                                       },
                                                 Type = RecordType.PublisherAccount
                                             });
    }

    public async Task Post(ProcessPublisherAccountTags request)
    {
        var dynPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

        if (dynPublisherAccount == null || dynPublisherAccount.Tags.IsNullOrEmptyRydr())
        {
            return;
        }

        var existingRydrPublisherTags = new HashSet<string>(await _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<string>(@"
SELECT    DISTINCT pt.Tag
FROM      PublisherTags pt
WHERE     pt.PublisherAccountId = @PublisherAccountId;
",
                                                                                                                                new
                                                                                                                                {
                                                                                                                                    dynPublisherAccount.PublisherAccountId
                                                                                                                                })),
                                                            StringComparer.OrdinalIgnoreCase);

        foreach (var publisherTag in dynPublisherAccount.Tags)
        {
            if (existingRydrPublisherTags.IsNullOrEmptyRydr() || !existingRydrPublisherTags.Contains(publisherTag.ToString()))
            {
                await _rydrDataService.ExecAdHocAsync(@"
INSERT    IGNORE INTO PublisherTags
          (PublisherAccountId, Tag)
VALUES    (@PublisherAccountId, @Tag);
",
                                                      new
                                                      {
                                                          Tag = publisherTag.ToString().Left(100),
                                                          dynPublisherAccount.PublisherAccountId
                                                      });
            }
            else
            {
                existingRydrPublisherTags.Remove(publisherTag.ToString());
            }
        }

        if (!existingRydrPublisherTags.IsNullOrEmptyRydr())
        {
            var tagsToRemove = string.Concat("'", string.Join("','", existingRydrPublisherTags.Select(t => t.ToString())), "'");

            await _rydrDataService.ExecAdHocAsync(string.Concat(@"
DELETE   FROM PublisherTags
WHERE    PublisherAccountId = @PublisherAccountId
         AND Tag IN(", tagsToRemove, @");
"),
                                                  new
                                                  {
                                                      dynPublisherAccount.PublisherAccountId,
                                                      Tags = tagsToRemove
                                                  });
        }
    }

    public async Task Post(PublisherAccountDeleted request)
    {
        var dynPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

        if (dynPublisherAccount != null)
        {
            await CancelAllPublisherAccountDealsAsync(dynPublisherAccount.PublisherAccountId, "Account was removed", request);

            _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                 {
                                                     Ids = new List<long>
                                                           {
                                                               dynPublisherAccount.PublisherAccountId
                                                           },
                                                     Type = RecordType.PublisherAccount
                                                 });
        }

        _deferRequestsService.RemoveRecurringJob(PostSyncRecentPublisherAccountMedia.GetRecurringJobId(request.PublisherAccountId));
        _deferRequestsService.RemoveRecurringJob(PostAnalyzePublisherMedia.GetRecurringJobId(request.PublisherAccountId));
        _deferRequestsService.RemoveRecurringJob(PostUpdateCreatorMetrics.GetRecurringJobId(request.PublisherAccountId));

        _deferRequestsService.RemoveRecurringJob(PublisherAccountRecentDealStatsUpdate.GetRecurringJobId(request.PublisherAccountId, dynPublisherAccount == null || dynPublisherAccount.IsInfluencer()
                                                                                                                                         ? 0
                                                                                                                                         : dynPublisherAccount.WorkspaceId));
    }

    private Task ProcessChangedAccountMetricsAsync(DynPublisherAccount dynPublisherAccount)
    {
        if (dynPublisherAccount == null || dynPublisherAccount.IsDeleted())
        {
            return Task.CompletedTask;
        }

        return dynPublisherAccount.Metrics.ProcessDailyStatsAsync<DynDailyStatSnapshot>(dynPublisherAccount.Id, RecordType.DailyStatSnapshot);
    }

    private async Task ProcessChangedAccountTypeAsync(PublisherAccountUpdated request, DynPublisherAccount dynPublisherAccount)
    {
        var isDeleted = dynPublisherAccount == null || dynPublisherAccount.IsDeleted();

        if (!isDeleted &&
            (request.ToRydrAccountType == RydrAccountType.None ||
             request.FromRydrAccountType == RydrAccountType.None ||
             request.FromRydrAccountType == request.ToRydrAccountType))
        { // Nothing currently to do unless the account type was changed from one valid type to another
            return;
        }

        // Deleted or Changed account type...cancel any existing deals, requests, etc.
        if (isDeleted)
        { // Cancel when deleted
            await CancelAllPublisherAccountDealsAsync(dynPublisherAccount.PublisherAccountId, "Account was removed",
                                                      request);
        }
        else
        { // Pause deals, cancel requests when changing...
            await PauseAllPublisherAccountDealsAndCancelActiveRequestsAsync(dynPublisherAccount.PublisherAccountId, "User closed their business account");
        }

        if (request.WorkspaceId > GlobalItemIds.MinUserDefinedObjectId && request.ToRydrAccountType != RydrAccountType.None)
        {
            await _workspaceService.TrackEventNotificationAsync(request.WorkspaceId, nameof(PublisherAccountUpdated), request.ToRydrAccountType.ToString());
        }
    }

    private async Task PauseAllPublisherAccountDealsAndCancelActiveRequestsAsync(long publisherAccountId, string reason)
    {
        await foreach (var dealIdBatch in _dynamoDb.FromQueryIndex<DynItemIdTypeReferenceGlobalIndex>(d => d.Id == publisherAccountId &&
                                                                                                           Dynamo.BeginsWith(d.TypeReference, string.Concat((int)DynItemType.Deal, "|")))
                                                   .Filter(d => d.DeletedOnUtc == null &&
                                                                d.TypeId == (int)DynItemType.Deal &&
                                                                d.StatusId == DealStatus.Published.ToString())
                                                   .Select(d => new
                                                                {
                                                                    d.EdgeId,
                                                                    d.StatusId
                                                                })
                                                   .QueryAsync(_dynamoDb)
                                                   .ToBatchesOfAsync(100))
        {
            await foreach (var dynDeal in _dynamoDb.QueryItemsAsync<DynDeal>(dealIdBatch.Select(de => de.EdgeId.ToLong(0))
                                                                                        .Where(dealId => dealId > 0)
                                                                                        .Select(i => new DynamoId(publisherAccountId, i.ToEdgeId())))
                                                   .Where(d => d.DealStatus == DealStatus.Published))
            {
                await Try.ExecAsync(() => _adminServiceGatewayFactory().SendAsync(new PutDeal
                                                                                  {
                                                                                      Model = new Deal
                                                                                              {
                                                                                                  Id = dynDeal.DealId,
                                                                                                  Status = DealStatus.Paused
                                                                                              },
                                                                                      Reason = reason,
                                                                                      Id = dynDeal.DealId
                                                                                  }.WithAdminRequestInfo()),
                                    maxAttempts: 1);

                // Delete any active deal requests
                var activeDealRequests = await DealExtensions.DefaultDealRequestService.GetAllActiveDealRequestsAsync(dynDeal.DealId);

                foreach (var activeDealRequest in activeDealRequests)
                {
                    _deferRequestsService.DeferDealRequest(new DeleteDealRequestInternal
                                                           {
                                                               DealId = activeDealRequest.DealId,
                                                               PublisherAccountId = activeDealRequest.PublisherAccountId,
                                                               Reason = reason
                                                           });
                }
            }
        }
    }

    private async Task CancelAllPublisherAccountDealsAsync<T>(long publisherAccountId, string reason, T request)
        where T : IRequestBase
    {
        await foreach (var dealId in _dynamoDb.FromQueryIndex<DynItemIdTypeReferenceGlobalIndex>(d => d.Id == publisherAccountId &&
                                                                                                      Dynamo.BeginsWith(d.TypeReference, string.Concat((int)DynItemType.Deal, "|")))
                                              .Filter(d => d.DeletedOnUtc == null &&
                                                           d.TypeId == (int)DynItemType.Deal &&
                                                           d.StatusId == DealStatus.Published.ToString())
                                              .Select(d => new
                                                           {
                                                               d.EdgeId,
                                                               d.StatusId
                                                           })
                                              .QueryAsync(_dynamoDb)
                                              .Where(de => de.StatusId.EqualsOrdinalCi(DealStatus.Published.ToString()))
                                              .Select(de => de.EdgeId.ToLong(0))
                                              .Where(dealId => dealId > 0))
        {
            _deferRequestsService.DeferDealRequest(new DeleteDealInternal
                                                   {
                                                       DealId = dealId,
                                                       Reason = reason
                                                   }.PopulateWithRequestInfo(request));
        }

        // No need to cancel requests for the deals directly here, the deleteDeal processing handles that for each deal deleted naturally...
    }
}
