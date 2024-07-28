using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Services.Internal;

public class UtcOnlyDateTimeProvider : IDateTimeProvider
{
    private UtcOnlyDateTimeProvider(Func<DateTime> utcNowProvider = null)
    {
        _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);

        var now = _utcNowProvider();

        if (now.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentOutOfRangeException(nameof(utcNowProvider), now.Kind, $"UTC Now provider must return a DateTime with a DateTimeKind of UTC [{DateTimeKind.Utc}].");
        }
    }

    public static IDateTimeProvider Instance { get; } = new UtcOnlyDateTimeProvider();
    public static IDateTimeProvider Create(Func<DateTime> utcNowProvider) => new UtcOnlyDateTimeProvider(utcNowProvider);

    public static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, 0,
                                                    DateTimeKind.Utc);

    private readonly Func<DateTime> _utcNowProvider;

    public DateTime UtcNow => _utcNowProvider();

    public long UtcNowTs => _utcNowProvider().ToUnixTimestamp(UnixEpoch);
}
