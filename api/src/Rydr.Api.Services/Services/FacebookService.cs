using System.Net;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Auth;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Configuration;
using Rydr.FbSdk.Enums;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services;

[Restrict(VisibleLocalhostOnly = true)]
public class FacebookPublicService : BaseApiService
{
    private static readonly IDeferRequestsService _deferRequestsService = RydrEnvironment.Container.Resolve<IDeferRequestsService>();

    private static readonly Dictionary<string, Func<DynPublisherAccount, FacebookFieldValueItem, IPocoDynamo, Task>> _fbWebhookTypeProcessMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            {
                "story_insights", ProcessStoryInsightWebhookItemAsync
            }
        };

    public async Task<object> Post(PostFacebookWebhook request)
    {
        foreach (var entry in request.Entry
                                     .Where(e => e.Id.HasValue() &&
                                                 !e.Changes.IsNullOrEmpty()))
        {
            var createdByPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                     .TryGetPublisherAccountAsync(PublisherType.Facebook, entry.Id);

            if (createdByPublisherAccount == null)
            {
                continue;
            }

            foreach (var change in entry.Changes
                                        .Where(c => c.Field.HasValue() &&
                                                    _fbWebhookTypeProcessMap.ContainsKey(c.Field)))
            {
                await _fbWebhookTypeProcessMap[change.Field](createdByPublisherAccount, change.Value, _dynamoDb);
            }
        }

        return new HttpResult(HttpStatusCode.OK);
    }

    public object Get(GetFacebookWebhook request)
        => new HttpResult(request.Challenge)
           {
               ContentType = MimeTypes.PlainText
           };

    private static async Task ProcessStoryInsightWebhookItemAsync(DynPublisherAccount publisherAccount, FacebookFieldValueItem storyInsightItem, IPocoDynamo dynamoDb)
    {
        var dynPublisherMedia = await dynamoDb.GetItemByRefAsync<DynPublisherMedia>(publisherAccount.Id,
                                                                                    DynPublisherMedia.BuildRefId(PublisherType.Facebook, storyInsightItem.MediaId),
                                                                                    DynItemType.PublisherMedia, true, true);

        if (dynPublisherMedia == null)
        {
            return;
        }

        _deferRequestsService.DeferLowPriRequest(new PostPublisherMediaStatsReceived
                                                 {
                                                     PublisherAccountId = publisherAccount.PublisherAccountId,
                                                     PublisherMediaId = dynPublisherMedia.PublisherMediaId,
                                                     Stats = new List<PublisherStatValue>
                                                             {
                                                                 new()
                                                                 {
                                                                     Name = "impressions",
                                                                     Value = storyInsightItem.Impressions
                                                                 },
                                                                 new()
                                                                 {
                                                                     Name = "reach",
                                                                     Value = storyInsightItem.Reach
                                                                 },
                                                                 new()
                                                                 {
                                                                     Name = "taps_forward",
                                                                     Value = storyInsightItem.TapsForward
                                                                 },
                                                                 new()
                                                                 {
                                                                     Name = "taps_back",
                                                                     Value = storyInsightItem.TapsBack
                                                                 },
                                                                 new()
                                                                 {
                                                                     Name = "exits",
                                                                     Value = storyInsightItem.Exits
                                                                 },
                                                                 new()
                                                                 {
                                                                     Name = "replies",
                                                                     Value = storyInsightItem.Replies
                                                                 }
                                                             }
                                                 }.WithAdminRequestInfo());
    }
}

public class FacebookService : BaseAuthenticatedApiService
{
    private readonly IAssociationService _associationService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IUserService _userService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IRydrDataService _rydrDataService;
    private readonly IPublisherDataService _publisherDataService;

    public FacebookService(IAssociationService associationService,
                           IWorkspaceService workspaceService,
                           IPublisherAccountService publisherAccountService,
                           IRydrDataService rydrDataService,
                           IUserService userService,
                           IDeferRequestsService deferRequestsService)
    {
        _associationService = associationService;
        _workspaceService = workspaceService;
        _publisherAccountService = publisherAccountService;
        _userService = userService;
        _deferRequestsService = deferRequestsService;
        _rydrDataService = rydrDataService;
        _publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(PublisherType.Facebook.ToString());
    }

