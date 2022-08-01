using ServiceStack.OrmLite;

namespace Rydr.Api.Core.DataAccess.Config
{
    public class MySqlConfiguration : BaseDataAccessConfiguration
    {
        public MySqlConfiguration(string appName, string shardName = null) : base(appName, DataConfigurationPrefix.MySql, shardName) { }

        public override IOrmLiteDialectProvider GetDialectProvider() => MySqlDialect.Provider;
    }
}
