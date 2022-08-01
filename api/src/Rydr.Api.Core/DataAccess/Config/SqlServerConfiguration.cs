using System;
using System.Data.SqlClient;
using Rydr.Api.Core.Extensions;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.DataAccess.Config
{
    public class SqlServerConfiguration : BaseDataAccessConfiguration
    {
        private readonly string _connectionAppName;

        public SqlServerConfiguration(string appName, string shardName = null, string connectionAppName = null) : base(appName, DataConfigurationPrefix.SqlServer, shardName)
        {
            _connectionAppName = connectionAppName.HasValue()
                                     ? _connectionAppName
                                     : null;
        }

        public override IOrmLiteDialectProvider GetDialectProvider() => SqlServer2017Dialect.Provider;

        private string ConnectionString(string connectionAppName)
        {
            var connString = base.ConnectionString(true);

            if (!connString.HasValue())
            {
                throw new Exception($"Could not find a connection string named {GetConnectionStringKeyName()}.");
            }

            connectionAppName ??= _connectionAppName;

            if (connectionAppName == null)
            {
                return connString;
            }

            var sb = new SqlConnectionStringBuilder(connString)
                     {
                         ApplicationName = connectionAppName
                     };

            return sb.ToString();
        }

        public override string ConnectionString(bool noExceptionOnMissing = false) => ConnectionString(_connectionAppName);
    }
}
