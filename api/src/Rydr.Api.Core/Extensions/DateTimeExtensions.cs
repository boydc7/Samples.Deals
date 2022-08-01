using System;
using System.Linq;
using Ical.Net.DataTypes;
using Rydr.Api.Core.Services.Internal;

namespace Rydr.Api.Core.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime? NullIf(this DateTime? source, DateTime nullIf)
            => NullIf(source, d => d == nullIf);

        public static DateTime? NullIf(this DateTime? source, Func<DateTime, bool> nullIf)
            => source.HasValue
                   ? nullIf(source.Value)
                         ? null
                         : source
                   : null;

        public static DateTime UtcEpoch { get; } = UtcOnlyDateTimeProvider.UnixEpoch;

        private static DateTime GetUnspecifiedEpoch(DateTimeKind kind)
            => kind == DateTimeKind.Utc
                   ? UtcEpoch
                   : new DateTime(1970, 1, 1, 0, 0, 0, 0,
                                  kind);

        public static string ToSqlString(this DateTime? dateTime, bool includeMilliseconds = false)
            => dateTime.HasValue
                   ? ToSqlString(dateTime.Value, includeMilliseconds)
                   : null;

        public static string ToSqlString(this DateTime dateTime, bool includeMilliseconds = false)
            => includeMilliseconds
                   ? dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff")
                   : dateTime.ToString("yyyy-MM-dd HH:mm:ss");

        public static string ToSqlDateString(this DateTime? dateTime) => dateTime?.ToString("yyyy-MM-dd");

        public static string ToSqlDateString(this DateTime dateTime) => dateTime.ToString("yyyy-MM-dd");

        public static string ToIso8601Utc(this DateTime dateTime)
            => string.Concat(dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"), "Z");

        public static long? ToUnixTimestamp(this DateTime? dateTime, params DateTime?[] coalesce)
        {
            return dateTime.HasValue
                       ? ToUnixTimestamp(dateTime.Value)
                       : coalesce.FirstOrDefault(v => v.HasValue && v.Value >= UtcEpoch)?.ToUnixTimestamp();
        }

        public static DateTime Greatest(this DateTime first, DateTime second)
            => first >= second
                   ? first
                   : second;

        public static long ToUnixTimestamp(this DateTime dateTime, params DateTime?[] coalesce)
        {
            return dateTime >= UtcEpoch
                       ? ToUnixTimestamp(dateTime, GetUnspecifiedEpoch(dateTime.Kind))
                       : coalesce.FirstOrDefault(v => v.HasValue && v.Value >= UtcEpoch)?.ToUnixTimestamp() ?? 0;
        }

        public static long ToUnixTimestamp(this DateTime dateTime, DateTime epoch) => (long)dateTime.Subtract(epoch).TotalSeconds;

        public static long ToUnixTimestampMs(this DateTime dateTime, DateTime? epoch = null) => (long)dateTime.Subtract(epoch ?? GetUnspecifiedEpoch(dateTime.Kind)).TotalMilliseconds;

        public static DateTime? ToDateTime(this long? unixTimestamp) => unixTimestamp.HasValue
                                                                            ? ToDateTime(unixTimestamp.Value)
                                                                            : (DateTime?)null;

        public static DateTime ToDateTime(this long unixTimestamp) => ToDateTime(unixTimestamp, GetUnspecifiedEpoch(DateTimeKind.Utc));

        public static DateTime ToDateTime(this int unixTimestamp) => ToDateTime(unixTimestamp, GetUnspecifiedEpoch(DateTimeKind.Utc));

        public static DateTime ToDateTime(this long unixTimestamp, DateTime epoch) => epoch.AddSeconds(unixTimestamp);

        public static DateTime ToDateTimeKind(this DateTime dateTime, DateTimeKind toKind) => new DateTime(dateTime.Ticks, toKind);

        public static DateTime AsUtc(this DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
            {
                return dateTime;
            }

            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute,
                                dateTime.Second, dateTime.Millisecond, DateTimeKind.Utc);
        }

        public static DateTime ToUtc(this DateTime dateTime, string systemTimeZoneId)
        {
            var fromTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(systemTimeZoneId);
            var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(dateTime, fromTimeZoneInfo);

            return utcDateTime;
        }

        public static DateTime FromUtc(this DateTime utc, string systemTimeZoneId)
        {
            var toTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(systemTimeZoneId);
            var toDateTime = TimeZoneInfo.ConvertTimeFromUtc(utc, toTimeZoneInfo);

            return toDateTime;
        }

        public static DateTime Coalesce(this DateTime? dateTime, params DateTime[] coalesce)
            => dateTime.HasValue && dateTime.Value >= UtcEpoch
                   ? dateTime.Value
                   : coalesce.FirstOrDefault(v => v >= UtcEpoch);

        public static DateTime Coalesce(this DateTime dateTime, params DateTime[] coalesce)
            => dateTime >= UtcEpoch
                   ? dateTime
                   : coalesce.FirstOrDefault(v => v >= UtcEpoch);

        public static DateTimeOffset? ToTimezone(this DateTime? dateTime, string timezoneId)
            => dateTime.HasValue
                   ? ToTimezone(dateTime.Value, timezoneId)
                   : (DateTimeOffset?)null;

        public static DateTimeOffset ToTimezone(this DateTime dateTime, string timezoneId)
        {
            Guard.AgainstArgumentOutOfRange(dateTime.Kind != DateTimeKind.Utc, "Timezone conversion should only source from a UTC datetime value");

            var calDateTime = new CalDateTime(dateTime);

            var tzDateTime = calDateTime.ToTimeZone(timezoneId);

            return tzDateTime.AsDateTimeOffset;
        }

        public static DateTime ToDateInUtc(this DateTimeOffset offset)
        { // Get the date component of the offset treated as UTC and subtract the offset to arrive at the
            // date in the timezone of the offset represented in UTC
            var offsetStartOfDayInUtc = offset.Date.AsUtc() - offset.Offset;

            return offsetStartOfDayInUtc;
        }

        public static DateTime StartOfWeek(this DateTime source, DayOfWeek startOfWeek = DayOfWeek.Monday)
        {
            var offset = (7 + (source.DayOfWeek - startOfWeek)) % 7;

            var result = source.AddDays(-offset).Date;

            return result;
        }

        public static DateTime StartOfNextMonth(this DateTime source)
        {
            var nowNextMonth = source.AddMonths(1);

            return new DateTime(nowNextMonth.Year, nowNextMonth.Month, 1, 0, 0, 0, source.Kind);
        }

        public static DateTime StartOfMonth(this DateTime source)
            => new DateTime(source.Year, source.Month, 1, 0, 0, 0, source.Kind);
    }
}
