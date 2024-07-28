namespace Rydr.ActiveCampaign;

public class NullActiveCampaignClient : ActiveCampaignClient
{
    public NullActiveCampaignClient(string accountName, string apiKey, string eventTrackingKey, string eventTrackingAcctId)
        : base(accountName, apiKey, eventTrackingKey, eventTrackingAcctId) { }

    protected override Task<T> GetAsync<T>(string path, object parameters = null, int forceLimit = 0, int forceOffset = 0)
        => Task.FromResult(default(T));

    protected override Task<T> PostAsync<T>(string path, object filters = null, T bodyContent = null)
        where T : class
        => Task.FromResult(default(T));

    protected override Task<T> PutAsync<T>(string path, object filters = null, T bodyContent = null)
        where T : class
        => Task.FromResult(default(T));

    protected override Task DeleteAsync(string path)
        => Task.CompletedTask;

#pragma warning disable 1998
    protected override async IAsyncEnumerable<IReadOnlyList<T>> GetPagedAsync<TRequest, T>(string path, int limit, object parameters = null)
#pragma warning restore 1998
    {
        yield break;
    }
}
