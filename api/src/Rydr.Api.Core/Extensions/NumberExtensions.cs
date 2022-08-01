using System;
using System.Globalization;
using Rydr.Api.Core.Services.Internal;

namespace Rydr.Api.Core.Extensions
{
    public static class NumberExtensions
    {
        public static bool HasValueGz(this int? source)
            => source.HasValue && source.Value > 0;

        public static bool HasValueGz(this long? source)
            => source.HasValue && source.Value > 0;

        public static decimal? NullIf(this decimal? source, decimal nullIf)
            => NullIf(source, d => d == nullIf);

        public static decimal? NullIf(this decimal? source, Func<decimal, bool> nullIf)
            => source.HasValue
                   ? nullIf(source.Value)
                         ? null
                         : source
                   : null;

        public static int? NullIfNotPositive(this int source)
            => NullIf(source, l => l <= 0);

        public static int? NullIfNotPositive(this int? source)
            => NullIf(source, l => l <= 0);

        public static int? NullIf(this int source, Func<int, bool> nullIf)
            => nullIf(source)
                   ? (int?)null
                   : source;

        public static int? NullIf(this int? source, Func<int, bool> nullIf)
            => source.HasValue
                   ? nullIf(source.Value)
                         ? null
                         : source
                   : null;

        public static long? NullIf(this long? source, long nullIf)
            => NullIf(source, d => d == nullIf);

        public static long? NullIf(this long? source, Func<long, bool> nullIf)
            => source.HasValue
                   ? nullIf(source.Value)
                         ? null
                         : source
                   : null;

        public static long? NullIfNotPositive(this long source)
            => NullIf(source, l => l <= 0);

        public static long? NullIfNotPositive(this long? source)
            => NullIf(source, l => l <= 0);

        public static long? NullIf(this long source, Func<long, bool> nullIf)
            => nullIf(source)
                   ? (long?)null
                   : source;

        public static double? NullIf(this double source, Func<double, bool> nullIf)
            => nullIf(source)
                   ? (double?)null
                   : source;

        public static string ToStringInvariant(this int source) => source.ToString(CultureInfo.InvariantCulture);

        public static string ToStringInvariant(this long source) => source.ToString(CultureInfo.InvariantCulture);

        public static string ToStringInvariant(this decimal source) => source.ToString(CultureInfo.InvariantCulture);

        public static int Nn(this int? source, int valueIfNegative)
            => source.HasValue && source.Value >= 0
                   ? source.Value
                   : valueIfNegative;

        public static int Nn(this int source, int valueIfNegative)
            => source >= 0
                   ? source
                   : valueIfNegative;

        public static long Nn(this long? source, long valueIfNegative)
            => source.HasValue && source.Value >= 0
                   ? source.Value
                   : valueIfNegative;

        public static long Nn(this long source, long valueIfNegative)
            => source >= 0
                   ? source
                   : valueIfNegative;

        public static decimal Nn(this decimal? source, decimal valueIfNegative)
            => source.HasValue && source.Value >= 0
                   ? source.Value
                   : valueIfNegative;

        public static decimal Nn(this decimal source, decimal valueIfNegative)
            => source >= 0
                   ? source
                   : valueIfNegative;

        public static int Nz(this int? source, int valueIfZero)
            => source.HasValue && source.Value != 0
                   ? source.Value
                   : valueIfZero;

        public static int Nz(this int source, int valueIfZero)
            => source != 0
                   ? source
                   : valueIfZero;

        public static long Nz(this long? source, long valueIfZero) => source.HasValue && source.Value != 0
                                                                          ? source.Value
                                                                          : valueIfZero;

        public static long Nz(this long source, long valueIfZero) => source != 0
                                                                         ? source
                                                                         : valueIfZero;

        public static T DefaultIf<T>(this T value, Func<T, bool> predicate)
            => predicate(value)
                   ? default
                   : value;

        public static int Gz(this int? first, int second) => first.HasValue
                                                                 ? Gz(first.Value, second)
                                                                 : second;

        public static int Gz(this int first, int second) => first > 0
                                                                ? first
                                                                : second;

        public static long Gz(this long? first, long second) => first.HasValue && first.Value > 0
                                                                    ? first.Value
                                                                    : second;

        public static long Gz(this long? first, long? second) => first.HasValue && first.Value > 0
                                                                     ? first.Value
                                                                     : second.GetValueOrDefault();

        public static decimal Gz(this decimal? first, decimal second) => first.HasValue
                                                                             ? Gz(first.Value, second)
                                                                             : second;

        public static decimal Gz(this decimal first, decimal second) => first > 0
                                                                            ? first
                                                                            : second;

        public static double MinGz(this double first, double second)
            => first > 0 && second > 0
                   ? first < second
                         ? first
                         : second
                   : first > 0
                       ? first
                       : second;

        public static double MaxGz(this double first, double second)
            => first > 0 && second > 0
                   ? first > second
                         ? first
                         : second
                   : first > 0
                       ? first
                       : second;

        public static long MinGz(this int first, int second)
            => MinGz(first, (long)second);

        public static long MinGz(this long first, long second)
            => first > 0 && second > 0
                   ? first < second
                         ? first
                         : second
                   : first > 0
                       ? first
                       : second;

        public static long MaxGz(this long first, long second)
            => first > 0 && second > 0
                   ? first > second
                         ? first
                         : second
                   : first > 0
                       ? first
                       : second;

        public static int MaxGz(this int first, int second)
            => first > 0 && second > 0
                   ? first > second
                         ? first
                         : second
                   : first > 0
                       ? first
                       : second;

        public static decimal Round(this decimal source, int decimals = 10) => Math.Round(source, decimals, MidpointRounding.AwayFromZero);

        public static decimal? Round(this decimal? source, int decimals = 10) => source.HasValue
                                                                                     ? Round(source.Value, decimals)
                                                                                     : (decimal?)null;

        public static int ToInteger(this string value, int defaultValue = 0) => value.ToIntNullable(defaultValue).Value;

        public static int ToInteger(this long value, int defaultValue = 0) => Try.Get(() => (int)value, defaultValue);

        public static long ToLong(this string value, long defaultValue = 0) => value.ToInt64Nullable(defaultValue).Value;

        public static double ToDoubleRydr(this string value, double defaultValue = 0)
        {
            if (!value.HasValue())
            {
                return defaultValue;
            }

            return double.TryParse(value, out var result)
                       ? result
                       : defaultValue;
        }

        public static int? ToIntNullable(this string value, int? defaultValue = null)
        {
            if (!value.HasValue())
            {
                return defaultValue;
            }

            return int.TryParse(value, out var i)
                       ? i
                       : defaultValue;
        }

        public static long? ToInt64Nullable(this string value, long? defaultValue = null)
        {
            if (!value.HasValue())
            {
                return defaultValue;
            }

            return long.TryParse(value, out var i)
                       ? i
                       : defaultValue;
        }

        public static T TryToEnum<T>(this long source, T defaultValue)
            where T : struct
            => TryToEnum(source.ToInteger(), defaultValue);

        public static T TryToEnum<T>(this int? source, T defaultValue)
            where T : struct
            => source.HasValue
                   ? TryToEnum(source.Value, defaultValue)
                   : defaultValue;

        public static T TryToEnum<T>(this int source, T defaultValue)
            where T : struct
        {
            if (!Enum.IsDefined(typeof(T), source))
            {
                return defaultValue;
            }

            return (T)Enum.ToObject(typeof(T), source);
        }
    }
}