    public async Task<OnlyResultResponse<ConnectedApiInfo>> Post(PostFacebookConnectUser request)
    {
        var dynUser = await _userService.GetUserAsync(request.UserId);

        var tokenPublisherAccount = await _publisherAccountService.ConnectPublisherAccountAsync(new PublisherAccount
                                                                                                {
                                                                                                    Type = PublisherType.Facebook,
                                                                                                    AccountType = PublisherAccountType.User,
                                                                                                    RydrAccountType = RydrAccountType.TokenAccount,
                                                                                                    AccountId = request.AccountId,
                                                                                                    AccessToken = request.AuthToken,
                                                                                                    Email = dynUser.Email,
                                                                                                    ProfilePicture = dynUser.Avatar,
                                                                                                    FullName = dynUser.FullName(),
                                                                                                    UserName = request.UserName.Coalesce(dynUser.UserName).ToNullIfEmpty(),
                                                                                                    CreatedBy = dynUser.UserId,
                                                                                                    ModifiedBy = dynUser.UserId
                                                                                                });

        // Get personal workspace for this user created from/linked to this provider...if one exists
        var dynDefaultWorkspace = await _workspaceService.TryGetPersonalWorkspaceAsync(request.UserId,
                                                                                       w => w.CreatedViaPublisherType == PublisherType.Facebook)
                                  ??
                                  await _workspaceService.CreateAsync(new Workspace
                                                                      {
                                                                          Name = string.Concat(PublisherType.Facebook.ToString(), " ",
                                                                                               tokenPublisherAccount.AccountId, " (",
                                                                                               string.Join(", ", new[]
                                                                                                                 {
                                                                                                                     tokenPublisherAccount.UserName, tokenPublisherAccount.FullName
                                                                                                                 }.Where(s => s.HasValue())),
                                                                                               ")"),
                                                                          WorkspaceType = WorkspaceType.Personal,
                                                                          WorkspaceFeatures = request.Features == WorkspaceFeature.None
                                                                                                  ? WorkspaceFeature.Default
                                                                                                  : request.Features
                                                                      },
                                                                      tokenPublisherAccount);

        if (tokenPublisherAccount.WorkspaceId <= GlobalItemIds.MinUserDefinedObjectId)
        {
            await _publisherAccountService.UpdatePublisherAccountAsync(tokenPublisherAccount,
                                                                       pa => { pa.WorkspaceId = dynDefaultWorkspace.Id; });
        }

        if (dynDefaultWorkspace.DefaultPublisherAccountId != tokenPublisherAccount.PublisherAccountId)
        {
            await _workspaceService.LinkTokenAccountAsync(dynDefaultWorkspace, tokenPublisherAccount);
        }

        if (dynUser.DefaultWorkspaceId <= 0 || dynUser.DefaultWorkspaceId != dynDefaultWorkspace.Id)
        {
            dynUser.DefaultWorkspaceId = dynDefaultWorkspace.Id;

            await _userService.UpdateUserAsync(dynUser);
        }

        var userInfo = new ConnectedApiInfo
                       {
                           OwnerUserId = dynUser.UserId,
                           OwnerName = dynUser.FullName(),
                           OwnerEmail = dynUser.Email,
                           OwnerUserName = dynUser.UserName,
                           OwnerAuthProviderId = dynUser.AuthProviderUid,
                           WorkspaceId = dynDefaultWorkspace.Id,
                           WorkspaceName = dynDefaultWorkspace.Name,
                           DefaultPublisherAccount = (await _publisherAccountService.GetPublisherAccountAsync(dynDefaultWorkspace.DefaultPublisherAccountId)
                                                     ).ToPublisherAccount()
                       };

        return userInfo.AsOnlyResultResponse();
    }

