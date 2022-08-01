using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services.Publishers
{
    public class InstagramPublisherDataService : BasePublisherDataService
    {
        private readonly string _defaultInstagramAppId;
        private DynPublisherApp _defaultInstagramApp;

        public InstagramPublisherDataService(IPocoDynamo dynamoDb,
                                             IAuthorizationService authorizationService,
                                             IEncryptionService encryptionService,
                                             IRequestStateManager requestStateManager,
                                             IPublisherAccountService publisherAccountService)
            : base(dynamoDb, authorizationService, encryptionService, requestStateManager, publisherAccountService)
        {
            // ReSharper disable once NotResolvedInText
            _defaultInstagramAppId = RydrEnvironment.GetAppSetting("Instagram.DefaultAppId") ?? throw new ArgumentNullException("Instagram.DefaultAppId");
        }

        public override PublisherType PublisherType => PublisherType.Instagram;

        public override async Task<DynPublisherApp> GetDefaultPublisherAppAsync()
            => _defaultInstagramApp ??= await _dynamoDb.GetItemByEdgeIntoAsync<DynPublisherApp>(DynItemType.PublisherApp, DynPublisherApp.BuildEdgeId(PublisherType.Instagram, _defaultInstagramAppId));

        protected override async Task<List<PublisherMedia>> DoGetRecentMediaAsync(DynPublisherAccount forAccount, DynPublisherAppAccount withAppAccount, int limit = 50)
        {
            if (withAppAccount == null || withAppAccount.IsDeleted())
            {
                return new List<PublisherMedia>();
            }

            var client = await withAppAccount.GetOrCreateIgBasicClientAsync();

            var recentMedias = await client.GetBasicIgAccountMediaAsync()
                                           .SelectManyToListAsync(b => b.Select(igm => igm.ToPublisherMedia(forAccount.PublisherAccountId)),
                                                                  limit);

            return recentMedias;
        }

        protected override async Task<bool> ValidateAndDecorateAppAccountAsync(DynPublisherAppAccount appAccount, string rawAccessToken = null)
        {
            var client = await appAccount.GetOrCreateIgBasicClientAsync(rawAccessToken);

            var igProfile = await client.GetMyAccountAsync(false);

            Guard.AgainstInvalidData((igProfile?.Id).IsNullOrEmpty() ||
                                     (igProfile?.UserName).IsNullOrEmpty(),
                                     $"BasicIg client token invalid - code [{appAccount.PublisherAccountId}|{appAccount.PublisherAppId}]");

            appAccount.ForUserId = igProfile.Id;

            await PublisherMediaSyncService.SyncUserDataAsync(new SyncPublisherAppAccountInfo(appAccount));

            return true;
        }
    }
}
