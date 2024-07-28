using Rydr.Api.Core.Interfaces.DataAccess;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.DataAccess.Config;

public class OrmLiteDbCreateConfiguration : IDbCreateConfiguration
{
    private readonly bool _dropExistingTables;
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly string _namedConnection;
    private readonly Type[] _typesToInitiate;

    public OrmLiteDbCreateConfiguration(ISqlConnectionFactory connectionFactory,
                                        string namedConnection,
                                        Type[] typesToInitiate,
                                        bool dropExistingTables = false)
    {
        _connectionFactory = connectionFactory;
        _namedConnection = namedConnection;
        _typesToInitiate = typesToInitiate;
        _dropExistingTables = dropExistingTables;
    }

    public void Configure()
    { // During creation we ensure things are generated using unicode values, but not during query operations
        OrmLiteConfig.DialectProvider.GetStringConverter().UseUnicode = true;
        OrmLiteConfig.DialectProvider.GetDateTimeConverter().DateStyle = DateTimeKind.Utc;
        OrmLiteConfig.SkipForeignKeys = true;

        _connectionFactory.DialectProvider.GetStringConverter().UseUnicode = true;

        using(var client = _connectionFactory.OpenDbConnection(_namedConnection))
        {
            // Call to CreateTables with the bool flag set to drop isn't working as expected at the moment, so drop explicity
            if (_dropExistingTables)
            {
                client.DropTables(_typesToInitiate);
            }

            client.CreateTables(_dropExistingTables, _typesToInitiate);
        }
    }
}
