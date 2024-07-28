using Rydr.FbSdk.Models;

namespace Rydr.FbSdk;

public partial class InstagramBasicClient
{
    public async IAsyncEnumerable<List<IgMedia>> GetBasicIgAccountMediaAsync(int pageLimit = 50)
    {
        var param = new
                    {
                        fields = GetFieldStringForType<IgMedia>(),
                        limit = pageLimit
                    };

        await foreach (var accountMedias in GetPagedAsync<IgMedia>("me/media", param, true).ConfigureAwait(false))
        {
            yield return accountMedias;
        }
    }

    public async Task<IgMedia> GetBasicIgMediaAsync(string igMediaId)
    {
        var media = await GetAsync<IgMedia>(igMediaId,
                                            new
                                            {
                                                fields = GetFieldStringForType<IgMedia>()
                                            },
                                            true);

        return media;
    }
}
