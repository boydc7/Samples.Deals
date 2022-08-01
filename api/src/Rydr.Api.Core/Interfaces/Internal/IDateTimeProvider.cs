using System;

namespace Rydr.Api.Core.Interfaces.Internal
{
    public interface IDateTimeProvider
    {
        DateTime UtcNow { get; }
        long UtcNowTs { get; }
    }
}
