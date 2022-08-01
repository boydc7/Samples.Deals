using System;
using Rydr.ActiveCampaign;
using Rydr.ActiveCampaign.Models;
using Rydr.Api.Core.Configuration;
using ServiceStack;

namespace Rydr.Api.Core.Extensions
{
    public static class ActiveCampaignExtensions
    {
        private static readonly string _activeCampaignApiKey = RydrEnvironment.GetAppSetting("ActiveCampaign.ApiKey");
        private static readonly string _activeCampaignEventTrackingKey = RydrEnvironment.GetAppSetting("ActiveCampaign.EventTrackingKey");
        private static readonly string _activeCampaignEventTrackingAcctId = RydrEnvironment.GetAppSetting("ActiveCampaign.EventTrackingAcctId");
        private static readonly IActiveCampaignClient _nullCampaignClient;

        static ActiveCampaignExtensions()
        {
            _nullCampaignClient = _activeCampaignApiKey.HasValue() && _activeCampaignEventTrackingKey.HasValue() && _activeCampaignEventTrackingAcctId.HasValue()
                                      ? null
                                      : new NullActiveCampaignClient("getrydr", null, null, null);
        }

        public static IActiveCampaignClient GetOrCreateRydrClient(this ActiveCampaignClientFactory source)
            => _nullCampaignClient ?? source.GetOrCreateClient("getrydr", _activeCampaignApiKey, _activeCampaignEventTrackingKey, _activeCampaignEventTrackingAcctId);

        public static bool TryAddDelimitedValue(this AcContactCustomFieldValue existingValue, string valueToAdd, out string newValue)
        {
            if ((existingValue?.Value).IsNullOrEmpty())
            {
                newValue = valueToAdd;

                return true;
            }

            var delimitedValue = string.Concat("|", valueToAdd, "|");

            var existingCompareValue = existingValue.Value.StartsWithOrdinalCi("|")
                                           ? existingValue.Value
                                           : string.Concat("|", existingValue.Value, "|");

            if (existingCompareValue.Contains(delimitedValue, StringComparison.OrdinalIgnoreCase))
            {
                newValue = existingValue.Value;

                return false;
            }

            newValue = string.Concat(existingCompareValue, delimitedValue);

            return true;
        }

        public static bool TryRemoveDelimitedValue(this AcContactCustomFieldValue existingValue, string valueToRemove, out string newValue)
        {
            newValue = existingValue?.Value;

            if (newValue.IsNullOrEmpty())
            {
                return false;
            }

            if (existingValue.Value.StartsWithOrdinalCi("|"))
            {
                var delimitedValue = string.Concat("|", valueToRemove, "|");

                if (!existingValue.Value.Contains(delimitedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                newValue = existingValue.Value.Replace(delimitedValue, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

                return true;
            }

            if (existingValue.Value.EqualsOrdinalCi(valueToRemove))
            {
                newValue = string.Empty;

                return true;
            }

            return false;
        }
    }
}
