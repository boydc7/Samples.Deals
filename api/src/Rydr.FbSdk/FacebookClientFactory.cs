using System.Collections.Concurrent;
using Rydr.FbSdk.Configuration;
using Rydr.FbSdk.Extensions;
using ServiceStack;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Rydr.FbSdk;

public class FacebookClientFactory
{
    private readonly ConcurrentDictionary<string, PooledClient> _clientMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<(string appId, string appSecret, string accessToken, string apiVersion), IFacebookClient> _clientFactory;

    private FacebookClientFactory()
    {
        _clientFactory = FacebookSdkConfig.UseLoggedClient && FacebookSdkConfig.ClientFactory == null
                             ? t => new LoggedFacebookClient(t.appId, t.appSecret, t.accessToken)
                             : FacebookSdkConfig.ClientFactory ?? GetDefaultClient;
    }

    public static FacebookClientFactory Instance { get; } = new();

    private IFacebookClient GetDefaultClient((string appId, string appSecret, string accessToken, string apiVersion) clientParams)
        => new FacebookClient(clientParams.appId, clientParams.appSecret, clientParams.accessToken);

    public IFacebookClient GetOrCreateClient(string appId, string appSecret, string accessToken, string apiVersion = null)
    {
        if (apiVersion.IsNullOrEmpty())
        {
            apiVersion = FacebookClient.ApiVersion;
        }

        var clientKey = FacebookSdkConfig.ClientPoolingDisabled
                            ? string.Empty
                            : string.Concat(appId, "|", appSecret, "|", accessToken, "|", apiVersion).ToShaBase64();

        if (FacebookSdkConfig.ClientPoolingDisabled || !_clientMap.ContainsKey(clientKey) || !_clientMap[clientKey].IsValid())
        {
            var pooledClient = FacebookSdkConfig.ClientPoolingDisabled
                                   ? GetOrCreatePooledClient(clientKey, null, appId, appSecret, accessToken, apiVersion)
                                   : _clientMap.AddOrUpdate(clientKey,
                                                            k => GetOrCreatePooledClient(k, null, appId, appSecret, accessToken, apiVersion),
                                                            (k, x) => GetOrCreatePooledClient(k, x, appId, appSecret, accessToken, apiVersion));

            return pooledClient.Client;
        }

        return _clientMap.GetOrAdd(clientKey, k => GetOrCreatePooledClient(k, null, appId, appSecret, accessToken, apiVersion))
                         .Client;
    }

    private PooledClient GetOrCreatePooledClient(string clientKey, PooledClient existingPooledClient,
                                                 string appId, string appSecret, string accessToken, string apiVersion)
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
            pooledClient.Client = _clientFactory((appId, appSecret, accessToken, apiVersion));

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
        public IFacebookClient Client { get; set; }
        public DateTime CreatedOnUtc { get; set; }
        public string ClientKey { get; set; }
        public bool Valid { get; set; }

        public bool IsValid()
            => Valid && Client != null;
    }
}
