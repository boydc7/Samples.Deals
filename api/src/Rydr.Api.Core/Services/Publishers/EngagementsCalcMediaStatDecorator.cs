using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.FbSdk.Enums;

namespace Rydr.Api.Core.Services.Publishers;

public class EngagementsCalcMediaStatDecorator : IPublisherMediaStatDecorator
{
    public async IAsyncEnumerable<DynPublisherMediaStat> DecorateAsync(IAsyncEnumerable<DynPublisherMediaStat> stats)
    {
        await foreach (var stat in stats)
        {
            if (stat.Stats.IsNullOrEmpty())
            {
                yield return stat;

                continue;
            }

            var engagmentStat = stat.Stats.FirstOrDefault(s => s.Name.EqualsOrdinalCi(FbIgInsights.EngagementsName));

            if (engagmentStat == null)
            {
                yield return stat;

                continue;
            }

            var saveStat = stat.Stats.FirstOrDefault(s => s.Name.EqualsOrdinalCi(FbIgInsights.SaveName));

            engagmentStat.Value += (saveStat?.Value).Gz(0);

            yield return stat;
        }
    }
}
