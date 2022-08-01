using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Publishers;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services.Publishers
{
    public abstract class BasePublisherDataService : IPublisherDataService
    {
        protected readonly IPocoDynamo _dynamoDb;
        protected readonly IAuthorizationService _authorizationService;
        protected readonly IEncryptionService _encryptionService;
        private readonly IRequestStateManager _requestStateManager;
        private readonly IPublisherAccountService _publisherAccountService;

        private IPublisherMediaSyncService _publisherMediaSyncService;

        public BasePublisherDataService(IPocoDynamo dynamoDb,
                                        IAuthorizationService authorizationService,
                                        IEncryptionService encryptionService,
                                        IRequestStateManager requestStateManager,
                                        IPublisherAccountService publisherAccountService)
        {
            _dynamoDb = dynamoDb;
            _authorizationService = authorizationService;
            _encryptionService = encryptionService;
            _requestStateManager = requestStateManager;
            _publisherAccountService = publisherAccountService;
        }

        public abstract PublisherType PublisherType { get; }

        protected abstract Task<bool> ValidateAndDecorateAppAccountAsync(DynPublisherAppAccount appAccount, string rawAccessToken = null);
        protected abstract Task<List<PublisherMedia>> DoGetRecentMediaAsync(DynPublisherAccount forAccount, DynPublisherAppAccount withAppAccount, int limit = 50);

        protected IPublisherMediaSyncService PublisherMediaSyncService
            => _publisherMediaSyncService ??= RydrEnvironment.Container.ResolveNamed<IPublisherMediaSyncService>(PublisherType.ToString());

        public abstract Task<DynPublisherApp> GetDefaultPublisherAppAsync();

        public async Task<List<PublisherMedia>> GetRecentMediaAsync(long tokenPublisherAccountId, long publisherAccountId, long publisherAppId = 0, int limit = 50)
        {
            var dynPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId);

            var dynPublisherAppAccount = await _dynamoDb.GetPublisherAppAccountOrDefaultAsync(dynPublisherAccount.PublisherAccountId, publisherAppId,
                                                                                              tokenPublisherAccountId: tokenPublisherAccountId);

            var media = await DoGetRecentMediaAsync(dynPublisherAccount, dynPublisherAppAccount, limit);

            return media;
        }

        public async Task<DynPublisherApp> GetPublisherAppOrDefaultAsync(long publisherAppId, AccessIntent accessIntent = AccessIntent.Unspecified, long workspaceId = 0)
        {
            var state = workspaceId <= 0 || accessIntent != AccessIntent.Unspecified
                            ? _requestStateManager.GetState()
                            : null;

            var workspaceIdToUse = workspaceId.Gz(state?.WorkspaceId ?? 0);

            var workspacePublisherAppId = WorkspaceService.DefaultWorkspaceService.GetDefaultPublisherAppId(workspaceIdToUse, PublisherType);

            var publisherApp = publisherAppId > 0
                                   ? await _dynamoDb.GetItemByRefAsync<DynPublisherApp>(publisherAppId, DynItemType.PublisherApp)
                                   : workspacePublisherAppId > 0
                                       ? await _dynamoDb.GetItemByRefAsync<DynPublisherApp>(workspacePublisherAppId, DynItemType.PublisherApp)
                                       : await GetDefaultPublisherAppAsync();

            if (accessIntent != AccessIntent.Unspecified)
            {
                state.Intent = accessIntent;
            }

            await _authorizationService.VerifyAccessToAsync(publisherApp, a => a.PublisherType == PublisherType, state: state);

            if (publisherApp.DedicatedWorkspaceId > 0 && workspaceIdToUse != publisherApp.WorkspaceId && !state.IsSystemRequest)
            {
                throw new UnauthorizedException($"The PublisherApp requested does not exist or you do not have access to it - code [rpa-dwi{publisherApp.DedicatedWorkspaceId}-swi{state.WorkspaceId}]");
            }

            return publisherApp;
        }

        public async Task PutAccessTokenAsync(long publisherAccountId, string accessToken, int expiresIn = 0, long workspaceId = 0, long publisherAppId = 0)
        {
            Guard.AgainstNullArgument(accessToken.IsNullOrEmpty(), nameof(accessToken));

            // Store the token for use with this account/app combination
            var publisherApp = await GetPublisherAppOrDefaultAsync(publisherAppId, AccessIntent.ReadOnly, workspaceId);

            var existingPublisherAppAccount = await _dynamoDb.TryGetPublisherAppAccountAsync(publisherAccountId, publisherApp.Id);

            // Do not need to worry here about existing being replaced, we actually want that...
            var publisherAppAccount = new DynPublisherAppAccount
                                      {
                                          PublisherAppId = publisherApp.Id,
                                          PublisherAccountId = publisherAccountId,
                                          PubAccessToken = await _encryptionService.Encrypt64Async(accessToken),
                                          DynItemType = DynItemType.PublisherAppAccount,
                                          TokenLastUpdated = DateTimeHelper.UtcNowTs,
                                          IsShadowAppAccont = false, // Can't be a shadow account if it has a token
                                          IsSyncDisabled = false,
                                          FailuresSinceLastSuccess = 0,
                                          SyncStepsLastFailedOn = existingPublisherAppAccount?.SyncStepsLastFailedOn,
                                          ExpiresAt = expiresIn > 0
                                                          ? DateTimeHelper.UtcNowTs + expiresIn
                                                          : 0
                                      };

            Guard.AgainstInvalidData(!(await ValidateAndDecorateAppAccountAsync(publisherAppAccount, accessToken)), "The access-token specified is invalid");

            await _dynamoDb.PutItemTrackedAsync(publisherAppAccount, existingPublisherAppAccount);

            await AddOrUpdateMediaSyncAsync(publisherAccountId);
        }

        protected async Task AddOrUpdateMediaSyncAsync(long publisherAccountId)
        {
            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

            // NOTE: LEAVE the != null check here, will apply on first signup when the account does not exist yet in dynamo, but will if AccessToken PUT succeeds
            if (publisherAccount != null && (publisherAccount.IsDeleted() || publisherAccount.IsSyncDisabled))
            {
                publisherAccount.FailuresSinceLastSuccess = 0;
                publisherAccount.IsSyncDisabled = false;
                publisherAccount.DeletedBy = null;
                publisherAccount.DeletedOn = null;

                await _dynamoDb.PutItemTrackDeferAsync(publisherAccount, RecordType.PublisherAccount);
            }

            await PublisherMediaSyncService.AddOrUpdateMediaSyncAsync(publisherAccountId);
        }
    }
}
