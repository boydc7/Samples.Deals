using System;
using System.Collections.Generic;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Dto.Helpers
{
    public static class DtoExtensions
    {
        public static IEnumerable<T> AsEnumerable<T>(this T scalar)
        {
            yield return scalar;
        }

        public static long Gz(this long first, long second)
            => first > 0
                   ? first
                   : second;

        public static long Greatest(this long first, long second)
            => first > second
                   ? first
                   : second;

        public static T TryToEnum<T>(this string source, T defaultValue)
            where T : struct
            => Enum.TryParse(source, ignoreCase: true, out T enumValue)
                   ? enumValue
                   : defaultValue;

        public static bool IsValidRange(this IntRange source)
            => source != null && source.IsValid();

        public static bool IsValidRange(this LongRange source)
            => source != null && source.IsValid();

        public static bool IsValidRange(this DoubleRange source)
            => source != null && source.IsValid();
    }
}
