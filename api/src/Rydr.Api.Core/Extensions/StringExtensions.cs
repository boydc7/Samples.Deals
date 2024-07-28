using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.FbSdk.Extensions;
using ServiceStack;

namespace Rydr.Api.Core.Extensions;

public static class StringExtensions
{
    private static readonly string[] _iso8601UtcDateFormats =
    {
        "yyyyMMdd", "yyyy-MM-dd"
    };

    private static readonly string[] _iso8601UtcFormats =
    {
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyyMMddTHHmmssZ",
        "yyyyMMddTHHmmZ",
        "yyyyMMddTHHmmsszzz",
        "yyyyMMddTHHmmsszz",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:sszz",
        "yyyyMMddTHHmmzzz",
        "yyyyMMddTHHmmzz",
        "yyyy-MM-ddTHH:mmzzz",
        "yyyy-MM-ddTHH:mmzz",
        "yyyy-MM-ddTHH:mmZ",
        "yyyyMMddTHHzzz",
        "yyyyMMddTHHzz",
        "yyyyMMddTHHZ",
        "yyyy-MM-ddTHHzzz",
        "yyyy-MM-ddTHHzz",
        "yyyy-MM-ddTHHZ",
        "yyyy-MM-ddTHH:mm:ss.fffffffZ",
        "yyyy-MM-ddTHH:mm:ss.ffffffZ",
        "yyyy-MM-ddTHH:mm:ss.fffffZ",
        "yyyy-MM-ddTHH:mm:ss.ffffZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ss.ffZ",
        "yyyy-MM-ddTHH:mm:ss.fZ"
    };

    public static bool ContainsAnyOf(this string source, params string[] tests)
        => ContainsAny(source, tests);

    public static bool ContainsAny(this string source, IEnumerable<string> tests)
        => tests != null && source.HasValue() &&
           tests.Any(test => source.IndexOf(test, StringComparison.OrdinalIgnoreCase) >= 0);

    public static string Coalesce(this string source, string valueIfNullOrEmpty)
        => source.HasValue()
               ? source
               : valueIfNullOrEmpty;

    public static string ToEdgeIdSuffix(this string edgeIdSource)
    {
        var delimitIndex = edgeIdSource?.IndexOf("|", StringComparison.OrdinalIgnoreCase) ?? -1;

        if (delimitIndex < 0)
        {
            return edgeIdSource;
        }

        var suffix = edgeIdSource.Substring(delimitIndex + 1);

        return suffix;
    }

    public static bool HasValue(this CompressedString source) => source != null && !string.IsNullOrEmpty(source.Value);

    public static bool HasValue(this string source) => !string.IsNullOrEmpty(source);

    public static DateTime ToDateTime(this string dateTimeString, DateTime? defaultValue = null, bool convertToUtcVsGuarding = false)
    {
        var dt = ToDateTimeNullable(dateTimeString, defaultValue, convertToUtcVsGuarding);

        return dt ?? DateTime.MinValue;
    }

    public static string Reverse(this string source)
    {
        var ca = source.ToCharArray();

        Array.Reverse(ca);

        return new string(ca);
    }

    public static DateTime? ToDateNullable(this string dateTimeString, DateTime? defaultValue = null)
    {
        var d = ToDateTimeNullable(dateTimeString, defaultValue);

        return d?.Date ?? d;
    }

    public static DateTime? ToDateTimeNullable(this string dateTimeString, DateTime? defaultValue = null, bool convertToUtcVsGuarding = false)
    {
        if (!dateTimeString.HasValue())
        {
            return defaultValue;
        }

        if (long.TryParse(dateTimeString, out var uts))
        { // Assume this is a unixtimestamp
            return uts.ToDateTime();
        }

        if (DateTime.TryParseExact(dateTimeString, _iso8601UtcDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var isoDateOnly))
        {
            return isoDateOnly.AsUtc();
        }

        if (DateTime.TryParseExact(dateTimeString, _iso8601UtcFormats, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var isoDt))
        {
            if (convertToUtcVsGuarding && isoDt.Kind != DateTimeKind.Utc)
            {
                isoDt = isoDt.ToUniversalTime();
            }

            Guard.AgainstInvalidData(isoDt.Kind != DateTimeKind.Utc, "Should be UTC and is not");

            return isoDt;
        }

        return DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                   ? dt
                   : defaultValue;
    }

