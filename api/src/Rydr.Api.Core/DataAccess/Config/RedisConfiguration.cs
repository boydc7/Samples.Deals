using ServiceStack.OrmLite;

namespace Rydr.Api.Core.DataAccess.Config;

public class RedisConfiguration : BaseDataAccessConfiguration
{
    public RedisConfiguration(string appName, string shardName = null) : base(appName, DataConfigurationPrefix.Redis, shardName) { }

    public override IOrmLiteDialectProvider GetDialectProvider() => null;
}
