using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DAX;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services
{
    public class DynamoDealService : IDealService
    {
        private static readonly ILog _log = LogManager.GetLogger("DynamoDealService");

        private static readonly List<string> _allDealStatTypeString = EnumsNET.Enums.GetNames<DealStatType>()
                                                                              .Where(es => !es.EqualsOrdinalCi(DealStatType.Unknown.ToString()) &&
                                                                                           !es.EqualsOrdinalCi(DealStatType.PublishedDeals.ToString()))
                                                                              .AsList();

        private readonly IPersistentCounterAndListService _counterAndListService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IPocoDynamo _dynamoDb;
        private readonly IRequestStateManager _requestStateManager;
        private readonly IServerNotificationService _serverNotificationService;

        public DynamoDealService(IPocoDynamo dynamoDb,
                                 IRequestStateManager requestStateManager,
                                 IPersistentCounterAndListService counterAndListService,
                                 IServerNotificationService serverNotificationService,
                                 IPublisherAccountService publisherAccountService)
        {
            _dynamoDb = dynamoDb;
            _requestStateManager = requestStateManager;
            _counterAndListService = counterAndListService;
            _serverNotificationService = serverNotificationService;
            _publisherAccountService = publisherAccountService;
        }

        public async Task<bool> CanBeDeletedAsync(long dealId)
        {
            var dynDeal = await GetDealAsync(dealId);

            var result = await CanBeDeletedAsync(dynDeal);

            return result;
        }

        public async Task<bool> CanBeDeletedAsync(DynDeal dynDeal)
        {
            if (dynDeal == null || dynDeal.IsDeleted() || dynDeal.DealStatus == DealStatus.Deleted)
            {
                return false;
            }

            // Cannot be deleted if existing InProgress requests exists
            if (await _dynamoDb.FromQuery<DynDealRequest>(dr => dr.Id == dynDeal.DealId)
                               .Filter(dr => dr.TypeId == (int)DynItemType.DealRequest &&
                                             dr.DeletedOnUtc == null &&
                                             dr.StatusId == DealRequestStatus.InProgress.ToString())
                               .ExecAsync()
                               .AnyAsync())
            {
                _log.WarnFormat("Deal cannot be deleted - InProgress deal requests still exist. Deal [{0}]", dynDeal.DealId);

                return false;
            }

            return true;
        }

        public Task<IReadOnlyList<DynDeal>> GetDynDealsAsync(IEnumerable<DynamoId> dealIds)
            => _dynamoDb.GetItemsAsync<DynDeal>(dealIds)
                        .Take(1000)
                        .ToListReadOnly();

        public Task<DynDeal> GetDealAsync(long publisherAccountId, long dealId)
            => _dynamoDb.GetItemAsync<DynDeal>(publisherAccountId, dealId.ToEdgeId());

        public Task<DynDeal> GetDealAsync(long dealId, bool ignoreNotFound = false)
            => _dynamoDb.GetItemByEdgeIntoAsync<DynDeal>(DynItemType.Deal, dealId.ToEdgeId(), ignoreNotFound);

        public async Task<List<DynDealStat>> GetDealStatsAsync(long dealId)
        {
            var dynDeal = await GetDealAsync(dealId);

            var results = await _dynamoDb.GetItemsAsync<DynDealStat>(_allDealStatTypeString.Select(dst => new DynamoId(dealId,
                                                                                                                       DynDealStat.BuildEdgeId(dynDeal.PublisherAccountId, dst))))
                                         .Take(500)
                                         .ToList();

            return results;
        }

        public async Task<Dictionary<long, List<DynDealStat>>> GetDealStatsAsync<T>(IEnumerable<T> deals)
            where T : IHasDealId, IHasPublisherAccountId
        {
            var response = new Dictionary<long, List<DynDealStat>>();

            await foreach (var dynDealStat in _dynamoDb.GetItemsAsync<DynDealStat>(deals.SelectMany(d => _allDealStatTypeString.Select(dst => new DynamoId(d.DealId,
                                                                                                                                                           DynDealStat.BuildEdgeId(d.PublisherAccountId, dst))))))
            {
                if (!response.ContainsKey(dynDealStat.DealId))
                {
                    response[dynDealStat.DealId] = new List<DynDealStat>();
                }

                response[dynDealStat.DealId].Add(dynDealStat);
            }

            return response;
        }

        public async Task<DynDealStat> GetDealStatAsync(long dealId, DealStatType dealStatType)
        {
            var dynDeal = await GetDealAsync(dealId);

            var result = await GetDealStatAsync(dealId, dynDeal.PublisherAccountId, dealStatType);

            return result;
        }

        public async Task<DynDealStat> GetDealStatAsync(long dealId, long dealPublisherAccountId, DealStatType dealStatType)
        {
            var stat = await _dynamoDb.GetItemAsync<DynDealStat>(dealId, DynDealStat.BuildEdgeId(dealPublisherAccountId, dealStatType));

            return stat ?? new DynDealStat
                           {
                               DealId = dealId,
                               StatType = dealStatType
                           };
        }

        public async Task ProcesDealStatsAsync(long dealId, long fromPublisherAccountId, DealStatType toTotalStatType, DealStatType? fromTotalStatType)
        {
            // Stores/updates DynDealStat and DynPublisherAccountStat objects
            // Here we basically do up to 6 things:
            //    1) Increment/create the value of the DynDealStat.Cnt for the toTotalStatType value (i.e. Total approvals, etc.)
            //    2) Increment/create the value of the DynDealStat.Cnt for the toCurrentStatType value (i.e. Current approvals, etc.)
            //    3) Decrement the value of the DynDealStat.Cnt for the fromCurrentStat value (i.e. the Current in progress, etc.)
            //    4) Increment/create the value of the DynPublisherAccountStat.Cnt for the toTotalStatType value
            //    5) Increment/create the value of the DynPublisherAccountStat.Cnt for the toCurrentStatType value
            //    6) Decrement the value of the DynPublisherAccountStat.Cnt for the fromCurrentStat value

            // TO publisher is always the deal owner publisher account
            // FROM publisher is always the creator/influencer publisher account

            const string updateExpression = @"
SET ModifiedBy = :cid,
    ModifiedWorkspaceId = :rai,
    ModifiedOnUtc = :nutc,
    Cnt = if_not_exists(Cnt, :zeroval) + :cnti,
    PublisherAccountId = if_not_exists(PublisherAccountId, :pid),
    StatType = if_not_exists(StatType, :tcs),
    TypeId = if_not_exists(TypeId, :tid),
    TypeReference = if_not_exists(TypeReference, :tyr),
    ReferenceId = if_not_exists(ReferenceId, :rid),
    WorkspaceId = if_not_exists(WorkspaceId, :wid),
    CreatedBy = if_not_exists(CreatedBy, :cid),
    CreatedWorkspaceId = if_not_exists(CreatedWorkspaceId, :rai),
    CreatedOnUtc = if_not_exists(CreatedOnUtc, :nutc)";

            Guard.AgainstArgumentOutOfRange(!toTotalStatType.IsTotalStatType() ||
                                            (fromTotalStatType != null && fromTotalStatType != DealStatType.Unknown && !fromTotalStatType.Value.IsTotalStatType()),
                                            "Only Total* StatType values should be passed for processing");

            var dynDeal = await GetDealAsync(dealId);
            var dealContextWorkspaceId = dynDeal.GetContextWorkspaceId();

            var toCurrentStat = toTotalStatType.ToCurrentStatType();
            var fromCurrentStat = fromTotalStatType?.ToCurrentStatType() ?? DealStatType.Unknown;

            var state = _requestStateManager.GetState();

            var toTotalDealStatKeyAttributes = new Dictionary<string, AttributeValue>
                                               {
                                                   {
                                                       "Id", new AttributeValue
                                                             {
                                                                 N = dealId.ToStringInvariant()
                                                             }
                                                   },
                                                   {
                                                       "EdgeId", new AttributeValue
                                                                 {
                                                                     S = DynDealStat.BuildEdgeId(dynDeal.PublisherAccountId, toTotalStatType)
                                                                 }
                                                   }
                                               };

            var toTotalPubAccountStatKeyAttributes = new Dictionary<string, AttributeValue>
                                                     {
                                                         {
                                                             "Id", new AttributeValue
                                                                   {
                                                                       N = dynDeal.PublisherAccountId.ToStringInvariant()
                                                                   }
                                                         },
                                                         {
                                                             "EdgeId", new AttributeValue
                                                                       {
                                                                           S = DynPublisherAccountStat.BuildEdgeId(DynItemType.DealStat, dealContextWorkspaceId, toTotalStatType)
                                                                       }
                                                         }
                                                     };

            var toTotalFromPubAccountStatKeyAttributes = new Dictionary<string, AttributeValue>
                                                         {
                                                             ["Id"] = new AttributeValue
                                                                      {
                                                                          N = fromPublisherAccountId.ToStringInvariant()
                                                                      },
                                                             ["EdgeId"] = new AttributeValue
                                                                          {
                                                                              S = DynPublisherAccountStat.BuildEdgeId(DynItemType.DealStat, 0, toTotalStatType)
                                                                          }
                                                         };

            var toTotalDealExpressionAttrValues = new Dictionary<string, AttributeValue>
                                                  {
                                                      {
                                                          ":tcs", new AttributeValue
                                                                  {
                                                                      N = ((int)toTotalStatType).ToString()
                                                                  }
                                                      },
                                                      {
                                                          ":cnti", new AttributeValue
                                                                   {
                                                                       N = "1"
                                                                   }
                                                      },
                                                      {
                                                          ":cid", new AttributeValue
                                                                  {
                                                                      N = state.UserId.ToStringInvariant()
                                                                  }
                                                      },
                                                      {
                                                          ":rai", new AttributeValue
                                                                  {
                                                                      N = state.WorkspaceId.ToStringInvariant()
                                                                  }
                                                      },
                                                      {
                                                          ":nutc", new AttributeValue
                                                                   {
                                                                       N = DateTimeHelper.UtcNowTs.ToStringInvariant()
                                                                   }
                                                      },
                                                      {
                                                          ":pid", new AttributeValue
                                                                  {
                                                                      N = dynDeal.PublisherAccountId.ToStringInvariant()
                                                                  }
                                                      },
                                                      {
                                                          ":tid", new AttributeValue
                                                                  {
                                                                      N = ((int)DynItemType.DealStat).ToStringInvariant()
                                                                  }
                                                      },
                                                      {
                                                          ":tyr", new AttributeValue
                                                                  {
                                                                      S = string.Concat((int)DynItemType.DealStat, "|", dynDeal.PublisherAccountId.ToStringInvariant())
                                                                  }
                                                      },
                                                      {
                                                          ":wid", new AttributeValue
                                                                  {
                                                                      N = dynDeal.WorkspaceId.ToStringInvariant()
                                                                  }
                                                      },
                                                      {
                                                          ":rid", new AttributeValue
                                                                  {
                                                                      S = dynDeal.PublisherAccountId.ToStringInvariant()
                                                                  }
                                                      },
                                                      {
                                                          ":zeroval", new AttributeValue
                                                                      {
                                                                          N = "0"
                                                                      }
                                                      }
                                                  };

            var toTotalPubAccountExpressionAttrValues = new Dictionary<string, AttributeValue>(toTotalDealExpressionAttrValues)
                                                        {
                                                            [":tid"] = new AttributeValue
                                                                       {
                                                                           N = ((int)DynItemType.PublisherAccountStat).ToStringInvariant()
                                                                       },
                                                            [":tyr"] = new AttributeValue
                                                                       {
                                                                           S = string.Concat((int)DynItemType.PublisherAccountStat, "|", dynDeal.PublisherAccountId.ToStringInvariant())
                                                                       }
                                                        };

            var toTotalFromPubAccountExpressionAttrValues = new Dictionary<string, AttributeValue>(toTotalPubAccountExpressionAttrValues)
                                                            {
                                                                [":pid"] = new AttributeValue
                                                                           {
                                                                               N = fromPublisherAccountId.ToStringInvariant()
                                                                           },
                                                                [":tyr"] = new AttributeValue
                                                                           {
                                                                               S = string.Concat((int)DynItemType.PublisherAccountStat, "|", fromPublisherAccountId.ToStringInvariant())
                                                                           },
                                                                [":rid"] = new AttributeValue
                                                                           {
                                                                               S = fromPublisherAccountId.ToStringInvariant()
                                                                           },
                                                                [":wid"] = new AttributeValue
                                                                           {
                                                                               N = "0"
                                                                           }
                                                            };

            if (toCurrentStat == fromCurrentStat)
            { // Should not happen, but we still need to increment the total - log, use a normal non-transacted request to update the toTotal
                _log.WarnFormat("From and To CurrentStats are the same - should not happen. DealId [{0}], statType [{1}]", dealId, toCurrentStat.ToString());

                var simpleDealRequest = new UpdateItemRequest
                                        {
                                            TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                            Key = toTotalDealStatKeyAttributes,
                                            UpdateExpression = updateExpression,
                                            ExpressionAttributeValues = toTotalDealExpressionAttrValues,
                                            ReturnValues = ReturnValue.NONE
                                        };

                await _dynamoDb.DynamoDb.UpdateItemAsync(simpleDealRequest);

                var simplePubAccountRequest = new UpdateItemRequest
                                              {
                                                  TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                  Key = toTotalPubAccountStatKeyAttributes,
                                                  UpdateExpression = updateExpression,
                                                  ExpressionAttributeValues = toTotalPubAccountExpressionAttrValues,
                                                  ReturnValues = ReturnValue.NONE
                                              };

                await _dynamoDb.DynamoDb.UpdateItemAsync(simplePubAccountRequest);

                if (fromPublisherAccountId > 0)
                {
                    var simpleFromPubAccountRequest = new UpdateItemRequest
                                                      {
                                                          TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                          Key = toTotalFromPubAccountStatKeyAttributes,
                                                          UpdateExpression = updateExpression,
                                                          ExpressionAttributeValues = toTotalFromPubAccountExpressionAttrValues,
                                                          ReturnValues = ReturnValue.NONE
                                                      };

                    await _dynamoDb.DynamoDb.UpdateItemAsync(simpleFromPubAccountRequest);
                }

                return;
            }

            // Transact updates to the toTotal, toCurrent, and optionally fromCurrent
            var request = new TransactWriteItemsRequest
                          {
                              TransactItems = new List<TransactWriteItem>
                                              {
                                                  new TransactWriteItem
                                                  { // The to total deal item
                                                      Update = new Update
                                                               {
                                                                   TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                                   Key = toTotalDealStatKeyAttributes,
                                                                   UpdateExpression = updateExpression,
                                                                   ExpressionAttributeValues = toTotalDealExpressionAttrValues,
                                                                   ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                               }
                                                  },
                                                  new TransactWriteItem
                                                  { // The to current deal item
                                                      Update = new Update
                                                               {
                                                                   TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                                   Key = new Dictionary<string, AttributeValue>
                                                                         {
                                                                             {
                                                                                 "Id", new AttributeValue
                                                                                       {
                                                                                           N = dealId.ToStringInvariant()
                                                                                       }
                                                                             },
                                                                             {
                                                                                 "EdgeId", new AttributeValue
                                                                                           {
                                                                                               S = DynDealStat.BuildEdgeId(dynDeal.PublisherAccountId, toCurrentStat)
                                                                                           }
                                                                             }
                                                                         },
                                                                   UpdateExpression = updateExpression,
                                                                   ExpressionAttributeValues = new Dictionary<string, AttributeValue>(toTotalDealExpressionAttrValues)
                                                                                               {
                                                                                                   [":tcs"] = new AttributeValue
                                                                                                              {
                                                                                                                  N = ((int)toCurrentStat).ToString()
                                                                                                              }
                                                                                               },
                                                                   ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                               }
                                                  },
                                                  new TransactWriteItem
                                                  { // The to total publisher account item
                                                      Update = new Update
                                                               {
                                                                   TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                                   Key = toTotalPubAccountStatKeyAttributes,
                                                                   UpdateExpression = updateExpression,
                                                                   ExpressionAttributeValues = toTotalPubAccountExpressionAttrValues,
                                                                   ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                               }
                                                  },
                                                  new TransactWriteItem
                                                  { // The to current publisher account item
                                                      Update = new Update
                                                               {
                                                                   TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                                   Key = new Dictionary<string, AttributeValue>
                                                                         {
                                                                             {
                                                                                 "Id", new AttributeValue
                                                                                       {
                                                                                           N = dynDeal.PublisherAccountId.ToStringInvariant()
                                                                                       }
                                                                             },
                                                                             {
                                                                                 "EdgeId", new AttributeValue
                                                                                           {
                                                                                               S = DynPublisherAccountStat.BuildEdgeId(DynItemType.DealStat, dealContextWorkspaceId, toCurrentStat)
                                                                                           }
                                                                             }
                                                                         },
                                                                   UpdateExpression = updateExpression,
                                                                   ExpressionAttributeValues = new Dictionary<string, AttributeValue>(toTotalPubAccountExpressionAttrValues)
                                                                                               {
                                                                                                   [":tcs"] = new AttributeValue
                                                                                                              {
                                                                                                                  N = ((int)toCurrentStat).ToString()
                                                                                                              }
                                                                                               },
                                                                   ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                               }
                                                  }
                                              }
                          };

            // If we have a FROM publisher account info, add it's total/current items to the transaction
            if (fromPublisherAccountId > 0)
            { // The to total FROM publisher account item
                request.TransactItems.Add(new TransactWriteItem
                                          {
                                              Update = new Update
                                                       {
                                                           TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                           Key = toTotalFromPubAccountStatKeyAttributes,
                                                           UpdateExpression = updateExpression,
                                                           ExpressionAttributeValues = toTotalFromPubAccountExpressionAttrValues,
                                                           ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                       }
                                          });

                // The to current FROM publisher account item
                request.TransactItems.Add(new TransactWriteItem
                                          {
                                              Update = new Update
                                                       {
                                                           TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                           Key = new Dictionary<string, AttributeValue>
                                                                 {
                                                                     {
                                                                         "Id", new AttributeValue
                                                                               {
                                                                                   N = fromPublisherAccountId.ToStringInvariant()
                                                                               }
                                                                     },
                                                                     {
                                                                         "EdgeId", new AttributeValue
                                                                                   {
                                                                                       S = DynPublisherAccountStat.BuildEdgeId(DynItemType.DealStat, 0, toCurrentStat)
                                                                                   }
                                                                     }
                                                                 },
                                                           UpdateExpression = updateExpression,
                                                           ExpressionAttributeValues = new Dictionary<string, AttributeValue>(toTotalFromPubAccountExpressionAttrValues)
                                                                                       {
                                                                                           [":tcs"] = new AttributeValue
                                                                                                      {
                                                                                                          N = ((int)toCurrentStat).ToString()
                                                                                                      }
                                                                                       },
                                                           ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                       }
                                          });
            }

            // If we have a valid from current stat, add them to the transaction
            if (fromCurrentStat != DealStatType.Unknown)
            {
                // The from current deal item
                request.TransactItems.Add(new TransactWriteItem
                                          {
                                              Update = new Update
                                                       {
                                                           TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                           Key = new Dictionary<string, AttributeValue>
                                                                 {
                                                                     {
                                                                         "Id", new AttributeValue
                                                                               {
                                                                                   N = dealId.ToStringInvariant()
                                                                               }
                                                                     },
                                                                     {
                                                                         "EdgeId", new AttributeValue
                                                                                   {
                                                                                       S = DynDealStat.BuildEdgeId(dynDeal.PublisherAccountId, fromCurrentStat)
                                                                                   }
                                                                     }
                                                                 },
                                                           UpdateExpression = updateExpression,
                                                           ExpressionAttributeValues = new Dictionary<string, AttributeValue>(toTotalDealExpressionAttrValues)
                                                                                       {
                                                                                           [":cnti"] = new AttributeValue
                                                                                                       {
                                                                                                           N = "-1"
                                                                                                       },
                                                                                           [":tcs"] = new AttributeValue
                                                                                                      {
                                                                                                          N = ((int)fromCurrentStat).ToString()
                                                                                                      }
                                                                                       },
                                                           ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                       }
                                          });

                // The from current publisher item
                request.TransactItems.Add(new TransactWriteItem
                                          {
                                              Update = new Update
                                                       {
                                                           TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                           Key = new Dictionary<string, AttributeValue>
                                                                 {
                                                                     {
                                                                         "Id", new AttributeValue
                                                                               {
                                                                                   N = dynDeal.PublisherAccountId.ToStringInvariant()
                                                                               }
                                                                     },
                                                                     {
                                                                         "EdgeId", new AttributeValue
                                                                                   {
                                                                                       S = DynPublisherAccountStat.BuildEdgeId(DynItemType.DealStat, dealContextWorkspaceId, fromCurrentStat)
                                                                                   }
                                                                     }
                                                                 },
                                                           UpdateExpression = updateExpression,
                                                           ExpressionAttributeValues = new Dictionary<string, AttributeValue>(toTotalPubAccountExpressionAttrValues)
                                                                                       {
                                                                                           [":cnti"] = new AttributeValue
                                                                                                       {
                                                                                                           N = "-1"
                                                                                                       },
                                                                                           [":tcs"] = new AttributeValue
                                                                                                      {
                                                                                                          N = ((int)fromCurrentStat).ToString()
                                                                                                      }
                                                                                       },
                                                           ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                       }
                                          });

                // The from current FROM publisher item
                if (fromPublisherAccountId > 0)
                {
                    request.TransactItems.Add(new TransactWriteItem
                                              {
                                                  Update = new Update
                                                           {
                                                               TableName = DynItemTypeHelpers.DynamoItemsTableName,
                                                               Key = new Dictionary<string, AttributeValue>
                                                                     {
                                                                         {
                                                                             "Id", new AttributeValue
                                                                                   {
                                                                                       N = fromPublisherAccountId.ToStringInvariant()
                                                                                   }
                                                                         },
                                                                         {
                                                                             "EdgeId", new AttributeValue
                                                                                       {
                                                                                           S = DynPublisherAccountStat.BuildEdgeId(DynItemType.DealStat, 0, fromCurrentStat)
                                                                                       }
                                                                         }
                                                                     },
                                                               UpdateExpression = updateExpression,
                                                               ExpressionAttributeValues = new Dictionary<string, AttributeValue>(toTotalFromPubAccountExpressionAttrValues)
                                                                                           {
                                                                                               [":cnti"] = new AttributeValue
                                                                                                           {
                                                                                                               N = "-1"
                                                                                                           },
                                                                                               [":tcs"] = new AttributeValue
                                                                                                          {
                                                                                                              N = ((int)fromCurrentStat).ToString()
                                                                                                          }
                                                                                           },
                                                               ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                           }
                                              });
                }
            }

            // Execute
            try
            {
                await _dynamoDb.DynamoDb.TransactWriteItemsAsync(request);
            }
            catch(TransactionCanceledException tx) when(_log.LogExceptionReturnFalse(tx, $"ProcesDealStatsAsync DealId [{dealId}], fromPublisherAccountId [{fromPublisherAccountId}], toTotalStatType [{toTotalStatType.ToString()}], fromTotalStatType [{fromTotalStatType?.ToString() ?? "NULL"}]"))
            { // Unreachable code
                throw;
            }
            catch(DaxTransactionCanceledException dx) when(_log.LogExceptionReturnFalse(dx, $"ProcesDealStatsAsync DealId [{dealId}], fromPublisherAccountId [{fromPublisherAccountId}], toTotalStatType [{toTotalStatType.ToString()}], fromTotalStatType [{fromTotalStatType?.ToString() ?? "NULL"}]"))
            { // Unreachable code
                throw;
            }
        }

        public async Task SendDealNotificationAsync(long fromPublisherAccountId, long toPublisherAccountId, long dealId,
                                                    string title, string message, ServerNotificationType notificationType,
                                                    long workspaceId, string protectedSetPrefix = null)
        {
            var setKey = protectedSetPrefix == null
                             ? null
                             : string.Concat(protectedSetPrefix, "|", dealId);

            if (setKey != null && _counterAndListService.Exists(setKey, toPublisherAccountId.ToStringInvariant()))
            {
                return;
            }

            var fromPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(fromPublisherAccountId);
            var toPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(toPublisherAccountId);

            await _serverNotificationService.NotifyAsync(new ServerNotification
                                                         {
                                                             From = fromPublisherAccount?.ToPublisherAccountInfo(),
                                                             To = toPublisherAccount.ToPublisherAccountInfo(),
                                                             ForRecord = new RecordTypeId(RecordType.Deal, dealId),
                                                             ServerNotificationType = notificationType,
                                                             Title = title,
                                                             Message = message,
                                                             InWorkspaceId = workspaceId
                                                         });

            if (setKey != null)
            {
                _counterAndListService.AddUniqueItem(setKey, toPublisherAccountId.ToStringInvariant());
            }
        }

        public async Task<(Dictionary<long, PublisherMedia> PublisherMediaMap, Dictionary<long, Place> PlaceMap,
                           Dictionary<long, Hashtag> HashtagMap, Dictionary<long, PublisherAccount> PublisherMap)> GetDealMapsForTransformAsync(IReadOnlyCollection<DynDeal> dynDeals)
        {
            if (dynDeals == null || dynDeals.Count <= 0)
            {
                return (new Dictionary<long, PublisherMedia>(), new Dictionary<long, Place>(), new Dictionary<long, Hashtag>(), new Dictionary<long, PublisherAccount>());
            }

            var publisherMediaMap = await _dynamoDb.GetItemsAsync<DynPublisherMedia>(dynDeals.Where(r => !r.PublisherMediaIds.IsNullOrEmpty())
                                                                                             .SelectMany(d => d.PublisherMediaIds.Select(mid => (d.PublisherAccountId,
                                                                                                                                                 PublisherMediaId: mid)))
                                                                                             .GroupBy(t => t.PublisherMediaId)
                                                                                             .Select(g => new DynamoId(g.Max(t => t.PublisherAccountId),
                                                                                                                       g.Key.ToEdgeId())))
                                                   .Where(dpm => dpm != null && !dpm.IsDeleted())
                                                   .SelectAwait(dpm => dpm.ToPublisherMediaAsyncValue())
                                                   .ToDictionarySafe(dpm => dpm.Id);

            var distinctPlaces = new HashSet<long>(dynDeals.Count * 2);
            var distinctHashtagIds = new HashSet<long>();
            var distinctPublisherIds = new HashSet<long>();

            foreach (var dynDeal in dynDeals)
            {
                if (dynDeal.PlaceId > 0)
                {
                    distinctPlaces.Add(dynDeal.PlaceId);
                }

                if (dynDeal.ReceivePlaceId > 0)
                {
                    distinctPlaces.Add(dynDeal.ReceivePlaceId);
                }

                if (!dynDeal.ReceiveHashtagIds.IsNullOrEmptyRydr())
                {
                    distinctHashtagIds.UnionWith(dynDeal.ReceiveHashtagIds);
                }


                if (!dynDeal.ReceivePublisherAccountIds.IsNullOrEmptyRydr())
                {
                    distinctPublisherIds.UnionWith(dynDeal.ReceivePublisherAccountIds);
                }
            }

            var placeMap = distinctPlaces.Count > 0
                               ? await _dynamoDb.GetItemsAsync<DynPlace>(distinctPlaces.Select(h => h.ToItemDynamoId()))
                                                .Where(p => p != null && !p.IsDeleted())
                                                .Select(p => p.ToPlace())
                                                .ToDictionarySafe(p => p.Id)
                               : new Dictionary<long, Place>();

            var hashtagMap = distinctHashtagIds.Count > 0
                                 ? await _dynamoDb.GetItemsFromAsync<DynHashtag, DynItemTypeOwnerSpaceReferenceGlobalIndex>(_dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(t => t.TypeOwnerSpace == DynItem.BuildTypeOwnerSpaceHash(DynItemType.Hashtag, UserAuthInfo.PublicWorkspaceId))
                                                                                                                                     .Filter(d => d.DeletedOnUtc == null && Dynamo.In(d.Id, distinctHashtagIds))
                                                                                                                                     .Select(i => new
                                                                                                                                                  {
                                                                                                                                                      i.Id,
                                                                                                                                                      i.EdgeId
                                                                                                                                                  })
                                                                                                                                     .ExecAsync())
                                                  .ToDictionarySafe(h => h.Id, h => h.ToHashtag())
                                 : new Dictionary<long, Hashtag>();

            var publisherMap = distinctPublisherIds.Count > 0
                                   ? PublisherExtensions.DefaultPublisherAccountService
                                                        .GetPublisherAccounts(distinctPublisherIds)
                                                        .ToDictionarySafe(p => p.PublisherAccountId, p => p.ToPublisherAccount())
                                   : new Dictionary<long, PublisherAccount>();

            return (publisherMediaMap, placeMap, hashtagMap, publisherMap);
        }
    }
}