    public static string ToEncoding(this string value, Encoding toEncoding)
    {
        try
        {
            return value.HasValue()
                       ? toEncoding.GetString(Encoding.Convert(Encoding.Unicode,
                                                               Encoding.GetEncoding(toEncoding.EncodingName,
                                                                                    new EncoderReplacementFallback(string.Empty),
                                                                                    new DecoderExceptionFallback()),
                                                               Encoding.Unicode.GetBytes(value)))
                       : value;
        }
        catch
        {
            return value;
        }
    }

    public static bool ToBoolean(this string value)
    {
        if (!value.HasValue())
        {
            return false;
        }

        if (bool.TryParse(value, out var boolVal))
        {
            return boolVal;
        }

        if (int.TryParse(value, out var intVal))
        {
            if (intVal != 0)
            {
                return true;
            }
        }

        return new[]
               {
                   "T", "Y", "yes", "true"
               }.Any(s => EqualsOrdinalCi(s, value));
    }

    public static string PrependIfNotStartsWith(this string source, string prepend)
    {
        if (!source.HasValue() || !prepend.HasValue() || source.StartsWithOrdinalCi(prepend))
        {
            return source;
        }

        return string.Concat(prepend, source);
    }

    public static string AppendIfNotEndsWith(this string source, string append)
    {
        if (!source.HasValue() || !append.HasValue() || source.EndsWithOrdinalCi(append))
        {
            return source;
        }

        return string.Concat(source, append);
    }

    public static int IndexOfAny(this string source, IEnumerable<string> needles, int startIndex, StringComparison comparer)
    {
        var firstPos = -1;

        if (source.IsNullOrEmpty())
        {
            return firstPos;
        }

        foreach (var needle in needles)
        {
            var pos = source.IndexOf(needle, startIndex, comparer);

            if (pos >= 0 && (firstPos < 0 || pos < firstPos))
            {
                firstPos = pos;
            }
        }

        return firstPos;
    }

    public static bool ContainsSafe(this string source, string that, StringComparison comp = StringComparison.OrdinalIgnoreCase)
        => source != null && that != null && source.IndexOf(that, comp) > -1;

