using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Models.Internal;
using ServiceStack;

namespace Rydr.Api.Core.Interfaces.Internal
{
    public interface IAdminServiceGateway : IServiceGateway, IServiceGatewayAsync { }

    public class InProcessAdminServiceGateway : InProcessServiceGateway, IAdminServiceGateway
    {
        public InProcessAdminServiceGateway()
            : base(new RydrBasicRequest
                   {
                       Authorization = string.Concat("Bearer ", RydrEnvironment.AdminKey)
                   })
        {
            Request.Headers.Add("Authorization", string.Concat("Bearer ", RydrEnvironment.AdminKey));

            Request.Items[Keywords.AuthSecret] = RydrEnvironment.AdminKey;

            Request.RequestAttributes |= RequestAttributes.RydrInternalRequest;
        }
    }
}
