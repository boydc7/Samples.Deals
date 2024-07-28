using Rydr.Api.Core.Interfaces.DataAccess;

namespace Rydr.Api.Core.DataAccess.Config;

public class NullDbCreateConfiguration : IDbCreateConfiguration
{
    public static readonly NullDbCreateConfiguration Instance = new();

    private NullDbCreateConfiguration() { }

    public void Configure() { }
}
