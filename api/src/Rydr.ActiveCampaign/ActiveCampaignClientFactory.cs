using System;
using System.Collections.Concurrent;
using System.Linq;
using Rydr.ActiveCampaign.Configuration;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Rydr.ActiveCampaign
{
    public class ActiveCampaignClientFactory : IDisposable
    {
        private readonly ConcurrentDictionary<string, PooledClient> _clientMap = new ConcurrentDictionary<string, PooledClient>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<(string accountName, string apiKey, string eventTrackingKey, string eventTrackingAcctId), IActiveCampaignClient> _clientFactory;

        private ActiveCampaignClientFactory()
        {
            _clientFactory = ActiveCampaignSdkConfig.UseLoggedClient && ActiveCampaignSdkConfig.ClientFactory == null
                                 ? t => new LoggedActiveCampaignClient(t.accountName, t.apiKey, t.eventTrackingKey, t.eventTrackingAcctId)
                                 : ActiveCampaignSdkConfig.ClientFactory ?? GetDefaultClient;
        }

        public static ActiveCampaignClientFactory Instance { get; } = new ActiveCampaignClientFactory();

        private IActiveCampaignClient GetDefaultClient((string accountName, string apiKey, string eventTrackingKey, string eventTrackingAcctId) clientParams)
            => new ActiveCampaignClient(clientParams.accountName, clientParams.apiKey, clientParams.eventTrackingKey, clientParams.eventTrackingAcctId);

        public IActiveCampaignClient GetOrCreateClient(string accountName, string apiKey, string eventTrackingKey, string eventTrackingAcctId)
        {
            var clientKey = ActiveCampaignSdkConfig.ClientPoolingDisabled
                                ? string.Empty
                                : string.Concat(accountName, "|", apiKey, "|", eventTrackingKey).ToShaBase64();

            if (ActiveCampaignSdkConfig.ClientPoolingDisabled || !_clientMap.ContainsKey(clientKey) || !_clientMap[clientKey].IsValid())
            {
                var pooledClient = ActiveCampaignSdkConfig.ClientPoolingDisabled
                                       ? GetOrCreatePooledClient(clientKey, null, accountName, apiKey, eventTrackingKey, eventTrackingAcctId)
                                       : _clientMap.AddOrUpdate(clientKey,
                                                                k => GetOrCreatePooledClient(k, null, accountName, apiKey, eventTrackingKey, eventTrackingAcctId),
                                                                (k, x) => GetOrCreatePooledClient(k, x, accountName, apiKey, eventTrackingKey, eventTrackingAcctId));

                return pooledClient.Client;
            }

            return _clientMap.GetOrAdd(clientKey, k => GetOrCreatePooledClient(k, null, accountName, apiKey, eventTrackingKey, eventTrackingAcctId))
                             .Client;
        }

        private PooledClient GetOrCreatePooledClient(string clientKey, PooledClient existingPooledClient, string accountName, string apiKey, string eventTrackingKey, string eventTrackingAcctId)
        {
            if (existingPooledClient != null && existingPooledClient.IsValid())
            {
                return existingPooledClient;
            }

            var pooledClient = existingPooledClient ?? new PooledClient();

            pooledClient.CreatedOnUtc = DateTime.UtcNow;
            pooledClient.ClientKey = clientKey;

            var existingClient = pooledClient.Client;

            try
            {
                pooledClient.Client = _clientFactory((accountName, apiKey, eventTrackingKey, eventTrackingAcctId));

                pooledClient.Valid = true;
            }
            catch(Exception)
            {
                // TODO: Log exception
            }

            existingClient?.Dispose();

            if (!pooledClient.IsValid())
            {
                pooledClient.Client?.Dispose();
                pooledClient.Client = null;
            }

            return pooledClient;
        }

        private class PooledClient
        {
            public IActiveCampaignClient Client { get; set; }
            public DateTime CreatedOnUtc { get; set; }
            public string ClientKey { get; set; }
            public bool Valid { get; set; }

            public bool IsValid()
                => Valid && Client != null;
        }

        public void Dispose()
        {
            if (_clientMap == null || _clientMap.Count <= 0)
            {
                return;
            }

            var clientKeys = _clientMap.Keys.ToList();

            foreach (var clientKey in clientKeys)
            {
                if (!_clientMap.TryRemove(clientKey, out var client) || client?.Client == null)
                {
                    continue;
                }

                try
                {
                    client.Client.Dispose();
                }
                catch
                { /* ignore */
                }

                client.Client = null;
                client = null;
            }
        }
    }
}
