using System.Collections.Concurrent;
using Rydr.FbSdk.Configuration;
using Rydr.FbSdk.Extensions;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Rydr.FbSdk;

public class InstagramBaseClientFactory
{
    private readonly ConcurrentDictionary<string, PooledClient> _clientMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<(string appId, string appSecret, string accessToken), IInstagramBasicClient> _clientFactory;

    private InstagramBaseClientFactory()
    {
        _clientFactory = FacebookSdkConfig.UseLoggedClient && FacebookSdkConfig.InstagramBasicClientFactory == null
                             ? t => new LoggedInstagramBasicClient(t.appId, t.appSecret, t.accessToken)
                             : FacebookSdkConfig.InstagramBasicClientFactory ?? GetDefaultClient;
    }

    public static InstagramBaseClientFactory Instance { get; } = new();

    private IInstagramBasicClient GetDefaultClient((string appId, string appSecret, string accessToken) clientParams)
        => new InstagramBasicClient(clientParams.appId, clientParams.appSecret, clientParams.accessToken);

    public IInstagramBasicClient GetOrCreateClient(string appId, string appSecret, string accessToken)
    {
        var clientKey = FacebookSdkConfig.ClientPoolingDisabled
                            ? string.Empty
                            : string.Concat(appId, "|", appSecret, "|", accessToken).ToShaBase64();

        if (FacebookSdkConfig.ClientPoolingDisabled || !_clientMap.ContainsKey(clientKey) || !_clientMap[clientKey].IsValid())
        {
            var pooledClient = FacebookSdkConfig.ClientPoolingDisabled
                                   ? GetOrCreatePooledClient(clientKey, null, appId, appSecret, accessToken)
                                   : _clientMap.AddOrUpdate(clientKey,
                                                            k => GetOrCreatePooledClient(k, null, appId, appSecret, accessToken),
                                                            (k, x) => GetOrCreatePooledClient(k, x, appId, appSecret, accessToken));

            return pooledClient.Client;
        }

        return _clientMap.GetOrAdd(clientKey, k => GetOrCreatePooledClient(k, null, appId, appSecret, accessToken))
                         .Client;
    }

    private PooledClient GetOrCreatePooledClient(string clientKey, PooledClient existingPooledClient, string appId, string appSecret, string accessToken)
    {
        if (existingPooledClient != null && existingPooledClient.IsValid())
        {
            return existingPooledClient;
        }

        var pooledClient = existingPooledClient ?? new PooledClient();

        pooledClient.CreatedOnUtc = DateTime.UtcNow;
        pooledClient.ClientKey = clientKey;

        var existingFbClient = pooledClient.Client;

        try
        {
            pooledClient.Client = _clientFactory((appId, appSecret, accessToken));

            // TODO: Authenticate?

            pooledClient.Valid = true;
        }
        catch(Exception)
        {
            // TODO: Log exception
        }

        existingFbClient?.Dispose();

        if (!pooledClient.IsValid())
        {
            pooledClient.Client?.Dispose();
            pooledClient.Client = null;
        }

        return pooledClient;
    }

    private class PooledClient
    {
        public IInstagramBasicClient Client { get; set; }
        public DateTime CreatedOnUtc { get; set; }
        public string ClientKey { get; set; }
        public bool Valid { get; set; }

        public bool IsValid()
            => Valid && Client != null;
    }
}
