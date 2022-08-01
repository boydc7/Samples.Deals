using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnumsNET;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services
{
    public class PublisherAccountPublicService : BaseApiService
    {
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IRydrDataService _rydrDataService;
        private readonly IDealService _dealService;

        public PublisherAccountPublicService(IPublisherAccountService publisherAccountService,
                                             IRydrDataService rydrDataService,
                                             IDealService dealService)
        {
            _publisherAccountService = publisherAccountService;
            _rydrDataService = rydrDataService;
            _dealService = dealService;
        }

        [RydrForcedSimpleCacheResponse(3600)]
        public async Task<OnlyResultResponse<BusinessReportData>> Get(GetBusinessReportExternal request)
        {
            var bizReportMap = await MapItemService.DefaultMapItemService
                                                   .TryGetMapByHashedEdgeAsync(DynItemType.PublisherAccount, request.BusinessReportId);

            var businessPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(bizReportMap.ReferenceNumber.Value);
            var completedOnStart = bizReportMap.Items["CompletedOnStart"].ToDateTime();
            var completedOnEnd = bizReportMap.Items["CompletedOnEnd"].ToDateTime();
            var dealId = bizReportMap.Items["DealId"].ToLong();
            var nowUtc = _dateTimeProvider.UtcNowTs;

            var completionMetrics = await _adminServiceGatewayFactory().SendAsync(new GetDealCompletionMediaMetrics
                                                                                  {
                                                                                      PublisherAccountId = businessPublisherAccount.PublisherAccountId,
                                                                                      CompletedOnStart = completedOnStart,
                                                                                      CompletedOnEnd = completedOnEnd,
                                                                                      DealId = dealId
                                                                                  }.WithAdminRequestInfo());

            var completedDealsIds = (await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<CompletedDealRequestId>(@"
SELECT  DISTINCT dr.DealId, dr.PublisherAccountId, dr.CompletedOn
FROM    DealRequests dr
WHERE   dr.CompletedOn IS NOT NULL
        AND dr.CompletedOn >= @CompletedOnStart
        AND dr.CompletedOn < @CompletedOnEnd
        AND EXISTS
        (
        SELECT  NULL
        FROM    DealRequestMedia drm
        WHERE   drm.DealId = dr.DealId
                AND drm.PublisherAccountId = dr.PublisherAccountId
        )
        AND EXISTS
        (
        SELECT  NULL
        FROM    Deals d
        WHERE   d.Id = dr.DealId
                AND d.PublisherAccountId = @BusinessPublisherAccountId
        )
LIMIT   1000;",
                                                                                                                        new
                                                                                                                        {
                                                                                                                            BusinessPublisherAccountId = businessPublisherAccount.PublisherAccountId,
                                                                                                                            CompletedOnStart = completedOnStart,
                                                                                                                            CompletedOnEnd = completedOnEnd
                                                                                                                        }))).AsListReadOnly();

            var response = new BusinessReportData
                           {
                               CompletionMetrics = completionMetrics.Result,
                               CompletedDealRequests = new List<BusinessReportDealRequest>(completedDealsIds?.Count ?? 0)
                           };

            if (completedDealsIds.IsNullOrEmptyReadOnly())
            {
                return response.AsOnlyResultResponse();
            }

            var dealMap = (await _dealService.GetDynDealsAsync(completedDealsIds.Select(cdi => cdi.DealId).Distinct()
                                                                                .Select(did => new DynamoId(businessPublisherAccount.PublisherAccountId,
                                                                                                            did.ToEdgeId())))).ToDictionarySafe(d => d.DealId);

            var creatorPublisherAccountMap = await _publisherAccountService.GetPublisherAccountsAsync(completedDealsIds.Select(cdi => cdi.PublisherAccountId)
                                                                                                                       .Distinct())
                                                                           .ToDictionarySafe(p => p.PublisherAccountId,
                                                                                             p => p.ToPublisherAccountProfile());

            response.DealCompletionMetrics = dealId > 0
                                                 ? null
                                                 : new Dictionary<long, DealCompletionMediaMetrics>(dealMap.Count);

            var linkItemMaps = new List<DynItemMap>(completedDealsIds.Count);

            foreach (var dealRequestIds in completedDealsIds)
            {
                var dynDeal = dealMap[dealRequestIds.DealId];

                var linkId = dynDeal.ToDealPublicLinkId(string.Concat(Guid.NewGuid().ToString(), "|", dealRequestIds.PublisherAccountId, "|", nowUtc));

                response.CompletedDealRequests.Add(new BusinessReportDealRequest
                                                   {
                                                       DealRequestReportLink = linkId,
                                                       CompletedOn = dealRequestIds.CompletedOn,
                                                       DealId = dynDeal.DealId,
                                                       DealTitle = dynDeal.Title,
                                                       PublisherAccount = creatorPublisherAccountMap[dealRequestIds.PublisherAccountId]
                                                   });

                if (response.DealCompletionMetrics != null && !response.DealCompletionMetrics.ContainsKey(dynDeal.DealId))
                {
                    var dealCompletionMetrics = await _adminServiceGatewayFactory().SendAsync(new GetDealCompletionMediaMetrics
                                                                                              {
                                                                                                  PublisherAccountId = dynDeal.PublisherAccountId,
                                                                                                  CompletedOnStart = completedOnStart,
                                                                                                  CompletedOnEnd = completedOnEnd,
                                                                                                  DealId = dynDeal.DealId
                                                                                              }.WithAdminRequestInfo());

                    response.DealCompletionMetrics[dynDeal.DealId] = dealCompletionMetrics.Result;
                }

                var linkIdHashCode = linkId.ToLongHashCode();

                linkItemMaps.Add(new DynItemMap
                                 {
                                     Id = linkIdHashCode,
                                     EdgeId = DynItemMap.BuildEdgeId(DynItemType.DealRequest, linkId),
                                     MappedItemEdgeId = dealRequestIds.PublisherAccountId.ToStringInvariant(),
                                     ReferenceNumber = dynDeal.DealId,
                                     ExpiresAt = bizReportMap.ExpiresAt
                                 });
            }

            await _dynamoDb.PutItemsAsync(linkItemMaps);

            return response.AsOnlyResultResponse();
        }

        private class CompletedDealRequestId
        {
            public long DealId { get; set; }
            public long PublisherAccountId { get; set; }
            public DateTime CompletedOn { get; set; }
        }
    }

    [RydrCacheResponse(900, "deals", "query")]
    public class PublisherAccountService : BaseAuthenticatedApiService
    {
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IUserNotificationService _userNotificationService;
        private readonly IWorkspaceService _workspaceService;
        private readonly IDeferRequestsService _deferRequestsService;

        public PublisherAccountService(IPublisherAccountService publisherAccountService,
                                       IUserNotificationService userNotificationService,
                                       IWorkspaceService workspaceService,
                                       IDeferRequestsService deferRequestsService)
        {
            _publisherAccountService = publisherAccountService;
            _userNotificationService = userNotificationService;
            _workspaceService = workspaceService;
            _deferRequestsService = deferRequestsService;
        }

        public Task<OnlyResultResponse<PublisherAccount>> Get(GetMyPublisherAccount request)
            => DoGetPublisherAccountForRequestAsync(request.RequestPublisherAccountId).Then(p => p.AsOnlyResultResponse());

        public Task<OnlyResultResponse<PublisherAccount>> Get(GetPublisherAccount request)
            => DoGetPublisherAccountForRequestAsync(request.Id).Then(p => p.AsOnlyResultResponse());

        public Task<OnlyResultResponse<PublisherAccountInfo>> Get(GetPublisherAccountExternal request)
            => DoGetPublisherAccountForRequestAsync(request.Id).Then(p => p.CreateCopy<PublisherAccountInfo>().AsOnlyResultResponse());

        public async Task<OnlyResultResponse<StringIdResponse>> Get(GetBusinessReportExternalLink request)
        {
            var publisherAccountId = request.GetPublisherIdFromIdentifier();
            var nowUtc = _dateTimeProvider.UtcNowTs;

            var linkId = string.Concat(Guid.NewGuid().ToString(), "|", publisherAccountId, "|", nowUtc, "|", request.DealId, "|",
                                       request.CompletedOnStart.ToUnixTimestamp(), "|", request.CompletedOnEnd.ToUnixTimestamp())
                               .ToSafeSha64();

            var linkIdHashCode = linkId.ToLongHashCode();

            await MapItemService.DefaultMapItemService
                                .PutMapAsync(new DynItemMap
                                             {
                                                 Id = linkIdHashCode,
                                                 EdgeId = DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, linkId),
                                                 ReferenceNumber = publisherAccountId,
                                                 ExpiresAt = nowUtc + request.Duration.Gz(100_000), // Defaults to about 30 hours from now
                                                 Items = new Dictionary<string, string>
                                                         {
                                                             {
                                                                 "CompletedOnStart", request.CompletedOnStart.ToUnixTimestamp().ToStringInvariant()
                                                             },
                                                             {
                                                                 "CompletedOnEnd", request.CompletedOnEnd.ToUnixTimestamp().ToStringInvariant()
                                                             },
                                                             {
                                                                 "DealId", request.DealId.ToStringInvariant()
                                                             }
                                                         }
                                             });

            return new StringIdResponse
                   {
                       Id = linkId
                   }.AsOnlyResultResponse();
        }

        public async Task<OnlyResultsResponse<PublisherAccountLinkInfo>> Get(GetPublisherAccounts request)
        {
            List<PublisherAccountLinkInfo> results = null;

            if (request.PublisherAccountId > 0)
            {
                results = await _publisherAccountService.GetLinkedPublisherAccountsAsync(request.PublisherAccountId)
                                                        .Take(1000)
                                                        .Select(p => new PublisherAccountLinkInfo
                                                                     {
                                                                         PublisherAccount = p.ToPublisherAccount(),
                                                                         UnreadNotifications = _userNotificationService.GetUnreadCount(p.PublisherAccountId, p.GetContextWorkspaceId(request.WorkspaceId))
                                                                     })
                                                        .ToList();
            }
            else
            {
                results = await _workspaceService.GetWorkspaceUserPublisherAccountsAsync(request.WorkspaceId, request.UserId)
                                                 .Take(1000)
                                                 .Select(p => new PublisherAccountLinkInfo
                                                              {
                                                                  PublisherAccount = p.ToPublisherAccount(),
                                                                  UnreadNotifications = _userNotificationService.GetUnreadCount(p.PublisherAccountId, p.GetContextWorkspaceId(request.WorkspaceId))
                                                              })
                                                 .ToList();
            }

            return results.AsOnlyResultsResponse();
        }

        public async Task<LongIdResponse> Post(PostPublisherAccount request)
        {
            var dynPublisherAccount = await _publisherAccountService.ConnectPublisherAccountAsync(request.Model, request.WorkspaceId);

            _deferRequestsService.DeferLowPriRequest(new ProcessPublisherAccountTags
                                                     {
                                                         PublisherAccountId = dynPublisherAccount.PublisherAccountId
                                                     }.WithAdminRequestInfo());

            return dynPublisherAccount.ToLongIdResponse();
        }

        public async Task<LongIdResponse> Put(PutPublisherAccount request)
        {
            var dynPublisherAccount = await _publisherAccountService.ConnectPublisherAccountAsync(request.Model, request.WorkspaceId);

            _deferRequestsService.DeferLowPriRequest(new ProcessPublisherAccountTags
                                                     {
                                                         PublisherAccountId = dynPublisherAccount.PublisherAccountId
                                                     }.WithAdminRequestInfo());

            return dynPublisherAccount.ToLongIdResponse();
        }

        [RequiredRole("Admin")]
        public async Task<OnlyResultResponse<PublisherAccount>> Put(PutPublisherAccountAdmin request)
        {
            var existingPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);

            existingPublisherAccount.MaxDelinquentAllowed = request.MaxDelinquent >= 0
                                                                ? request.MaxDelinquent
                                                                : existingPublisherAccount.MaxDelinquentAllowed;

            await _publisherAccountService.PutPublisherAccount(existingPublisherAccount);

            return existingPublisherAccount.ToPublisherAccount().AsOnlyResultResponse();
        }


        public async Task Put(PutPublisherAccountTag request)
        {
            var dynPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.Id);

            dynPublisherAccount.Tags ??= new HashSet<Tag>();

            if (request.Tag.Key.StartsWithOrdinalCi("rydr"))
            { // Remove the existing matching
                dynPublisherAccount.Tags.RemoveWhere(t => t.Key.EqualsOrdinalCi(request.Tag.Key));
            }

            if (!dynPublisherAccount.Tags.Add(request.Tag))
            {
                return;
            }

            await _publisherAccountService.UpdatePublisherAccountAsync(dynPublisherAccount, pa => pa.Tags = dynPublisherAccount.Tags);
        }

        public void Delete(DeletePublisherAccount request)
            => _deferRequestsService.DeferRequest(new DeletePublisherAccountInternal
                                                  {
                                                      PublisherAccountId = request.Id
                                                  }.PopulateWithRequestInfo(request));

        [RequiredRole("Admin")]
        public async Task Put(PutPublisherAccountToken request)
        {
            var dynPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);

            var publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(dynPublisherAccount.PublisherType.ToString());

            var publisherApp = await publisherDataService.GetPublisherAppOrDefaultAsync(request.PublisherAppId);

            var publisherAppAccount = await _dynamoDb.GetPublisherAppAccountAsync(dynPublisherAccount.PublisherAccountId, publisherApp.PublisherAppId);

            var fbClient = await publisherAppAccount.GetOrCreateFbClientAsync(request.AccessToken);

            var tokenDebug = await fbClient.DebugTokenAsync();

            if (!request.Force &&
                (!dynPublisherAccount.AccountId.EqualsOrdinalCi(tokenDebug.Data.UserId.ToStringInvariant()) ||
                 !tokenDebug.Data.AppId.EqualsOrdinalCi(publisherApp.AppId)))
            {
                throw new InvalidDataArgumentException($"Token specified does not align with the rydr app and/or publisher account specified - tokenApp [{tokenDebug.Data.AppId}], tokenUserId [{tokenDebug.Data.UserId}], rydrApp [{publisherApp.AppId}], rydrAccountId [{dynPublisherAccount.AccountId}]");
            }

            await publisherDataService.PutAccessTokenAsync(dynPublisherAccount.PublisherAccountId, request.AccessToken);
        }

        private async Task<PublisherAccount> DoGetPublisherAccountForRequestAsync(long publisherAccountId)
        {
            var dynPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId);

            var publisherAccount = dynPublisherAccount.ToPublisherAccount();

            if (dynPublisherAccount.RydrAccountType.HasAnyFlags(RydrAccountType.Admin | RydrAccountType.Business))
            {   // Want completed and redeemed...so get from start of completed to end of redeemed, then filter out others
                publisherAccount.RecentCompleters = await _publisherAccountService.GetPublisherAccountsAsync(_dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(i => i.TypeOwnerSpace == DynItem.BuildTypeOwnerSpaceHash(DynItemType.DealRequestStatusChange, dynPublisherAccount.PublisherAccountId) &&
                                                                                                                                                                                      Dynamo.Between(i.ReferenceId,
                                                                                                                                                                                                     DynDealRequestStatusChange.CompletedStatusChangeReferenceBetweenMinMax[0],
                                                                                                                                                                                                     DynDealRequestStatusChange.RedeemedStatusChangeReferenceBetweenMinMax[1]))
                                                                                                                      .Filter(i => i.DeletedOnUtc == null &&
                                                                                                                                   i.TypeId == (int)DynItemType.DealRequestStatusChange &&
                                                                                                                                   Dynamo.In(i.StatusId, DealEnumHelpers.CompletedRedeemedDealRequestStatuses))
                                                                                                                      .ExecAsync(50.ToDynamoBatchCeilingTake())
                                                                                                                      .OrderByDescending(i => DynItem.GetFinalEdgeSegment(i.ReferenceId).ToLong())
                                                                                                                      .Select(i => DynItem.GetFirstEdgeSegment(i.EdgeId).ToLong()) // This is the PublisherAccountId
                                                                                                                      .Distinct()
                                                                                                                      .Take(10))
                                                                                  .Select(p => p.ToPublisherAccountProfile())
                                                                                  .ToList(10);
            }

            return publisherAccount;
        }
    }
}
