using System;

namespace Rydr.Api.Core.Extensions
{
    public static class GuidExtensions
    {
        public static string ToStringId(this Guid from) => from.ToString("N").ToUpperInvariant();

        public static Guid ToGuidId(this string from) => Guid.TryParse(from, out var returnValue)
                                                             ? returnValue
                                                             : Guid.Empty;
    }
}
