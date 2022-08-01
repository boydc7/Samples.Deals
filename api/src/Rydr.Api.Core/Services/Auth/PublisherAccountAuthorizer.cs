using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Services.Auth
{
    public class PublisherAccountAuthorizer : IAuthorizer
    {
        public bool CanExplicitlyAuthorize => true;
        public bool CanUnauthorize => false;

        public Task<AuthorizerResult> VerifyAccessToAsync<T>(T toObject, IHasUserAuthorizationInfo state)
            where T : ICanBeAuthorized
        {
            if (state.RequestPublisherAccountId <= 0)
            {
                return AuthorizerResult.UnspecifiedAsync;
            }

            if (toObject.CreatedBy == state.RequestPublisherAccountId ||
                toObject.WorkspaceId == state.RequestPublisherAccountId ||
                toObject.OwnerId == state.RequestPublisherAccountId)
            {
                return AuthorizerResult.ExplicitlyAuthorizedAsync;
            }

            if (toObject is IHasPublisherAccountId publisherAccountIdObj)
            {
                if (state.RequestPublisherAccountId == publisherAccountIdObj.PublisherAccountId)
                {
                    return AuthorizerResult.ExplicitlyAuthorizedAsync;
                }
            }

            if (toObject is DynItem toDynItem)
            {
                if (toDynItem.Id == state.RequestPublisherAccountId ||
                    toDynItem.OwnerId == state.RequestPublisherAccountId ||
                    toDynItem.ReferenceId.ToLong() == state.RequestPublisherAccountId ||
                    toDynItem.EdgeId.ToLong() == state.RequestPublisherAccountId)
                {
                    return AuthorizerResult.ExplicitlyAuthorizedAsync;
                }
            }

            return AuthorizerResult.UnspecifiedAsync;
        }
    }
}
