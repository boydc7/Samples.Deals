using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnumsNET;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Publishers;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services
{
    public class PublisherMediaService : BaseAuthenticatedApiService
    {
        public static readonly List<PublisherContentType> AllPublisherContentTypeEnums = Enums.GetValues<PublisherContentType>()
                                                                                              .Where(v => v != PublisherContentType.Unknown)
                                                                                              .AsList();

        private static readonly List<int> _allPublisherContentTypeInts = Enums.GetValues<PublisherContentType>()
                                                                              .Where(v => v != PublisherContentType.Unknown)
                                                                              .Select(v => (int)v)
                                                                              .ToList();

        private readonly IDeferRequestsService _deferRequestsService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IWorkspaceService _workspaceService;
        private readonly IFileStorageService _fileStorageService;

        public PublisherMediaService(IDeferRequestsService deferRequestsService, IPublisherAccountService publisherAccountService,
                                     IWorkspaceService workspaceService, IFileStorageService fileStorageService)
        {
            _deferRequestsService = deferRequestsService;
            _publisherAccountService = publisherAccountService;
            _workspaceService = workspaceService;
            _fileStorageService = fileStorageService;
        }

        public async Task Put(PutPublisherMediaAnalysisPriority request)
        {
            var dynPublisherMedia = await _dynamoDb.GetItemByEdgeIntoAsync<DynPublisherMedia>(DynItemType.PublisherMedia, request.Id);

            await _dynamoDb.UpdateItemAsync(dynPublisherMedia.Id, dynPublisherMedia.EdgeId,
                                            () => new DynPublisherMedia
                                                  {
                                                      AnalyzePriority = request.Priority
                                                  });
        }

        public async Task<OnlyResultResponse<PublisherMedia>> Get(GetPublisherMedia request)
        {
            var dynPublisherMedia = await _dynamoDb.GetItemByEdgeIntoAsync<DynPublisherMedia>(DynItemType.PublisherMedia, request.Id);
            var publisherMedia = await dynPublisherMedia.ToPublisherMediaAsync();

            return publisherMedia.AsOnlyResultResponse();
        }

        public async Task<OnlyResultResponse<PublisherApprovedMedia>> Get(GetPublisherApprovedMedia request)
        {
            var dynPublisherApprovedMedia = await _dynamoDb.GetItemByEdgeIntoAsync<DynPublisherApprovedMedia>(DynItemType.ApprovedMedia, request.Id);

            var publisherApprovedMedia = await dynPublisherApprovedMedia.ToPublisherApprovedMediaAsync();

            var dynMediaFile = await _fileStorageService.TryGetFileAsync(publisherApprovedMedia.MediaFileId);

            if (dynMediaFile.FileType == FileType.Video && !dynMediaFile.IsFinalStatus())
            {
                await _fileStorageService.RefreshConvertStatusAsync(dynMediaFile, VideoConvertGenericTypeArguments.Instance);
            }

            publisherApprovedMedia.ConvertStatus = dynMediaFile.ToFileConvertStatus();

            return publisherApprovedMedia.AsOnlyResultResponse();
        }

        public async Task<OnlyResultsResponse<PublisherApprovedMedia>> Get(GetPublisherApprovedMedias request)
        {
            List<DynPublisherApprovedMedia> dynApprovedMedias = null;

            if (request.DealId > 0)
            {
                var dynDeal = await DealExtensions.DefaultDealService.GetDealAsync(request.DealId);

                if (dynDeal.PublisherApprovedMediaIds.IsNullOrEmpty())
                {
                    return new OnlyResultsResponse<PublisherApprovedMedia>();
                }

                dynApprovedMedias = await _dynamoDb.GetItemsAsync<DynPublisherApprovedMedia>(dynDeal.PublisherApprovedMediaIds.Select(ami => new DynamoId(dynDeal.PublisherAccountId, ami.ToEdgeId())))
                                                   .ToList();
            }
            else
            {
                dynApprovedMedias = await _dynamoDb.FromQuery<DynPublisherApprovedMedia>(a => a.Id == request.GetPublisherIdFromIdentifier() &&
                                                                                              Dynamo.BeginsWith(a.EdgeId, "00"))
                                                   .Filter(a => a.DeletedOnUtc == null &&
                                                                a.TypeId == (int)DynItemType.ApprovedMedia)
                                                   .ExecAsync()
                                                   .Skip(request.Skip)
                                                   .Take(request.Take)
                                                   .ToList(request.Take);
            }

            if (dynApprovedMedias.IsNullOrEmpty())
            {
                return new OnlyResultsResponse<PublisherApprovedMedia>();
            }

            var results = new List<PublisherApprovedMedia>(dynApprovedMedias.Count);

            var fileMap = await _fileStorageService.GetFilesAsync(dynApprovedMedias.Select(m => m.MediaFileId).Distinct())
                                                   .ToDictionarySafe(f => f.Id);

            foreach (var dynApprovedMedia in dynApprovedMedias)
            {
                var approvedMedia = await dynApprovedMedia.ToPublisherApprovedMediaAsync();

                if (approvedMedia.MediaFileId > 0 && fileMap.ContainsKey(approvedMedia.MediaFileId))
                {
                    approvedMedia.ConvertStatus = fileMap[approvedMedia.MediaFileId].ToFileConvertStatus();
                }

                results.Add(approvedMedia);
            }

            return results.AsOnlyResultsResponse();
        }

        [RydrForcedSimpleCacheResponse(900)]
        public async Task<OnlyResultResponse<PublisherMediaAnalysis>> Get(GetPublisherMediaAnalysis request)
        {
            var dynPublisherMedia = await _dynamoDb.GetItemByEdgeIntoAsync<DynPublisherMedia>(DynItemType.PublisherMedia, request.Id);

            var result = await _dynamoDb.GetItemAsync<DynPublisherMediaAnalysis>(dynPublisherMedia.PublisherMediaId, DynPublisherMediaAnalysis.BuildEdgeId(dynPublisherMedia.PublisherType, dynPublisherMedia.MediaId));

            return result.To(dma =>
                             {
                                 if (dma == null)
                                 {
                                     return new PublisherMediaAnalysis();
                                 }

                                 var pma = dma.ConvertTo<PublisherMediaAnalysis>();

                                 pma.ImageFacesAvgAge = dma.ImageFacesCount > 0
                                                            ? Math.Round(dma.ImageFacesAgeSum / (double)dma.ImageFacesCount / 2d, 2)
                                                            : 0;

                                 pma.TextEntities = dma.PopularEntities;
                                 pma.IsPositiveSentiment = dma.IsPositiveSentimentType();
                                 pma.IsNegativeSentiment = dma.IsNegativeSentimentType();
                                 pma.IsNeutralSentiment = dma.IsNeutralSentimentType();
                                 pma.IsMixedSentiment = dma.IsMixedSentimentType();

                                 return pma;
                             })
                         .AsOnlyResultResponse();
        }

        [RydrForcedSimpleCacheResponse(60)]
        public async Task<OnlyResultsResponse<PublisherMedia>> Get(GetRecentMedia request)
        {
            request.Limit = request.Limit.Gz(50);

            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherIdentifier.EqualsOrdinalCi("me")
                                                                                               ? request.RequestPublisherAccountId
                                                                                               : request.PublisherIdentifier.ToLong());

            var tokenAccount = publisherAccount.IsBasicLink
                                   ? publisherAccount
                                   : await _workspaceService.TryGetDefaultPublisherAccountAsync(request.WorkspaceId)
                                     ??
                                     await _publisherAccountService.GetPublisherAccountAsync(request.RequestPublisherAccountId);

            var publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(publisherAccount.PublisherType.ToString());

            var dynMediaMap = new Dictionary<string, DynPublisherMedia>(request.Limit * 2, StringComparer.OrdinalIgnoreCase);

            // Go to the publisher and get any recent media we may or may not have synced
            // Wrapped in a try here simply to ensure a result that includes data, if we already have it synced or not...
            var recentPublisherMedia = await Try.ExecAsync(() => publisherDataService.GetRecentMediaAsync(tokenAccount?.PublisherAccountId ?? publisherAccount.Id, publisherAccount.Id,
                                                                                                          request.PublisherAppId, request.Limit));

            if (!recentPublisherMedia.IsNullOrEmpty())
            { // Go get an medias we already have based on the info we got from the publisher...
                await foreach (var dynRecentPublisherMedia in _dynamoDb.GetItemsFromAsync<DynPublisherMedia, DynItemMap>(_dynamoDb.GetItemsAsync<DynItemMap>(recentPublisherMedia.Select(f => new DynamoId(publisherAccount.PublisherAccountId,
                                                                                                                                                                                                           DynItemMap.BuildEdgeId(DynItemType.PublisherMedia, DynPublisherMedia.BuildRefId(PublisherType.Facebook, f.MediaId))))),
                                                                                                                         m => m.GetMappedDynamoId())
                                                                       .Where(dpm => dpm.DeletedOnUtc == null &&
                                                                                     dpm.TypeId == (int)DynItemType.PublisherMedia))
                {
                    dynMediaMap[dynRecentPublisherMedia.MediaId] = dynRecentPublisherMedia;
                }

                // Any fb media we get here that does not yet exist in our synced set has to be stored here and now, otherwise follow-up calls to the fb
                // endpoint will return nothing and we won't be able to show it until we sync it later (304 not modified gets returned once we have the set)
                foreach (var media in recentPublisherMedia.Where(f => !dynMediaMap.ContainsKey(f.MediaId)))
                {
                    var newDynMedia = await media.ToDynPublisherMediaAsync(forceNoPut: true);

                    await _dynamoDb.TryPutItemMappedAsync(newDynMedia, newDynMedia.ReferenceId);

                    _deferRequestsService.DeferLowPriRequest(new PostPublisherMediaReceived
                                                             {
                                                                 PublisherAccountId = newDynMedia.PublisherAccountId,
                                                                 PublisherMediaId = newDynMedia.PublisherMediaId
                                                             }.WithAdminRequestInfo());

                    dynMediaMap[newDynMedia.MediaId] = newDynMedia;
                }
            }

            // If we don't have enough media from the publisher to fullfil the query request, see if we have any more synced locally
            if (dynMediaMap.Count < request.Limit)
            {
                await foreach (var syncedMedia in GetSyncedMediaAsync(publisherAccount, liveMediaOnly: request.LiveMediaOnly,
                                                                      limit: request.Limit.ToDynamoBatchCeilingTake()).Where(m => !dynMediaMap.ContainsKey(m.MediaId)))
                {
                    dynMediaMap[syncedMedia.MediaId] = syncedMedia;
                }
            }

            _log.DebugInfoFormat("  GetRecentMedia for [{0}] returned [{1}] publisher-direct media, [{2}] returned media",
                                 publisherAccount.DisplayName(), recentPublisherMedia?.Count ?? 0, dynMediaMap?.Count ?? 0);

            // Return up to the limit of each posts and stories
            var countPosts = 0;
            var countStories = 0;

            var results = new List<PublisherMedia>(request.Limit * 2);

            foreach (var dynPublisherMedia in dynMediaMap.Values
                                                         .OrderByDescending(dm => dm.MediaCreatedAt)
                                                         .TakeWhile(dm => countPosts < request.Limit || countStories < request.Limit))
            {
                if (dynPublisherMedia.ContentType == PublisherContentType.Post)
                {
                    if (countPosts >= request.Limit)
                    {
                        continue;
                    }

                    countPosts++;
                }

                if (dynPublisherMedia.ContentType == PublisherContentType.Story)
                {
                    if (countStories >= request.Limit)
                    {
                        continue;
                    }

                    countStories++;
                }

                var publisherMedia = await dynPublisherMedia.ToPublisherMediaAsync();

                results.Add(publisherMedia);
            }

            return results.AsOnlyResultsResponse();
        }

        [RydrForcedSimpleCacheResponse(900)]
        public async Task<OnlyResultsResponse<PublisherMedia>> Get(GetRecentSyncedMedia request)
        {
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherIdentifier.EqualsOrdinalCi("me")
                                                                                               ? request.RequestPublisherAccountId
                                                                                               : request.PublisherIdentifier.ToLong());

            var results = new List<PublisherMedia>(request.Limit * 2);

            await foreach (var syncedMedia in GetSyncedMediaAsync(publisherAccount, request.CreatedAfter, request.ContentTypes,
                                                                  request.LiveMediaOnly, request.Limit).OrderByDescending(dm => dm.MediaCreatedAt))
            {
                var publisherMedia = await syncedMedia.ToPublisherMediaAsync();

                results.Add(publisherMedia);
            }

            _log.DebugInfoFormat("  GetRecentSyncedMedia for PublisherAccount [{0}] returned [{1}] synced media", publisherAccount.DisplayName(), results.Count);

            return results.AsOnlyResultsResponse();
        }

        public async Task<OnlyResultsResponse<PublisherMedia>> Get(GetRecentPublisherMedia request)
        {
            var response = new OnlyResultsResponse<PublisherMedia>();

            request.Limit = request.Limit.Gz(20);

            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherIdentifier.EqualsOrdinalCi("me")
                                                                                               ? request.RequestPublisherAccountId
                                                                                               : request.PublisherIdentifier.ToLong());

            var tokenAccount = publisherAccount.IsBasicLink
                                   ? publisherAccount
                                   : await _workspaceService.TryGetDefaultPublisherAccountAsync(request.WorkspaceId)
                                     ??
                                     await _publisherAccountService.GetPublisherAccountAsync(request.RequestPublisherAccountId);

            var publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(publisherAccount.PublisherType.ToString());

            // Go to the publisher and get any recent media we may or may not have synced
            var recentPublisherMedia = await publisherDataService.GetRecentMediaAsync(tokenAccount?.PublisherAccountId ?? publisherAccount.Id, publisherAccount.Id, request.PublisherAppId, request.Limit);

            if (recentPublisherMedia.IsNullOrEmpty())
            {
                _log.DebugInfoFormat("  GetRecentPublisherMedia for [{0}] returned [0] publisher-direct media, [0] of those were new to us", publisherAccount.DisplayName());

                return response;
            }

            // Go get an medias we already have based on the info we got from the publisher...
            var existingMediaMap = await _dynamoDb.GetItemsAsync<DynItemMap>(recentPublisherMedia.Select(f => new DynamoId(publisherAccount.PublisherAccountId,
                                                                                                                           DynItemMap.BuildEdgeId(DynItemType.PublisherMedia,
                                                                                                                                                  DynPublisherMedia.BuildRefId(PublisherType.Facebook,
                                                                                                                                                                               f.MediaId)))))
                                                  .ToDictionarySafe(m => DynItem.GetFinalEdgeSegment(m.EdgeId), StringComparer.OrdinalIgnoreCase);

            // Results are any direct-publisher results that we do not already have
            response.Results = recentPublisherMedia.Where(r => !existingMediaMap.ContainsKey(r.MediaId)).AsListReadOnly();

            // Any fb media we get here that does not yet exist in our synced set has to be stored here and now, otherwise follow-up calls to the fb
            // endpoint will return nothing and we won't be able to show it until we sync it later (304 not modified gets returned once we have the set)
            foreach (var newPublisherMedia in response.Results)
            {
                var newDynMedia = await newPublisherMedia.ToDynPublisherMediaAsync(forceNoPut: true);

                await _dynamoDb.TryPutItemMappedAsync(newDynMedia, newDynMedia.ReferenceId);

                _deferRequestsService.DeferLowPriRequest(new PostPublisherMediaReceived
                                                         {
                                                             PublisherAccountId = newDynMedia.PublisherAccountId,
                                                             PublisherMediaId = newDynMedia.PublisherMediaId
                                                         }.WithAdminRequestInfo());
            }

            _log.DebugInfoFormat("  GetRecentPublisherMedia for [{0}] returned [{1}] publisher-direct media, [{2}] of those were new to us",
                                 publisherAccount.DisplayName(), recentPublisherMedia.Count, response.Results.Count);

            return response;
        }

        [RequiredRole("Admin")]
        public void Post(PostTriggerSyncRecentPublisherAccountMedia request)
            => _deferRequestsService.PublishMessage(new PostSyncRecentPublisherAccountMedia
                                                    {
                                                        PublisherAccountId = request.PublisherAccountId,
                                                        PublisherAppId = request.PublisherAppId,
                                                        WithWorkspaceId = request.WithWorkspaceId,
                                                        Force = request.Force
                                                    });

        public async Task<LongIdResponse> Post(PostPublisherMedia request)
        {
            var updateRequest = request.ConvertTo<PostPublisherMediaUpsert>();

            updateRequest.PopulateWithRequestInfo(request);

            var response = await _adminServiceGatewayFactory().SendAsync(updateRequest);

            return response;
        }

        public async Task<LongIdResponse> Post(PostPublisherApprovedMedia request)
        {
            if (request.Model.PublisherAccountId <= 0)
            {
                request.Model.PublisherAccountId = request.RequestPublisherAccountId;
            }

            var dynMediaFile = request.Model.MediaFileId > 0
                                   ? await _fileStorageService.TryGetFileAsync(request.Model.MediaFileId)
                                   : null;

            if (dynMediaFile.FileType == FileType.Video)
            { // Videos require thumbnails - on POSTs, only need to set the thumbnail url to any value and it will get generated in the transform below...
                request.Model.ThumbnailUrl = dynMediaFile.Id.ToStringInvariant();
            }

            var dynPublisherApprovedMedia = await request.Model.ToDynPublisherApprovedMediaAsync();

            // If using a file object as approved media, ensure the file is confirmed already
            await _fileStorageService.ConfirmUploadAsync(dynPublisherApprovedMedia.MediaFileId);

            await _dynamoDb.PutItemAsync(dynPublisherApprovedMedia);

            return dynPublisherApprovedMedia.PublisherApprovedMediaId.ToLongIdResponse();
        }

        public async Task<LongIdResponse> Put(PutPublisherApprovedMedia request)
        {
            var existingPublisherApprovedMedia = await _dynamoDb.GetItemAsync<DynPublisherApprovedMedia>(request.Model.PublisherAccountId.Gz(request.RequestPublisherAccountId),
                                                                                                         request.Model.Id.ToEdgeId());

            var result = await _dynamoDb.UpdateFromExistingAsync(existingPublisherApprovedMedia, x => request.Model.ToDynPublisherApprovedMediaAsync(x), request);

            return result.PublisherApprovedMediaId.ToLongIdResponse();
        }

        private async IAsyncEnumerable<DynPublisherMedia> GetSyncedMediaAsync(DynPublisherAccount publisherAccount, DateTime? createdAfter = null,
                                                                              IEnumerable<PublisherContentType> contentTypes = null,
                                                                              bool liveMediaOnly = false, int limit = 100)
        {
            var contentTypeValues = contentTypes?.Select(c => (int)c).AsList().NullIfEmpty() ?? _allPublisherContentTypeInts;

            var mediaCreatedAfter = (createdAfter ?? DateTimeHelper.MinApplicationDate).ToUnixTimestamp();

            var storiesCreatedAfter = liveMediaOnly
                                          ? _dateTimeProvider.UtcNow.AddHours(-24).ToUnixTimestamp()
                                          : DateTimeHelper.MinApplicationDateTs;

            limit = limit.Gz(100);

            // If asked for all types, we get the top(limit) of each, then return that (i.e. the return will be up to limit*2). If getting just one type, don't have to do that naturally
            if (contentTypeValues.Count >= _allPublisherContentTypeInts.Count)
            {
                // NOTE: Have to order the results here before taking as the types are interleaved on different requests, and will be naturally unsorted in the
                // result of the SelectMany over each type.
                foreach (var publisherContentType in AllPublisherContentTypeEnums)
                {
                    await foreach (var dynPublisherMedia in _dynamoDb.FromQuery<DynPublisherMedia>(pm => pm.Id == publisherAccount.PublisherAccountId &&
                                                                                                         Dynamo.BeginsWith(pm.EdgeId, "00"))
                                                                     .Filter(pm => pm.DeletedOnUtc == null &&
                                                                                   pm.TypeId == (int)DynItemType.PublisherMedia &&
                                                                                   pm.ContentType == publisherContentType &&
                                                                                   pm.MediaCreatedAt >= mediaCreatedAfter)
                                                                     .ExecAsync()
                                                                     .Where(m => m.ContentType != PublisherContentType.Story ||
                                                                                 m.MediaCreatedAt >= storiesCreatedAfter)
                                                                     .Take(limit))
                    {
                        yield return dynPublisherMedia;
                    }
                }
            }
            else
            { // Just get limit amount of the given type(s)
                await foreach (var dynMedia in _dynamoDb.FromQuery<DynPublisherMedia>(pm => pm.Id == publisherAccount.PublisherAccountId &&
                                                                                            Dynamo.BeginsWith(pm.EdgeId, "00"))
                                                        .Filter(pm => pm.DeletedOnUtc == null &&
                                                                      pm.TypeId == (int)DynItemType.PublisherMedia &&
                                                                      Dynamo.In(pm.ContentType, contentTypeValues) &&
                                                                      pm.MediaCreatedAt >= mediaCreatedAfter)
                                                        .ExecAsync()
                                                        .Where(m => m.ContentType != PublisherContentType.Story ||
                                                                    m.MediaCreatedAt >= storiesCreatedAfter)
                                                        .Take(limit))
                {
                    yield return dynMedia;
                }
            }
        }
    }
}
