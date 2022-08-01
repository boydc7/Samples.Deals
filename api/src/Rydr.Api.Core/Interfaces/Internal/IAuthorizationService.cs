using System.Threading.Tasks;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Interfaces.Internal
{
    public interface IAuthorizationService
    {
        Task VerifyAccessToAsync<T>(T toObject, IHasUserAuthorizationInfo state = null)
            where T : ICanBeAuthorized;
    }

    public interface IAuthorizeService
    {
        Task AuthorizeAsync(long fromRecordId, long toRecordId, string authType = null);
        Task DeAuthorizeAsync(long fromRecordId, long toRecordId, string authType = null);
        Task DeAuthorizeAllToFromAsync(long toFromRecordId, string forAuthType = null);
        Task<bool> IsAuthorizedAsync(long fromRecordId, long toRecordId, string authType = null);
    }

    public interface IAuthorizer
    {
        bool CanUnauthorize { get; }
        bool CanExplicitlyAuthorize { get; }

        Task<AuthorizerResult> VerifyAccessToAsync<T>(T toObject, IHasUserAuthorizationInfo state)
            where T : ICanBeAuthorized;
    }

    public interface IAuthorizer<in T> : IAuthorizer
        where T : class, ICanBeAuthorized
    {
        AuthorizerResult VerifyAccessTo(T toObject, IHasUserAuthorizationInfo state);
    }

    public interface IClientTokenAuthorizationService
    {
        Task<string> GetUidFromTokenAsync(string token);
        string GetTempClientToken(long userId);
    }
}
