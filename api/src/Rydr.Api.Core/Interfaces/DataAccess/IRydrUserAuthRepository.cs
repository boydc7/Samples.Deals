using Rydr.Api.Core.Models.Doc;
using ServiceStack;
using ServiceStack.Auth;

namespace Rydr.Api.Core.Interfaces.DataAccess;

public interface IRydrUserAuthRepository : IUserAuthRepository, IManageRoles, IClearable, IRequiresSchema, IManageApiKeys
{
    Task<DynUser> CreateUserAuthAsync(DynUser newUser);
    Task<DynUser> GetDynUserByUserNameAsync(string userName);
    Task<DynUser> GetDynUserAsync(long userId);
}
