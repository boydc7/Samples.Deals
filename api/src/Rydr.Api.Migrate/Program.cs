using System;
using System.Linq;
using Amazon;
using Amazon.DynamoDBv2;
using Dapper;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using MySql.Data.MySqlClient;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Migrate
{
    internal class Program
    {
        private static void Main()
        {
            Licensing.RegisterLicense(@"7376-e1JlZjo3Mzc2LE5hbWU6IlJ5ZHIgVGVjaG5vbG9naWVzLCBJbmMiLFR5cGU6SW5kaWUsTWV0YTowLEhhc2g6RnRLMzhRWTJTMXU1OWdHVUJ5TGM1ZlN2dHREY3hCWVBvQ3Y2NDZmWWtFVlJZKzhycUYwUno1MzNmUE1LRmlYdUg3MmJWZmFuenQ3Sml0eG5XQVJFeEtMa3VCNUpkdXhxcGd5Ykw5cGhVeVluRmdJNFhQYmFOejY4dVNGZGdFa25YcHF1OG50Z3dianErcmVSUVQyb2FTSGJWNGhpYlVjZW9sWittdnZ4bHNNPSxFeHBpcnk6MjAyMC0wNi0xMn0=");

            var config = new AmazonDynamoDBConfig
                         {
                             RegionEndpoint = RegionEndpoint.USWest2
                         };

            // Set these before running...or comment out...
            // config.ServiceURL = "http://localhost:8000";
            const string dynamoTablePrefix = "prod_";

            /*
                        var esConnectionSettings = new ConnectionSettings(new SingleNodeConnectionPool(new Uri("http://localhost:9200"))).ConnectionLimit(300)
                                                                                                                                         .MaximumRetries(4)
                                                                                                                                         .DisableAutomaticProxyDetection()
                                                                                                                                         .MaxRetryTimeout(TimeSpan.FromMinutes(3))
                                                                                                                                         .RequestTimeout(TimeSpan.FromSeconds(100));

                        var esClient = new ElasticClient(esConnectionSettings);

                        var sqsClient = new AmazonSQSClient("AKIAJ64IFIU2C525YOSQ", "iELokUWqMMMAgWqJIfYtzKN3H8Uwl0M+rHTM31uU", new AmazonSQSConfig
                                                                                                                                {
                                                                                                                                    RegionEndpoint = RegionEndpoint.USWest2
                                                                                                                                });

                        // var redisClient = new RedisClient("rydrdev.sc77fr.0001.usw2.cache.amazonaws.com");
            */

            var dynTypesToRegister = new[]
                                     {
                                         typeof(DynAssociation),
                                         typeof(DynItem),
                                         typeof(DynPublisherMediaAnalysis),
                                         typeof(DynPublisherAccount),
                                         typeof(DynPublisherMediaStat),
                                         typeof(DynDeal),
                                         typeof(DynDealRequest),
                                         typeof(DynNotification),
                                         typeof(DynPublisherAccountStat),
                                         typeof(DynDealStat),
                                         typeof(DynWorkspace),
                                         typeof(DynPublisherMedia),
                                         typeof(DynDealStatusChange),
                                         typeof(DynDealRequestStatusChange),
                                         typeof(DynUser),
                                         typeof(DynPlace),
                                         typeof(DynDialog),
                                         typeof(DynDialogMessage)
                                     };

            var dynamoDb = new PocoDynamo(new AmazonDynamoDBClient("AKIAJ64IFIU2C525YOSQ", "iELokUWqMMMAgWqJIfYtzKN3H8Uwl0M+rHTM31uU", config))
                           {
                               ReadCapacityUnits = 1,
                               WriteCapacityUnits = 1,
                               ConsistentRead = true,
                               ScanIndexForward = true,
                               PagingLimit = 500000
                           };

            foreach (var dynType in dynTypesToRegister)
            {
                var aliasAttribute = new AliasAttribute(string.Concat(dynamoTablePrefix, "Items"));

                dynType.AddAttributes(aliasAttribute);

                if (dynType == typeof(DynItem))
                {
                    typeof(DynItemEdgeIdGlobalIndex).AddAttributes(new AliasAttribute(string.Concat(dynamoTablePrefix, typeof(DynItemEdgeIdGlobalIndex).Name)));

                    typeof(DynItemTypeOwnerSpaceReferenceGlobalIndex).AddAttributes(new AliasAttribute(string.Concat(dynamoTablePrefix, typeof(DynItemTypeOwnerSpaceReferenceGlobalIndex).Name)));

                    typeof(DynItemIdTypeReferenceGlobalIndex).AddAttributes(new AliasAttribute(string.Concat(dynamoTablePrefix, typeof(DynItemIdTypeReferenceGlobalIndex).Name)));
                }

                dynamoDb.RegisterTable(dynType);
            }

            var dynItemMapType = typeof(DynItemMap);
            dynItemMapType.AddAttributes(new AliasAttribute(string.Concat(dynamoTablePrefix, "ItemMaps")));
            dynamoDb.RegisterTable(dynItemMapType);

            var updated = 0;

            /*
             Using sql....

             using(var sqlConnection = new MySqlConnection(""))
             {
                sqlConnection.Open();

                ...

                sqlConnection.Execute(@"",
                                          new
                                          {
                                          });
                }
            */
            /****************************************************************************************************************************************************
             PUT LOGIC TO MIGRATE BELOW HERE
            ****************************************************************************************************************************************************/
            foreach (var dynDealRequest in dynamoDb.FromScan<DynDealRequest>(d => d.TypeId == (int)DynItemType.DealRequest &&
                                                                                  d.StatusId == "InProgress")
                                                   .Exec())
            {
                if (dynDealRequest.RequestStatus != DealRequestStatus.InProgress)
                {
                    continue;
                }




                updated++;
            }

            Console.WriteLine($"Updated [{updated}] things");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static T MigrateGetItemEdgeInto<T>(IPocoDynamo dynamo, DynItemType itemType, string edgeId)
            where T : class
        { // Safe here to just lookup/store by the index model, as everything in there is read-only
            var indexModel = dynamo.FromQueryIndex<DynItemEdgeIdGlobalIndex>(i => i.EdgeId == edgeId &&
                                                                                  Dynamo.BeginsWith(i.TypeReference,
                                                                                                    string.Concat((int)itemType, "|")))
                                   .Exec()
                                   .SingleOrDefault();

            return indexModel == null
                       ? null
                       : dynamo.GetItem<T>(indexModel.GetDynamoId());
        }

        private static T MigrateGetItemRefInto<T>(IPocoDynamo dynamo, DynItemType itemType, long hashAndRefId)
            where T : class
        {
            var indexModel = dynamo.FromQueryIndex<DynItemIdTypeReferenceGlobalIndex>(i => i.Id == hashAndRefId &&
                                                                                           i.TypeReference == DynItem.BuildTypeReferenceHash(itemType,
                                                                                                                                             hashAndRefId.ToStringInvariant()))
                                   .Exec()
                                   .SingleOrDefault(m => m.TypeId == (int)itemType);

            return indexModel == null
                       ? null
                       : dynamo.GetItem<T>(indexModel.GetDynamoId());
        }
    }
}
