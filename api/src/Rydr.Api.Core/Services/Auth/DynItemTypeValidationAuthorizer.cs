using System;
using System.Collections.Generic;
using System.Linq;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Services.Auth
{
    public class DynItemTypeValidationAuthorizer<T> : BaseTypedValidationAuthorizer<T>
        where T : DynItem
    {
        private readonly string _validItemTypesString;
        private readonly HashSet<DynItemType> _validItemTypes;

        public DynItemTypeValidationAuthorizer(IEnumerable<DynItemType> validItemTypes,
                                               IRequestStateManager requestStateManager,
                                               IEnumerable<Func<T, IRequestState, AuthorizerResult>> validations = null)
            : base(requestStateManager, validations)
        {
            _validItemTypes = validItemTypes.AsHashSet();
            _validItemTypesString = string.Join("|", _validItemTypes.Select(t => (int)t));
        }

        public override bool CanExplicitlyAuthorize => false;

        protected override AuthorizerResult DoVerifyAccessTo(T toObject, IHasUserAuthorizationInfo state)
            => _validItemTypes.Contains(toObject.DynItemType)
                   ? AuthorizerResult.Unspecified
                   : AuthorizerResult.Unauthorized($"ItemType invalid state - code [{toObject.Id}.{toObject.EdgeId} |!=| {_validItemTypesString}] |=| [{toObject.TypeId}]");
    }
}
