using ServiceStack.OrmLite;

namespace Rydr.Api.Core.DataAccess.Config
{
    public class RedshiftConfiguration : BaseDataAccessConfiguration
    {
        public RedshiftConfiguration(string appName, string shardName = null) : base(appName, DataConfigurationPrefix.Redshift, shardName) { }

        public override IOrmLiteDialectProvider GetDialectProvider() => PostgreSqlDialect.Provider;
    }
}
