using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Helpers;
using ServiceStack.Auth;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Auth;

public class FirebaseTokenAuthorizationService : IClientTokenAuthorizationService
{
    private readonly ILog _log = LogManager.GetLogger("FirebasePushServerNotificationService");

    public async Task<string> GetUidFromTokenAsync(string token)
    {
        try
        {
            var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);

            return decodedToken.Uid;
        }
        catch(FirebaseAuthException fbax)
        {
            _log.DebugInfo($"Invalid Firebase AuthToken, ignoring (returning null) - Exception: [{fbax.Message}]");
        }
        catch(FirebaseException fx)
        {
            _log.Exception(fx, wasHandled: true);
        }

        return null;
    }

    public string GetTempClientToken(long userId)
    {
        var expiresAt = DateTimeHelper.UtcNow.AddHours(1);

        var apiKeyToken = ((ApiKeyAuthProvider)AuthenticateService.GetAuthProvider(ApiKeyAuthProvider.Name)).GenerateNewApiKeys(Guid.NewGuid().ToString())
                                                                                                            .First(k => k.Id.HasValue())
                                                                                                            .Id;

        var tempApiKey = new ApiKey
                         {
                             Id = apiKeyToken,
                             UserAuthId = userId.ToStringInvariant(),
                             ExpiryDate = expiresAt
                         };

        // Cannot inject this, circular dependency
        var authRepo = RydrEnvironment.Container.Resolve<IRydrUserAuthRepository>();

        authRepo.StoreAll(tempApiKey.AsEnumerable());

        return tempApiKey.Id;
    }
}
