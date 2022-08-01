using System;
using System.Data;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.DataAccess
{
    public class RydrSqlConnectionFactory : SqlConnectionFactoryBase, IRydrSqlConnectionFactory
    {
        public RydrSqlConnectionFactory(OrmLiteConnectionFactory connectionFactory, IRequestStateManager requestStateManager)
            : base(connectionFactory, requestStateManager) { }
    }

    public class DwSqlConnectionFactory : SqlConnectionFactoryBase, IDwSqlConnectionFactory
    {
        public DwSqlConnectionFactory(OrmLiteConnectionFactory connectionFactory, IRequestStateManager requestStateManager)
            : base(connectionFactory, requestStateManager) { }
    }

    public abstract class SqlConnectionFactoryBase : ISqlConnectionFactory
    {
        private static readonly bool _shardByOrg = RydrEnvironment.GetAppSetting("Environment.ShardByOrg", "false").ToBoolean();

        protected readonly OrmLiteConnectionFactory _connectionFactory;
        private readonly IRequestStateManager _requestStateManager;

        protected SqlConnectionFactoryBase(OrmLiteConnectionFactory connectionFactory, IRequestStateManager requestStateManager)
        {
            _connectionFactory = connectionFactory;
            _requestStateManager = requestStateManager;
        }

        public IOrmLiteDialectProvider DialectProvider => _connectionFactory.DialectProvider;

        public string ConnectionString => _connectionFactory.ConnectionString;

        [Obsolete]
        public IDbConnection CreateDbConnection()
            => throw new NotImplementedException("Please use OpenDbConnection instead of CreateDbConnection");

        public IDbConnection OpenDbConnection()
        {
            if (!_shardByOrg)
            {
                return _connectionFactory.OpenDbConnection();
            }

            var state = _requestStateManager.GetState();

            return (state.WorkspaceId > 0
                        ? _connectionFactory.OpenDbConnection(state.WorkspaceId.ToStringInvariant())
                        : _connectionFactory.OpenDbConnection());
        }

        public IDbConnection OpenDbConnection(string namedConnection)
            => _connectionFactory.OpenDbConnection(namedConnection);
    }
}
