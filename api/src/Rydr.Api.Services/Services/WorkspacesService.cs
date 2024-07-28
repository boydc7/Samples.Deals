using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services;

[RydrCacheResponse(1800)]
public class WorkspacesService : BaseAuthenticatedApiService
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IServerNotificationService _serverNotificationService;
    private readonly IUserService _userService;
    private readonly IMapItemService _mapItemService;
    private readonly IRydrDataService _rydrDataService;

    public WorkspacesService(IWorkspaceService workspaceService,
                             IPublisherAccountService publisherAccountService,
                             IDeferRequestsService deferRequestsService,
                             IServerNotificationService serverNotificationService,
                             IUserService userService,
                             IMapItemService mapItemService,
                             IRydrDataService rydrDataService)
    {
        _workspaceService = workspaceService;
        _publisherAccountService = publisherAccountService;
        _deferRequestsService = deferRequestsService;
        _serverNotificationService = serverNotificationService;
        _userService = userService;
        _mapItemService = mapItemService;
        _rydrDataService = rydrDataService;
    }

    public async Task<OnlyResultResponse<Workspace>> Get(GetWorkspace request)
    {
        var dynWorkspace = await _workspaceService.GetWorkspaceAsync(request.Id);

        var forWorkspaceUserId = request.IsSystemRequest
                                     ? dynWorkspace.OwnerId
                                     : request.UserId;

        var publisherAccounts = await _workspaceService.GetWorkspaceUserPublisherAccountsAsync(dynWorkspace.Id, forWorkspaceUserId)
                                                       .Take(100)
                                                       .ToList(100);

        var response = await dynWorkspace.ToWorkspaceAsync(forWorkspaceUserId, publisherAccounts)
                                         .AsOnlyResultResponseAsync();

        return response;
    }

    public async Task<OnlyResultsResponse<Workspace>> Get(GetWorkspaces request)
    {
        var userId = request.GetUserIdFromIdentifier();

        var workspaces = new List<Workspace>();
        var take = 50.ToDynamoBatchCeilingTake();

        await foreach (var dynWorkspace in _workspaceService.GetUserWorkspacesAsync(userId))
        {
            var forWorkspaceUserId = request.IsSystemRequest
                                         ? dynWorkspace.OwnerId
                                         : userId;

            var hasValidTokenAccount = false;

            // We just return up to 3 profiles for display niceties here basically, hence to take 3...
            var publisherAccounts = new List<DynPublisherAccount>(take);

            await foreach (var dynPublisherAccount in _workspaceService.GetWorkspaceUserPublisherAccountsAsync(dynWorkspace.Id, forWorkspaceUserId,
                                                                                                               forWorkspaceUserId == dynWorkspace.OwnerId))
            {
                if (dynPublisherAccount.IsSoftLinked || dynPublisherAccount.IsBasicLink || !dynPublisherAccount.IsTokenAccount())
                {
                    hasValidTokenAccount = hasValidTokenAccount || dynPublisherAccount.IsSoftLinked || dynPublisherAccount.IsBasicLink;

                    publisherAccounts.Add(dynPublisherAccount);

                    if (publisherAccounts.Count >= take)
                    {
                        break;
                    }

                    continue;
                }

                if (hasValidTokenAccount || dynPublisherAccount.IsSyncDisabled)
                {
                    continue;
                }

                // No valid token account found yet, and this one is disabled...
                var publisherAppAccount = await _dynamoDb.GetDefaultPublisherAppAccountAsync(dynPublisherAccount);

                hasValidTokenAccount = publisherAppAccount != null && publisherAppAccount.IsValid();
            }

            var workspace = await dynWorkspace.ToWorkspaceAsync(forWorkspaceUserId, publisherAccounts.OrderBy(p => p.UserName));

            workspace.RequiresReauth = publisherAccounts.Count > 0 && !hasValidTokenAccount;

            var accessRequestIds = await _dynamoDb.FromQuery<DynItemMap>(m => m.Id == workspace.Id &&
                                                                              Dynamo.BeginsWith(m.EdgeId, string.Concat((int)DynItemType.InviteRequest, "|")))
                                                  .QueryColumnAsync(m => m.Id, _dynamoDb)
                                                  .Take(take)
                                                  .ToList(take);

            workspace.AccessRequests = accessRequestIds?.Count ?? 0;

            workspaces.Add(workspace);
        }

        return workspaces.AsOnlyResultsResponse();
    }

    public OnlyResultsResponse<WorkspaceAccessRequest> Get(GetWorkspaceAccessRequests request)
    {
        var workspaceId = request.GetWorkspaceIdFromIdentifier();

        var requests = _dynamoDb.FromQuery<DynItemMap>(m => m.Id == workspaceId &&
                                                            Dynamo.BeginsWith(m.EdgeId, string.Concat((int)DynItemType.InviteRequest, "|")))
                                .Exec()
                                .Select(m =>
                                        {
                                            var dynUser = _userService.TryGetUser(m.ReferenceNumber.Value);

                                            if (dynUser == null || dynUser.IsDeleted())
                                            {
                                                _mapItemService.DeleteMap(m.Id, m.EdgeId);

                                                return null;
                                            }

                                            return new WorkspaceAccessRequest
                                                   {
                                                       UserId = dynUser.UserId,
                                                       Name = dynUser.DisplayName.Coalesce(dynUser.FullName),
                                                       UserName = dynUser.AuthProviderUserName.Coalesce(dynUser.UserName),
                                                       UserEmail = dynUser.Email,
                                                       Avatar = dynUser.Avatar,
                                                       RequestedOn = m.MappedItemEdgeId.ToDateTime()
                                                   };
                                        })
                                .Where(m => m != null)
                                .Skip(request.Skip)
                                .Take(request.Take);

        return requests.AsOnlyResultsResponse();
    }

    public async Task<OnlyResultsResponse<WorkspacePublisherAccountInfo>> Get(GetWorkspacePublisherAccounts request)
    {
        var workspaceId = request.GetWorkspaceIdFromIdentifier();

        var dynWorkspace = await _workspaceService.GetWorkspaceAsync(workspaceId);

        var forWorkspaceUserId = request.IsSystemRequest
                                     ? dynWorkspace.OwnerId
                                     : request.UserId;

        List<DynPublisherAccount> publisherAccounts = null;

        if (request.UserNamePrefix.HasValue())
        {
            var publisherAccountIds = await _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<long>(@"
SELECT   DISTINCT wupa.PublisherAccountId AS Id
FROM     WorkspaceUserPublisherAccounts wupa
WHERE    wupa.UserId = @ForWorkspaceUserId
         AND wupa.WorkspaceId = @WorkspaceId
         AND wupa.DeletedOn IS NULL
         AND EXISTS 
         (
         SELECT    NULL
         FROM      PublisherAccounts pa
         WHERE     pa.Id = wupa.PublisherAccountId
                   AND pa.DeletedOn IS NULL
                   AND pa.UserName LIKE (@UserSearch)
         )
LIMIT    @Limit;
",
                                                                                                        new
                                                                                                        {
                                                                                                            ForWorkspaceUserId = forWorkspaceUserId,
                                                                                                            WorkspaceId = workspaceId,
                                                                                                            UserSearch = string.Concat(request.UserNamePrefix, "%"),
                                                                                                            Limit = request.Take
                                                                                                        }));

            publisherAccounts = await _publisherAccountService.GetPublisherAccountsAsync(publisherAccountIds)
                                                              .Take(request.Take)
                                                              .ToList(request.Take);
        }
        else
        {
            publisherAccounts = await _workspaceService.GetWorkspaceUserPublisherAccountsAsync(dynWorkspace.Id, forWorkspaceUserId)
                                                       .Skip(request.Skip)
                                                       .Take(request.Take)
                                                       .ToList(request.Take);
        }

        var results = new List<WorkspacePublisherAccountInfo>(publisherAccounts.Count);

        foreach (var publisherAccount in publisherAccounts)
        {
            var workspacePublisherAccount = await publisherAccount.ToWorkspacePublisherAccountInfoAsync(dynWorkspace);
            results.Add(workspacePublisherAccount);
        }

        return results.AsOnlyResultsResponse();
    }

    public async Task<OnlyResultsResponse<WorkspaceUser>> Get(GetWorkspaceUsers request)
    {
        var workspaceId = request.GetWorkspaceIdFromIdentifier();

        var results = new List<WorkspaceUser>(request.Take);

        await foreach (var workspaceUser in _workspaceService.GetWorkspaceUsersAsync(workspaceId, request.Skip))
        {
            results.Add(workspaceUser);

            if (results.Count >= request.Take)
            {
                break;
            }
        }

        return results.AsOnlyResultsResponse();
    }

    public async Task<OnlyResultsResponse<WorkspacePublisherAccountInfo>> Get(GetWorkspaceUserPublisherAccounts request)
    {
        var workspaceId = request.GetWorkspaceIdFromIdentifier();

        var userId = request.WorkspaceUserIdentifier.EqualsOrdinalCi("me")
                         ? request.UserId
                         : request.WorkspaceUserIdentifier.ToLong(0);

        var dynWorkspace = await _workspaceService.GetWorkspaceAsync(workspaceId);

        if (request.Unlinked)
        { // Asked for unlinked accounts...
            if (await _workspaceService.IsWorkspaceAdmin(dynWorkspace, userId))
            { // Owners are always linked to all, asking for unlinked profiles by the owner...which is always none...nothing to do
                return new OnlyResultsResponse<WorkspacePublisherAccountInfo>();
            }

            var unlinked = await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<Int64Id>(string.Concat(@"
SELECT  DISTINCT opa.PublisherAccountId AS Id
FROM    WorkspaceUserPublisherAccounts opa
WHERE   opa.UserId = @OwnerId
        AND opa.WorkspaceId = @WorkspaceId
        AND opa.DeletedOn IS NULL
        AND NOT EXISTS
        (
        SELECT  NULL
        FROM    WorkspaceUserPublisherAccounts upa
        WHERE   upa.UserId = @UserId
                AND upa.WorkspaceId = @WorkspaceId
                AND upa.PublisherAccountId = opa.PublisherAccountId
                AND upa.DeletedOn IS NULL
        )",
                                                                                                             request.PublisherAccountId <= 0
                                                                                                                 ? string.Empty
                                                                                                                 : @"
        AND opa.PublisherAccountId = @PublisherAccountId",
                                                                                                             request.UserNamePrefix.IsNullOrEmpty()
                                                                                                                 ? string.Empty
                                                                                                                 : @"
        AND opa.UserName LIKE (@UserSearch)",
                                                                                                             request.RydrAccountType == RydrAccountType.None
                                                                                                                 ? string.Empty
                                                                                                                 : @"
        AND EXISTS
        (
        SELECT  NULL
        FROM    PublisherAccounts pa
        WHERE   pa.Id = opa.PublisherAccountId
                AND pa.RydrAccountType = @RydrAccountType
        )", @"
ORDER BY
        opa.PublisherAccountId
LIMIT   @Take
OFFSET  @Skip;
"),
                                                                                               new
                                                                                               {
                                                                                                   dynWorkspace.OwnerId,
                                                                                                   UserId = userId,
                                                                                                   WorkspaceId = dynWorkspace.Id,
                                                                                                   request.Take,
                                                                                                   request.Skip,
                                                                                                   RydrAccountType = (int)request.RydrAccountType,
                                                                                                   UserSearch = string.Concat(request.UserNamePrefix, "%")
                                                                                               }));

            var unlinkedPublisherAccounts = unlinked == null
                                                ? null
                                                : await _publisherAccountService.GetPublisherAccountsAsync(unlinked.Select(io => io.Id))
                                                                                .Take(request.Take)
                                                                                .ToList(request.Take);

            var unlinkedResults = new List<WorkspacePublisherAccountInfo>(unlinkedPublisherAccounts.Count);

            foreach (var publisherAccount in unlinkedPublisherAccounts)
            {
                var workspacePublisherAccount = await publisherAccount.ToWorkspacePublisherAccountInfoAsync(dynWorkspace);
                unlinkedResults.Add(workspacePublisherAccount);
            }

            return unlinkedResults.AsOnlyResultsResponse();
        }

        // Asked for linked accounts...only 1?
        if (request.PublisherAccountId > 0)
        {
            var publisherAccount = await _workspaceService.TryGetWorkspaceUserPublisherAccountAsync(workspaceId,
                                                                                                    userId,
                                                                                                    request.PublisherAccountId);

            return new OnlyResultsResponse<WorkspacePublisherAccountInfo>
                   {
                       Results = publisherAccount == null
                                     ? null
                                     : new List<WorkspacePublisherAccountInfo>
                                       {
                                           await publisherAccount.ToWorkspacePublisherAccountInfoAsync(dynWorkspace)
                                       }
                   };
        }

        // All of them, or all with a filter...
        List<DynPublisherAccount> linkedAccounts = null;

        if (request.UserNamePrefix.IsNullOrEmpty())
        {
            linkedAccounts = await _workspaceService.GetWorkspaceUserPublisherAccountsAsync(workspaceId, userId)
                                                    .Where(dpa => request.RydrAccountType == RydrAccountType.None ||
                                                                  dpa.RydrAccountType == request.RydrAccountType)
                                                    .Skip(request.Skip)
                                                    .Take(request.Take)
                                                    .ToList(request.Take);
        }
        else
        {
            var publisherAccountIds = await _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<long>(string.Concat(@"
SELECT   DISTINCT wupa.PublisherAccountId AS Id
FROM     WorkspaceUserPublisherAccounts wupa
WHERE    wupa.UserId = @ForWorkspaceUserId
         AND wupa.WorkspaceId = @WorkspaceId
         AND wupa.DeletedOn IS NULL
         AND EXISTS 
         (
         SELECT    NULL
         FROM      PublisherAccounts pa
         WHERE     pa.Id = wupa.PublisherAccountId
                   AND pa.DeletedOn IS NULL
                   AND pa.UserName LIKE (@UserSearch)",
                                                                                                                      request.RydrAccountType == RydrAccountType.None
                                                                                                                          ? string.Empty
                                                                                                                          : @"
                   AND pa.RydrAccountType = @RydrAccountType", @"
         )
"),
                                                                                                        new
                                                                                                        {
                                                                                                            ForWorkspaceUserId = userId,
                                                                                                            WorkspaceId = workspaceId,
                                                                                                            UserSearch = string.Concat(request.UserNamePrefix, "%"),
                                                                                                            request.RydrAccountType
                                                                                                        }));

            linkedAccounts = await _publisherAccountService.GetPublisherAccountsAsync(publisherAccountIds)
                                                           .Take(request.Take)
                                                           .ToList(request.Take);
        }

        var linkedResults = new List<WorkspacePublisherAccountInfo>(linkedAccounts.Count);

        foreach (var publisherAccount in linkedAccounts)
        {
            var workspacePublisherAccount = await publisherAccount.ToWorkspacePublisherAccountInfoAsync(dynWorkspace);
            linkedResults.Add(workspacePublisherAccount);
        }

        return linkedResults.AsOnlyResultsResponse();
    }

    public async Task<OnlyResultsResponse<WorkspaceUser>> Get(GetWorkspacePublisherAccountUsers request)
    {
        var workspace = await _workspaceService.GetWorkspaceAsync(request.GetWorkspaceIdFromIdentifier());

        var userIds = await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<DynamoItemIdEdge>(@"
SELECT  DISTINCT upa.UserId AS Id, u.UserName AS EdgeId
FROM    WorkspaceUserPublisherAccounts upa
JOIN    Users u
ON      upa.UserId = u.Id
WHERE   upa.PublisherAccountId = @PublisherAccountId
        AND upa.WorkspaceId = @WorkspaceId
        AND upa.DeletedOn IS NULL
ORDER BY
        upa.UserId
LIMIT   @Take
OFFSET  @Skip;
",
                                                                                                   new
                                                                                                   {
                                                                                                       PublisherAccountId = request.GetPublisherIdFromIdentifier(),
                                                                                                       WorkspaceId = workspace.Id,
                                                                                                       request.Take,
                                                                                                       request.Skip
                                                                                                   }));

        if (userIds == null)
        {
            return new OnlyResultsResponse<WorkspaceUser>();
        }

        var results = new List<WorkspaceUser>(request.Take);

        await foreach (var linkedUser in _userService.GetUsersAsync(userIds)
                                                     .Take(request.Take))
        {
            var role = await _workspaceService.GetWorkspaceUserRoleAsync(workspace.Id, linkedUser.UserId);

            results.Add(linkedUser.ToWorkspaceUser(role));
        }

        return results.AsOnlyResultsResponse();
    }

    public async Task<OnlyResultResponse<Workspace>> Post(PostWorkspace request)
    { // Anything created by this post is a team (personal workspaces are implicitly created when a user signs up)
        request.Model.WorkspaceType = WorkspaceType.Team;

        var requestPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.RequestPublisherAccountId);

        var workspaceTokenAccount = requestPublisherAccount.IsTokenAccount()
                                        ? requestPublisherAccount
                                        : null;

        var dynWorkspace = await _workspaceService.CreateAsync(request.Model, workspaceTokenAccount);

        if (workspaceTokenAccount == null && dynWorkspace.DefaultPublisherAccountId > 0)
        {
            workspaceTokenAccount = await _publisherAccountService.TryGetPublisherAccountAsync(dynWorkspace.DefaultPublisherAccountId);
        }

        try
        {
            if (!request.LinkAccounts.IsNullOrEmpty())
            {
                var putLinkRequest = new PutPublisherAccountLinks
                                     {
                                         LinkAccounts = request.LinkAccounts
                                     }.PopulateWithRequestInfo(request);

                putLinkRequest.WorkspaceId = dynWorkspace.Id;

                if (workspaceTokenAccount.IsTokenAccount())
                {
                    putLinkRequest.RequestPublisherAccountId = workspaceTokenAccount.PublisherAccountId;
                }

                await Gateway.SendAsync(putLinkRequest);
            }

            await _workspaceService.AssociateInviteCodeAsync(dynWorkspace.Id);

            var workspace = await dynWorkspace.ToWorkspaceAsync(dynWorkspace.OwnerId);

            return workspace.AsOnlyResultResponse();
        }
        catch
        {
            _deferRequestsService.DeferLowPriRequest(new DeleteWorkspaceInternal
                                                     {
                                                         Id = dynWorkspace.Id
                                                     }.WithAdminRequestInfo());

            throw;
        }
    }

    public async Task<LongIdResponse> Put(PutWorkspace request)
    {
        var existing = await _workspaceService.GetWorkspaceAsync(request.Id);

        var fromWorkspaceType = existing?.WorkspaceType ?? WorkspaceType.Unspecified;

        var dynWorkspace = await _dynamoDb.UpdateFromExistingAsync(existing, x => request.Model.ToDynWorkspace(x), request);

        _deferRequestsService.DeferRequest(new WorkspaceUpdated
                                           {
                                               Id = request.Id,
                                               FromWorkspaceType = fromWorkspaceType,
                                               ToWorkspaceType = dynWorkspace.WorkspaceType
                                           });

        return dynWorkspace.ToLongIdResponse();
    }

    public void Delete(DeleteWorkspace request)
        => _deferRequestsService.DeferLowPriRequest(new DeleteWorkspaceInternal
                                                    {
                                                        Id = request.Id
                                                    });

    public async Task Post(PostRequestWorkspaceAccess request)
    {
        var userIdRequestingAccess = request.GetUserIdFromIdentifier();

        var dynUser = await _userService.GetUserAsync(userIdRequestingAccess);

        var inviteToken = await _mapItemService.TryGetMapByHashedEdgeAsync(DynItemType.InviteToken, request.InviteToken);

        // Push a temporary map token invite
        await _mapItemService.PutMapAsync(new DynItemMap
                                          {
                                              Id = inviteToken.ReferenceNumber.Value,
                                              EdgeId = DynItemMap.BuildEdgeId(DynItemType.InviteRequest, userIdRequestingAccess.ToStringInvariant()),
                                              ReferenceNumber = userIdRequestingAccess,
                                              MappedItemEdgeId = _dateTimeProvider.UtcNowTs.ToStringInvariant(),
                                              ExpiresAt = _dateTimeProvider.UtcNowTs + (60 * 60 * 24 * 45) // 45 days later
                                          });

        // Get the user's default workspace to use as the from account
        var fromPublisherAccount = await _workspaceService.TryGetDefaultPublisherAccountAsync(dynUser.DefaultWorkspaceId);

        // Notify the workspace owner of a pending invite
        if (fromPublisherAccount != null)
        {
            var toWorkspaceAccount = (await _workspaceService.TryGetDefaultPublisherAccountAsync(inviteToken.ReferenceNumber.Value)
                                     )?.ToPublisherAccountInfo();

            if (toWorkspaceAccount != null)
            {
                await _serverNotificationService.NotifyAsync(new ServerNotification
                                                             {
                                                                 From = fromPublisherAccount.ToPublisherAccountInfo(),
                                                                 To = toWorkspaceAccount,
                                                                 Message = "fromPublisherAccount.UserName is requesting access to your team.",
                                                                 Title = "Team access request from fromPublisherAccount.UserName",
                                                                 ServerNotificationType = ServerNotificationType.WorkspaceEvent,
                                                                 InWorkspaceId = inviteToken.ReferenceNumber.Value
                                                             });
            }
        }
    }

    public async Task Delete(DeleteWorkspaceAccessRequest request)
    {
        var workspaceId = request.GetWorkspaceIdFromIdentifier();

        await _mapItemService.DeleteMapAsync(workspaceId, DynItemMap.BuildEdgeId(DynItemType.InviteRequest, request.RequestedUserId.ToStringInvariant()));
    }

    public async Task Put(PutLinkWorkspaceTokenAccount request)
    {
        var existingPublisherAccount = request.TokenAccount.Id > 0
                                           ? await _publisherAccountService.GetPublisherAccountAsync(request.TokenAccount.Id)
                                           : await _publisherAccountService.TryGetPublisherAccountAsync(request.TokenAccount.Type, request.TokenAccount.AccountId);

        if (existingPublisherAccount != null)
        {
            request.TokenAccount.Id = existingPublisherAccount.PublisherAccountId;
        }

        await PostUpsertModelAsync<PostPublisherAccountUpsert, PublisherAccount>(request.TokenAccount, request);

        var workspaceId = request.GetWorkspaceIdFromIdentifier();
        var dynWorkspace = await _workspaceService.GetWorkspaceAsync(workspaceId);

        var dynPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.TokenAccount.Id);

        await _workspaceService.LinkTokenAccountAsync(dynWorkspace, dynPublisherAccount);
    }

    public Task Put(PutLinkWorkspaceUser request)
        => _workspaceService.LinkUserAsync(request.GetWorkspaceIdFromIdentifier(), request.LinkUserId);

    public Task Delete(DeleteLinkedWorkspaceUser request)
        => _workspaceService.DelinkUserAsync(request.GetWorkspaceIdFromIdentifier(), request.LinkedUserId);

    public Task Put(PutWorkspaceUserRole request)
        => _workspaceService.SetWorkspaceUserRoleAsync(request.GetWorkspaceIdFromIdentifier(), request.GetUserIdFromIdentifier(), request.WorkspaceRole);

    public Task Put(PutLinkWorkspaceUserPublisherAccount request)
        => _workspaceService.LinkUserToPublisherAccountAsync(request.GetWorkspaceIdFromIdentifier(), request.WorkspaceUserId, request.PublisherAccountId);

    public Task Delete(DeleteLinkWorkspaceUserPublisherAccount request)
        => _workspaceService.DelinkUserFromPublisherAccountAsync(request.GetWorkspaceIdFromIdentifier(), request.WorkspaceUserId, request.PublisherAccountId);
}