    public async Task<OnlyResultsResponse<FacebookPlaceInfo>> Get(SearchFbPlaces request)
    {
        var workspace = await _workspaceService.GetWorkspaceAsync(request.WorkspaceId);

        var publisherApp = await _publisherDataService.GetPublisherAppOrDefaultAsync(request.PublisherAppId);

        var publisherAppAccount = workspace.DefaultPublisherAccountId <= 0
                                      ? null
                                      : await _dynamoDb.GetPublisherAppAccountAsync(workspace.DefaultPublisherAccountId, publisherApp.PublisherAppId);

        var fbClient = publisherAppAccount == null
                           ? await publisherApp.GetOrCreateFbClientAsync(FacebookSdkConfig.StaticFbSystemToken)
                           : await publisherAppAccount.GetOrCreateFbClientAsync();

        var results = new List<FacebookPlaceInfo>(50);

        await foreach (var fbPlacesBatch in fbClient.SearchPlacesAsync(request.Query, request))
        {
            foreach (var fbPlace in fbPlacesBatch)
            {
                if (fbPlace == null || fbPlace.IsPermanentlyClosed)
                {
                    continue;
                }

                var facebookPlaceInfo = fbPlace.ConvertTo<FacebookPlaceInfo>();

                if (facebookPlaceInfo.Description.IsNullOrEmpty())
                {
                    facebookPlaceInfo.Description = fbPlace.About;
                }

                if (fbPlace.CoverPhoto != null && fbPlace.CoverPhoto.SourceUrl.HasValue())
                {
                    facebookPlaceInfo.CoverPhotoUrl = fbPlace.CoverPhoto.SourceUrl;
                }

                if (fbPlace.Location != null)
                {
                    facebookPlaceInfo.Location = fbPlace.Location.ConvertTo<Address>();
                    facebookPlaceInfo.Location.StateProvince = fbPlace.Location.State;
                    facebookPlaceInfo.Location.Address1 = fbPlace.Location.Street;
                }

                facebookPlaceInfo.RydrPlaceId = (await _dynamoDb.TryGetPlaceAsync(PublisherType.Facebook, fbPlace.Id)
                                                )?.Id ?? 0;

                results.Add(facebookPlaceInfo);
            }

            if (results.Count >= 50)
            {
                break;
            }
        }

        return results.AsOnlyResultsResponse();
    }

