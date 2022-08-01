using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Services.Auth
{
    public class TypeValidationAuthorizer : IAuthorizer
    {
        private readonly IDictionary<Type, IAuthorizer> _typeValidationMap;

        public TypeValidationAuthorizer(IDictionary<Type, IAuthorizer> typeValidationMap)
        {
            _typeValidationMap = typeValidationMap;
        }

        public bool CanExplicitlyAuthorize => false;
        public bool CanUnauthorize => true;

        public Task<AuthorizerResult> VerifyAccessToAsync<T>(T toObject, IHasUserAuthorizationInfo state)
            where T : ICanBeAuthorized
            => _typeValidationMap.ContainsKey(typeof(T))
                   ? _typeValidationMap[typeof(T)].VerifyAccessToAsync(toObject, state)
                   : AuthorizerResult.UnspecifiedAsync;
    }
}
