using System.Runtime.CompilerServices;
using System.Text;
using Nest;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Search;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Extensions;

public static class SearchExtensions
{
    private static readonly ILog _log = LogManager.GetLogger("SearchExtensions");

    public static bool SuccessfulOnly(this IResponse response)
        => response != null && response.IsValid && response.OriginalException == null && response.ServerError == null;

    public static bool SuccessfulOnly(this BulkResponse bulkResponse)
        => bulkResponse == null || (!bulkResponse.Errors && SuccessfulOnly((IResponse)bulkResponse));

    public static EsCreatorSearch ToEsCreatorSearch<T>(this T source)
        where T : BaseCreatorSearch
    {
        var esCreatorSearch = source.ConvertTo<EsCreatorSearch>();

        esCreatorSearch.Search = source.Query;
        esCreatorSearch.Tags = source.Tags.NullIfEmpty();
        esCreatorSearch.FollowedByRange = source.FollowerRange;

        return esCreatorSearch;
    }

    public static EsBusinessSearch ToEsBusinessSearch(this GetBusinessesSearch source)
    {
        var esBusinessSearch = source.ConvertTo<EsBusinessSearch>();

        esBusinessSearch.Search = source.Query;
        esBusinessSearch.Tags = source.Tags.NullIfEmpty();

        return esBusinessSearch;
    }

    public static string DelimitAll(this string source, char[] charsToDelimit, char delimitWith)
    {
        if (!source.HasValue())
        {
            return null;
        }

        return source.IndexOfAny(charsToDelimit) >= 0
                   ? new string(source.SelectMany(c => DelimitIfIn(c, delimitWith, charsToDelimit)).ToArray())
                   : source;
    }

    private static IEnumerable<char> DelimitIfIn(char source, char delimiter, ICollection<char> charsToDelimit)
    {
        if (charsToDelimit.Contains(source))
        {
            yield return delimiter;
        }

        yield return source;
    }

    public static string StripChars(this string source, params char[] ignoreChars)
        => source == null
               ? null
               : new string(source.Where(c => !ignoreChars.Contains(c)).ToArray());

    public static bool Successful(this BulkResponse bulkResponse, [CallerMemberName] string methodName = null)
    {
        if (SuccessfulOnly(bulkResponse))
        {
            return true;
        }

        LogFailure(bulkResponse, methodName);
        bulkResponse.ItemsWithErrors.Take(20).Each(i => _log.ErrorFormat("   BulkResponse Search Item Error [{0}]", i.Error.Reason));

        return false;
    }

    public static bool Successful(this IResponse response, [CallerMemberName] string methodName = null)
    {
        if (SuccessfulOnly(response))
        {
            return true;
        }

        LogFailure(response, methodName);

        return false;
    }

    public static Exception ToException(this BulkResponse response, [CallerMemberName] string methodName = null)
    {
        var itemErrors = response?.ItemsWithErrors?.Take(20).Where(i => i.Error?.Reason.HasValue() ?? false).ToList();

        return itemErrors != null && itemErrors.Count > 0
                   ? new AggregateException(itemErrors.Select(i => new Exception($"BulkItem Search Exception from method [{methodName}] - [{i.Error.Reason}]")))
                   : ToException((IResponse)response, methodName);
    }

    public static Exception ToException(this IResponse response, [CallerMemberName] string methodName = null) => response?.OriginalException ?? new Exception(ToFailureString(response, methodName));

    private static void LogFailure(this IResponse response, string methodName)
    {
        _log.Exception(response.OriginalException, ToFailureString(response, methodName));
    }

    private static string ToFailureString(IResponse response, string methodName) => string.Concat("Search Exception from method [", methodName, "]. IsValid [", response?.IsValid, "], ServerErrror [", response?.ServerError, "], ",
                                                                                                  "Reason [", response?.ServerError?.Error?.Reason, "], DebugInfo [", response?.DebugInformation?.Left(500), "], Request [",
                                                                                                  response?.ApiCall?.RequestBodyInBytes == null
                                                                                                      ? string.Empty
                                                                                                      : Encoding.UTF8.GetString(response?.ApiCall?.RequestBodyInBytes).Left(500), "]");
}
