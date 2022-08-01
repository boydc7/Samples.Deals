namespace Rydr.Api.Core.Services.Internal
{
    public static class CronBuilder
    {
        public static string Minutely(int every)
        {
            Guard.AgainstArgumentOutOfRange(every <= 0 || every > 59, "Minutely cron expression in Hangfire cannot be > 59");

            return string.Concat(every > 1
                                     ? string.Concat("*/", every)
                                     : "*",
                                 " * * * *");
        }

        public static string Hourly(int every, int onMinute = 1)
        {
            Guard.AgainstArgumentOutOfRange(every <= 0 || every > 23, "Hourly cron expression in Hangfire cannot be > 23");
            Guard.AgainstArgumentOutOfRange(onMinute < 0 || onMinute > 59, "onMinute must be between 0 and 59");

            return string.Concat(onMinute,
                                 " */", every,
                                 " * * *");
        }

        public static string Daily(int onHour, int onMinute = 0)
        {
            Guard.AgainstArgumentOutOfRange(onHour < 0 || onHour > 23, "Daily cron expression in Hangfire must have hour between 0 and 23");
            Guard.AgainstArgumentOutOfRange(onMinute < 0 || onMinute > 59, "atMinute must be between 0 and 59");

            return string.Concat(onMinute,
                                 " ",
                                 onHour,
                                 " * * *");
        }
    }
}
