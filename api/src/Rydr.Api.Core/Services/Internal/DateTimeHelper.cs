using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Services.Internal;

public static class DateTimeHelper
{
    private static IDateTimeProvider _instance;

    public static IDateTimeProvider Provider => _instance ??= UtcOnlyDateTimeProvider.Instance;

    public static readonly DateTime MinApplicationDate = new(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime MaxApplicationDate = new(2060, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    public static readonly long MinApplicationDateTs = MinApplicationDate.ToUnixTimestamp();
    public static readonly long MaxApplicationDateTs = MaxApplicationDate.ToUnixTimestamp();

    public static DateTime UtcNow => Provider.UtcNow;

    public static long UtcNowTs => Provider.UtcNow.ToUnixTimestamp();

    public static long UtcNowTsMs => Provider.UtcNow.ToUnixTimestampMs();

    public static void SetDateTimeProvider(IDateTimeProvider provider)
    {
        _instance = provider;
    }
}
