using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Auth
{
    public class CompositeAuthorizationService : IAuthorizationService
    {
        private readonly IRequestStateManager _requestStateManager;
        private readonly List<IAuthorizer> _authorizers;

        public CompositeAuthorizationService(IEnumerable<IAuthorizer> authorizers, IRequestStateManager requestStateManager)
        {
            _requestStateManager = requestStateManager;
            _authorizers = authorizers.AsList();
        }

        public async Task VerifyAccessToAsync<T>(T toObject, IHasUserAuthorizationInfo state = null)
            where T : ICanBeAuthorized
        {
            Guard.AgainstRecordNotFound(toObject == null);

            if (state == null || !(state is IRequestState requestState))
            {
                requestState = _requestStateManager.GetState();
            }

            var explicitlyAuthorized = requestState.IsSystemRequest || requestState.UserType == UserType.Admin;

            // If an admin/system level request, just exit out of here early...
            if (explicitlyAuthorized)
            {
                return;
            }

            var isReadOnlyIntent = requestState.Intent == AccessIntent.ReadOnly ||
                                   toObject.DefaultAccessIntent() == AccessIntent.ReadOnly ||
                                   requestState.HttpVerb.IsNullOrEmpty() ||
                                   requestState.HttpVerb.EqualsOrdinalCi("GET") ||
                                   requestState.HttpVerb.EqualsOrdinalCi("OPTIONS");

            Exception authException = null;
            var explicitRead = false;

            foreach (var authorizer in _authorizers)
            {
                if ((explicitlyAuthorized && !authorizer.CanUnauthorize) ||
                    (explicitRead && isReadOnlyIntent && !authorizer.CanUnauthorize) ||
                    (authException != null && !authorizer.CanExplicitlyAuthorize))
                { // If already explicitly authorized and this authorizer cannot de-authorize,
                    // OR
                    // already de-authorized and the authorizer cannot explicitly authorize
                    // THEN there is no point in bothering running this one
                    continue;
                }

                var result = await authorizer.VerifyAccessToAsync(toObject, requestState);

                switch (result.FailLevel)
                {
                    case AuthorizerFailLevel.Unauthorized:
                    {
                        throw (result.AuthException ??
                               authException ??
                               new UnauthorizedException("You do not have access to the resource requested. Code [unkgen]"));
                    }

                    case AuthorizerFailLevel.ExplicitlyAuthorized:
                    {
                        explicitlyAuthorized = true;

                        break;
                    }

                    case AuthorizerFailLevel.ExplicitRead:
                        explicitRead = true;

                        break;

                    case AuthorizerFailLevel.FailUnlessExplicitlyAuthorized:
                    {
                        authException = (result.AuthException ??
                                         authException ??
                                         new UnauthorizedException("You do not have access to the resource requested. Code [unkgen]"));

                        break;
                    }
                }
            }

            if (explicitRead && isReadOnlyIntent)
            {
                return;
            }

            if (!explicitlyAuthorized && authException != null)
            {
                throw authException;
            }
        }
    }
}
