using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.FbSdk;
using Rydr.FbSdk.Models;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Extensions
{
    public static class FacebookClientExtensions
    {
        private static readonly IPocoDynamo _dynamoDb = RydrEnvironment.Container.Resolve<IPocoDynamo>();
        private static readonly IAuthorizationService _authorizationService = RydrEnvironment.Container.Resolve<IAuthorizationService>();
        private static readonly IEncryptionService _encryptionService = RydrEnvironment.Container.Resolve<IEncryptionService>();

        public static Task<IFacebookClient> GetOrCreateFbClientAsync(this DynPublisherAppAccount publisherAppAccount, string rawAccessToken = null)
            => GetOrCreateFbClientAsync(new SyncPublisherAppAccountInfo(publisherAppAccount)
                                        {
                                            RawAccessToken = rawAccessToken.HasValue()
                                                                 ? rawAccessToken
                                                                 : null
                                        });

        public static async Task<IFacebookClient> GetOrCreateFbClientAsync(this SyncPublisherAppAccountInfo publisherAppAccountInfo)
        {
            var publisherApp = await _dynamoDb.GetPublisherAppAsync(publisherAppAccountInfo.PublisherAppId);

            await _authorizationService.VerifyAccessToAsync(publisherApp, a => a.PublisherType == PublisherType.Facebook);

            var result = await GetOrCreateFbClientAsync(publisherApp, publisherAppAccountInfo.RawAccessToken.HasValue()
                                                                          ? publisherAppAccountInfo.RawAccessToken
                                                                          : await _encryptionService.Decrypt64Async(publisherAppAccountInfo.EncryptedAccessToken));

            return result;
        }

        public static async Task<IFacebookClient> GetOrCreateFbClientAsync(this DynPublisherApp publisherApp, string accessToken)
        {
            Guard.AgainstNullArgument(accessToken.IsNullOrEmpty(), nameof(accessToken));

            var decryptedSecret = await _encryptionService.Decrypt64Async(publisherApp.AppSecret);

            return FacebookClient.Factory.GetOrCreateClient(publisherApp.AppId, decryptedSecret, accessToken,
                                                            publisherApp.ApiVersion.Coalesce(FacebookClient.ApiVersion));
        }

        public static bool IsExpired(this FbDebugToken source)
            => source?.Data == null || (source.Data.ExpiresAt > 0 && source.Data.ExpiresAt <= DateTimeHelper.UtcNowTs);
    }
}
