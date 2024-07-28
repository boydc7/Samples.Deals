using Funq;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Services.Filters;
using ServiceStack;

namespace Rydr.Api.Services.Helpers;

public class RequestFilterConfiguration : IAppHostConfigurer
{
    public void Apply(ServiceStackHost appHost, Container container)
    { // Request filter registrations
        appHost.RegisterTypedRequestFilter(c => SkipTakeFilter.Instance);
        appHost.RegisterTypedRequestFilter(c => QuerySkipTakeFilter.Instance);
        appHost.RegisterTypedRequestFilter(c => DealUpsertFilter.Instance);
    }
}
