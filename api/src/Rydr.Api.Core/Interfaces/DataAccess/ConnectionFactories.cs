using System.Data;
using ServiceStack.Data;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.Interfaces.DataAccess
{
    public interface IRydrSqlConnectionFactory : ISqlConnectionFactory { }

    public interface IDwSqlConnectionFactory : ISqlConnectionFactory { }

    public interface ISqlConnectionFactory : IDbConnectionFactory
    {
        IOrmLiteDialectProvider DialectProvider { get; }
        string ConnectionString { get; }
        IDbConnection OpenDbConnection(string namedConnection);
    }
}