    public static bool StartsWithOrdinalCi(this string source, string startsWith)
        => source != null && startsWith != null && source.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase);

    public static bool EndsWithOrdinalCi(this string source, string endsWith)
        => source != null && endsWith != null && source.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase);

    public static bool EqualsOrdinal(this string source, string that, bool matchOnBothNullEmpty = true)
    {
        var sourceEmpty = !source.HasValue();
        var thatEmpty = !that.HasValue();

        return sourceEmpty || thatEmpty
                   ? matchOnBothNullEmpty && sourceEmpty && thatEmpty
                   : source.Equals(that, StringComparison.Ordinal);
    }

    public static bool EqualsOrdinalCi(this string source, string that, bool matchIfBothEmpty = true)
    {
        var sourceEmpty = !source.HasValue();
        var thatEmpty = !that.HasValue();

        return sourceEmpty || thatEmpty
                   ? matchIfBothEmpty && sourceEmpty && thatEmpty
                   : source.Equals(that, StringComparison.OrdinalIgnoreCase);
    }

    public static T TryToEnum<T>(this string source, T defaultValue)
        where T : struct
    {
        if (!source.HasValue())
        {
            return defaultValue;
        }

        return Enum.TryParse(source, true, out T parsedEnum)
                   ? parsedEnum
                   : defaultValue;
    }

    public static T? TryToEnum<T>(this string source)
        where T : struct
    {
        if (!source.HasValue())
        {
            return null;
        }

        return Enum.TryParse(source, true, out T parsedEnum)
                   ? parsedEnum
                   : null;
    }

    public static T TryToEnum<T>(this string source, T defaultValue, params string[] coalesce)
        where T : struct
    {
        if (source.HasValue() && Enum.TryParse(source, true, out T parsedEnum))
        {
            return parsedEnum;
        }

        foreach (var val in coalesce)
        {
            if (val.HasValue() && Enum.TryParse(val, true, out parsedEnum))
            {
                return parsedEnum;
            }
        }

        return defaultValue;
    }

    public static byte[] CompressGzip(this string source, Encoding encoding)
    {
        var bytes = encoding.GetBytes(source);

        using(var compressed = new MemoryStream())
        using(var decompressed = new MemoryStream(bytes))
        {
            using(var compressor = new BufferedStream(new GZipStream(compressed, CompressionMode.Compress), bytes.Length + 1024))
            {
                decompressed.CopyTo(compressor);
            }

            return compressed.ToArray();
        }
    }

    public static string CompressGzip64(this string source, Encoding encoding)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        var byteArray = source.CompressGzip(encoding);

        return byteArray.ToBase64();
    }

    public static byte[] DecompressGzip(this byte[] bytes)
    {
        using(var decompressed = new MemoryStream())
        using(var compressed = new MemoryStream(bytes))
        {
            using(var decompressor = new GZipStream(compressed, CompressionMode.Decompress))
            {
                decompressor.CopyTo(decompressed);
            }

            return decompressed.ToArray();
        }
    }

    public static string DecompressGzip64(this string source, Encoding encoding)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        var bytes = source.FromBase64();
        var byteArray = bytes.DecompressGzip();

        return encoding.GetString(byteArray);
    }

    public static string ReplaceRepeatingWhitespace(this string input, string replaceWith = " ") => input.IsNullOrEmpty()
                                                                                                        ? input
                                                                                                        : Regex.Replace(input, @"\s{2,}", replaceWith);

    public static string ReplaceNewLines(this string input, string replaceWith = " ") => input.IsNullOrEmpty()
                                                                                             ? input
                                                                                             : Regex.Replace(input, @"\r\n?|\n", replaceWith);

    public static string ToRydrFormattedHtmlEmail(this string body, string from = null)
    {
        from = from.Coalesce(RydrEnvironment.GetAppSetting("MailServer.From", "noreply@getrydr.com"));

        var emailBody = $@"<!DOCTYPE html>
<html xmlns='http://www.w3.org/1999/xhtml'>
<head>
    <title>Rydr Email</title>
</head>
<body style='background: #f9f9f9;'>
    <div style='max-width:800px;margin:0 auto;padding:20px;font-family:Arial;font-size:12px;line-height:1.3em;'>
        <div style='background: white; border: solid 1px #ddd;'>
            <div style='padding:20px 20px 10px 20px;border-bottom:solid 1px #ddd;'>
                <a href='{RydrEnvironment.GetAppSetting("Environment.RydrLoginUrl")}' style='float:right;text-decoration:none; margin-top:-5px; display: inline-block; padding: 6px 12px; margin-bottom: 0; font-size: 14px; font-weight: normal; line-height: 1.42857143; text-align: center; white-space: nowrap; vertical-align: middle; -ms-touch-action: manipulation; touch-action: manipulation; cursor: pointer; -webkit-user-select: none; -moz-user-select: none; -ms-user-select: none; user-select: none; background-image: none; border: 1px solid transparent; border-radius: 4px; border: solid 1px #dadada; background: #f4f4f4; background: -moz-linear-gradient(top, #f4f4f4 0%, #f1f1f1 100%); background: -webkit-gradient(linear, left top, left bottom, color-stop(0%,#f4f4f4), color-stop(100%,#f1f1f1)); background: -webkit-linear-gradient(top, #f4f4f4 0%,#f1f1f1 100%); background: -o-linear-gradient(top, #f4f4f4 0%,#f1f1f1 100%); background: -ms-linear-gradient(top, #f4f4f4 0%,#f1f1f1 100%); background: linear-gradient(top, #f4f4f4 0%,#f1f1f1 100%); color: #555;'>Log in</a>
                <a href='{RydrEnvironment.GetAppSetting("Environment.RydrUrl")}'>
                    <img src='{RydrEnvironment.GetAppSetting("Environment.RydrLogoUrl")}' style='width:180px;' />
                </a>
            </div>
            <div style='padding: 20px; border-bottom: solid 1px #ddd;'>
                {body}
                <br />
                <br />
                Sincerely,
                <br />
                <br />
                <b>The folks at Rydr</b>
            </div>
            <div style='padding: 20px 20px 10px 20px; background: #f9f9f9;'>
                This message was intended for <a href='mailto: {from}'>{from}</a>.
                If you do not wish to receive this type of email from Rydr in the future, please <a href='{RydrEnvironment.GetAppSetting("Environment.RydrLoginUrl")}'>log in to your account</a> to manage your notification settings.
            </div>
        </div>
    </div>
</body>
</html>
";

        return emailBody;
    }
}
