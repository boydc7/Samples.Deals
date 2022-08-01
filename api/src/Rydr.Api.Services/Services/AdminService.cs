using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Nest;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Admin;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services
{
    public class AdminService : BaseAdminApiService
    {
        private static readonly int _syncIntervalMinutes = RydrEnvironment.GetAppSetting("PublisherAccount.SyncIntervalMinutes", 60);
        private static readonly string _publisherAccountDealStatEdgePrefix = string.Concat((int)DynItemType.PublisherAccountStat, "|", DynItemType.DealStat.ToString(), "|");
        private static readonly string _dealRequestTypeReferencePrefix = string.Concat((int)DynItemType.DealRequest, "|");

        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IWorkspaceService _workspaceService;
        private readonly IServiceCacheInvalidator _serviceCacheInvalidator;
        private readonly IElasticClient _elasticClient;
        private readonly IRydrDataService _rydrDataService;
        private readonly IDeferRequestsService _deferRequestsService;
        private readonly IDealRequestService _dealRequestService;
        private readonly IAssociationService _associationService;
        private readonly IMapItemService _mapItemService;
        private readonly IUserService _userService;
        private readonly IEncryptionService _encryptionService;
        private readonly IWorkspaceSubscriptionService _workspaceSubscriptionService;
        private readonly IDealService _dealService;

        public AdminService(IPublisherAccountService publisherAccountService,
                            IWorkspaceService workspaceService,
                            IServiceCacheInvalidator serviceCacheInvalidator,
                            IElasticClient elasticClient,
                            IRydrDataService rydrDataService,
                            IDeferRequestsService deferRequestsService,
                            IDealRequestService dealRequestService,
                            IAssociationService associationService,
                            IMapItemService mapItemService,
                            IUserService userService,
                            IEncryptionService encryptionService,
                            IWorkspaceSubscriptionService workspaceSubscriptionService,
                            IDealService dealService)
        {
            _publisherAccountService = publisherAccountService;
            _workspaceService = workspaceService;
            _serviceCacheInvalidator = serviceCacheInvalidator;
            _elasticClient = elasticClient;
            _rydrDataService = rydrDataService;
            _deferRequestsService = deferRequestsService;
            _dealRequestService = dealRequestService;
            _associationService = associationService;
            _mapItemService = mapItemService;
            _userService = userService;
            _encryptionService = encryptionService;
            _workspaceSubscriptionService = workspaceSubscriptionService;
            _dealService = dealService;
        }

        public async Task<StringIdResponse> Post(ChargeCompletedUsage request)
        {
            var workspace = await _workspaceService.GetWorkspaceAsync(request.GetWorkspaceIdFromIdentifier());
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.GetPublisherIdFromIdentifier());

            var startTimestamp = request.StartDate.ToUnixTimestamp();

            var chargedCount = 0;

            await foreach (var completedDealRequest in _dealRequestService.GetDealOwnerRequestsEverInStatusAsync(publisherAccount.PublisherAccountId, DealRequestStatus.Completed,
                                                                                                                 request.StartDate, request.EndDate, workspace.Id)
                                                                          .Where(r => (request.ForceRecharge || r.UsageChargedOn < DateTimeHelper.MinApplicationDateTs) &&
                                                                                      r.DealWorkspaceId == workspace.Id &&
                                                                                      r.DealPublisherAccountId == publisherAccount.PublisherAccountId &&
                                                                                      !r.CompletionMediaIds.IsNullOrEmpty() &&
                                                                                      (r.RequestStatus == DealRequestStatus.Completed ||
                                                                                       r.RequestStatus == DealRequestStatus.Cancelled) &&
                                                                                      r.ReferenceId.ToLong() >= startTimestamp)
                                                                          .Take(request.Limit.Gz(int.MaxValue)))
            {
                var chargedUsage = await _workspaceSubscriptionService.ChargeCompletedRequestUsageAsync(completedDealRequest,
                                                                                                        request.ForceRecharge,
                                                                                                        forceUsageTimestamp: request.ForceNowUsageTimestamp
                                                                                                                                 ? _dateTimeProvider.UtcNowTs
                                                                                                                                 : 0);

                // Update the tracked IDs in dynamo
                if (chargedUsage)
                {
                    await _dynamoDb.PutItemTrackedInterlockedDeferAsync(completedDealRequest,
                                                                        ddr => ddr.UsageChargedOn = _dateTimeProvider.UtcNowTs,
                                                                        RecordType.DealRequest);

                    chargedCount++;
                }
            }

            return new StringIdResponse
                   {
                       Id = $"Charged [{chargedCount}] requests"
                   };
        }

        [RequiredRole("Admin")]
        public async Task<StringIdResponse> Put(PutUncancelDealRequest request)
        {
            var dynDeal = await _dealService.GetDealAsync(request.DealId);
            var dynDealRequest = await _dealRequestService.GetDealRequestAsync(request.DealId, request.PublisherAccountId);

            var requestStatusChanges = await _dynamoDb.FromQuery<DynDealRequestStatusChange>(r => r.Id == dynDeal.DealId &&
                                                                                                  Dynamo.BeginsWith(r.EdgeId, DynDealRequestStatusChange.BuildEdgeId("", dynDealRequest.PublisherAccountId)))
                                                      .ExecAsync()
                                                      .OrderByDescending(r => r.OccurredOn)
                                                      .ToList();

            if (requestStatusChanges.IsNullOrEmptyRydr())
            {
                return new StringIdResponse
                       {
                           Id = "No StatusChanges found, nothing to do"
                       };
            }

            if (requestStatusChanges.Any(s => s.ToDealRequestStatus == DealRequestStatus.Completed))
            {
                return new StringIdResponse
                       {
                           Id = "DealRequest was completed at some point, un-cancel not allowed"
                       };
            }

            var statusPriorToCanceled = requestStatusChanges[0].ToDealRequestStatus == DealRequestStatus.Cancelled
                                            ? requestStatusChanges[1].ToDealRequestStatus
                                            : DealRequestStatus.Unknown;

            if (statusPriorToCanceled != DealRequestStatus.Redeemed && statusPriorToCanceled != DealRequestStatus.InProgress)
            {
                return new StringIdResponse
                       {
                           Id = $"DealRequest was in status of [{statusPriorToCanceled.ToString()}] prior to cancellation, which is not supported, must have been cancelled from an InProgress or Redeemed status to un-cancel"
                       };
            }

            var updateDealRequest = new UpdateDealRequest
                                    {
                                        DealId = dynDeal.DealId,
                                        Reason = "Admin un-cancellation",
                                        UpdatedByPublisherAccountId = dynDeal.PublisherAccountId,
                                        Model = new DealRequest
                                                {
                                                    DealId = dynDeal.DealId,
                                                    PublisherAccountId = dynDealRequest.PublisherAccountId,
                                                    // NOTE: Correctly using inProgress here, even if it was redeemed before. InProg
                                                    // is a transient status now, but essential to track approvals/etc
                                                    Status = DealRequestStatus.InProgress
                                                },
                                        WorkspaceId = dynDeal.WorkspaceId,
                                        RequestPublisherAccountId = dynDeal.PublisherAccountId
                                    };

            await _adminServiceGatewayFactory().SendAsync(updateDealRequest);

            if (dynDeal.DealGroupId.HasValue())
            {
                await _mapItemService.PutMapAsync(new DynItemMap
                                                  {
                                                      Id = dynDealRequest.PublisherAccountId,
                                                      EdgeId = DynItemMap.BuildEdgeId(DynItemType.DealGroup, string.Concat("active|", dynDeal.DealGroupId))
                                                  });
            }

            return new StringIdResponse
                   {
                       Id = "Successfully un-cancelled deal"
                   };
        }

        public async Task<StringIdResponse> Post(PostPayInvoice request)
        {
            var stripe = await StripeService.GetInstanceAsync();

            var invoiceResult = await stripe.PayInvoiceAsync(request.InvoiceId, Guid.NewGuid().ToString());

            return new StringIdResponse
                   {
                       Id = invoiceResult.ToJsv()
                   };
        }

        public async Task<OnlyResultsResponse<PublisherAppAccount>> Post(PostGetPublisherAppAccounts request)
        {
            var publisherAppAccounts = new List<PublisherAppAccount>();
            var publisherApps = new Dictionary<long, DynPublisherApp>();

            var dynPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);

            await foreach (var dynPublisherAppAccount in _dynamoDb.FromQuery<DynPublisherAppAccount>(pa => pa.Id == dynPublisherAccount.PublisherAccountId)
                                                                  .Filter(pa => pa.TypeId == (int)DynItemType.PublisherAppAccount &&
                                                                                pa.DeletedOnUtc == null &&
                                                                                pa.PubAccessToken != null &&
                                                                                pa.IsShadowAppAccont == false)
                                                                  .ExecAsync()
                                                                  .Where(p => p.PubAccessToken.HasValue() &&
                                                                              !p.IsShadowAppAccont))
            {
                var dynPublisherApp = publisherApps.ContainsKey(dynPublisherAppAccount.PublisherAppId)
                                          ? publisherApps[dynPublisherAppAccount.PublisherAppId]
                                          : await _dynamoDb.GetPublisherAppAsync(dynPublisherAppAccount.PublisherAppId, true);

                if (!publisherApps.ContainsKey(dynPublisherAppAccount.PublisherAppId) && dynPublisherApp != null)
                {
                    publisherApps[dynPublisherAppAccount.PublisherAppId] = dynPublisherApp;
                }

                publisherAppAccounts.Add(new PublisherAppAccount
                                         {
                                             PublisherAccountId = dynPublisherAccount.PublisherAccountId,
                                             PublisherUserName = dynPublisherAccount.UserName,
                                             FullName = dynPublisherAccount.FullName,
                                             Email = dynPublisherAccount.Email,
                                             PublisherAppId = dynPublisherAppAccount.PublisherAppId,
                                             PublisherAppType = dynPublisherApp.PublisherType,
                                             AccessToken = await _encryptionService.Decrypt64Async(dynPublisherAppAccount.PubAccessToken),
                                             TokenForUserId = dynPublisherAppAccount.ForUserId,
                                             PubTokenType = dynPublisherAppAccount.PubTokenType,
                                             TokenScopes = dynPublisherAppAccount.PubAccessTokenScopes.AsList(),
                                             TokenLastUpdated = dynPublisherAppAccount.TokenLastUpdated.ToDateTime(),
                                             IsSyncDisabled = dynPublisherAppAccount.IsSyncDisabled,
                                             FailuresSinceLastSuccess = dynPublisherAppAccount.FailuresSinceLastSuccess,
                                             LastFailedOn = dynPublisherAppAccount.LastFailedOn.ToDateTime(),
                                             SyncStepsFailCount = dynPublisherAppAccount.SyncStepsFailCount,
                                             SyncStepsLastFailedOn = dynPublisherAppAccount.SyncStepsLastFailedOn
                                         });
            }

            return publisherAppAccounts.AsOnlyResultsResponse();
        }

        public Task Post(SubscribeWorksapceUnlimitted request)
            => WorkspaceService.DefaultWorkspaceSubscriptionService
                               .AddSystemSubscriptionAsync(request.SubscribeWorkspaceId, SubscriptionType.Unlimited);

        public Task<MqRetryResponse> Post(MqRetry request)
        {
            var processor = RydrEnvironment.Container.TryResolveNamed<IMessageQueueProcessor>(request.TypeName);

            if (processor == null)
            {
                return Task.FromResult(new MqRetryResponse
                                       {
                                           AttemptCount = -1,
                                           FailCount = 1
                                       });
            }

            return request.ProcessInQ
                       ? processor.ProcessInqAsync(request)
                       : processor.ReprocessDlqAsync(request);
        }

        public async Task Post(SoftLinkMapRydr request)
        {
            var toPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.ToPublisherAccountId);

            foreach (var rydrPublisherAccountKey in PublisherExtensions.RydrSystemPublisherAccountIds)
            {
                var rydrPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(toPublisherAccount.PublisherType, rydrPublisherAccountKey);

                var map = new DynItemMap
                          {
                              Id = rydrPublisherAccount.PublisherAccountId,
                              EdgeId = toPublisherAccount.ToRydrSoftLinkedAssociationId()
                          };

                if (!(await _mapItemService.MapExistsAsync(map.Id, map.EdgeId)))
                {
                    await _mapItemService.PutMapAsync(map);
                }

                _deferRequestsService.DeferRequest(new LinkPublisherAccount
                                                   {
                                                       ToWorkspaceId = request.ToWorkspaceId,
                                                       FromPublisherAccountId = rydrPublisherAccount.PublisherAccountId,
                                                       ToPublisherAccountId = request.ToPublisherAccountId
                                                   }.WithAdminRequestInfo());
            }
        }

        public async Task Post(RemapSoftBasicPublisherAccount request)
        {
            var softPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.SoftLinkedPublisherAccountId);
            var basicPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.BasicPublisherAccountId);
            var basicWorkspace = await _workspaceService.GetWorkspaceAsync(basicPublisherAccount.WorkspaceId);

            softPublisherAccount.PublisherType = basicPublisherAccount.PublisherType;
            softPublisherAccount.AlternateAccountId = softPublisherAccount.AlternateAccountId.Coalesce(basicPublisherAccount.AlternateAccountId);
            softPublisherAccount.AccountId = basicPublisherAccount.AccountId;
            softPublisherAccount.EdgeId = basicPublisherAccount.GetEdgeId();
            softPublisherAccount.WorkspaceId = basicPublisherAccount.WorkspaceId;
            softPublisherAccount.PageId = basicPublisherAccount.PageId;
            softPublisherAccount.FullName = basicPublisherAccount.FullName;
            softPublisherAccount.Email = basicPublisherAccount.Email;
            softPublisherAccount.Description = basicPublisherAccount.Description;
            softPublisherAccount.Metrics = basicPublisherAccount.Metrics;
            softPublisherAccount.PrimaryPlaceId = basicPublisherAccount.PrimaryPlaceId;
            softPublisherAccount.ProfilePicture = basicPublisherAccount.ProfilePicture;
            softPublisherAccount.Website = basicPublisherAccount.Website;
            softPublisherAccount.AgeRangeMin = basicPublisherAccount.AgeRangeMin;
            softPublisherAccount.AgeRangeMax = basicPublisherAccount.AgeRangeMax;
            softPublisherAccount.Gender = basicPublisherAccount.Gender;

            // Remove old soft account and add one back with new ids
            var publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(PublisherType.Instagram.ToString());

            var publisherApp = await publisherDataService.GetDefaultPublisherAppAsync();

            var publisherAppAccount = await _dynamoDb.GetPublisherAppAccountAsync(basicPublisherAccount.PublisherAccountId, publisherApp.PublisherAppId);

            var accessToken = await _encryptionService.Decrypt64Async(publisherAppAccount.PubAccessToken);


            // Remove the existing basic account, will be mapped to new one
            await _adminServiceGatewayFactory().SendAsync(new DeletePublisherAccountInternal
                                                          {
                                                              PublisherAccountId = basicPublisherAccount.PublisherAccountId
                                                          });

            await _publisherAccountService.HardDeletePublisherAccountForReplacementOnlyAsync(basicPublisherAccount.PublisherAccountId);

            await _publisherAccountService.HardDeletePublisherAccountForReplacementOnlyAsync(softPublisherAccount.PublisherAccountId);
            await _publisherAccountService.PutPublisherAccount(softPublisherAccount);

            await publisherDataService.PutAccessTokenAsync(softPublisherAccount.PublisherAccountId, accessToken,
                                                           publisherAppAccount.ExpiresAt.HasValue
                                                               ? (int)(publisherAppAccount.ExpiresAt.Value - _dateTimeProvider.UtcNowTs)
                                                               : 0);

            basicWorkspace.SecondaryTokenPublisherAccountIds.Remove(basicPublisherAccount.PublisherAccountId);
            basicWorkspace.SecondaryTokenPublisherAccountIds.Add(softPublisherAccount.PublisherAccountId);

            await _workspaceService.UpdateWorkspaceAsync(basicWorkspace,
                                                         () => new DynWorkspace
                                                               {
                                                                   SecondaryTokenPublisherAccountIds = basicWorkspace.SecondaryTokenPublisherAccountIds
                                                               });

            // Link the publisher account connected to the worksapce
            await _adminServiceGatewayFactory().SendAsync(new LinkPublisherAccount
                                                          {
                                                              ToWorkspaceId = basicWorkspace.Id,
                                                              FromPublisherAccountId = 0, // Basic IG accounts are not linked up into any proper token account
                                                              ToPublisherAccountId = softPublisherAccount.PublisherAccountId
                                                          }.WithAdminRequestInfo());

            await Task.Delay(15000);

            await _rydrDataService.ExecAdHocAsync(@"
DELETE   FROM PublisherAccounts
WHERE    Id = @PublisherAccountId;

DELETE   FROM PublisherAccountLinks
WHERE    ToPublisherAccountId = @PublisherAccountId;

DELETE   FROM WorkspaceUserPublisherAccounts
WHERE    PublisherAccountId = @PublisherAccountId;
",
                                                  new
                                                  {
                                                      basicPublisherAccount.PublisherAccountId
                                                  });
        }

        public async Task Post(RemapUser request)
        {
            var fromUser = await _userService.GetUserByAuthUidAsync(request.FromUserFirebaseId);
            var toUser = await _userService.GetUserByAuthUidAsync(request.ToUserFirebaseId);

            Guard.AgainstInvalidData(!fromUser.AuthProviderUid.EqualsOrdinal(request.FromUserFirebaseId), "Mismatch Firebase identifiers for FROM user");
            Guard.AgainstInvalidData(!toUser.AuthProviderUid.EqualsOrdinal(request.ToUserFirebaseId), "Mismatch Firebase identifiers for TO user");

            // Take the old user's identifiers and map them onto the to user - have to delete records for the current TO user, and the basically put them back with new identifying info
            // Store all the variables and models and everything first, then process
            var toUserExistingEdgeId = toUser.EdgeId;
            var toUserExistingAuthUid = toUser.AuthProviderUid;
            var toUserExistingMapLongId = toUserExistingAuthUid.ToLongHashCode();

            toUser.Email = fromUser.Email;
            toUser.UserName = fromUser.Email.Coalesce(fromUser.AuthProviderUid);
            toUser.AuthProviderUid = fromUser.AuthProviderUid;
            toUser.FirstName = fromUser.FirstName.Coalesce(toUser.FirstName).Trim();
            toUser.LastName = fromUser.LastName.Coalesce(toUser.LastName).Trim();
            toUser.FullName = string.Concat(toUser.FirstName, " ", toUser.LastName).Trim();
            toUser.LastAuthPublisherType = fromUser.LastAuthPublisherType;
            toUser.Avatar = fromUser.Avatar.Coalesce(toUser.Avatar);

            var toUserNewMapLongId = toUser.AuthProviderUid.ToLongHashCode();

            // Remove the old user workspaces, user account, etc
            await foreach (var existingFromUserWorkspace in _workspaceService.GetWorkspacesOwnedByAsync(fromUser.UserId))
            {
                _deferRequestsService.DeferLowPriRequest(new DeleteWorkspaceInternal
                                                         {
                                                             Id = existingFromUserWorkspace.Id
                                                         });
            }

            // Remove the old user
            await _userService.DeleteUserAsync(fromUser, hardDelete: true, authUid: fromUser.AuthProviderUid.Coalesce(request.FromUserFirebaseId));

            // Delete existing records for the TO user
            await _dynamoDb.DeleteItemAsync<DynUser>(toUser.Id, toUserExistingEdgeId);
            await _mapItemService.DeleteMapAsync(toUserExistingMapLongId, DynItemMap.BuildEdgeId(DynItemType.User, toUserExistingAuthUid));

            // Put the new records for the TO user
            await _mapItemService.PutMapAsync(new DynItemMap
                                              {
                                                  Id = toUserNewMapLongId,
                                                  EdgeId = DynItemMap.BuildEdgeId(DynItemType.User, toUser.AuthProviderUid),
                                                  ReferenceNumber = toUser.UserId,
                                                  MappedItemEdgeId = toUser.EdgeId
                                              });

            await _userService.UpdateUserAsync(toUser);
        }

        public async Task<StringIdResponse> Post(RebalanceRecurringJobs request)
        {
            var countProcessed = 0;
            var countDeleted = 0;

            var response = new StringIdResponse
                           {
                               Id = "Nothing processed"
                           };

            var publisherAccountIds = await _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<long>(@"
SELECT    DISTINCT pa.Id
FROM      PublisherAccounts pa
WHERE     pa.PublisherType = 1;
"));

            if (publisherAccountIds.IsNullOrEmpty())
            {
                return response;
            }

            _log.Info($"Starting rebalancing of [{publisherAccountIds.Count}] accounts");

            // In terms of spreading things out, we essentially take all the seconds we have in the syncInterval
            var syncSecondsAvailable = _syncIntervalMinutes * 60.0;
            var waitTicksPerJob = (long)(TimeSpan.TicksPerMillisecond * ((syncSecondsAvailable / publisherAccountIds.Count) * 1000.0)); // One job every x ticks
            var lastJobScheduledAt = 0L;
            var doLogWaitTimes = !RydrEnvironment.IsReleaseEnvironment;

            var facebookMediaSyncService = RydrEnvironment.Container.ResolveNamed<IPublisherMediaSyncService>(PublisherType.Facebook.ToString());

            foreach (var publisherAccountId in publisherAccountIds)
            {
                var mediaSyncJobId = PostSyncRecentPublisherAccountMedia.GetRecurringJobId(publisherAccountId);

                RecurringJob.RemoveIfExists(mediaSyncJobId);
                RecurringJob.RemoveIfExists(PostAnalyzePublisherMedia.GetRecurringJobId(publisherAccountId));
                RecurringJob.RemoveIfExists(PostUpdateCreatorMetrics.GetRecurringJobId(publisherAccountId));

                if (request.DeleteOnly)
                {
                    countDeleted++;

                    continue;
                }

                var dynPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

                if (dynPublisherAccount == null || dynPublisherAccount.IsDeleted())
                {
                    countDeleted++;

                    continue;
                }

                if (lastJobScheduledAt > 0)
                {
                    var waitForTicks = waitTicksPerJob - (DateTimeHelper.UtcNow.Ticks - lastJobScheduledAt);

                    if (waitForTicks > 2500)
                    {
                        var delay = TimeSpan.FromTicks(waitForTicks);

                        if (doLogWaitTimes)
                        {
                            _log.Info($"Waiting for [{(int)delay.TotalMinutes}:{delay.Seconds}:{delay.Milliseconds}] (minutes:seconds.milliseconds)...");
                        }

                        await Task.Delay(delay);
                    }
                }

                countProcessed++;

                await facebookMediaSyncService.AddOrUpdateMediaSyncAsync(dynPublisherAccount.PublisherAccountId);

                lastJobScheduledAt = DateTimeHelper.UtcNow.Ticks;

                if (doLogWaitTimes)
                {
                    _log.DebugInfo($"   Updated media sync jobs for [{dynPublisherAccount.DisplayName()}]");
                }
            }

            response.Id = $"[{countProcessed}] accounts scheduled, [{countDeleted}] accounts deleted";

            var dealStatsProcessed = 0;
            var dealStatsDeleted = 0;

            // Now need to process the deal stats jobs
            foreach (var publisherAccountLink in _rydrDataService.QueryLazy(d => d.Select<RydrPublisherAccountLink>(l => l.ToPublisherAccountId > 0), t => t.Id))
            {
                var fromPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountLink.FromPublisherAccountId);
                var toPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountLink.ToPublisherAccountId);

                if ((publisherAccountLink.FromPublisherAccountId > 0 && fromPublisherAccount == null) || toPublisherAccount == null)
                {
                    await _rydrDataService.DeleteByIdAsync<RydrPublisherAccountLink, string>(publisherAccountLink.Id);

                    dealStatsDeleted++;

                    continue;
                }

                var contextWorkspaceId = toPublisherAccount.GetContextWorkspaceId(publisherAccountLink.WorkspaceId);

                RecurringJob.RemoveIfExists(PublisherAccountRecentDealStatsUpdate.GetRecurringJobId(toPublisherAccount.PublisherAccountId, contextWorkspaceId));

                if (publisherAccountLink.DeletedOn.HasValue || request.DeleteOnly)
                {
                    dealStatsDeleted++;

                    continue;
                }

                // Accounts have to be non-deleted, workspace has to be associated to each, from has to be associated to to...
                if (toPublisherAccount.IsDeleted() ||
                    !(await _associationService.IsAssociatedAsync(publisherAccountLink.WorkspaceId, toPublisherAccount.PublisherAccountId)))
                {
                    _deferRequestsService.DeferRequest(new DelinkPublisherAccount
                                                       {
                                                           FromWorkspaceId = publisherAccountLink.WorkspaceId,
                                                           FromPublisherAccountId = fromPublisherAccount?.PublisherAccountId ?? 0,
                                                           ToPublisherAccountId = toPublisherAccount.PublisherAccountId
                                                       });

                    dealStatsDeleted++;

                    continue;
                }

                // From accounts must be valid as well (if a from account is included, could be basic)
                if (publisherAccountLink.FromPublisherAccountId > 0 &&
                    (fromPublisherAccount.IsDeleted() ||
                     !(await _associationService.IsAssociatedAsync(publisherAccountLink.WorkspaceId, fromPublisherAccount.PublisherAccountId))) ||
                    !(await _associationService.IsAssociatedAsync(publisherAccountLink.FromPublisherAccountId, toPublisherAccount.PublisherAccountId)))
                {
                    _deferRequestsService.DeferRequest(new DelinkPublisherAccount
                                                       {
                                                           FromWorkspaceId = publisherAccountLink.WorkspaceId,
                                                           FromPublisherAccountId = fromPublisherAccount.PublisherAccountId,
                                                           ToPublisherAccountId = toPublisherAccount.PublisherAccountId
                                                       });

                    dealStatsDeleted++;

                    continue;
                }

                _deferRequestsService.PublishMessageRecurring(new PostDeferredLowPriMessage
                                                              {
                                                                  Dto = new PublisherAccountRecentDealStatsUpdate
                                                                        {
                                                                            PublisherAccountId = toPublisherAccount.PublisherAccountId,
                                                                            InWorkspaceId = publisherAccountLink.WorkspaceId
                                                                        }.WithAdminRequestInfo()
                                                                         .ToJsv(),
                                                                  Type = typeof(PublisherAccountRecentDealStatsUpdate).FullName
                                                              }.WithAdminRequestInfo(),
                                                              CronBuilder.Daily(RandomProvider.GetRandomIntBeween(7, 11),
                                                                                RandomProvider.GetRandomIntBeween(1, 59)),
                                                              PublisherAccountRecentDealStatsUpdate.GetRecurringJobId(toPublisherAccount.PublisherAccountId, contextWorkspaceId));

                dealStatsProcessed++;
            }

            response.Id = $"{response.Id}, [{dealStatsProcessed}] deal stat updates scheduled, [{dealStatsDeleted}] deal stat updates deleted";

            _log.Info($"Finished rebalancing of [{publisherAccountIds.Count}] accounts - {response.Id}");

            return response;
        }

        public async Task Post(RebuildEsBusinesses request)
        {
            if (request.PublisherAccountId > 0)
            {
                var dynPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);

                var esBusiness = await dynPublisherAccount.ToEsBusinessAsync();

                await _elasticClient.IndexAsync(esBusiness, idx => idx.Index(ElasticIndexes.BusinessesAlias)
                                                                      .Id(esBusiness.PublisherAccountId));

                return;
            }

            // Sync all as appropriate
            await foreach (var publisherAccountBatch in _publisherAccountService.GetPublisherAccountsAsync(await _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<long>(@"
SELECT    DISTINCT pa.Id
FROM      PublisherAccounts pa
WHERE     pa.DeletedOn IS NULL
          AND pa.AccountType = 2
          AND pa.RydrAccountType = 1;
")))
                                                                                .ToBatchesOfAsync(50))
            {
                if (publisherAccountBatch.IsNullOrEmpty())
                {
                    continue;
                }

                var esBusinesses = new List<EsBusiness>(publisherAccountBatch.Count);

                foreach (var publisherAccount in publisherAccountBatch)
                {
                    var esBusiness = await publisherAccount.ToEsBusinessAsync();

                    esBusinesses.Add(esBusiness);
                }

                var response = await _elasticClient.IndexManyAsync(esBusinesses, ElasticIndexes.BusinessesAlias);

                if (!response.Successful())
                {
                    break;
                }
            }
        }

        public async Task Post(RebuildEsCreators request)
        {
            if (request.PublisherAccountId > 0)
            {
                _deferRequestsService.DeferLowPriRequest(new PostUpdateCreatorMetrics
                                                         {
                                                             PublisherIdentifier = request.PublisherAccountId.ToStringInvariant()
                                                         });

                return;
            }

            // Sync all as appropriate
            foreach (var publisherAccountId in (await _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<long>(@"
SELECT    DISTINCT pa.Id
FROM      PublisherAccounts pa
WHERE     pa.DeletedOn IS NULL
          AND pa.AccountType = 2
          AND pa.RydrAccountType = 2;
"))
                                               ))
            {
                _deferRequestsService.DeferLowPriRequest(new PostUpdateCreatorMetrics
                                                         {
                                                             PublisherIdentifier = publisherAccountId.ToStringInvariant()
                                                         });
            }
        }

        public async Task Post(RebuildEsMedias request)
        {
            if (request.PublisherAccountId > 0)
            {
                await RebuildOnePublisherEsMediaAsync(request.PublisherAccountId);

                return;
            }

            // Sync all as appropriate
            foreach (var publisherAccountId in (await _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<long>(@"
SELECT    DISTINCT pa.Id
FROM      PublisherAccounts pa
WHERE     pa.DeletedOn IS NULL
          AND pa.AccountType = 2
          AND pa.RydrAccountType <= 3
          AND pa.OptInToAi > 0;
"))
                                               ))
            {
                await RebuildOnePublisherEsMediaAsync(publisherAccountId);
            }
        }

        public async Task Post(RebuildEsDeals request)
        {
            if (request.PublisherAccountId > 0)
            {
                await RebuildOnePublisherEsDealsAsync(request.PublisherAccountId, request.DeferDealAsAffected);

                return;
            }

            // Sync all as appropriate
            foreach (var publisherAccountId in (await _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<long>(@"
SELECT    DISTINCT d.PublisherAccountId
FROM      Deals d;
"))
                                               ))
            {
                await RebuildOnePublisherEsDealsAsync(publisherAccountId, request.DeferDealAsAffected);
            }
        }

        public async Task Post(AuditCurrentPublisherAccountStats request)
        {
            if (request.PublisherAccountId > 0 && request.InWorkspaceId > 0)
            {
                await AuditOnePublisherWorkspaceStatsAsync(request.PublisherAccountId, request.InWorkspaceId);

                return;
            }

            if (request.PublisherAccountId <= 0 && request.InWorkspaceId <= 0)
            {
                // All the workspaces and all the publishers in those workspaces...
                var allWorkspaceIds = await _workspaceService.GetAllWorkspaceIdsAsync();

                foreach (var workspaceId in allWorkspaceIds)
                {
                    await AuditOneWorkspaceAsync(workspaceId);
                }

                return;
            }

            if (request.PublisherAccountId > 0)
            { // All workspaces for the given publisher
                var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);
                var publisherAccountName = publisherAccount.DisplayName();

                // In this case, we only need to perform once for each workspace context
                var workspaceContextIdsProcessed = new HashSet<long>();

                await foreach (var workspaceId in _workspaceService.GetAssociatedWorkspaceIdsAsync(publisherAccount))
                {
                    var contextWorkspaceId = publisherAccount.GetContextWorkspaceId(workspaceId);

                    if (!workspaceContextIdsProcessed.Add(contextWorkspaceId))
                    {
                        continue;
                    }

                    _log.DebugInfoFormat("Starting audit of stats for PublisherAccount [{0}] in workspaceId [{1}]", publisherAccountName, workspaceId);

                    await AuditOnePublisherWorkspaceStatsAsync(publisherAccount.PublisherAccountId, workspaceId);
                }

                return;
            }

            // Must have a workspace only, so audit all publisher accounts in the given workspace
            await AuditOneWorkspaceAsync(request.InWorkspaceId);
        }

        private async Task AuditOneWorkspaceAsync(long workspaceId)
        {
            var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId);
            var workspaceName = string.Concat(workspace.Name, " (", workspace.Id, ")");

            await foreach (var publisherAccount in _workspaceService.GetWorkspaceUserPublisherAccountsAsync(workspace.Id, workspace.OwnerId))
            {
                var publisherAccountName = publisherAccount.DisplayName();

                _log.DebugInfoFormat("Starting audit of stats for PublisherAccount [{0}] in workspace [{1}]", publisherAccountName, workspaceName);

                await AuditOnePublisherWorkspaceStatsAsync(publisherAccount.PublisherAccountId, workspace.Id);
            }
        }

        private async Task AuditOnePublisherWorkspaceStatsAsync(long publisherAccountId, long inWorkspaceId)
        {
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId);
            var workspace = await _workspaceService.GetWorkspaceAsync(inWorkspaceId);

            var contextWorkspaceId = publisherAccount.GetContextWorkspaceId(workspace);
            var dealRequestTypeOwner = DynItem.BuildTypeOwnerSpaceHash(DynItemType.DealRequest, publisherAccount.PublisherAccountId);
            var nowUtc = _dateTimeProvider.UtcNowTs;

            var actualStatMap = new Dictionary<DealStatType, long>();

            var dealRequestStatuses = publisherAccount.IsInfluencer()
                                          ? _dynamoDb.FromQueryIndex<DynItemEdgeIdGlobalIndex>(i => i.EdgeId == publisherAccount.PublisherAccountId.ToEdgeId() &&
                                                                                                    Dynamo.BeginsWith(i.TypeReference, _dealRequestTypeReferencePrefix))
                                                     .Select(i => new
                                                                  {
                                                                      i.StatusId
                                                                  })
                                                     .Exec()
                                                     .Select(i => i.StatusId)

                                          // Just a performance fork - if the contextWorkspaceId > 0, we know we are filtering to a single workspaceId, and do not need to go fetch all the actual request
                                          // objects to inspect for a correct contextWorkspace
                                          : contextWorkspaceId > 0
                                              ? _dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(i => i.TypeOwnerSpace == dealRequestTypeOwner &&
                                                                                                                         Dynamo.Between(i.ReferenceId, "1500000000", nowUtc.ToStringInvariant()))
                                                         .Filter(dr => dr.WorkspaceId == contextWorkspaceId)
                                                         .Select(i => new
                                                                      {
                                                                          i.StatusId
                                                                      })
                                                         .Exec()
                                                         .Select(i => i.StatusId)
                                              : _dynamoDb.GetItems<DynDealRequest>(_dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(i => i.TypeOwnerSpace == dealRequestTypeOwner &&
                                                                                                                                                            Dynamo.Between(i.ReferenceId, "1500000000", nowUtc.ToStringInvariant()))
                                                                                            .Select(i => new
                                                                                                         {
                                                                                                             i.Id,
                                                                                                             i.EdgeId
                                                                                                         })
                                                                                            .Exec()
                                                                                            .Select(i => i.GetDynamoId()))
                                                         .Where(dr => dr.DealContextWorkspaceId == contextWorkspaceId)
                                                         .Select(i => i.StatusId);

            foreach (var dealRequestStatus in dealRequestStatuses)
            {
                var requestStatus = dealRequestStatus.TryToEnum(DealRequestStatus.Unknown);

                var statType = requestStatus.ToStatType().ToCurrentStatType();

                if (statType == DealStatType.Unknown)
                {
                    continue;
                }

                var val = actualStatMap.ContainsKey(statType)
                              ? actualStatMap[statType]
                              : 0;

                actualStatMap[statType] = val + 1;
            }

            // Go get all the existing current stats
            // For an influencer and/or personal workspaces, they are not bound by workspaces
            // Others are, so get items based on a specific workspace...
            var edgePrefix = string.Concat(_publisherAccountDealStatEdgePrefix, contextWorkspaceId.ToStringInvariant(), "|Current");

            var existingDealStats = _dynamoDb.FromQuery<DynPublisherAccountStat>(d => d.Id == publisherAccount.PublisherAccountId &&
                                                                                      Dynamo.BeginsWith(d.EdgeId, edgePrefix))
                                             .Filter(d => d.TypeId == (int)DynItemType.PublisherAccountStat &&
                                                          d.StatType != (int)DealStatType.Unknown)
                                             .Exec()
                                             .ToDictionary(ds => ds.StatType);

            var dealStatsToUpdate = new List<KeyValuePair<DealStatType, long>>();

            foreach (var actualStat in actualStatMap)
            {
                if (existingDealStats.ContainsKey(actualStat.Key) && existingDealStats[actualStat.Key].Cnt.GetValueOrDefault() == actualStat.Value)
                { // Existing and actual match, nothing to do
                    _log.InfoFormat("Stat [{0}] values match - existing [{1}], actual [{2}]", actualStat.Key.ToString(), existingDealStats[actualStat.Key].Cnt.Value, actualStat.Value);

                    continue;
                }

                _log.WarnFormat("Stat [{0}] values DO NOT match - existing [{1}], actual [{2}]", actualStat.Key.ToString(), existingDealStats.ContainsKey(actualStat.Key)
                                                                                                                                ? existingDealStats[actualStat.Key].Cnt.GetValueOrDefault()
                                                                                                                                : 0, actualStat.Value);

                // Something is off, need to update this one
                dealStatsToUpdate.Add(actualStat);
            }

            // We just validated that every actualStatMap value was checked, but there may be some existing that are not actually set (i.e. 0)
            existingDealStats.Where(x => x.Value.Cnt.GetValueOrDefault() != 0 && !actualStatMap.ContainsKey(x.Key))
                             .Each(x =>
                                   {
                                       _log.WarnFormat("Stat [{0}] exists with non-zero value of [{1}] but actually should be 0", x.Key.ToString(), x.Value.Cnt.Value);

                                       dealStatsToUpdate.Add(new KeyValuePair<DealStatType, long>(x.Key, 0));
                                   });

            // Anything to update?
            if (dealStatsToUpdate.IsNullOrEmpty())
            {
                _log.InfoFormat("Everything matches correctly, nothing to do");

                return;
            }

            // Update those that need adjustment
            foreach (var dealStatToUpdate in dealStatsToUpdate)
            {
                var existingPublisherStat = existingDealStats.ContainsKey(dealStatToUpdate.Key)
                                                ? existingDealStats[dealStatToUpdate.Key]
                                                : null;

                var edgeId = DynPublisherAccountStat.BuildEdgeId(DynItemType.DealStat, contextWorkspaceId, dealStatToUpdate.Key);

                if (existingPublisherStat == null)
                { // Add a new one
                    _log.DebugInfoFormat("Adding new publisher stat for type [{0}], value of [{1}], edgeId [{2}]", dealStatToUpdate.Key.ToString(), dealStatToUpdate.Value, edgeId);

                    var replacedItem = await _dynamoDb.PutItemAsync(new DynPublisherAccountStat
                                                                    {
                                                                        Id = publisherAccount.PublisherAccountId,
                                                                        EdgeId = edgeId,
                                                                        Cnt = dealStatToUpdate.Value,
                                                                        PublisherAccountId = publisherAccount.PublisherAccountId,
                                                                        StatType = dealStatToUpdate.Key,
                                                                        TypeId = (int)DynItemType.PublisherAccountStat,
                                                                        ReferenceId = publisherAccount.PublisherAccountId.ToStringInvariant(),
                                                                        WorkspaceId = workspace.Id,
                                                                        ModifiedBy = workspace.OwnerId,
                                                                        ModifiedOnUtc = nowUtc,
                                                                        CreatedBy = workspace.OwnerId,
                                                                        CreatedOnUtc = nowUtc,
                                                                        CreatedWorkspaceId = workspace.Id
                                                                    },
                                                                    true);

                    if (replacedItem != null)
                    { // Put back the replaced one
                        await _dynamoDb.PutItemAsync(replacedItem);

                        throw new OperationCannotBeCompletedException($"PublisherStat created under race condition - account likely needs clean audit. Stat impacted [{dealStatToUpdate.Key.ToString()}]");
                    }
                }
                else
                { // Update existing one
                    _log.DebugInfoFormat("Updating existing publisher stat for type [{0}] from existing value of [{1}] to new value of [{2}], edgeId [{3}]", dealStatToUpdate.Key.ToString(), existingPublisherStat.Cnt.GetValueOrDefault(), dealStatToUpdate.Value, edgeId);

                    _dynamoDb.UpdateItem(_dynamoDb.UpdateExpression<DynPublisherAccountStat>(publisherAccount.PublisherAccountId, edgeId)
                                                  .Set(() => new DynPublisherAccountStat
                                                             {
                                                                 Cnt = dealStatToUpdate.Value
                                                             })
                                                  .Condition(x => x.StatType == dealStatToUpdate.Key &&
                                                                  x.Cnt == existingPublisherStat.Cnt.GetValueOrDefault()));
                }
            }

            await _serviceCacheInvalidator.InvalidatePublisherAccountAsync(publisherAccount.PublisherAccountId, "publisheracct");
        }

        private async Task RebuildOnePublisherEsMediaAsync(long publisherAccountId)
        {
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId);

            // Enumerable of batches of esMedias to send in batch call to es...
            IEnumerable<List<EsMedia>> getEsMediaBatches()
            {
                foreach (var dynPublisherMediaBatch in _dynamoDb.FromQuery<DynPublisherMedia>(m => m.Id == publisherAccount.PublisherAccountId &&
                                                                                                   Dynamo.BeginsWith(m.EdgeId, "00"))
                                                                .Filter(m => m.TypeId == (int)DynItemType.PublisherMedia &&
                                                                             m.DeletedOnUtc == null &&
                                                                             m.IsAnalyzed)
                                                                .Exec()
                                                                .ToBatchesOf(50.ToDynamoBatchCeilingTake())
                                                                .Where(b => !b.IsNullOrEmpty()))
                {
                    var dynMediaAnalyses = _dynamoDb.GetItems<DynPublisherMediaAnalysis>(dynPublisherMediaBatch.Select(m => new DynamoId(m.PublisherMediaId,
                                                                                                                                         DynPublisherMediaAnalysis.BuildEdgeId(m.PublisherType, m.MediaId))));

                    if (dynMediaAnalyses.IsNullOrEmpty())
                    {
                        continue;
                    }

                    var esMediaBatch = new List<EsMedia>(dynMediaAnalyses.Count);

                    foreach (var dynMediaAnalysis in dynMediaAnalyses)
                    {
                        var dynPublisherMedia = dynPublisherMediaBatch.FirstOrDefault(m => m.PublisherMediaId == dynMediaAnalysis.PublisherMediaId);

                        if (dynPublisherMedia == null)
                        {
                            continue;
                        }

                        var esMedia = dynPublisherMedia.ToEsMedia(dynMediaAnalysis);

                        esMediaBatch.Add(esMedia);
                    }

                    if (!esMediaBatch.IsNullOrEmpty())
                    {
                        yield return esMediaBatch;
                    }
                }
            }

            // Push each batch to es
            foreach (var esMediaBatch in getEsMediaBatches())
            {
                var response = await _elasticClient.IndexManyAsync(esMediaBatch, ElasticIndexes.MediaAlias);

                if (!response.Successful())
                {
                    break;
                }
            }
        }

        private async Task RebuildOnePublisherEsDealsAsync(long publisherAccountId, bool deferDealAsAffected = false)
        {
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId);

            async IAsyncEnumerable<EsDeal> getEsDeals()
            {
                await foreach (var deal in _dynamoDb.FromQuery<DynDeal>(d => d.Id == publisherAccount.PublisherAccountId &&
                                                                             Dynamo.BeginsWith(d.EdgeId, "00"))
                                                    .Filter(m => m.TypeId == (int)DynItemType.Deal)
                                                    .ExecAsync())
                {
                    var esDeal = await deal.ToEsDealAsync();

                    if (esDeal != null)
                    {
                        yield return esDeal;
                    }
                }
            }

            await foreach (var esDealBatch in getEsDeals().ToBatchesOfAsync(50.ToDynamoBatchCeilingTake()))
            {
                if (esDealBatch.IsNullOrEmpty())
                {
                    continue;
                }

                var response = await _elasticClient.IndexManyAsync(esDealBatch, ElasticIndexes.DealsAlias);

                if (deferDealAsAffected)
                {
                    // Defer the deal and all active requests as affected...
                    _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                         {
                                                             CompositeIds = esDealBatch.Select(e => new DynamoItemIdEdge
                                                                                                    {
                                                                                                        Id = e.PublisherAccountId,
                                                                                                        EdgeId = e.DealId.ToEdgeId()
                                                                                                    })
                                                                                       .AsList(),
                                                             Type = RecordType.Deal
                                                         });

                    foreach (var esDeal in esDealBatch)
                    {
                        var deferRequest = new PostDeferredAffected
                                           {
                                               CompositeIds = (await _dealRequestService.GetAllActiveDealRequestsAsync(esDeal.DealId)
                                                              ).Select(dr => new DynamoItemIdEdge
                                                                             {
                                                                                 Id = esDeal.DealId,
                                                                                 EdgeId = dr.PublisherAccountId.ToEdgeId()
                                                                             })
                                                               .AsList(),
                                               Type = RecordType.DealRequest
                                           };

                        if (!deferRequest.CompositeIds.IsNullOrEmpty())
                        {
                            _deferRequestsService.PublishMessage(deferRequest);
                        }
                    }
                }

                if (!response.Successful())
                {
                    break;
                }
            }
        }
    }
}