    [RydrForcedSimpleCacheResponse(900)]
    public async Task<OnlyResultsResponse<FacebookAccount>> Get(GetFbIgBusinessAccounts request)
    {
        var workspace = await _workspaceService.GetWorkspaceAsync(request.WorkspaceId);

        var workspacePublisherAccount = await _workspaceService.TryGetDefaultPublisherAccountAsync(workspace.Id);

        var publisherApp = await _publisherDataService.GetPublisherAppOrDefaultAsync(request.PublisherAppId);

        var publisherAppAccount = await _dynamoDb.GetPublisherAppAccountAsync(workspacePublisherAccount.PublisherAccountId, publisherApp.PublisherAppId);

        var fbClient = await publisherAppAccount.GetOrCreateFbClientAsync();

        var results = new List<FacebookAccount>(50);

        try
        {
            // Basically here we return any accounts the user has linked as fbig accounts at Facebook that are NOT yet linked/in RYDR
            await foreach (var fbIgBusinessBatchEnumerable in fbClient.GetFbIgBusinessAccountsAsync(publisherAppAccount.ForUserId))
            {
                // Build up an enumerable we'll use later to enumerate over and decorate/return to the user, but also to build up a map of rydr publisher accounts
                var fbIgBusinessBatchTuples = fbIgBusinessBatchEnumerable.Select(fb => (FbAcct: fb,
                                                                                        FbIgId: (fb.InstagramBusinessAccount?.Id).Coalesce(fb.Id),
                                                                                        IgId: fb.InstagramBusinessAccount?.InstagramId))
                                                                         .Select(t => (t.FbAcct,
                                                                                       FbEdgeId: t.FbIgId.HasValue()
                                                                                                     ? DynPublisherAccount.BuildEdgeId(PublisherType.Facebook, t.FbIgId)
                                                                                                     : null,
                                                                                       IgEdgeId: t.IgId.HasValue()
                                                                                                     ? DynPublisherAccount.BuildEdgeId(PublisherType.Instagram, t.IgId)
                                                                                                     : null))
                                                                         .AsListReadOnly();

                // Now go get all the possible needed publisher account models in one batch get, map those for lookup in the decoration loop
                IEnumerable<DynamoId> getMapItemIds()
                {
                    foreach (var fbIgBizTuple in fbIgBusinessBatchTuples)
                    {
                        if (fbIgBizTuple.FbEdgeId != null)
                        {
                            var mapEdgeId = DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, fbIgBizTuple.FbEdgeId);
                            var mapLongId = mapEdgeId.ToShaBase64().ToLongHashCode();

                            yield return new DynamoId(mapLongId, mapEdgeId);
                        }

                        if (fbIgBizTuple.IgEdgeId != null)
                        {
                            var mapEdgeId = DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, fbIgBizTuple.IgEdgeId);
                            var mapLongId = mapEdgeId.ToShaBase64().ToLongHashCode();

                            yield return new DynamoId(mapLongId, mapEdgeId);
                        }
                    }
                }

                var rydrPublisherAccountMap = await _dynamoDb.GetItemsFromAsync<DynPublisherAccount, DynItemMap>(_dynamoDb.QueryItemsAsync<DynItemMap>(getMapItemIds()),
                                                                                                                 m => new DynamoId(m.ReferenceNumber.Value,
                                                                                                                                   m.MappedItemEdgeId))
                                                             .ToDictionarySafe(p => p.EdgeId, StringComparer.OrdinalIgnoreCase);

                // Loop over the fb accounts, decorate them, return to the user...
                foreach (var fbIgBusinessTuple in fbIgBusinessBatchTuples)
                {
                    var facebookAccount = fbIgBusinessTuple.FbAcct.ConvertTo<FacebookAccount>();

                    if (facebookAccount == null)
                    {
                        continue;
                    }

                    var rydrPublisherAccount = rydrPublisherAccountMap.ContainsKey(fbIgBusinessTuple.FbEdgeId)
                                                   ? rydrPublisherAccountMap[fbIgBusinessTuple.FbEdgeId]
                                                   : null;

                    if (rydrPublisherAccount == null && fbIgBusinessTuple.IgEdgeId.HasValue() &&
                        rydrPublisherAccountMap.ContainsKey(fbIgBusinessTuple.IgEdgeId))
                    { // Don't have a mapped fb publisher account for this fb user, but we do have an existing publisher account in the system for
                        // the matching instagram platform account, which could exist if the user had previously linked (or more likely, been invited to
                        // a deal) from the instagram public search with only known ig info...so, convert that ig account over to a proper fb publisher
                        // account now
                        var igPublisherAccount = rydrPublisherAccountMap[fbIgBusinessTuple.IgEdgeId];

                        // Populate the account for return to the client, upconvert later
                        igPublisherAccount.PopulateIgAccountWithFbInfo(facebookAccount);

                        _deferRequestsService.DeferLowPriRequest(new PublisherAccountUpConvertFacebook
                                                                 {
                                                                     PublisherAccountId = igPublisherAccount.PublisherAccountId,
                                                                     WithFacebookAccount = facebookAccount
                                                                 });

                        // And the new one is now a proper fb model, so change the map up in case others use it
                        rydrPublisherAccount = igPublisherAccount;
                        rydrPublisherAccountMap[fbIgBusinessTuple.FbEdgeId] = igPublisherAccount;
                        rydrPublisherAccountMap.Remove(fbIgBusinessTuple.IgEdgeId);
                    }

                    // If the publisher account is not yet in the system, not yet associated with this particular user, return it
                    // If it is associated, filter it out (null is filtered next)
                    if (rydrPublisherAccount == null)
                    { // Not yet in rydr...nothing else we can do
                        results.Add(facebookAccount);

                        continue;
                    }

                    // Have the rydr object at least, so populate the fb account with info we have
                    facebookAccount.PublisherAccountId = rydrPublisherAccount.PublisherAccountId;

                    if (facebookAccount?.InstagramBusinessAccount != null)
                    {
                        facebookAccount.InstagramBusinessAccount.LinkedAsAccountType = rydrPublisherAccount.RydrAccountType;
                    }

                    if (rydrPublisherAccount.IsDeleted())
                    { // Deleted, needs to be re-added/linked/etc
                        results.Add(facebookAccount);

                        continue;
                    }

                    var hasAccessToAccount = await _workspaceService.UserHasAccessToAccountAsync(request.WorkspaceId, request.UserId, rydrPublisherAccount.PublisherAccountId);

                    if (!hasAccessToAccount)
                    {
                        results.Add(facebookAccount);

                        continue;
                    }

                    var isAssociated = await _associationService.IsAssociatedAsync(request.WorkspaceId, rydrPublisherAccount.PublisherAccountId);

                    if (!isAssociated)
                    {
                        results.Add(facebookAccount);
                    }

                    // For any other situation, this facebook account is already in RYDR and linked appropriately to the given user
                }
            }
        }
        catch(FbApiException fbx)
        {
            _log.Exception(fbx, "Unable to GetFbIgBusinessAccountsAsync successfully");
        }

        // RYDR accounts have access to unlinked accounts that have been setup to run a rydr prior to requiring an account to link up fb/ig/rydr accounts and permissions...
        if (workspacePublisherAccount.IsRydrSystemPublisherAccount() && workspace.IsRydrWorkspace())
        {
            var rydrBizAccountsUnlinked = await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<Int64Id>(@"
SELECT   DISTINCT pa.Id AS Id
FROM     PublisherAccounts pa
WHERE    pa.PublisherType = 1
         AND pa.AccountType = 2
         AND pa.RydrAccountType = 1
         AND pa.AccountId LIKE 'rydr_2_%'
         AND pa.DeletedOn IS NULL
         AND NOT EXISTS
         (
         SELECT  NULL
         FROM    PublisherAccountLinks pal
         WHERE   pal.FromPublisherAccountId = @WorkspacePublisherAccountId
                 AND pal.WorkspaceId = @WorkspaceId
                 AND pal.ToPublisherAccountId = pa.Id
                 AND pal.DeletedOn IS NULL 
         );
",
                                                                                                              new
                                                                                                              {
                                                                                                                  WorkspacePublisherAccountId = workspacePublisherAccount.PublisherAccountId,
                                                                                                                  request.WorkspaceId
                                                                                                              }));

            if (rydrBizAccountsUnlinked != null)
            {
                results.AddRange(await _publisherAccountService.GetPublisherAccountsAsync(rydrBizAccountsUnlinked.Select(r => r.Id))
                                                               .Select(p => new FacebookAccount
                                                                            {
                                                                                Name = p.FullName,
                                                                                About = p.Description,
                                                                                UserName = p.UserName,
                                                                                Website = p.Website,
                                                                                PublisherAccountId = p.PublisherAccountId,
                                                                                InstagramBusinessAccount = new FacebookIgBusinessAccount
                                                                                                           {
                                                                                                               Id = p.AccountId,
                                                                                                               LinkedAsAccountType = p.RydrAccountType,
                                                                                                               Name = p.FullName,
                                                                                                               InstagramId = p.AlternateAccountId.ToLong(0),
                                                                                                               ProfilePictureUrl = p.ProfilePicture,
                                                                                                               UserName = p.UserName,
                                                                                                               Website = p.Website,
                                                                                                               Description = p.Description
                                                                                                           }
                                                                            })
                                                               .ToList());
            }
        }

        return results.AsOnlyResultsResponse();
    }

    public async Task<OnlyResultsResponse<FacebookBusiness>> Get(GetFbBusinesses request)
    {
        var workspacePublisherAccount = await _workspaceService.TryGetDefaultPublisherAccountAsync(request.WorkspaceId);

        var publisherApp = await _publisherDataService.GetPublisherAppOrDefaultAsync(request.PublisherAppId);

        var publisherAppAccount = await _dynamoDb.GetPublisherAppAccountAsync(workspacePublisherAccount.PublisherAccountId, publisherApp.PublisherAppId);

        Guard.AgainstInvalidData(!publisherAppAccount.HasBusinessManagementScope(), "The token account associated with the workspace is invalid or does not contain the business_management scope");

        var fbClient = await publisherAppAccount.GetOrCreateFbClientAsync();

        var fbBusinesses = await fbClient.GetBusinessesListAsync(publisherAppAccount.ForUserId)
                                         .SelectManyToListAsync(b => b.Select(fb => fb.ConvertTo<FacebookBusiness>()), 250);

        return fbBusinesses.AsOnlyResultsResponse();
    }
}
