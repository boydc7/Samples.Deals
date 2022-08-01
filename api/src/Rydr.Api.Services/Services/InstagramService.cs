using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.FbSdk;
using Rydr.FbSdk.Extensions;
using Rydr.FbSdk.Models;
using ServiceStack;
using ServiceStack.Caching;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Rydr.Api.Services.Services
{
    public class InstagramPublicService : BaseApiService
    {
#if LOCAL || LOCALDOCKER
        private static readonly string _igAuthCompleteRedirectUrl = "https://localhost:2080/instagram/authcomplete";
#else
        private static readonly string _igAuthCompleteRedirectUrl = string.Concat(RydrUrls.WebHostUri.AbsoluteUri.TrimEnd('/'), "/instagram/authcomplete");
#endif

        private static readonly string _baseRedirectUrl = "https://done.getrydr.com";

        private readonly IPublisherDataService _publisherDataService;
        private readonly IEncryptionService _encryptionService;
        private readonly ICacheClient _cacheClient;
        private readonly IUserService _userService;
        private readonly IRequestStateManager _requestStateManager;
        private readonly IPublisherAccountService _publisherAccountService;

        public InstagramPublicService(IEncryptionService encryptionService, ICacheClient cacheClient,
                                      IUserService userService, IRequestStateManager requestStateManager,
                                      IPublisherAccountService publisherAccountService)
        {
            _encryptionService = encryptionService;
            _cacheClient = cacheClient;
            _userService = userService;
            _requestStateManager = requestStateManager;
            _publisherAccountService = publisherAccountService;
            _publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(PublisherType.Instagram.ToString());
        }

        public async Task Get(GetInstagramAuthComplete request)
        {
            // Start by getting the state info we stored when this auth process started, ensuring this response is valid...
            string addQueryParamIfValue(string url, string key, string value)
                => value.IsNullOrEmpty()
                       ? url
                       : url.AddQueryParam(key, value);

            string removeIgRedirectTrailer(string from)
                => from.EndsWithOrdinalCi("#_")
                       ? from.Left(from.Length - 2)
                       : from;

            void redirectWithError(string logString, string error, string errorReason, string errorDesc)
            {
                _log.Warn(logString);

                var redirectUrl = addQueryParamIfValue(_baseRedirectUrl, "error", error);
                redirectUrl = addQueryParamIfValue(redirectUrl, "errorReason", errorReason);
                redirectUrl = addQueryParamIfValue(redirectUrl, "errorDesc", errorDesc);

                Response.RedirectToUrl(redirectUrl);
            }

            if (request.HasError())
            {
                redirectWithError("IG response error", request.Error, request.ErrorReason, request.ErrorDescription);

                return;
            }

            var igState = removeIgRedirectTrailer(request.State);
            var cachedState = _cacheClient.TryGet<SimpleCacheItem>(igState);

            if (cachedState == null || cachedState.ReferenceId <= 0)
            {
                var myRedirectUrl = addQueryParamIfValue(_baseRedirectUrl, "error", "Invalid state or identifier specified.");

                Response.RedirectToUrl(myRedirectUrl);

                return;
            }

            // Get or create the user running the flow...
            var dynUser = await _userService.GetUserAsync(cachedState.ReferenceId);

            _requestStateManager.UpdateState(dynUser.UserId, 0, 0, UserAuthInfo.AdminUserId);

            // Valid state/response...try to exchange the code received for a short lived token
            var publisherApp = await _publisherDataService.GetPublisherAppOrDefaultAsync(cachedState.ReferenceCode.ToLong());

            var decryptedSecret = await _encryptionService.Decrypt64Async(publisherApp.AppSecret);

            var igCode = removeIgRedirectTrailer(request.Code);

            var shortToken = await InstagramBasicClient.GetAccessTokenAsync(publisherApp.AppId, decryptedSecret, _igAuthCompleteRedirectUrl, igCode);

            if (shortToken.HasError())
            {
                redirectWithError($"IgAccessToken retrieval error - [{shortToken.ToJsv().Left(250)}]", shortToken.ErrorType, shortToken.Code, shortToken.ErrorMessage);

                return;
            }

            // Have the short token, exchange that for a long-lived token
            var longToken = await InstagramBasicClient.GetLongLivedAccessTokenAsync(decryptedSecret, shortToken.AccessToken);

            if (longToken == null || !longToken.IsValid())
            {
                redirectWithError($"Long token exchange response invalid - [{longToken?.ToJsv().Left(250)}]", "LongTokenExchange", "", "Long token exchange response invalid");

                return;
            }

            // Have to get the profile from the token in order to create the token account
            var igProfile = await InstagramBasicClient.GetAccountForToken(longToken.AccessToken);

            var existingMatchedPublisherAccount = await _publisherAccountService.TryGetAnyExistingPublisherAccountAsync(PublisherType.Instagram, igProfile.Id)
                                                  ??
                                                  await _publisherAccountService.TryGetPublisherAccountByUserNameAsync(PublisherType.Instagram, igProfile.UserName);

            var igUserRequest = new PostInstagramUser
                                {
                                    UserIdentifier = dynUser.UserId.ToStringInvariant(),
                                    AccessToken = longToken.AccessToken,
                                    AccountId = igProfile.Id,
                                    UserName = igProfile.UserName,
                                    ExpiresInSeconds = longToken.ExpiresInSeconds,
                                    MediaCount = igProfile.MediaCount,
                                    UserId = dynUser.UserId,
                                    RydrAccountType = existingMatchedPublisherAccount?.RydrAccountType ?? RydrAccountType.None
                                };

            var postIgUserKey = Guid.NewGuid().ToStringId();

            await _cacheClient.TrySetAsync(igUserRequest, postIgUserKey, CacheConfig.FromHours(2));

            var successRedirectUrl = _baseRedirectUrl.AddQueryParam("username", igProfile.UserName)
                                                     .AddQueryParam("linkedasaccounttype", (int)igUserRequest.RydrAccountType)
                                                     .AddQueryParam("postbackid", postIgUserKey)
                                                     .AddQueryParam("rydrx", Guid.NewGuid().ToStringId());

            Response.RedirectToUrl(successRedirectUrl);
        }
    }

    public class InstagramService : BaseAuthenticatedApiService
    {
        private static readonly string _baseIgBasicAuthDialogUrl = string.Concat(InstagramBasicClient.BaseAuthDialogUrl, "oauth/authorize");

#if LOCAL || LOCALDOCKER
        private static readonly string _igAuthCompleteRedirectUrl = "https://localhost:2080/instagram/authcomplete";
#else
        private static readonly string _igAuthCompleteRedirectUrl = string.Concat(RydrUrls.WebHostUri.AbsoluteUri.TrimEnd('/'), "/instagram/authcomplete");
#endif

        private readonly IPublisherDataService _publisherDataService;
        private readonly IEncryptionService _encryptionService;
        private readonly ICacheClient _cacheClient;
        private readonly IUserService _userService;
        private readonly IWorkspaceService _workspaceService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IFileStorageProvider _fileStorageProvider;
        private readonly IDeferRequestsService _deferRequestsService;

        public InstagramService(IEncryptionService encryptionService, ICacheClient cacheClient,
                                IUserService userService, IWorkspaceService workspaceService,
                                IPublisherAccountService publisherAccountService, IFileStorageProvider fileStorageProvider,
                                IDeferRequestsService deferRequestsService)
        {
            _encryptionService = encryptionService;
            _cacheClient = cacheClient;
            _userService = userService;
            _workspaceService = workspaceService;
            _publisherAccountService = publisherAccountService;
            _fileStorageProvider = fileStorageProvider;
            _deferRequestsService = deferRequestsService;
            _publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(PublisherType.Instagram.ToString());
        }

        public async Task<OnlyResultResponse<StringIdResponse>> Get(GetInstagramAuthUrl request)
        {
            var publisherApp = await _publisherDataService.GetPublisherAppOrDefaultAsync(request.PublisherAppId);

            var decryptedSecret = await _encryptionService.Decrypt64Async(publisherApp.AppSecret);

            var redirectUrl = BaseFacebookClient.DecorateUrlWithIdentifiers(_baseIgBasicAuthDialogUrl, publisherApp.AppId, decryptedSecret,
                                                                            FacebookAccessToken.RydrAppIgBasicScopesString, _igAuthCompleteRedirectUrl);

            var stateKey = Guid.NewGuid().ToStringId();

            redirectUrl = redirectUrl.AddQueryParam("state", stateKey);
            redirectUrl = redirectUrl.AddQueryParam("response_type", "code");

            var cacheItem = new SimpleCacheItem
                            {
                                ReferenceId = request.UserId,
                                ReferenceCode = request.PublisherAppId.ToStringInvariant()
                            };

            await _cacheClient.TrySetAsync(cacheItem, stateKey, CacheConfig.FromHours(1));

            return new StringIdResponse
                   {
                       Id = redirectUrl
                   }.AsOnlyResultResponse();
        }

        public async Task<LongIdResponse> Post(PostBackInstagramUser request)
        {
            var igRequestKey = request.PostBackId.EndsWithOrdinalCi("#_")
                                   ? request.PostBackId.Left(request.PostBackId.Length - 2)
                                   : request.PostBackId;

            var postIgRequest = _cacheClient.TryGet<PostInstagramUser>(igRequestKey);

            postIgRequest.UserIdentifier = request.UserId.ToStringInvariant();
            postIgRequest.RydrAccountType = request.RydrAccountType;

            postIgRequest = postIgRequest.WithAdminRequestInfo();

            postIgRequest.UserId = request.UserId;

            if (request.WorkspaceId > GlobalItemIds.MinUserDefinedObjectId)
            {
                postIgRequest.WorkspaceId = request.WorkspaceId;
            }

            var (publisherModel, thumbnails) = request.RawFeed.HasValue()
                                                   ? TryGetPublisherAccountModelFromRawFeed(request.RawFeed)
                                                   : (null, null);

            if ((publisherModel?.AccountId).HasValue())
            {
                Guard.AgainstInvalidData(!postIgRequest.AccountId.EqualsOrdinalCi(publisherModel.AccountId) &&
                                         !postIgRequest.UserName.EqualsOrdinalCi(publisherModel.UserName), "Existing account and parsed account values are not aligned");

                postIgRequest.MediaCount = (long)publisherModel.Metrics.GetValueOrDefault(PublisherMetricName.Media).MaxGz(postIgRequest.MediaCount);
                postIgRequest.Follows = (long)publisherModel.Metrics.GetValueOrDefault(PublisherMetricName.Follows).MaxGz(postIgRequest.Follows);
                postIgRequest.FollowedBy = (long)publisherModel.Metrics.GetValueOrDefault(PublisherMetricName.FollowedBy).MaxGz(postIgRequest.FollowedBy);
                postIgRequest.FullName = publisherModel.FullName.Coalesce(postIgRequest.FullName);
                postIgRequest.Description = publisherModel.Description.Coalesce(postIgRequest.Description);
                postIgRequest.Website = publisherModel.Website.Coalesce(postIgRequest.Website);
                postIgRequest.ProfilePicture = publisherModel.ProfilePicture.Coalesce(postIgRequest.ProfilePicture);
            }

            var postIgUserResponse = await _adminServiceGatewayFactory().SendAsync(postIgRequest);

            await ProcessSoftIgMediasAsync(thumbnails, postIgUserResponse.Id);

            return postIgUserResponse;
        }

        public async Task<LongIdResponse> Post(PostInstagramSoftUserRawFeed request)
        {
            if (request.RawFeed.IsNullOrEmpty() && request.FeedUrl.HasValue())
            {
                if (!request.FeedUrl.StartsWithOrdinalCi("http"))
                {
                    request.FeedUrl = $"https://www.instagram.com/{request.UserName}/";
                }

                request.RawFeed = (await request.FeedUrl.GetStringFromUrlAsync()).Coalesce(string.Empty);
            }

            var (publisherModel, thumbnails) = TryGetPublisherAccountModelFromRawFeed(request.RawFeed);

            Guard.AgainstInvalidData(publisherModel == null, "Could not parse IG data successfully");

            publisherModel.RydrAccountType = request.RydrAccountType == RydrAccountType.None
                                                 ? RydrAccountType.Influencer
                                                 : request.RydrAccountType;

            var publisherAccountId = await UpsertInstagramSoftBasicProfileAsync(publisherModel, request, false);

            await ProcessSoftIgMediasAsync(thumbnails, publisherAccountId);

            return publisherAccountId.ToLongIdResponse();
        }

        public async Task<OnlyResultResponse<PublisherMedia>> Post(PostInstagramSoftUserMediaRawFeed request)
        {
            var publisherAccountId = request.GetPublisherIdFromIdentifier();

            if (request.RawFeed.IsNullOrEmpty() && request.FeedUrl.HasValue())
            {
                request.RawFeed = (await request.FeedUrl.GetStringFromUrlAsync()).Coalesce(string.Empty);
            }

            var (imageId, imageEndIndex) = ParseValueFromRawIgString(request.RawFeed, "\"GraphImage\",\"id\":\"", 0, "\",\"");

            Guard.Against(imageId.IsNullOrEmpty() || imageEndIndex < 0, "Could not parse imageId from media raw feed");

            var (imageThumbnail, imageThumbnailIndex) = ParseValueFromRawIgString(request.RawFeed, "\"display_url\":\"", imageEndIndex, "\",\"");

            Guard.Against(imageThumbnail.IsNullOrEmpty() || imageThumbnailIndex <= imageEndIndex, "Could not parse imageThumbnail from media raw feed");

            var mediaInfo = new PublisherMediaInfo
                            {
                                MediaId = imageId,
                                MediaUrl = FormatInstagramRawUrl(imageThumbnail),
                                MediaType = "IMAGE",
                                CreatedAt = _dateTimeProvider.UtcNow,
                            };

            var newRydrMediaIds = await ProcessSoftIgMediasAsync(new[]
                                                                 {
                                                                     mediaInfo
                                                                 },
                                                                 publisherAccountId);

            var dynPublisherMedia = newRydrMediaIds.IsNullOrEmpty()
                                        ? null
                                        : await _dynamoDb.GetItemByEdgeIntoAsync<DynPublisherMedia>(DynItemType.PublisherMedia, newRydrMediaIds.First());

            var publisherMedia = await dynPublisherMedia.ToPublisherMediaAsync();

            return publisherMedia.AsOnlyResultResponse();
        }

        public async Task<LongIdResponse> Post(PostInstagramSoftUser request)
        {
            var response = new LongIdResponse();

            var publisherAccount = await GetPublicIgProfileAsync(request.UserName, request.RydrAccountType, request.Secure);

            if (publisherAccount == null)
            {
                return response;
            }

            var publisherAccountId = await UpsertInstagramSoftBasicProfileAsync(publisherAccount, request, true, request.Secure);

            return publisherAccountId.ToLongIdResponse();
        }

        public async Task Post(PostInstagramSoftUserMedia request)
        {
            var publisherAccount = request.PublisherAccountId > 0
                                       ? await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId)
                                       : await _publisherAccountService.TryGetAnyExistingPublisherAccountAsync(PublisherType.Facebook, request.AccountId);

            Guard.AgainstRecordNotFound(publisherAccount == null, "Publisher specified does not exist");

            // Go pull in some images from users feed
            var url = $"https://pubapi.getrydr.com/{(request.Secure ? "igsecureprofileimagestorydr" : "igprofileimagestorydr")}?iguser={publisherAccount.UserName}";

            var pubImagesJson = await Try.GetAsync(() => url.GetJsonFromUrlAsync());
            var pubImagesResponse = pubImagesJson.FromJson<PubApiProfileImagesResponse>();

            if (pubImagesResponse == null || pubImagesResponse.Thumbnails <= 0)
            {
                return;
            }

            var tempDownloadPath = $"dl.getrydr.com/temp/{pubImagesResponse.AccountId}";
            var newPublisherMediaIds = new List<long>();

            // Go get any medias pulled from the ig profile and store them with the publisher as media
            try
            {
                await foreach (var fileName in _fileStorageProvider.ListFolderAsync(tempDownloadPath, false)
                                                                   .Where(f => f.StartsWithOrdinalCi("img_")))
                {
                    var fileNameWithoutExtension = fileName.LeftPart('.');
                    var mediaId = fileNameWithoutExtension.RightPart('_');
                    var mediaUrl = $"http://dl.getrydr.com.s3-website-us-west-2.amazonaws.com/temp/{pubImagesResponse.AccountId}/{fileName}";

                    if (mediaId.IsNullOrEmpty())
                    {
                        continue;
                    }

                    var rydrMediaId = await CreateNewSoftPublisherMediaAsync(mediaId, mediaUrl, publisherAccount.PublisherAccountId);

                    if (rydrMediaId <= 0)
                    { // Already have this media
                        await _fileStorageProvider.DeleteAsync(new FileMetaData(tempDownloadPath, fileName));
                    }
                    else
                    {
                        newPublisherMediaIds.Add(rydrMediaId);
                    }
                }
            }
            finally
            {
                if (!newPublisherMediaIds.IsNullOrEmpty())
                {
                    _deferRequestsService.DeferRequest(new ProcessRelatedMediaFiles
                                                       {
                                                           PublisherAccountId = publisherAccount.PublisherAccountId,
                                                           PublisherMediaIds = newPublisherMediaIds,
                                                           StoreAsPermanentMedia = true
                                                       }.WithAdminRequestInfo());
                }
            }
        }

        public async Task<LongIdResponse> Post(PostInstagramUser request)
        {
            var dynUser = await _userService.GetUserAsync(request.GetUserIdFromIdentifier());

            // Get or create - If a valid workspaceId header was sent, use it, otherwise get the personal or create one...
            // NOTE: CORRECTLY USING Facebook PublisherType on the create...
            var dynWorkspace = request.WorkspaceId > GlobalItemIds.MinUserDefinedObjectId
                                   ? await _workspaceService.GetWorkspaceAsync(request.WorkspaceId)
                                   : await _workspaceService.TryGetPersonalWorkspaceAsync(dynUser.UserId,
                                                                                          w => w.CreatedViaPublisherType == PublisherType.Facebook)
                                     ??
                                     await _workspaceService.CreateAsync(dynUser.UserId,
                                                                         string.Concat(PublisherType.Instagram.ToString(), " ",
                                                                                       request.AccountId, " (", request.UserName, ")"),
                                                                         WorkspaceType.Personal, PublisherType.Facebook, request.AccountId,
                                                                         request.Features);

            var existingMatchedPublisherAccount = await _publisherAccountService.TryGetAnyExistingPublisherAccountAsync(PublisherType.Instagram, request.AccountId)
                                                  ??
                                                  await _publisherAccountService.TryGetPublisherAccountByUserNameAsync(PublisherType.Instagram, request.UserName);

            var newPublisherAccount = new PublisherAccount
                                      {
                                          Type = PublisherType.Instagram,
                                          AccountId = request.AccountId,
                                          AccessToken = request.AccessToken,
                                          AccessTokenExpiresIn = request.ExpiresInSeconds,
                                          AccountType = PublisherAccountType.FbIgUser,
                                          RydrAccountType = request.RydrAccountType == RydrAccountType.None
                                                                ? existingMatchedPublisherAccount?.RydrAccountType ?? RydrAccountType.Influencer
                                                                : request.RydrAccountType,
                                          UserName = request.UserName,
                                          Metrics = new Dictionary<string, double>
                                                    {
                                                        {
                                                            PublisherMetricName.Media, request.MediaCount
                                                        },
                                                        {
                                                            PublisherMetricName.Follows, request.Follows
                                                        },
                                                        {
                                                            PublisherMetricName.FollowedBy, request.FollowedBy
                                                        }
                                                    },
                                          FullName = request.FullName.Coalesce(existingMatchedPublisherAccount?.FullName),
                                          Description = request.Description.Coalesce(existingMatchedPublisherAccount?.Description),
                                          Website = request.Website.Coalesce(existingMatchedPublisherAccount?.Website),
                                          ProfilePicture = request.ProfilePicture.Coalesce(existingMatchedPublisherAccount?.ProfilePicture)
                                      };

            var dynPublisherAccount = await _publisherAccountService.ConnectPublisherAccountAsync(newPublisherAccount, dynWorkspace.Id);

            // Store a reference to this account as a secondary token provider
            dynWorkspace.SecondaryTokenPublisherAccountIds ??= new HashSet<long>();

            if (dynWorkspace.SecondaryTokenPublisherAccountIds.Add(dynPublisherAccount.PublisherAccountId))
            {
                await _workspaceService.UpdateWorkspaceAsync(dynWorkspace,
                                                             () => new DynWorkspace
                                                                   {
                                                                       SecondaryTokenPublisherAccountIds = dynWorkspace.SecondaryTokenPublisherAccountIds
                                                                   });
            }

            // Link the publisher account connected to the worksapce
            await _adminServiceGatewayFactory().SendAsync(new LinkPublisherAccount
                                                          {
                                                              ToWorkspaceId = dynWorkspace.Id,
                                                              FromPublisherAccountId = 0, // Basic IG accounts are not linked up into any proper token account
                                                              ToPublisherAccountId = dynPublisherAccount.PublisherAccountId
                                                          }.WithAdminRequestInfo());

            return dynPublisherAccount.PublisherAccountId.ToLongIdResponse();
        }

        private async Task<long> UpsertInstagramSoftBasicProfileAsync(PublisherAccount publisherAccount, IRequestBase request, bool processSoftMedia = false, bool secure = false)
        {
            // Existing could be proper facebook, basic-linked IG, or soft-linked fb already
            var existingPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(publisherAccount.Id)
                                           ??
                                           await _publisherAccountService.TryGetAnyExistingPublisherAccountAsync(PublisherType.Instagram, publisherAccount.AccountId)
                                           ??
                                           await _publisherAccountService.TryGetPublisherAccountByUserNameAsync(PublisherType.Instagram, publisherAccount.UserName);

            Guard.AgainstInvalidData(publisherAccount.AccountId.IsNullOrEmpty() && existingPublisherAccount == null, "Could not determine AccountId");

            // At least one identifier has to match between incoming and existing....if there's an existing...
            Guard.AgainstInvalidData(existingPublisherAccount != null && publisherAccount.AccountId.HasValue() &&
                                     !existingPublisherAccount.AccountId.EqualsOrdinalCi(publisherAccount.AccountId) &&
                                     !existingPublisherAccount.AlternateAccountId.EqualsOrdinalCi(publisherAccount.AccountId) &&
                                     !existingPublisherAccount.UserName.EqualsOrdinalCi(publisherAccount.UserName), "Existing account and parsed account values are not aligned");

            // When linking an soft account, we always keep the existing account type
            publisherAccount.RydrAccountType = existingPublisherAccount?.RydrAccountType ?? publisherAccount.RydrAccountType;

            if (existingPublisherAccount != null && !publisherAccount.Metrics.IsNullOrEmptyRydr())
            {
                existingPublisherAccount.Metrics ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                // Put incoming metrics into the existing metrics, as the update will take all existing metrics....
                existingPublisherAccount.Metrics[PublisherMetricName.Media] = publisherAccount.Metrics
                                                                                              .GetValueOrDefault(PublisherMetricName.Media,
                                                                                                                 existingPublisherAccount.Metrics
                                                                                                                                         .GetValueOrDefault(PublisherMetricName.Media));

                existingPublisherAccount.Metrics[PublisherMetricName.FollowedBy] = publisherAccount.Metrics
                                                                                                   .GetValueOrDefault(PublisherMetricName.FollowedBy,
                                                                                                                      existingPublisherAccount.Metrics
                                                                                                                                              .GetValueOrDefault(PublisherMetricName.FollowedBy));

                existingPublisherAccount.Metrics[PublisherMetricName.Follows] = publisherAccount.Metrics
                                                                                                .GetValueOrDefault(PublisherMetricName.Follows,
                                                                                                                   existingPublisherAccount.Metrics
                                                                                                                                           .GetValueOrDefault(PublisherMetricName.Follows));
            }

            var requestWorkspaceId = request.WorkspaceId;
            var requestUserId = request.UserId;

            var dynPublisherAccount = await _publisherAccountService.ConnectPublisherAccountAsync(publisherAccount, requestWorkspaceId);

            _deferRequestsService.DeferLowPriRequest(new ProcessPublisherAccountProfilePic
                                                     {
                                                         PublisherAccountId = dynPublisherAccount.PublisherAccountId,
                                                         ProfilePicKey = string.Concat("ig/", dynPublisherAccount.IsSoftLinked
                                                                                                  ? dynPublisherAccount.AlternateAccountId
                                                                                                                       .Coalesce(dynPublisherAccount.AccountId)
                                                                                                  : dynPublisherAccount.AccountId)
                                                     }.WithAdminRequestInfo());

            // Link the publisher account connected to the worksapce, if this is a valid user workspace...
            if (requestWorkspaceId > GlobalItemIds.MinUserDefinedObjectId)
            {
                await _adminServiceGatewayFactory().SendAsync(new LinkPublisherAccount
                                                              {
                                                                  ToWorkspaceId = requestWorkspaceId,
                                                                  FromPublisherAccountId = 0, // Basic IG accounts are not linked up into any proper token account
                                                                  ToPublisherAccountId = dynPublisherAccount.PublisherAccountId
                                                              }.WithAdminRequestInfo());

                if (requestUserId > GlobalItemIds.MinUserDefinedObjectId &&
                    await _workspaceService.UserHasAccessToWorkspaceAsync(requestWorkspaceId, requestUserId))
                {
                    await _workspaceService.LinkUserToPublisherAccountAsync(requestWorkspaceId, requestUserId, dynPublisherAccount.PublisherAccountId);
                }
            }

            if (processSoftMedia && dynPublisherAccount.IsSoftLinked)
            {
                _deferRequestsService.DeferRequest(new PostInstagramSoftUserMedia
                                                   {
                                                       PublisherAccountId = dynPublisherAccount.PublisherAccountId,
                                                       Secure = secure
                                                   }.WithAdminRequestInfo());
            }

            return dynPublisherAccount.PublisherAccountId;
        }

        private async Task<PublisherAccount> GetPublicIgProfileAsync(string igUserName, RydrAccountType asRydrAccountType, bool secure = false)
        {
            var url = $"https://pubapi.getrydr.com/{(secure ? "igsecureprofiletorydr" : "igprofiletorydr")}?iguser={igUserName}";

            var pubIgResponse = await url.GetJsonFromUrlAsync();

            if (pubIgResponse.IsNullOrEmpty())
            {
                return null;
            }

            var publisherAccount = pubIgResponse.FromJson<PublisherAccount>();

            if (publisherAccount == null || !publisherAccount.UserName.EqualsOrdinalCi(igUserName))
            {
                return null;
            }

            if (asRydrAccountType == RydrAccountType.Business || asRydrAccountType == RydrAccountType.Influencer)
            {
                publisherAccount.RydrAccountType = asRydrAccountType;
            }

            return publisherAccount;
        }

        private (PublisherAccount PublisherAccount, List<PublisherMediaInfo> Medias) TryGetPublisherAccountModelFromRawFeed(string rawFeed)
        {
            var accountIdInfo = ParseValueFromRawIgString(rawFeed, "\"logging_page_id\":\"profilePage_", 0, "\"");

            var publisherModel = new PublisherAccount
                                 {
                                     AccountId = accountIdInfo.Value,
                                     Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                                     UserName = ParseValueFromRawIgString(rawFeed, "\"username\":\"", accountIdInfo.EndIndex, "\"").Value,
                                     FullName = ParseValueFromRawIgString(rawFeed, "\"full_name\":\"", accountIdInfo.EndIndex, "\"").Value,
                                     Description = ParseValueFromRawIgString(rawFeed, "\"biography\":\"", accountIdInfo.EndIndex, "\"").Value,
                                     Website = FormatInstagramRawUrl(ParseValueFromRawIgString(rawFeed, "\"external_url\":\"", accountIdInfo.EndIndex, "\"").Value),
                                     ProfilePicture = FormatInstagramRawUrl(ParseValueFromRawIgString(rawFeed, "<meta property=\"og:image\" content=\"", 0, "\" />", "\">").Value),
                                     Type = PublisherType.Instagram,
                                     AccountType = PublisherAccountType.FbIgUser
                                 };

            publisherModel.Metrics[PublisherMetricName.Media] = ParseValueFromRawIgString(rawFeed, "\"edge_owner_to_timeline_media\":{\"count\":", accountIdInfo.EndIndex, ",", "}").Value.ToDoubleRydr();
            publisherModel.Metrics[PublisherMetricName.FollowedBy] = ParseValueFromRawIgString(rawFeed, "\"edge_followed_by\":{\"count\":", accountIdInfo.EndIndex, ",", "}").Value.ToDoubleRydr();
            publisherModel.Metrics[PublisherMetricName.Follows] = ParseValueFromRawIgString(rawFeed, "\"edge_follow\":{\"count\":", accountIdInfo.EndIndex, ",", "}").Value.ToDoubleRydr();

            if (publisherModel.AccountId.IsNullOrEmpty() || publisherModel.UserName.IsNullOrEmpty())
            {
                return (null, null);
            }

            var thumbnailUrls = new List<PublisherMediaInfo>(25);
            var currentImageIdEndIndex = 0;
            var currentImageThumbnailEndIndex = 0;

            do
            {
                var (currentImageId, currentImageEndIndex) = ParseValueFromRawIgString(rawFeed, "\"GraphImage\",\"id\":\"", currentImageIdEndIndex, "\",\"");

                if (currentImageId.IsNullOrEmpty() || currentImageEndIndex <= currentImageIdEndIndex)
                {
                    break;
                }

                currentImageIdEndIndex = currentImageEndIndex;

                var (currentImageThumbnail, currentImageThumbnailIndex) = ParseValueFromRawIgString(rawFeed, "\"thumbnail_src\":\"", currentImageThumbnailEndIndex, "\",\"");

                if (currentImageThumbnail.IsNullOrEmpty() || currentImageThumbnailIndex <= currentImageThumbnailEndIndex)
                {
                    break;
                }

                currentImageThumbnailEndIndex = currentImageThumbnailIndex;

                thumbnailUrls.Add(new PublisherMediaInfo
                                  {
                                      MediaId = currentImageId,
                                      MediaUrl = FormatInstagramRawUrl(currentImageThumbnail),
                                      MediaType = "IMAGE",
                                      CreatedAt = _dateTimeProvider.UtcNow,
                                  });
            } while (thumbnailUrls.Count <= 25);

            return (publisherModel, thumbnailUrls);
        }

        private string FormatInstagramRawUrl(string rawUrl)
            => rawUrl.IsNullOrEmpty()
                   ? rawUrl
                   : rawUrl.Replace("\\u0026", "&").Replace("&amp;", "&");

        private (string Value, int EndIndex) ParseValueFromRawIgString(string rawFeed, string startKey, int startIndex, params string[] endKeys)
        {
            var searchIndex = rawFeed.IndexOf(startKey, startIndex, StringComparison.OrdinalIgnoreCase);

            if (searchIndex < 0)
            {
                return (null, 0);
            }

            var valueStartIndex = searchIndex + startKey.Length;
            var valueEndIndex = rawFeed.IndexOfAny(endKeys, valueStartIndex, StringComparison.OrdinalIgnoreCase);

            return valueEndIndex > valueStartIndex
                       ? (rawFeed.Substring(valueStartIndex, valueEndIndex - valueStartIndex).Trim(), valueEndIndex)
                       : (null, 0);
        }

        private async Task<List<long>> ProcessSoftIgMediasAsync(ICollection<PublisherMediaInfo> mediaInfos, long publisherAccountId)
        {
            if (mediaInfos == null || mediaInfos.Count <= 0)
            {
                return null;
            }

            var newPublisherMediaIds = new List<long>(mediaInfos.Count);

            foreach (var mediaInfo in mediaInfos)
            {
                mediaInfo.MediaUrl = FormatInstagramRawUrl(mediaInfo.MediaUrl);

                var rydrMediaId = await CreateNewSoftPublisherMediaAsync(mediaInfo.MediaId, mediaInfo.MediaUrl, publisherAccountId);

                if (rydrMediaId > 0)
                {
                    newPublisherMediaIds.Add(rydrMediaId);
                }
            }

            if (!newPublisherMediaIds.IsNullOrEmpty())
            {
                _deferRequestsService.DeferRequest(new ProcessRelatedMediaFiles
                                                   {
                                                       PublisherAccountId = publisherAccountId,
                                                       PublisherMediaIds = newPublisherMediaIds,
                                                       StoreAsPermanentMedia = true
                                                   }.WithAdminRequestInfo());
            }

            return newPublisherMediaIds;
        }

        private async Task<long> CreateNewSoftPublisherMediaAsync(string mediaId, string mediaUrl, long publisherAccountId)
        {
            var dynMediaReferenceId = DynPublisherMedia.BuildRefId(PublisherType.Facebook, mediaId);
            var mapEdgeId = DynItemMap.BuildEdgeId(DynItemType.PublisherMedia, dynMediaReferenceId);

            if ((await _dynamoDb.GetItemAsync<DynItemMap>(publisherAccountId, mapEdgeId)) != null)
            {
                return 0;
            }

            var dynPublisherMedia = new DynPublisherMedia
                                    {
                                        PublisherAccountId = publisherAccountId,
                                        PublisherMediaId = Sequences.Next(),
                                        PublisherType = PublisherType.Facebook, // Corectly using facebook here...writable type is used
                                        ReferenceId = dynMediaReferenceId,
                                        ContentType = PublisherContentType.Post,
                                        MediaId = mediaId,
                                        PublisherUrl = mediaUrl,
                                        MediaUrl = mediaUrl,
                                        MediaCreatedAt = _dateTimeProvider.UtcNowTs,
                                        MediaType = "IMAGE",
                                        IsRydrHosted = false, // NOT rydr hosted until it gets moved to a permanent hosted location...
                                        DynItemType = DynItemType.PublisherMedia
                                    };

            dynPublisherMedia.UpdateDateTimeTrackedValues();

            await _dynamoDb.TryPutItemMappedAsync(dynPublisherMedia, dynPublisherMedia.ReferenceId);

            return dynPublisherMedia.RydrMediaId;
        }

        private class PubApiProfileImagesResponse
        {
            public string AccountId { get; set; }
            public int Thumbnails { get; set; }
        }
    }
}
