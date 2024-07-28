using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.FbSdk;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Extensions;

public static class InstagramClientExtensions
{
    private static readonly IPocoDynamo _dynamoDb = RydrEnvironment.Container.Resolve<IPocoDynamo>();
    private static readonly IAuthorizationService _authorizationService = RydrEnvironment.Container.Resolve<IAuthorizationService>();
    private static readonly IEncryptionService _encryptionService = RydrEnvironment.Container.Resolve<IEncryptionService>();

    public static Task<IInstagramBasicClient> GetOrCreateIgBasicClientAsync(this DynPublisherAppAccount publisherAppAccount, string rawAccessToken = null)
        => GetOrCreateIgBasicClientAsync(new SyncPublisherAppAccountInfo(publisherAppAccount)
                                         {
                                             RawAccessToken = rawAccessToken.HasValue()
                                                                  ? rawAccessToken
                                                                  : null
                                         });

    public static async Task<IInstagramBasicClient> GetOrCreateIgBasicClientAsync(this SyncPublisherAppAccountInfo publisherAppAccountInfo)
    {
        var publisherApp = await _dynamoDb.GetPublisherAppAsync(publisherAppAccountInfo.PublisherAppId);

        await _authorizationService.VerifyAccessToAsync(publisherApp, a => a.PublisherType == PublisherType.Instagram);

        var result = await GetOrCreateIgBasicClientAsync(publisherApp, publisherAppAccountInfo.RawAccessToken.HasValue()
                                                                           ? publisherAppAccountInfo.RawAccessToken
                                                                           : await _encryptionService.Decrypt64Async(publisherAppAccountInfo.EncryptedAccessToken));

        return result;
    }

    public static async Task<IInstagramBasicClient> GetOrCreateIgBasicClientAsync(this DynPublisherApp publisherApp, string accessToken)
    {
        Guard.AgainstNullArgument(accessToken.IsNullOrEmpty(), nameof(accessToken));

        var decryptedSecret = await _encryptionService.Decrypt64Async(publisherApp.AppSecret);

        return InstagramBasicClient.Factory.GetOrCreateClient(publisherApp.AppId, decryptedSecret, accessToken);
    }
}
