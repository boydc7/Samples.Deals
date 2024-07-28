using System.Linq.Expressions;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.Services.Publishers;

public class TimestampedWorkspaceService : TimestampDynItemIdCachedServiceBase<DynWorkspace>, IWorkspaceService
{
    public static readonly IReadOnlyList<char> InviteCodeCharacters = new List<char>
                                                                      {
                                                                          '1',
                                                                          '2',
                                                                          '3',
                                                                          '4',
                                                                          '5',
                                                                          '6',
                                                                          '7',
                                                                          '8',
                                                                          '9',
                                                                          'A',
                                                                          'B',
                                                                          'C',
                                                                          'D',
                                                                          'E',
                                                                          'F',
                                                                          'G',
                                                                          'H',
                                                                          'I',
                                                                          'J',
                                                                          'K',
                                                                          'L',
                                                                          'M',
                                                                          'N',
                                                                          'P',
                                                                          'Q',
                                                                          'R',
                                                                          'S',
                                                                          'T',
                                                                          'U',
                                                                          'V',
                                                                          'W',
                                                                          'X',
                                                                          'Y',
                                                                          'Z'
                                                                      };

    private readonly IPocoDynamo _dynamoDb;
    private readonly IAssociationService _associationService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IAuthorizeService _authorizeService;
    private readonly IRequestStateManager _requestStateManager;
    private readonly IMapItemService _mapItemService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IUserService _userService;
    private readonly IServiceCacheInvalidator _serviceCacheInvalidator;
    private readonly IRydrDataService _rydrDataService;
    private readonly IWorkspacePublisherSubscriptionService _workspacePublisherSubscriptionService;
    private readonly IWorkspaceSubscriptionService _workspaceSubscriptionService;

    public TimestampedWorkspaceService(ICacheClient cacheClient, IPocoDynamo dynamoDb,
                                       IAssociationService associationService,
                                       IPublisherAccountService publisherAccountService,
                                       IAuthorizeService authorizeService,
                                       IRequestStateManager requestStateManager,
                                       IMapItemService mapItemService,
                                       IDeferRequestsService deferRequestsService,
                                       IUserService userService,
                                       IServiceCacheInvalidator serviceCacheInvalidator,
                                       IRydrDataService rydrDataService,
                                       IWorkspacePublisherSubscriptionService workspacePublisherSubscriptionService,
                                       IWorkspaceSubscriptionService workspaceSubscriptionService)
        : base(dynamoDb, cacheClient, 300)
    {
        _dynamoDb = dynamoDb;
        _associationService = associationService;
        _publisherAccountService = publisherAccountService;
        _authorizeService = authorizeService;
        _requestStateManager = requestStateManager;
        _mapItemService = mapItemService;
        _deferRequestsService = deferRequestsService;
        _userService = userService;
        _serviceCacheInvalidator = serviceCacheInvalidator;
        _rydrDataService = rydrDataService;
        _workspacePublisherSubscriptionService = workspacePublisherSubscriptionService;
        _workspaceSubscriptionService = workspaceSubscriptionService;
    }

