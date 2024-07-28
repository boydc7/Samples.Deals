using System.Diagnostics;
using System.Net.Sockets;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;
using ServiceStack.Logging;

namespace Rydr.Api.Core.DataAccess.Config;

public class DynamoDbCreateConfiguration : IDbCreateConfiguration
{
    private readonly bool _dropExistingTables;
    private readonly List<Type> _typesToInitiate;
    private readonly IPocoDynamo _pocoDynamo;
    private readonly bool _disableSchemaCreate;
    private readonly ILog _log = LogManager.GetLogger("DynamoDbCreateConfiguration");
    private readonly string _dynamoTablePrefix = RydrEnvironment.GetAppSetting("AWS.Dynamo.TableNamePrefix", "dev_");

    public DynamoDbCreateConfiguration(IPocoDynamo pocoDynamo,
                                       List<Type> typesToInitiate,
                                       bool disableSchemaCreate,
                                       bool dropExistingTables = false)
    {
        _pocoDynamo = pocoDynamo;
        _disableSchemaCreate = disableSchemaCreate;
        _typesToInitiate = typesToInitiate ?? new List<Type>();
        _dropExistingTables = dropExistingTables;

        if (!_typesToInitiate.Contains(typeof(DynItem)))
        {
            _typesToInitiate.Insert(0, typeof(DynItem));
        }
    }

    public void Configure()
    {
        if (_dynamoTablePrefix.HasValue())
        {
            typeof(DynItemEdgeIdGlobalIndex).AddAttributes(new AliasAttribute(string.Concat(_dynamoTablePrefix.Trim(), nameof(DynItemEdgeIdGlobalIndex))));
            typeof(DynItemTypeOwnerSpaceReferenceGlobalIndex).AddAttributes(new AliasAttribute(string.Concat(_dynamoTablePrefix.Trim(), nameof(DynItemTypeOwnerSpaceReferenceGlobalIndex))));
            typeof(DynItemIdTypeReferenceGlobalIndex).AddAttributes(new AliasAttribute(string.Concat(_dynamoTablePrefix.Trim(), nameof(DynItemIdTypeReferenceGlobalIndex))));
        }

        foreach (var dynType in _typesToInitiate)
        {
            if (dynType.IsNestedPrivate)
            {
                continue;
            }

            var currentAlias = dynType.GetFirstAttribute<AliasAttribute>();

            if (currentAlias != null)
            {
                throw new Exception("Dynamo table models should NOT have an Alias attribute associated with them, until PocoDynamo/AWS Client supports prefixing tables in the client (not the context). Please remove it.");
            }

            var isItemType = dynType.BaseType == typeof(DynItem) || dynType.BaseType.BaseType == typeof(DynItem);

            var aliasAttribute = new AliasAttribute(string.Concat(_dynamoTablePrefix?.Trim() ?? string.Empty,
                                                                  isItemType
                                                                      ? "Item"
                                                                      : dynType.Name.ReplaceFirst("Dyn", string.Empty),
                                                                  "s"));

            dynType.AddAttributes(aliasAttribute);

            try
            {
                if (_dropExistingTables && !isItemType && !_disableSchemaCreate &&
                    (dynType != typeof(DynItem) || RydrEnvironment.CurrentEnvironment.EqualsOrdinalCi("local")))
                {
                    var meta = _pocoDynamo.GetTableMetadata(dynType);

                    _pocoDynamo.DeleteTables(new[]
                                             {
                                                 meta.Name
                                             });
                }

                _pocoDynamo.RegisterTable(dynType);
            }
            catch(SocketException se)
            {
                Debug.Print("####### You've got a SocketException in DynamoDbCreateConfiguration. Did you start dyanmo?\n\tError: " + se.Message);

                throw;
            }
        }

        if (!_disableSchemaCreate)
        {
            _log.DebugInfo("Starting Dynamo schema initialization");

            var sequenceTableAlreadyExists = _pocoDynamo.GetTableSchema<Seq>() != null;

            _pocoDynamo.InitSchema();

            if (!sequenceTableAlreadyExists)
            { // Just created the sequences table, bump the global sequence to the appropriate start point
                _pocoDynamo.Sequences.Increment(Sequences.GlobalSequenceKey, GlobalItemIds.MinUserDefinedObjectId);
            }
        }
    }
}
