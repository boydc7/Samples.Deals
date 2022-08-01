using Rydr.Api.Core.Configuration;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Host;
using ServiceStack.Web;

namespace Rydr.Api.Services.Filters
{
    public class SkipTakeFilter : ITypedFilter<IHasSkipTake>
    {
        private static readonly int _defaultTake = RydrEnvironment.GetAppSetting("Query.DefaultLimit", 100);

        private SkipTakeFilter() { }

        public static SkipTakeFilter Instance { get; } = new SkipTakeFilter();

        public void Invoke(IRequest req, IResponse res, IHasSkipTake dto)
        {
            if (dto.Skip < 0 || dto.Skip > 10000)
            {
                dto.Skip = 0;
            }

            if (dto.Take <= 0 || dto.Take > 10000)
            {
                dto.Take = _defaultTake;
            }
        }
    }

    public class QuerySkipTakeFilter : ITypedFilter<IQuery>
    {
        private static readonly int _defaultTake = RydrEnvironment.GetAppSetting("Query.DefaultLimit", 100);

        private QuerySkipTakeFilter() { }

        public static QuerySkipTakeFilter Instance { get; } = new QuerySkipTakeFilter();

        public void Invoke(IRequest req, IResponse res, IQuery dto)
        {
            if (!dto.Skip.HasValue || dto.Skip.Value < 0 || dto.Skip.Value > 7500)
            {
                dto.Skip = 0;
            }

            if (!dto.Take.HasValue || dto.Take.Value < 0 || dto.Take.Value > 10000)
            {
                dto.Take = _defaultTake;
            }
        }
    }
}
