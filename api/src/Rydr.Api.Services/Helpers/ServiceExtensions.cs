using System;
using Nest;
using Rydr.Api.QueryDto;

namespace Rydr.Api.Services.Helpers
{
    public static class ServiceExtensions
    {
        public static CreatorStat ToCreatorStat(this ExtendedStatsAggregate source, int scaledBy = 1)
        {
            scaledBy = scaledBy > 0
                           ? scaledBy
                           : 1;

            return source == null
                       ? null
                       : new CreatorStat
                         {
                             Min = source.Min.HasValue
                                       ? Math.Round(source.Min.Value / scaledBy, 2)
                                       : source.Min,
                             Max = source.Max.HasValue
                                       ? Math.Round(source.Max.Value / scaledBy, 2)
                                       : source.Max,
                             Avg = source.Average.HasValue
                                       ? Math.Round(source.Average.Value / scaledBy, 2)
                                       : source.Average,
                             Sum = Math.Round(source.Sum / scaledBy, 2),
                             StdDev = source.StdDeviation.HasValue
                                          ? Math.Round(source.StdDeviation.Value / scaledBy, 2)
                                          : source.StdDeviation
                         };
        }

        public static bool IsValidForRange(this CreatorStat source)
            => source?.Avg != null && source.StdDev.HasValue;
    }
}
