using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;

namespace Rydr.Api.Core.Extensions
{
    public static class DeferedExtensions
    {
        public static void TryDeferDealRequest<T>(this IDeferRequestsService deferRequestsService, T request)
            where T : RequestBase
            => Try.Exec(() =>
                        {
                            deferRequestsService.DeferDealRequest(request);

                            return true;
                        },
                        maxAttempts: 4,
                        waitMultiplierMs: 6250);
    }
}
