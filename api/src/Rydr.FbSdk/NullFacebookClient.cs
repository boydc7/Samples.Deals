namespace Rydr.FbSdk;

public class NullFacebookClient : FacebookClient
{
    public NullFacebookClient(string appId, string appSecret, string accessToken)
        : base(appId, appSecret, accessToken) { }

    protected override Task<T> GetAsync<T>(string path, object parameters = null, bool eTag = false)
        => Task.FromResult(default(T));

    protected override Task<T> PostAsync<T>(string path, object parameters = null, string withSecretProof = null)
        => Task.FromResult(default(T));

#pragma warning disable 1998
    protected override async IAsyncEnumerable<List<T>> GetPagedAsync<T>(string initialPath, object parameters = null, bool eTag = false)
#pragma warning restore 1998
    {
        yield break;
    }

    protected override IEnumerable<T> GetPaged<T>(string initialPath, object parameters = null) => Enumerable.Empty<T>();
}