    public async Task UpdateWorkspaceAsync(DynWorkspace workspace, Expression<Func<DynWorkspace>> put)
    {
        await _dynamoDb.UpdateItemAsync(workspace, put);

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 CompositeIds = new List<DynamoItemIdEdge>
                                                                {
                                                                    new(workspace.Id, workspace.EdgeId)
                                                                },
                                                 Type = RecordType.Workspace
                                             });

        FlushModel(workspace.Id);
    }

    public Task<DynWorkspace> TryGetWorkspaceAsync(long workspaceId)
    {
        if (workspaceId <= 0)
        {
            return Task.FromResult<DynWorkspace>(null);
        }

        return GetModelAsync(workspaceId,
                             () => _dynamoDb.GetItemAsync<DynWorkspace>(workspaceId, workspaceId.ToEdgeId()));
    }

    public DynWorkspace TryGetWorkspace(long workspaceId)
    {
        if (workspaceId <= 0)
        {
            return null;
        }

        return GetModel(workspaceId,
                        () => _dynamoDb.GetItem<DynWorkspace>(workspaceId, workspaceId.ToEdgeId()));
    }

    public Task<DynWorkspace> GetWorkspaceAsync(long workspaceId)
        => GetModelAsync(workspaceId,
                         () => _dynamoDb.GetItemAsync<DynWorkspace>(workspaceId, workspaceId.ToEdgeId()));

    public async Task<string> TryGetWorkspacePrimaryEmailAddressAsync(long workspaceId)
    {
        var workspace = await TryGetWorkspaceAsync(workspaceId);

        if (workspace == null)
        {
            return null;
        }

        var owner = await _userService.TryGetUserAsync(workspace.OwnerId);

        if (owner != null && owner.Email.HasValue())
        {
            return owner.Email;
        }

        var defaultPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(workspace.DefaultPublisherAccountId);

        return (defaultPublisherAccount?.Email).ToNullIfEmpty();
    }

    public async IAsyncEnumerable<WorkspaceUser> GetWorkspaceUsersAsync(long workspaceId, int skip = 0)
    {
        await foreach (var associatedUser in _associationService.GetAssociationsToAsync(workspaceId, RecordType.User, RecordType.Workspace)
                                                                .Skip(skip))
        {
            var user = await _userService.TryGetUserAsync(associatedUser.FromRecordId);

            if (user == null || user.IsDeleted())
            {
                await DelinkUserAsync(workspaceId, associatedUser.FromRecordId);

                continue;
            }

            var userRole = await GetWorkspaceUserRoleAsync(workspaceId, user.UserId);

            yield return user.ToWorkspaceUser(userRole);
        }
    }

    public Task<List<long>> GetAllWorkspaceIdsAsync()
        => _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<long>(@"
SELECT    DISTINCT w.Id
FROM      Workspaces w
WHERE     w.DeletedOn IS NULL;
"));

    public IAsyncEnumerable<DynWorkspace> GetWorkspacesOwnedByAsync(long userId, WorkspaceType workspaceType = WorkspaceType.Unspecified)
    {
        var query = _dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(i => i.TypeOwnerSpace == DynItem.BuildTypeOwnerSpaceHash(DynItemType.Workspace, userId));

        if (workspaceType != WorkspaceType.Unspecified)
        {
            query = query.Filter(q => q.StatusId == workspaceType.ToString());
        }

        return GetIdModelsAsync(query.Filter(i => i.DeletedOnUtc == null)
                                     .QueryAsync(_dynamoDb)
                                     .Take(100));
    }

    public IAsyncEnumerable<DynWorkspace> GetUserWorkspacesAsync(long userId)
        => GetIdModelsAsync(_associationService.GetAssociationsAsync(RecordType.User, userId, RecordType.Workspace)?
                                .Select(wa => wa.ToRecordId));

    public long GetDefaultPublisherAppId(long workspaceId, PublisherType forPublisherType) => 0;

    public async Task<DynPublisherAccount> TryGetDefaultPublisherAccountAsync(long workspaceId)
    {
        if (workspaceId <= 0)
        {
            return null;
        }

        var dynWorkspace = await TryGetWorkspaceAsync(workspaceId);

        var dynPublisher = dynWorkspace != null && dynWorkspace.DefaultPublisherAccountId > 0
                               ? await _publisherAccountService.GetPublisherAccountAsync(dynWorkspace.DefaultPublisherAccountId)
                               : null;

        return dynPublisher;
    }

    public async Task<bool> UserHasAccessToWorkspaceAsync(long workspaceId, long byUserId)
    {
        var (hasAccess, _) = await UserHasAccessToWorkspaceAsWorkspaceUserIdAsync(workspaceId, byUserId);

        return hasAccess;
    }

    public async Task<bool> UserHasAccessToAccountAsync(long workspaceId, long byUserId, long toPublisherAccountId)
    {
        if (toPublisherAccountId <= 0)
        {
            return false;
        }

        if (workspaceId <= 0 || byUserId <= 0)
        {
            var state = _requestStateManager.GetState();

            workspaceId = workspaceId.Gz(state.WorkspaceId);
            byUserId = byUserId.Gz(state.UserId);

            if (workspaceId <= 0 || byUserId <= 0)
            {
                return false;
            }
        }

        // First ensure the publisher account is authorized in this workspace
        if (!(await _authorizeService.IsAuthorizedAsync(workspaceId, toPublisherAccountId)))
        {
            return false;
        }

        var (userHasAccessToWorkspace, workspaceUserId) = await UserHasAccessToWorkspaceAsWorkspaceUserIdAsync(workspaceId, byUserId);

        if (!userHasAccessToWorkspace)
        {
            return false;
        }

        if (byUserId == workspaceUserId)
        { // Have access and the workspaceUserId is the same as the user requesting, which means it's the workspace owner... always has access to everything
            return true;
        }

        // Get the users role, if an admin they have access
        var workspaceUserRole = await GetWorkspaceUserRoleAsync(workspaceId, byUserId);

        if (workspaceUserRole == WorkspaceRole.Admin)
        { // Admin of the workspace, has access...
            return true;
        }

        var workspacePublisherSubscriptionType = await _workspacePublisherSubscriptionService.GetPublisherSubscriptionTypeAsync(workspaceId, toPublisherAccountId);

        if (!workspacePublisherSubscriptionType.IsActiveSubscriptionType())
        {
            return false;
        }

        var workspaceUserPublisherAccountMap = await _mapItemService.TryGetMapAsync(workspaceUserId, DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, toPublisherAccountId.ToStringInvariant()));

        return workspaceUserPublisherAccountMap != null &&
               workspaceUserPublisherAccountMap.ReferenceNumber.Value == toPublisherAccountId &&
               workspaceUserPublisherAccountMap.MappedItemEdgeId.EqualsOrdinalCi(string.Concat(byUserId, "|", workspaceId));
    }

    public async Task<DynPublisherAccount> TryGetWorkspaceUserPublisherAccountAsync(long workspaceId, long userId, long publisherAccountId)
    {
        if (!(await UserHasAccessToAccountAsync(workspaceId, userId, publisherAccountId)))
        {
            return null;
        }

        // Get the publisher account
        var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

        return publisherAccount;
    }

    public async IAsyncEnumerable<DynPublisherAccount> GetWorkspaceUserPublisherAccountsAsync(long workspaceId, long userId, bool includeTokenAccounts = false)
    {
        var dynWorkspace = await TryGetWorkspaceAsync(workspaceId);

        if (dynWorkspace == null || dynWorkspace.IsDeleted())
        {
            yield break;
        }

        IAsyncEnumerable<DynPublisherAccount> getAllPublisherAccounts()
            => _publisherAccountService.GetPublisherAccountsAsync(_associationService.GetAssociatedIdsAsync(workspaceId, RecordType.PublisherAccount, RecordType.Workspace)
                                                                                     .Select(a => a.ToLong(0))
                                                                                     .Where(l => l > 0))
                                       .Where(p => includeTokenAccounts || !p.IsTokenAccount());

        // Shortcut for the owner...
        if (dynWorkspace.OwnerId == userId)
        { // Owner of the workspace, just get all publisher accounts linked to the workspace...
            await foreach (var publisher in getAllPublisherAccounts())
            {
                yield return publisher;
            }

            yield break;
        }

        var workspaceUserMap = await _mapItemService.TryGetMapAsync(userId, DynItemMap.BuildEdgeId(DynItemType.WorkspaceUser, workspaceId.ToStringInvariant()));

        var workspaceUserId = workspaceUserMap?.ReferenceNumber ?? 0;

        if (workspaceUserId <= 0)
        {
            yield break;
        }

        var workspaceUserRole = GetWorkspaceUserRoleFromMap(workspaceUserMap);

        if (workspaceUserRole == WorkspaceRole.Admin)
        { // Admin of the workspace, just get all publisher accounts linked to the workspace...
            await foreach (var publisher in getAllPublisherAccounts())
            {
                yield return publisher;
            }

            yield break;
        }

        // Return any publisher accounts linked to the workspace user
        var userWorkspaceString = string.Concat(userId, "|", workspaceId);

        async IAsyncEnumerable<long> getUserPublisherAccountIds()
        {
            await foreach (var refNumber in _dynamoDb.FromQuery<DynItemMap>(m => m.Id == workspaceUserId &&
                                                                                 Dynamo.BeginsWith(m.EdgeId, string.Concat((int)DynItemType.PublisherAccount, "|")))
                                                     .Filter(m => m.MappedItemEdgeId == userWorkspaceString)
                                                     .QueryColumnAsync(m => m.ReferenceNumber, _dynamoDb)
                                                     .Where(r => r.HasValue && r.Value > 0))
            {
                if (!(await _authorizeService.IsAuthorizedAsync(workspaceId, refNumber.Value)))
                {
                    continue;
                }

                yield return refNumber.Value;
            }
        }

        await foreach (var publisherAccount in _publisherAccountService.GetPublisherAccountsAsync(getUserPublisherAccountIds())
                                                                       .Where(p => includeTokenAccounts || !p.IsTokenAccount()))
        {
            yield return publisherAccount;
        }
    }

    public async Task LinkUserToPublisherAccountAsync(long workspaceId, long userId, long publisherAccountId)
    {
        var workspace = await GetWorkspaceAsync(workspaceId);

        if (userId == (workspace?.OwnerId ?? long.MinValue))
        {
            return;
        }

        var existingWorkspaceUserId = await GetWorkspaceUserIdAsync(workspaceId, userId);

        var isAuthorized = await _authorizeService.IsAuthorizedAsync(workspaceId, publisherAccountId);

        Guard.AgainstUnauthorized(workspace == null || workspace.IsDeleted() || existingWorkspaceUserId <= 0 || !isAuthorized,
                                  "The workspace, user, or publisher account specified does not exist in the workspace or you do not have access to it");

        // Use a map here instead of the association service because we cannot authenticate/lookup a workspaceUser by any normal method...(i.e. by id, etc.)
        await _mapItemService.PutMapAsync(new DynItemMap
                                          {
                                              Id = existingWorkspaceUserId,
                                              EdgeId = DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, publisherAccountId.ToStringInvariant()),
                                              ReferenceNumber = publisherAccountId,
                                              MappedItemEdgeId = string.Concat(userId, "|", workspaceId)
                                          });

        _deferRequestsService.DeferLowPriRequest(new WorkspaceUserPublisherAccountLinked
                                                 {
                                                     RydrUserId = userId,
                                                     WorkspaceUserId = existingWorkspaceUserId,
                                                     InWorkspaceId = workspaceId,
                                                     ToPublisherAccountId = publisherAccountId
                                                 });
    }

    public async Task DelinkUserFromPublisherAccountAsync(long workspaceId, long userId, long publisherAccountId)
    {
        var existingWorkspaceUserId = await GetWorkspaceUserIdAsync(workspaceId, userId);

        if (existingWorkspaceUserId <= 0)
        {
            return;
        }

        await _mapItemService.DeleteMapAsync(existingWorkspaceUserId, DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, publisherAccountId.ToStringInvariant()));

        _deferRequestsService.DeferLowPriRequest(new WorkspaceUserPublisherAccountDelinked
                                                 {
                                                     RydrUserId = userId,
                                                     WorkspaceUserId = existingWorkspaceUserId,
                                                     InWorkspaceId = workspaceId,
                                                     FromPublisherAccountId = publisherAccountId
                                                 });
    }

    public async Task LinkUserAsync(long workspaceId, long userId)
    {
        var workspace = await TryGetWorkspaceAsync(workspaceId);

        if (workspace == null)
        {
            return;
        }

        await LinkUserInternalAsync(workspaceId, userId, workspace.OwnerId == userId);
    }

    public async Task<WorkspaceRole> GetWorkspaceUserRoleAsync(long workspaceId, long userId)
    {
        var workspace = await GetWorkspaceAsync(workspaceId);

        if (workspace == null)
        {
            return WorkspaceRole.Unknown;
        }

        if (workspace.OwnerId == userId)
        {
            return WorkspaceRole.Admin;
        }

        var workspaceUserMap = await _mapItemService.TryGetMapAsync(userId, DynItemMap.BuildEdgeId(DynItemType.WorkspaceUser, workspace.Id.ToStringInvariant()));

        return GetWorkspaceUserRoleFromMap(workspaceUserMap);
    }

    public async Task SetWorkspaceUserRoleAsync(long workspaceId, long userId, WorkspaceRole role)
    {
        var workspaceUserMap = await _mapItemService.TryGetMapAsync(userId, DynItemMap.BuildEdgeId(DynItemType.WorkspaceUser, workspaceId.ToStringInvariant()));

        if (workspaceUserMap?.ReferenceNumber == null)
        {
            return;
        }

        var workspaceRole = role == WorkspaceRole.Unknown
                                ? WorkspaceRole.User
                                : role;

        workspaceUserMap.Items ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        workspaceUserMap.Items["WorkspaceRole"] = ((int)workspaceRole).ToStringInvariant();

        await _mapItemService.PutMapAsync(workspaceUserMap);

        FlushModel(workspaceId);

        _deferRequestsService.DeferLowPriRequest(new WorkspaceUserUpdated
                                                 {
                                                     RydrUserId = userId,
                                                     WorkspaceUserId = workspaceUserMap.ReferenceNumber.Value,
                                                     InWorkspaceId = workspaceId,
                                                     WorkspaceRole = workspaceRole
                                                 });
    }

    public async Task<long> GetWorkspaceUserIdAsync(long workspaceId, long userId)
    {
        var workspaceUserMap = await _mapItemService.TryGetMapAsync(userId, DynItemMap.BuildEdgeId(DynItemType.WorkspaceUser, workspaceId.ToStringInvariant()));

        return workspaceUserMap?.ReferenceNumber ?? 0;
    }

    public async Task DelinkUserAsync(long workspaceId, long userId)
    { // Cannot delink the owner
        var dynWorkspace = await TryGetWorkspaceAsync(workspaceId);

        Guard.AgainstUnauthorized(dynWorkspace == null || dynWorkspace.OwnerId == userId, "The owner of a workspace cannot be delinked.");

        // Get the shadow workspace user (if any) for the user in the workspace
        var existingWorkspaceUserId = await GetWorkspaceUserIdAsync(workspaceId, userId);

        if (existingWorkspaceUserId > 0)
        { // Remove all maps of this user to any workspace publisher accounts
            _dynamoDb.DeleteItems<DynItemMap>(_dynamoDb.FromQuery<DynItemMap>(m => m.Id == existingWorkspaceUserId &&
                                                                                   Dynamo.BeginsWith(m.EdgeId, string.Concat((int)DynItemType.PublisherAccount, "|")))
                                                       .Filter(m => m.MappedItemEdgeId == string.Concat(userId, "|", workspaceId))
                                                       .Exec()
                                                       .Select(m =>
                                                               {
                                                                   _mapItemService.OnMapUpdate(m.Id, m.EdgeId);

                                                                   return new DynamoId(m.Id, m.EdgeId);
                                                               }));
        }

        // Deauth, disassociate, remove any userWorkspace map
        _requestStateManager.UpdateStateToSystemRequest();

        await _authorizeService.DeAuthorizeAsync(userId, workspaceId);
        await _associationService.TryDeleteAssociationAsync(RecordType.User, userId, RecordType.Workspace, workspaceId);

        await _mapItemService.DeleteMapAsync(userId, DynItemMap.BuildEdgeId(DynItemType.WorkspaceUser, workspaceId.ToStringInvariant()));

        _deferRequestsService.DeferLowPriRequest(new WorkspaceUserDelinked
                                                 {
                                                     RydrUserId = userId,
                                                     WorkspaceUserId = existingWorkspaceUserId,
                                                     InWorkspaceId = workspaceId
                                                 });
    }

    public async Task DelinkTokenAccountAsync(DynWorkspace fromDynWorkspace, long tokenPublisherAccountId = 0)
    {
        if (tokenPublisherAccountId <= 0)
        {
            if (fromDynWorkspace.DefaultPublisherAccountId > 0)
            {
                tokenPublisherAccountId = fromDynWorkspace.DefaultPublisherAccountId;
            }
            else
            {
                return;
            }
        }

        var tokenPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(tokenPublisherAccountId);

        if (tokenPublisherAccount == null)
        {
            return;
        }

        if (fromDynWorkspace.DefaultPublisherAccountId == tokenPublisherAccount.PublisherAccountId &&
            !tokenPublisherAccount.IsSystemAccount())
        {
            fromDynWorkspace.LastNonSystemTokenPublisherAccountId = tokenPublisherAccount.PublisherAccountId;
        }

        // Deauthorize the workspace from the publisher
        await _authorizeService.DeAuthorizeAsync(fromDynWorkspace.Id, tokenPublisherAccount.PublisherAccountId);

        // Disassociate the workspace from the token account
        await _associationService.TryDeleteAssociationAsync(RecordType.Workspace, fromDynWorkspace.Id, RecordType.PublisherAccount, tokenPublisherAccount.PublisherAccountId);

        if (fromDynWorkspace.DefaultPublisherAccountId == tokenPublisherAccountId)
        {
            fromDynWorkspace.DefaultPublisherAccountId = 0;
        }

        await _dynamoDb.PutItemTrackDeferAsync(fromDynWorkspace, RecordType.Workspace);

        FlushModel(fromDynWorkspace.Id);
    }

    public async Task LinkTokenAccountAsync(DynWorkspace toDynWorkspace, DynPublisherAccount tokenPublisherAccount)
    {
        Guard.AgainstInvalidData(!tokenPublisherAccount.IsTokenAccount(), "Workspace linked accounts must be valid user/token account");

        await DelinkTokenAccountAsync(toDynWorkspace);

        toDynWorkspace.DefaultPublisherAccountId = tokenPublisherAccount.PublisherAccountId;

        // NOTE: Must put the workspace model before we authorize/associate...record won't be there to be associated otherwise...
        await _dynamoDb.PutItemTrackDeferAsync(toDynWorkspace, RecordType.Workspace);

        FlushModel(toDynWorkspace.Id);

        // Authorize the workspace to the publisher account
        await _authorizeService.AuthorizeAsync(toDynWorkspace.Id, tokenPublisherAccount.PublisherAccountId);

        // Associate the workspace to the token account
        await _associationService.AssociateAsync(RecordType.Workspace, toDynWorkspace.Id, RecordType.PublisherAccount, tokenPublisherAccount.PublisherAccountId);
    }

    public async Task<DynWorkspace> CreateAsync(Workspace workspace, DynPublisherAccount defaultTokenAccount = null)
    {
        Guard.AgainstArgumentOutOfRange(defaultTokenAccount != null && !defaultTokenAccount.IsTokenAccount(), "Cannot create workspace from non-token account");

        var dynWorkspace = workspace.ToDynWorkspace();

        try
        {
            if (defaultTokenAccount == null && dynWorkspace.WorkspaceType != WorkspaceType.Personal)
            { // Does this user own a personal workspace with a linked/default token publisher account? If so we implicitly create the workspace with that
                // token account as the root/default token account for the workspace being created as well
                var ownedPersonalAccount = await this.TryGetPersonalWorkspaceAsync(dynWorkspace.OwnerId, w => w.DefaultPublisherAccountId > 0);

                defaultTokenAccount = await _publisherAccountService.TryGetPublisherAccountAsync(ownedPersonalAccount?.DefaultPublisherAccountId ?? 0);
            }

            if (defaultTokenAccount != null)
            {
                dynWorkspace.CreatedViaPublisherId = defaultTokenAccount.AccountId;
                dynWorkspace.CreatedViaPublisherType = defaultTokenAccount.PublisherType;

                await LinkTokenAccountAsync(dynWorkspace, defaultTokenAccount);
            }
            else
            { // LinkTokenAccount will put the item...if we do not link, have to put here
                await _dynamoDb.PutItemTrackDeferAsync(dynWorkspace, RecordType.Workspace);
            }

            // Link the user to the workspace
            await LinkUserInternalAsync(dynWorkspace.Id, dynWorkspace.OwnerId, true);

            _deferRequestsService.DeferRequest(new WorkspacePosted
                                               {
                                                   Id = dynWorkspace.Id,
                                                   UserId = dynWorkspace.OwnerId,
                                                   WorkspaceId = dynWorkspace.Id,
                                                   RoleId = UserAuthInfo.AdminUserId,
                                                   RequestPublisherAccountId = defaultTokenAccount?.PublisherAccountId ?? dynWorkspace.DefaultPublisherAccountId
                                               });
        }
        catch
        {
            _deferRequestsService.DeferLowPriRequest(new DeleteWorkspaceInternal
                                                     {
                                                         Id = dynWorkspace.Id
                                                     }.WithAdminRequestInfo());

            throw;
        }

        return dynWorkspace;
    }

    public async Task<DynWorkspace> CreateAsync(long ownerId, string name, WorkspaceType type,
                                                PublisherType createdViaPublisherType = PublisherType.Unknown,
                                                string createdViaPublisherId = null, WorkspaceFeature features = WorkspaceFeature.Default)
    {
        var dynWorkspace = new Workspace
                           {
                               Name = name,
                               WorkspaceType = type,
                               WorkspaceFeatures = features
                           }.ToDynWorkspace(createdViaPublisherType: createdViaPublisherType,
                                            createdViaPublisherId: createdViaPublisherId);

        dynWorkspace.OwnerId = ownerId;
        dynWorkspace.CreatedBy = ownerId;

        try
        {
            await _dynamoDb.PutItemTrackDeferAsync(dynWorkspace, RecordType.Workspace);

            // Link the user to the workspace
            await LinkUserInternalAsync(dynWorkspace.Id, dynWorkspace.OwnerId, true);

            _deferRequestsService.DeferRequest(new WorkspacePosted
                                               {
                                                   Id = dynWorkspace.Id,
                                                   UserId = dynWorkspace.OwnerId,
                                                   WorkspaceId = dynWorkspace.Id,
                                                   RoleId = UserAuthInfo.AdminUserId
                                               }.WithAdminRequestInfo());
        }
        catch
        {
            _deferRequestsService.DeferLowPriRequest(new DeleteWorkspaceInternal
                                                     {
                                                         Id = dynWorkspace.Id
                                                     }.WithAdminRequestInfo());

            throw;
        }

        return dynWorkspace;
    }

    public IAsyncEnumerable<long> GetAssociatedWorkspaceIdsAsync(DynPublisherAccount forPublisherAccount)
    {
        if (forPublisherAccount == null)
        {
            return AsyncEnumerable.Empty<long>();
        }

        // Get workspaces this publisher account is associated up to
        return _associationService.GetAssociationsToAsync(forPublisherAccount.PublisherAccountId, RecordType.Workspace, RecordType.PublisherAccount)
                                  .Select(a => a.FromRecordId);
    }

    public async Task AssociateInviteCodeAsync(long workspaceId)
    {
        var workspace = await GetWorkspaceAsync(workspaceId);

#if !LOCALDEBUG
            if (!workspace.WorkspaceType.RequiresInviteCode())
            {
                return;
            }
#endif

        // If already have an invite code and valid map, all done
        if (workspace.InviteCode.HasValue())
        {
            var existingMap = await MapItemService.DefaultMapItemService
                                                  .TryGetMapByHashedEdgeAsync(DynItemType.InviteToken, workspace.InviteCode);

            if (existingMap != null && existingMap.ReferenceNumber == workspaceId)
            {
                return;
            }
        }

        IEnumerable<char> getInviteCodeChars(int length)
        {
            for (var x = 0; x < length; x++)
            {
                var position = RandomProvider.GetRandomIntBeween(0, InviteCodeCharacters.Count - 1);

                yield return InviteCodeCharacters[position];
            }
        }

        var attempts = 0;

        do
        {
            attempts++;

            var code = new string(getInviteCodeChars(6).ToArray());

            var longId = code.ToLongHashCode();

            var codeMap = new DynItemMap
                          {
                              Id = longId,
                              EdgeId = DynItemMap.BuildEdgeId(DynItemType.InviteToken, code),
                              ReferenceNumber = workspaceId
                          };

            var existingMap = await _dynamoDb.GetItemAsync<DynItemMap>(codeMap.Id, codeMap.EdgeId);

            if (existingMap != null)
            { // Already exists, try again
                continue;
            }

            // Put the map
            var replacedMap = await _dynamoDb.PutItemAsync(codeMap, true);

            if (replacedMap == null || (replacedMap.ReferenceNumber.Value == workspaceId && replacedMap.EdgeId.EqualsOrdinalCi(codeMap.EdgeId)))
            {
                workspace.InviteCode = code;

                await _dynamoDb.PutItemAsync(workspace);

                FlushModel(workspace.Id);

                await _serviceCacheInvalidator.InvalidateWorkspaceAsync(workspace.Id, "workspaces");

                return;
            }

            // Race condition, another beat us to it - put that one back and continue
            await _dynamoDb.PutItemAsync(replacedMap);
        } while (attempts <= 100);

        throw new OperationCannotBeCompletedException("Could not successfully generate invite code", false);
    }

    private async Task LinkUserInternalAsync(long workspaceId, long userId, bool isOwner)
    { // Have to update to a system request here as the logged in user might not have direct access to the workspace...
        _requestStateManager.UpdateStateToSystemRequest();

        // Authorize the user and associate
        await _authorizeService.AuthorizeAsync(userId, workspaceId);

        await _associationService.AssociateAsync(RecordType.User, userId, RecordType.Workspace, workspaceId);

        // Remove any invite map that might exists
        await _mapItemService.DeleteMapAsync(workspaceId, DynItemMap.BuildEdgeId(DynItemType.InviteRequest, userId.ToStringInvariant()));

        var workspaceUserId = userId;

        if (!isOwner)
        { // Create a shadow workspaceUser map for this user to the workspace (which we use for permissioning the user to workspace specific assets)
            var mapEdgeId = DynItemMap.BuildEdgeId(DynItemType.WorkspaceUser, workspaceId.ToStringInvariant());

            var workspaceUser = await _mapItemService.TryGetMapAsync(userId, mapEdgeId);

            if (workspaceUser == null)
            {
                workspaceUser = new DynItemMap
                                {
                                    Id = userId,
                                    EdgeId = mapEdgeId,
                                    ReferenceNumber = Sequences.Next()
                                };

                await _mapItemService.PutMapAsync(workspaceUser);
            }

            workspaceUserId = workspaceUser.ReferenceNumber.Value;
        }

        _deferRequestsService.DeferLowPriRequest(new WorkspaceUserLinked
                                                 {
                                                     RydrUserId = userId,
                                                     WorkspaceUserId = workspaceUserId,
                                                     InWorkspaceId = workspaceId
                                                 });
    }

    private async Task<(bool HasAccess, long WorkspaceUserId)> UserHasAccessToWorkspaceAsWorkspaceUserIdAsync(long workspaceId, long byUserId)
    {
        if (workspaceId <= 0 || byUserId <= 0)
        {
            var state = _requestStateManager.GetState();

            workspaceId = workspaceId.Gz(state.WorkspaceId);
            byUserId = byUserId.Gz(state.UserId);

            if (workspaceId <= 0 || byUserId <= 0)
            {
                return (false, 0);
            }
        }

        // Get the workspace, ensure it is valid
        var workspace = await GetWorkspaceAsync(workspaceId);

        if (workspace == null || workspace.IsDeleted())
        {
            return (false, 0);
        }

        // Owner of the workspace is an easy shortcut
        if (workspace.OwnerId == byUserId)
        {
            return (true, byUserId);
        }

        // If not the owner, other users are only allowed to anything in this workspace if the workspace allows multiple users
        // AND
        // is paying/subscribed at the workspace level and publisher levels
        if (!workspace.WorkspaceType.AllowsMultipleUsers())
        {
            return (false, 0);
        }

        // Workspace itself has to have a valid subscription state
        var workspaceSubscriptionType = await _workspaceSubscriptionService.TryGetActiveWorkspaceSubscriptionAsync(workspace.Id);

        if (!workspaceSubscriptionType.IsMultiUserSubscription())
        {
            return (false, 0);
        }

        // Get the shadow workspaceUserId for this user in this workspace, ensure it is valid, and verify if that workspaceUser is authorized to use the publisher account in question
        var workspaceUserId = await GetWorkspaceUserIdAsync(workspaceId, byUserId);

        return (workspaceUserId > 0, workspaceUserId);
    }

    private WorkspaceRole GetWorkspaceUserRoleFromMap(DynItemMap workspaceUserMap)
    {
        if (workspaceUserMap?.ReferenceNumber == null || workspaceUserMap.ReferenceNumber <= 0)
        {
            return WorkspaceRole.Unknown;
        }

        if (workspaceUserMap.Items.IsNullOrEmptyRydr() || !workspaceUserMap.Items.ContainsKey("WorkspaceRole"))
        {
            return WorkspaceRole.User;
        }

        var role = (WorkspaceRole)workspaceUserMap.Items["WorkspaceRole"].ToInt(0);

        return role == WorkspaceRole.Unknown
                   ? WorkspaceRole.User
                   : role;
    }
}
