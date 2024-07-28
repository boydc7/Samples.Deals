using Rydr.ActiveCampaign.Models;

namespace Rydr.ActiveCampaign;

public partial class ActiveCampaignClient
{
    public async Task<AcTag> GetTagAsync(long tagId)
    {
        var tag = await GetAsync<GetAcTag>($"tags/{tagId}").ConfigureAwait(false);

        return tag?.Tag;
    }

    public async Task<AcTag> GetTagByNameAsync(string tagName)
    {
        var tag = await GetAsync<GetAcTags>("tags", new
                                                    {
                                                        tag = tagName
                                                    }).ConfigureAwait(false);

        return tag?.Tags?.FirstOrDefault(t => t.Tag.Equals(tagName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AcTag> PostTagAsync(AcTag tag)
    {
        var response = await PostAsync("tags", bodyContent: new PostAcTag
                                                            {
                                                                Tag = tag
                                                            }).ConfigureAwait(false);

        return response?.Tag;
    }
}
