using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.DataAccess.Config;

public abstract class BaseDataAccessConfiguration
{
    private static readonly ISecretService _secretService = RydrEnvironment.Container.Resolve<ISecretService>();

    protected readonly string _appName;
    private readonly string _dataConfigPrefix;

    protected BaseDataAccessConfiguration(string appName, string dataConfigPrefix, string shardName = null)
    {
        _appName = appName;
        _dataConfigPrefix = dataConfigPrefix;

        ShardName = shardName.HasValue()
                        ? shardName
                        : null;
    }

    public string ConnectionFactoryKeyName => string.Concat(_appName,
                                                            ShardName == null
                                                                ? null
                                                                : string.Concat("-", ShardName));

    public string ShardName { get; }

    public bool HasConnectionString => !string.IsNullOrWhiteSpace(ConnectionString(true));

    protected string GetConnectionStringKeyName() => string.Concat("ConnectionString.",
                                                                   _appName,
                                                                   ".",
                                                                   _dataConfigPrefix,
                                                                   ShardName == null
                                                                       ? null
                                                                       : string.Concat(".", ShardName),
                                                                   ".",
                                                                   RydrEnvironment.CurrentEnvironment);

    protected bool TryGetConnectionString(out string connectionString)
    {
        var connectionStringKeyName = GetConnectionStringKeyName();

        connectionString = RydrEnvironment.GetConnectionString(connectionStringKeyName);

        if (connectionString.EqualsOrdinalCi("SECRET"))
        {
            var secretValue = _secretService.TryGetSecretStringAsync(string.Concat("RydrApi.", connectionStringKeyName))
                                            .GetAwaiter().GetResult();

            connectionString = secretValue;
        }

        return connectionString.HasValue();
    }

    public abstract IOrmLiteDialectProvider GetDialectProvider();

    public override string ToString() => ConnectionString();

    public virtual string ConnectionString(bool noExceptionOnMissing = false)
    {
        if (TryGetConnectionString(out var connString))
        {
            return connString;
        }

        if (!noExceptionOnMissing)
        {
            throw new Exception($"Could not find a connection string named {GetConnectionStringKeyName()}.");
        }

        return null;
    }
}
