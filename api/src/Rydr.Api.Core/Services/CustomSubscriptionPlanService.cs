using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services
{
    public class CustomSubscriptionPlanService : ISubscriptionPlanService
    {
        private static readonly string _stripeCustomArrearsProductId = RydrEnvironment.GetAppSetting("Stripe.Products.Custom.Arrears", "prod_HZdM0UzS7EEfOP");
        private static readonly string _stripeCustomAdvanceProductId = RydrEnvironment.GetAppSetting("Stripe.Products.Custom.Advance", "prod_HeAXrmBbCzPK9m");
        private static readonly string _stripeCustomPostProductId = RydrEnvironment.GetAppSetting("Stripe.Products.Custom.Post", "prod_HZeU0UXqPstshF");

        private readonly ISubscriptionPlanService _innerSubscriptionPlanService;
        private readonly IPocoDynamo _dynamoDb;
        private readonly Dictionary<string, long> _reversedProductIdMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DynItemMap> _priceIdToMap = new Dictionary<string, DynItemMap>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DynItemMap> _productPriceInCentsToMap = new Dictionary<string, DynItemMap>();

        public CustomSubscriptionPlanService(ISubscriptionPlanService innerSubscriptionPlanService,
                                             IPocoDynamo dynamoDb)
        {
            _innerSubscriptionPlanService = innerSubscriptionPlanService;
            _dynamoDb = dynamoDb;
        }

        public static string CustomPerPostProductId => _stripeCustomPostProductId;

        public string PayPerBusinessPlanId => _innerSubscriptionPlanService.PayPerBusinessPlanId;

        public async ValueTask<bool> IsManagedPlanIdAsync(string planId)
        {
            if (planId.IsNullOrEmpty())
            {
                return false;
            }

            var subscriptionType = await GetSubscriptionTypeForPlanIdAsync(planId);

            return subscriptionType.IsManagedSubscriptionType();
        }

        public async ValueTask<IEnumerable<string>> GetPlanIdsForSubscriptionTypeAsync(SubscriptionType subscriptionType, int customMonthlyFeeCents = 0, int customPerPostFeeCents = 0)
        {
            if (!subscriptionType.IsManagedCustomPlan())
            {
                return await _innerSubscriptionPlanService.GetPlanIdsForSubscriptionTypeAsync(subscriptionType, customMonthlyFeeCents, customPerPostFeeCents);
            }

            var monthlyPlanPrice = subscriptionType switch
            {
                SubscriptionType.ManagedCustomAdvance => await GetOrCreateMapForCustomPriceAsync(_stripeCustomAdvanceProductId, customMonthlyFeeCents, isPerUnitUsage: false),
                SubscriptionType.ManagedCustom => await GetOrCreateMapForCustomPriceAsync(_stripeCustomArrearsProductId, customMonthlyFeeCents, isPerUnitUsage: false),
                _ => throw new ArgumentOutOfRangeException()
            };

            var perPostPlanPrice = await GetOrCreateMapForCustomPriceAsync(_stripeCustomPostProductId, customPerPostFeeCents, isPerUnitUsage: true);

            return new List<string>(2)
                   {
                       monthlyPlanPrice.MappedItemEdgeId,
                       perPostPlanPrice.MappedItemEdgeId
                   };
        }

        public async ValueTask<SubscriptionType> GetSubscriptionTypeForPlanIdAsync(string planId)
        {
            if (planId.IsNullOrEmpty())
            {
                return SubscriptionType.None;
            }

            if (planId.EqualsOrdinalCi(_stripeCustomAdvanceProductId) || planId.EqualsOrdinalCi(_stripeCustomArrearsProductId))
            {
                return SubscriptionType.ManagedCustom;
            }

            var advancePriceMap = await GetMapForCustomPriceAsync(_stripeCustomAdvanceProductId, planId);

            if (advancePriceMap != null)
            {
                return SubscriptionType.ManagedCustomAdvance;
            }

            var arrearsPriceMap = await GetMapForCustomPriceAsync(_stripeCustomArrearsProductId, planId);

            return arrearsPriceMap == null
                       ? await _innerSubscriptionPlanService.GetSubscriptionTypeForPlanIdAsync(planId)
                       : SubscriptionType.ManagedCustom;
        }

        public async ValueTask<string> GetSubscriptionPlanIdForUsageTypeAsync(SubscriptionType subscriptionType, SubscriptionUsageType usageType,
                                                                                     int customMonthlyFeeCents = 0, int customPerPostFeeCents = 0)
        {
            if (!subscriptionType.IsManagedCustomPlan())
            {
                return await _innerSubscriptionPlanService.GetSubscriptionPlanIdForUsageTypeAsync(subscriptionType, usageType, customMonthlyFeeCents, customPerPostFeeCents);
            }

            switch (usageType)
            {
                case SubscriptionUsageType.CompletedRequest:
                {
                    var perPostMap = await GetOrCreateMapForCustomPriceAsync(_stripeCustomPostProductId, customPerPostFeeCents, isPerUnitUsage: true);

                    return perPostMap.MappedItemEdgeId;
                }
                case SubscriptionUsageType.SubscriptionFee:
                {
                    switch (subscriptionType)
                    {
                        // Advance and arrears
                        case SubscriptionType.ManagedCustomAdvance:
                        {
                            var advanceMap = await GetOrCreateMapForCustomPriceAsync(_stripeCustomAdvanceProductId, customMonthlyFeeCents, isPerUnitUsage: false);

                            return advanceMap.MappedItemEdgeId;
                        }
                        case SubscriptionType.ManagedCustom:
                        {
                            var arrearsMap = await GetOrCreateMapForCustomPriceAsync(_stripeCustomArrearsProductId, customMonthlyFeeCents, isPerUnitUsage: false);

                            return arrearsMap.MappedItemEdgeId;
                        }
                        default:
                        {
                            throw new ArgumentOutOfRangeException(nameof(subscriptionType), "Invalid/Unhandled SubscriptionType for Custom SubscriptionUsageType.SubscriptionFee lookup");
                        }
                    }
                }
                case SubscriptionUsageType.None:
                case SubscriptionUsageType.SubscriptionFeeCredit:
                case SubscriptionUsageType.CompletedRequestCredit:
                default:
                    return null;
            }
        }

        private async ValueTask<DynItemMap> GetOrCreateMapForCustomPriceAsync(string productId, int priceInCents, bool isPerUnitUsage)
        {
            var existingPrice = await GetMapForCustomPriceAsync(productId, priceInCents);

            if (existingPrice != null)
            {
                return existingPrice;
            }

            // See if we have one at stripe already
            var lookupKey = string.Concat("price|", productId, "|", priceInCents);

            var stripe = await StripeService.GetInstanceAsync();

            var stripePrice = await stripe.GetProductPricesAsync(productId, lookupKey)
                                          .FirstOrDefaultAsync(p => p.ProductId.EqualsOrdinalCi(productId) &&
                                                                    p.UnitAmount.HasValue &&
                                                                    p.UnitAmount.Value == priceInCents);

            if (stripePrice == null)
            {
                if (productId.EqualsOrdinalCi(_stripeCustomAdvanceProductId))
                {
                    stripePrice = await stripe.CreateProductPriceAsync(productId, priceInCents, lookupKey);
                }
                else if (productId.EqualsOrdinalCi(_stripeCustomPostProductId) ||
                         productId.EqualsOrdinalCi(_stripeCustomArrearsProductId))
                {
                    stripePrice = await stripe.CreateProductMeteredPriceAsync(productId, priceInCents, sumUsage: isPerUnitUsage, lookupKey);
                }
            }

            if (stripePrice == null)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Could not find custom managed matching productId");
            }

            var (id, edgeId) = GetIdEdgeForCustomPrice(productId, stripePrice.Id);

            var priceMap = new DynItemMap
                           {
                               Id = id,
                               EdgeId = edgeId,
                               ReferenceNumber = priceInCents,
                               MappedItemEdgeId = stripePrice.Id,
                               Items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                       {
                                           {
                                               "ProductId", productId
                                           },
                                           {
                                               "PriceId", stripePrice.Id
                                           }
                                       }
                           };

            await _dynamoDb.PutItemAsync(priceMap);

            AddOrUpdateMaps(priceMap);

            return priceMap;
        }

        private async ValueTask<DynItemMap> GetMapForCustomPriceAsync(string productId, string priceId)
        {
            if (_priceIdToMap.TryGetValue(priceId, out var map))
            {
                return map;
            }

            var (id, edgeId) = GetIdEdgeForCustomPrice(productId, priceId);

            if (_priceIdToMap.ContainsKey(edgeId))
            {
                return _priceIdToMap[edgeId];
            }

            map = await _dynamoDb.GetItemAsync<DynItemMap>(id, edgeId);

            AddOrUpdateMaps(map);

            return map;
        }

        private async ValueTask<DynItemMap> GetMapForCustomPriceAsync(string productId, int priceInCents)
        {
            if (_productPriceInCentsToMap.TryGetValue(string.Concat(productId, "|", priceInCents), out var map))
            {
                return map;
            }

            var (id, edgePrefix) = GetIdEdgeForCustomPrice(productId, string.Empty);

            map = await _dynamoDb.FromQuery<DynItemMap>(m => m.Id == id &&
                                                             Dynamo.BeginsWith(m.EdgeId, edgePrefix))
                                 .Filter(m => m.ReferenceNumber == priceInCents)
                                 .ExecAsync()
                                 .FirstOrDefaultAsync(m => m.ReferenceNumber.HasValue &&
                                                           m.ReferenceNumber == priceInCents &&
                                                           m.MappedItemEdgeId.HasValue() &&
                                                           !m.Items.IsNullOrEmptyRydr());

            AddOrUpdateMaps(map);

            return map;
        }

        private (long Id, string EdgeId) GetIdEdgeForCustomPrice(string productId, string priceId)
        {
            if (!_reversedProductIdMap.ContainsKey(productId))
            {
                _reversedProductIdMap.Add(productId, productId.Reverse().ToLongHashCode());
            }

            var id = _reversedProductIdMap[productId];
            var edgeId = DynItemMap.BuildEdgeId(DynItemType.CustomSubscriptionPrice, string.Concat(productId, "|", priceId));

            return (id, edgeId);
        }

        private void AddOrUpdateMaps(DynItemMap map)
        {
            if (map == null)
            {
                return;
            }

            _priceIdToMap[map.MappedItemEdgeId] = map;

            if (map.Items.IsNullOrEmptyRydr() || !map.Items.ContainsKey("ProductId"))
            {
                return;
            }

            _productPriceInCentsToMap[string.Concat(map.Items["ProductId"], "|", map.ReferenceNumber.Value)] = map;
        }
    }
}
