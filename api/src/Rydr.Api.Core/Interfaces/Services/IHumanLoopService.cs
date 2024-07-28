using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Models.Supporting;
using ServiceStack;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IHumanLoopService
{
    IAsyncEnumerable<HumanLoopInfo> GetHumanLoopsAsync(string flowArn, DateTime? after = null);
    Task<HumanLoopResponse> GetHumanLoopResponseAsync(string humanLoopName);
    Task DeleteHumanLoopAsync(string humanLoopName);

    Task StartHumanLoopAsync<T>(string flowArn, string loopPrefix, string loopIdentifier, T inputObject)
        where T : class;
}

public static class HumanLoopService
{
    public const string PublisherAccountHumanResponseMapKey = "mypublisherhumanresponses";
    public const string PublisherAccountBusinessCategoryPrefix = "bizcat";
    public const string PublisherAccountCreatorCategoryPrefix = "infcat";

    public const string ImageModerationPrefix = "imgmod";
    public const string ImageModerationAnalysisSuffix = "moderations";
    public const string GenericInputOutputPrefix = "inout";

    public static readonly string HumanBusinessCategoryFlowArn = RydrEnvironment.IsLocalEnvironment
                                                                     ? null
                                                                     : RydrEnvironment.GetAppSetting("HumanFlow.BusinessCategoryArn");

    public static readonly string HumanCreatorCategoryFlowArn = RydrEnvironment.IsLocalEnvironment
                                                                    ? null
                                                                    : RydrEnvironment.GetAppSetting("HumanFlow.CreatorCategoryArn");

    public static (string Prefix, string Identifier) TryParseHumanLoopName(string loopName)
    {
        if (loopName.IsNullOrEmpty())
        {
            return (null, null);
        }

        var prefixEndIndex = loopName.IndexOf('-');

        if (prefixEndIndex < 0)
        {
            return (null, null);
        }

        var prefix = loopName.Substring(0, prefixEndIndex);

        var identifierEndIndex = loopName.IndexOf('-', prefixEndIndex + 1);

        if (identifierEndIndex < prefixEndIndex)
        {
            return (prefix, null);
        }

        var identifier = loopName.Substring(prefixEndIndex + 1, (identifierEndIndex - prefixEndIndex - 1));

        return (prefix, identifier);
    }
}
