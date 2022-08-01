using ServiceStack.OrmLite;

namespace Rydr.Api.Core.DataAccess.Config
{
    public class SqlLiteConfiguration : BaseDataAccessConfiguration
    {
        public SqlLiteConfiguration(string appName, string shardName = null) : base(appName, DataConfigurationPrefix.SqlLite, shardName) { }

#if LOCALDEBUG
        public override IOrmLiteDialectProvider GetDialectProvider() => SqliteDialect.Provider;
#else
        public override IOrmLiteDialectProvider GetDialectProvider() => MySqlDialect.Provider;
#endif
    }
}
