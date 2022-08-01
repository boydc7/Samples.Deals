using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IUserService
    {
        DynUser TryGetUser(long userId, bool retryDelayedOnNotFound = false);
        Task<DynUser> TryGetUserAsync(long userId, bool retryDelayedOnNotFound = false);
        DynUser GetUser(long userId);
        Task<DynUser> GetUserAsync(long userId);
        IAsyncEnumerable<DynUser> GetUsersAsync(IEnumerable<DynamoItemIdEdge> userIdAndUserNames);
        DynUser GetUserByAuthUid(string authUid);
        Task<DynUser> GetUserByAuthUidAsync(string authUid);

        DynUser GetUserByUserName(string userName);
        Task<DynUser> GetUserByUserNameAsync(string userName);

        Task DeleteUserAsync(DynUser dynUser, IHasUserAuthorizationInfo withState = null,
                             bool hardDelete = false, string authUid = null);

        Task UpdateUserAsync(DynUser dynUser);
        Task LinkAuthUidToUserAsync(string authUid, string authToken, DynUser toUser);
    }
}
