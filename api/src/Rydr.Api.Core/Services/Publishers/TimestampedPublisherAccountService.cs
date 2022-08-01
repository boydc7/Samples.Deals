using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Publishers
{
    public class TimestampedPublisherAccountService : TimestampDynItemIdCachedServiceBase<DynPublisherAccount>, IPublisherAccountService
    {
        private readonly IPocoDynamo _dynamoDb;
        private readonly IAssociationService _associationService;
        private readonly IDeferRequestsService _deferRequestsService;
        private readonly Func<ILocalRequestCacheClient> _localRequestCacheClientFactory;
        private readonly IMapItemService _mapItemService;
        private readonly IPublisherAccountConnectionDecorator _publisherAccountConnectionDecorator;
        private readonly IRydrDataService _rydrDataService;

        public TimestampedPublisherAccountService(ICacheClient cacheClient, IPocoDynamo dynamoDb,
                                                  IAssociationService associationService,
                                                  IDeferRequestsService deferRequestsService,
                                                  Func<ILocalRequestCacheClient> localRequestCacheClientFactory,
                                                  IMapItemService mapItemService,
                                                  IPublisherAccountConnectionDecorator publisherAccountConnectionDecorator,
                                                  IRydrDataService rydrDataService)
            : base(dynamoDb, cacheClient, 30)
        {
            _dynamoDb = dynamoDb;
            _associationService = associationService;
            _deferRequestsService = deferRequestsService;
            _localRequestCacheClientFactory = localRequestCacheClientFactory;
            _mapItemService = mapItemService;
            _publisherAccountConnectionDecorator = publisherAccountConnectionDecorator;
            _rydrDataService = rydrDataService;
        }

        public async Task<DynPublisherAccount> TryGetPublisherAccountByUserNameAsync(PublisherType publisherType, string userName)
        {
            if (userName.IsNullOrEmpty())
            {
                return null;
            }

            var alternatePublisherType = publisherType.IsWritablePublisherType()
                                             ? publisherType.NonWritableAlternateAccountType()
                                             : publisherType.WritableAlternateAccountType();

            var existingAccountIds = await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<Int64Id>(@"
SELECT  pa.Id AS Id
FROM    PublisherAccounts pa
WHERE   pa.UserName = @UserName
        AND pa.PublisherType IN (@PublisherType, @AlternatePublisherType)
        AND pa.AccountType = @AccountType
        AND pa.RydrAccountType IN (1,2)
        AND pa.DeletedOn IS NULL
LIMIT   1;
",
                                                                                                         new
                                                                                                         {
                                                                                                             UserName = userName.ToLowerInvariant(),
                                                                                                             PublisherType = publisherType,
                                                                                                             AlternatePublisherType = alternatePublisherType,
                                                                                                             AccountType = (int)PublisherAccountType.FbIgUser
                                                                                                         }));

            var existingAccountId = existingAccountIds?.FirstOrDefault()?.Id ?? 0;

            var existingPublisherAccount = await TryGetPublisherAccountAsync(existingAccountId);

            return existingPublisherAccount;
        }

        public async Task<DynPublisherAccount> TryGetPublisherAccountAsync(PublisherType publisherType, string publisherId)
        {
            if (publisherType == PublisherType.Unknown || publisherId.IsNullOrEmpty())
            {
                return null;
            }

            var publisherAccountId = await TryGetPublisherAccountIdFromMapAsync(publisherType, publisherId);

            if (publisherAccountId > 0)
            {
                return await TryGetPublisherAccountAsync(publisherAccountId);
            }

            var dynPublisherAccount = await _dynamoDb.GetItemByEdgeIntoAsync<DynPublisherAccount>(DynItemType.PublisherAccount,
                                                                                                  DynPublisherAccount.BuildEdgeId(publisherType, publisherId),
                                                                                                  true);

            if (dynPublisherAccount == null)
            {
                return null;
            }

            await SavePublisherAccountIdMapAsync(dynPublisherAccount);

            SetModel(dynPublisherAccount.PublisherAccountId, dynPublisherAccount);

            return dynPublisherAccount;
        }

        public async Task<DynPublisherAccount> TryGetPublisherAccountAsync(long publisherAccountId, bool retryDelayedOnNotFound = false)
        {
            if (publisherAccountId <= 0)
            {
                return null;
            }

            try
            {
                return await GetModelAsync(publisherAccountId,
                                           () => _dynamoDb.GetItemByRefAsync<DynPublisherAccount>(publisherAccountId, publisherAccountId.ToStringInvariant(),
                                                                                                  DynItemType.PublisherAccount, true, true));
            }
            catch(RecordNotFoundException) when(retryDelayedOnNotFound)
            {
                return await GetModelAsync(publisherAccountId,
                                           () => _dynamoDb.ExecDelayedAsync(d => d.GetItemByRefAsync<DynPublisherAccount>(publisherAccountId, publisherAccountId.ToStringInvariant(),
                                                                                                                          DynItemType.PublisherAccount, true, true)));
            }
        }

        public async Task<DynPublisherAccount> GetPublisherAccountAsync(long publisherAccountId, bool retryDelayedOnNotFound = false)
        {
            if (publisherAccountId <= 0)
            {
                return null;
            }

            try
            {
                return await GetModelAsync(publisherAccountId,
                                           () => _dynamoDb.GetItemByRefAsync<DynPublisherAccount>(publisherAccountId, DynItemType.PublisherAccount));
            }
            catch(RecordNotFoundException) when(retryDelayedOnNotFound)
            {
                return await GetModelAsync(publisherAccountId,
                                           () => _dynamoDb.ExecDelayedAsync(d => d.GetItemByRefAsync<DynPublisherAccount>(publisherAccountId, DynItemType.PublisherAccount)));
            }
        }

        public IEnumerable<DynPublisherAccount> GetPublisherAccounts(IEnumerable<long> publisherAccountIds)
            => GetMappedIdModels(publisherAccountIds, DynItemType.PublisherAccount);

        public IAsyncEnumerable<DynPublisherAccount> GetPublisherAccountsAsync(IEnumerable<long> publisherAccountIds)
            => GetMappedIdModelsAsync(publisherAccountIds, DynItemType.PublisherAccount);

        public IAsyncEnumerable<DynPublisherAccount> GetPublisherAccountsAsync(IAsyncEnumerable<long> publisherAccountIds)
            => GetMappedIdModelsAsync(publisherAccountIds, DynItemType.PublisherAccount);

        public async Task<bool> HardDeletePublisherAccountForReplacementOnlyAsync(long publisherAccountId)
        {
            var publisherAccount = await TryGetPublisherAccountAsync(publisherAccountId);

            if (publisherAccount == null)
            {
                return false;
            }

            var typeMapEdgeId = DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, publisherAccount.EdgeId);
            var typeMapEdgeIdHashCode = typeMapEdgeId.ToShaBase64().ToLongHashCode();

            await _dynamoDb.DeleteItemAsync<DynPublisherAccount>(publisherAccount.ToDynamoId());

            await MapItemService.DefaultMapItemService.DeleteMapAsync(publisherAccount.Id,
                                                                      DynItemMap.BuildEdgeId(publisherAccount.DynItemType,
                                                                                             publisherAccount.Id.ToEdgeId()));

            await _mapItemService.DeleteMapAsync(publisherAccount.Id,
                                                 DynItemMap.BuildEdgeId(publisherAccount.DynItemType,
                                                                        publisherAccount.Id.ToEdgeId()));

            await _mapItemService.DeleteMapAsync(typeMapEdgeIdHashCode, typeMapEdgeId);

            FlushModel(publisherAccountId);

            return true;
        }

        public async Task<DynPublisherAccount> UpdatePublisherAccountAsync<T>(T fromRequest)
            where T : RequestBase, IHaveModel<PublisherAccount>
        {
            // Not a try get here, as an update REQUIRES the account already exists
            var existingAccount = await GetPublisherAccountAsync(fromRequest.Model.Id);

            var currentRydrAccountType = existingAccount.RydrAccountType;

            var dynPublisherAccount = await _dynamoDb.UpdateFromExistingAsync(existingAccount, x => fromRequest.Model.ToDynPublisherAccount(x), fromRequest);

            OnPublisherUpdate(dynPublisherAccount, currentRydrAccountType);

            return dynPublisherAccount;
        }

        public async Task<DynPublisherAccount> UpdatePublisherAccountAsync(DynPublisherAccount toPublisherAccount,
                                                                           Action<DynPublisherAccount> updateAccountBlock)
        {
            var publisherAccount = await _dynamoDb.PutItemTrackedInterlockedDeferAsync(toPublisherAccount, updateAccountBlock, RecordType.PublisherAccount);

            OnPublisherUpdate(publisherAccount);

            return publisherAccount;
        }

        public async IAsyncEnumerable<DynPublisherAccount> GetLinkedPublisherAccountsAsync(long publisherAccountId)
        {
            var publisherAccount = await GetPublisherAccountAsync(publisherAccountId);

            // If this is a token/user account, get non-user accounts linked to this account.
            // If NOT a token/user account, get any token/user accounts linked from
            var query = publisherAccount.IsTokenAccount()
                            ? _associationService.GetAssociatedIdsAsync(publisherAccountId, RecordType.PublisherAccount, RecordType.PublisherAccount)
                                                 .Select(a => a.ToLong())
                                                 .Where(l => l > 0)
                            : _associationService.GetAssociationsToAsync(publisherAccountId, RecordType.PublisherAccount, RecordType.PublisherAccount)
                                                 .Select(a => a.FromRecordId)
                                                 .Where(l => l > 0);

            await foreach (var publisherAccountBatch in GetPublisherAccountsAsync(query))
            {
                yield return publisherAccountBatch;
            }
        }

        public async Task<DynPublisherAccount> ConnectPublisherAccountAsync(PublisherAccount publisherAccount, long workspaceId = 0)
        {
            var publisherAccountConnectInfo = new PublisherAccountConnectInfo
                                              {
                                                  ExistingPublisherAccount = await TryGetPublisherAccountAsync(publisherAccount.Id)
                                                                             ??
                                                                             await this.TryGetAnyExistingPublisherAccountAsync(publisherAccount.Type, publisherAccount.AccountId)
                                                                             ??
                                                                             await TryGetPublisherAccountByUserNameAsync(publisherAccount.Type, publisherAccount.UserName),
                                                  IncomingPublisherAccount = publisherAccount
                                              };

            if (publisherAccountConnectInfo.ExistingPublisherAccount != null &&
                publisherAccountConnectInfo.IncomingPublisherAccount.AccountType != PublisherAccountType.Unknown &&
                publisherAccountConnectInfo.IncomingPublisherAccount.RydrAccountType != RydrAccountType.None)
            {
                Guard.AgainstArgumentOutOfRange(publisherAccountConnectInfo.IncomingPublisherAccount.AccountType != publisherAccountConnectInfo.ExistingPublisherAccount.AccountType ||
                                                publisherAccountConnectInfo.IncomingPublisherAccount.RydrAccountType != publisherAccountConnectInfo.ExistingPublisherAccount.RydrAccountType,
                                                "When connecting an existing account it must be of the same AccountType and RydrAccountType requested");
            }

            var isNewAccount = publisherAccountConnectInfo.ExistingPublisherAccount == null;

            publisherAccountConnectInfo.NewPublisherAccount = publisherAccountConnectInfo.IncomingPublisherAccount
                                                                                         .ToDynPublisherAccount(publisherAccountConnectInfo.ExistingPublisherAccount);

#if LOCALDEBUG
            if (publisherAccountConnectInfo.ExistingPublisherAccount == null)
            {
                publisherAccountConnectInfo.NewPublisherAccount.Id = publisherAccountConnectInfo.NewPublisherAccount.AccountId switch
                {
                    // Chad Boyd personal
                    "574243964" => 120005,
                    "10156942417033965" => 120009,

                    // RydrJones user login
                    "142716683632243" => 120006,

                    // techrider999 fbIg
                    "17841411449838030" => 120007,

                    // boydc77 fbig
                    "17841411318206470" => 120008,
                    _ => publisherAccountConnectInfo.NewPublisherAccount.Id
                };

                publisherAccountConnectInfo.NewPublisherAccount.ReferenceId = publisherAccountConnectInfo.NewPublisherAccount.Id.ToStringInvariant();
            }
#endif

            if (publisherAccountConnectInfo.ExistingPublisherAccount != null && !publisherAccountConnectInfo.ExistingPublisherAccount.IsDeleted())
            {
                publisherAccountConnectInfo.NewPublisherAccount.PublisherType = publisherAccountConnectInfo.ExistingPublisherAccount.PublisherType;
            }

            await _publisherAccountConnectionDecorator.DecorateAsync(publisherAccountConnectInfo);

            var removedExistingSoftLinked = false;

            try
            {
                if (publisherAccountConnectInfo.ConvertExisting)
                { // Soft link conversion, or conversion of existing to something else, delete the existing so the edge is able to change (as it will)...
                    removedExistingSoftLinked = await HardDeletePublisherAccountForReplacementOnlyAsync(publisherAccountConnectInfo.ExistingPublisherAccount.PublisherAccountId);

                    // NOTE: this unfortunately cannot be deffered - timing with a background sync would be problematic, even if we added a soft-link flag
                    // to a deferred message...
                    foreach (var rydrSystemPublisherAccountKey in PublisherExtensions.RydrSystemPublisherAccountIds)
                    {
                        var rydrSystemPublisherAccount = await TryGetPublisherAccountAsync(publisherAccountConnectInfo.NewPublisherAccount.PublisherType,
                                                                                           rydrSystemPublisherAccountKey);

                        var rydrSystemPublisherAccountId = rydrSystemPublisherAccount?.PublisherAccountId ?? 0;

                        // If doing a soft-link conversion, need to add a soft-link association to any rydr system accounts that are currently linked to this
                        // account being converted, so we can continue to manage existing deals/etc. that are running for the biz
                        if (rydrSystemPublisherAccountId > 0 &&
                            await _associationService.IsAssociatedAsync(rydrSystemPublisherAccountId,
                                                                        publisherAccountConnectInfo.ExistingPublisherAccount.PublisherAccountId))
                        {
                            var map = new DynItemMap
                                      {
                                          Id = rydrSystemPublisherAccountId,
                                          EdgeId = publisherAccountConnectInfo.ExistingPublisherAccount
                                                                              .ToRydrSoftLinkedAssociationId()
                                      };

                            if (!(await _mapItemService.MapExistsAsync(map.Id, map.EdgeId)))
                            {
                                await _mapItemService.PutMapAsync(map);
                            }
                        }
                    }

                    _localRequestCacheClientFactory().FlushAll();
                }

                // Put the updated/new one
                if (workspaceId > 0 && publisherAccountConnectInfo.NewPublisherAccount.WorkspaceId <= GlobalItemIds.MinUserDefinedObjectId)
                {
                    publisherAccountConnectInfo.NewPublisherAccount.WorkspaceId = workspaceId;
                }

                await _dynamoDb.PutItemTrackDeferAsync(publisherAccountConnectInfo.NewPublisherAccount, RecordType.PublisherAccount);

                // Put the new access token, if needed
                await PutAccessTokenAsync(publisherAccountConnectInfo.NewPublisherAccount.PublisherAccountId,
                                          publisherAccountConnectInfo.IncomingPublisherAccount.AccessToken,
                                          publisherAccountConnectInfo.NewPublisherAccount.PublisherType,
                                          workspaceId);

                if (isNewAccount || removedExistingSoftLinked)
                {
                    await SavePublisherAccountIdMapAsync(publisherAccountConnectInfo.NewPublisherAccount);
                }

                FlushModel(publisherAccountConnectInfo.NewPublisherAccount.PublisherAccountId);

                return publisherAccountConnectInfo.NewPublisherAccount;
            }
            catch
            {
                if (removedExistingSoftLinked)
                {
                    await PutPublisherAccount(publisherAccountConnectInfo.ExistingPublisherAccount);
                }

                throw;
            }
        }

        public async Task PutAccessTokenAsync(long publisherAccountId, string accessToken, PublisherType publisherType, long workspaceId = 0)
        {
            if (accessToken.IsNullOrEmpty())
            { // If a non-token'd account, still need to add/update media sync jobs...so do that and be done.  Token'd accounts have this
                // occur later in the putAccessToken process below
                var publisherMediaSyncService = RydrEnvironment.Container.ResolveNamed<IPublisherMediaSyncService>(publisherType.ToString());

                await publisherMediaSyncService.AddOrUpdateMediaSyncAsync(publisherAccountId);

                return;
            }

            var publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(publisherType.ToString());

            await publisherDataService.PutAccessTokenAsync(publisherAccountId, accessToken, workspaceId: workspaceId);
        }

        public async Task PutPublisherAccount(DynPublisherAccount newPublisherAccount)
        {
            await _dynamoDb.PutItemAsync(newPublisherAccount);

            await SavePublisherAccountIdMapAsync(newPublisherAccount);

            OnPublisherUpdate(newPublisherAccount);
        }

        private void OnPublisherUpdate(DynPublisherAccount publisherAccount, RydrAccountType? previousRydrAccountType = null)
        {
            SetModel(publisherAccount.PublisherAccountId, publisherAccount);

            _deferRequestsService.DeferRequest(new PublisherAccountUpdated
                                               {
                                                   PublisherAccountId = publisherAccount.PublisherAccountId,
                                                   FromRydrAccountType = previousRydrAccountType ?? RydrAccountType.None,
                                                   ToRydrAccountType = previousRydrAccountType == null
                                                                           ? RydrAccountType.None
                                                                           : publisherAccount.RydrAccountType
                                               });
        }

        private async Task<long> TryGetPublisherAccountIdFromMapAsync(PublisherType publisherType, string publisherId)
        {
            var mapEdgeId = DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, DynPublisherAccount.BuildEdgeId(publisherType, publisherId));

            var mapLongId = mapEdgeId.ToShaBase64().ToLongHashCode();

            var itemMap = await MapItemService.DefaultMapItemService.TryGetMapAsync(mapLongId, mapEdgeId, true);

            return itemMap?.ReferenceNumber ?? 0;
        }

        private async Task SavePublisherAccountIdMapAsync(DynPublisherAccount dynPublisherAccount)
        {
            var mapEdgeId = DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, dynPublisherAccount.EdgeId);

            var mapLongId = mapEdgeId.ToShaBase64().ToLongHashCode();

            await MapItemService.DefaultMapItemService.PutMapAsync(new DynItemMap
                                                                   {
                                                                       Id = mapLongId,
                                                                       EdgeId = mapEdgeId,
                                                                       MappedItemEdgeId = dynPublisherAccount.EdgeId,
                                                                       ReferenceNumber = dynPublisherAccount.PublisherAccountId
                                                                   });
        }
    }
}
