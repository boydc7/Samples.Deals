using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Auth
{
    public abstract class BaseTypedValidationAuthorizer<T> : IAuthorizer<T>
        where T : class, ICanBeAuthorized
    {
        private readonly IRequestStateManager _requestStateManager;
        protected readonly List<Func<T, IRequestState, AuthorizerResult>> _validations;

        protected BaseTypedValidationAuthorizer(IRequestStateManager requestStateManager,
                                                IEnumerable<Func<T, IRequestState, AuthorizerResult>> validations = null)
        {
            _requestStateManager = requestStateManager;
            _validations = validations.AsList();
        }

        protected abstract AuthorizerResult DoVerifyAccessTo(T toObject, IHasUserAuthorizationInfo state);

        public virtual bool CanExplicitlyAuthorize => true;
        public virtual bool CanUnauthorize => true;

        public AuthorizerResult VerifyAccessTo(T toObject, IHasUserAuthorizationInfo state)
        {
            var stateToUse = state ?? _requestStateManager.GetState();

            var verifyResult = DoVerifyAccessTo(toObject, stateToUse);

            if (_validations.IsNullOrEmpty() || verifyResult.FailLevel == AuthorizerFailLevel.Unauthorized)
            {
                return verifyResult;
            }

            var requestState = ((stateToUse as IRequestState) ?? _requestStateManager.GetState());

            var validationResultToReturn = verifyResult;

            foreach (var validation in _validations)
            {
                var validationResult = validation(toObject, requestState);

                switch (validationResult.FailLevel)
                {
                    case AuthorizerFailLevel.Unauthorized:
                        return validationResult;

                    case AuthorizerFailLevel.ExplicitlyAuthorized when validationResultToReturn.FailLevel == AuthorizerFailLevel.FailUnlessExplicitlyAuthorized ||
                                                                       validationResultToReturn.FailLevel == AuthorizerFailLevel.Unspecified:
                        validationResultToReturn = validationResult;

                        break;

                    case AuthorizerFailLevel.FailUnlessExplicitlyAuthorized when validationResultToReturn.FailLevel == AuthorizerFailLevel.Unspecified:
                        validationResultToReturn = validationResult;

                        break;
                }
            }

            return validationResultToReturn ?? AuthorizerResult.Unspecified;
        }

        public Task<AuthorizerResult> VerifyAccessToAsync<TItem>(TItem toObject, IHasUserAuthorizationInfo state)
            where TItem : ICanBeAuthorized
        {
            if (!(toObject is T tt))
            {
                throw new InvalidDataArgumentException($"Typed validation authorization service can only be used for specific type - code [{typeof(TItem).Name}|{typeof(T).Name}]");
            }

            return Task.FromResult(VerifyAccessTo(tt, state));
        }
    }
}
