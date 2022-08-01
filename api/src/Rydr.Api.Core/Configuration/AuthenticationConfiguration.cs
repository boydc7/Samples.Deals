using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Funq;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Auth;
using Rydr.Api.Core.Services.Publishers;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Configuration
{
    public class AuthenticationConfiguration : IAppHostConfigurer
    {
        // ReSharper disable once UnusedMember.Local
        private readonly string _dynamoTablePrefix = RydrEnvironment.GetAppSetting("AWS.Dynamo.TableNamePrefix", "dev_");

        public void Apply(ServiceStackHost appHost, Container container)
        {
            typeof(Seq).AddAttributes(new AliasAttribute(string.Concat(_dynamoTablePrefix?.Trim() ?? string.Empty, "Sequences")));

            appHost.Plugins.Add(new CorsFeature("*",
                                                CorsFeature.DefaultMethods,
                                                allowCredentials: true,
                                                allowedHeaders: "Content-Type, Accept, Accept-Encoding, Authorization, X-Rydr-PublisherAccountId, X-Rydr-WorkspaceId, X-Requested-With, X-ss-pid, X-ss-id, X-ss-tok")
                                {
                                    AutoHandleOptionsRequests = true
                                });

            var apiKeyProvider = new ApiKeyAuthProvider // ../auth/apikey
                                 {
                                     RequireSecureConnection = !RydrEnvironment.IsLocalEnvironment,
                                     Environments = new[]
                                                    {
                                                        "live"
                                                    },
                                     KeySizeBytes = 50,
                                     AllowInHttpParams = false,
                                     ServiceRoutes = new Dictionary<Type, string[]>(),
                                     ExpireKeysAfter = TimeSpan.FromDays(3650),
                                     SessionCacheDuration = null
                                 };

            if (RydrEnvironment.IsDebugEnabled)
            {
                apiKeyProvider.AllowInHttpParams = true;
            }

            appHost.Plugins.Add(new AuthFeature(() => new RydrUserSession(),
                                                new IAuthProvider[]
                                                {
                                                    apiKeyProvider
                                                })
                                {
                                    SaveUserNamesInLowerCase = true,
                                    ValidateUniqueUserNames = true,
                                    MaxLoginAttempts = 6,
                                    DeleteSessionCookiesOnLogout = true,
                                    GenerateNewSessionCookiesOnAuthentication = true,
                                    PermanentSessionExpiry = TimeSpan.FromMinutes(RydrEnvironment.GetAppSetting("Security.Session.PermanentExpiryMinutes", 43200)),
                                    SessionExpiry = TimeSpan.FromMinutes(RydrEnvironment.GetAppSetting("Security.Session.ExpiryMinutes", 720)),
                                    IncludeRegistrationService = false,
                                    IncludeAssignRoleServices = false,
                                    HtmlRedirect = null,
                                    HtmlLogoutRedirect = null,
                                    ValidUserNameRegEx = new Regex("^[A-Za-z0-9-.+_@]{6,50}$", RegexOptions.Compiled)
                                });

            container.Register<IRequestPreFlightState>(c => new RequestState())
                     .ReusedWithin(ReuseScope.Request);

            container.Register<IRequestStateManager>(c => new ContextRequestStateManager(c.LazyResolve<IRequestPreFlightState>()))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.RegisterAutoWiredAs<TimestampedUserService, IUserService>()
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IClientTokenAuthorizationService>(c => new FirebaseTokenAuthorizationService())
                     .ReusedWithin(ReuseScope.Hierarchy);
        }
    }
}
