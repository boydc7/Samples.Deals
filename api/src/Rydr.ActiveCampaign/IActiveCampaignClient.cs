using Rydr.ActiveCampaign.Models;

namespace Rydr.ActiveCampaign;

public interface IActiveCampaignClient : IDisposable
{
    Task<AcContact> GetContactAsync(long contactId);
    Task<AcContact> GetContactByEmailAsync(string email);
    Task<AcContact> PostUpsertContactAsync(AcContact contact);

    Task<AcContactCustomField> GetContactCustomFieldAsync(long contactCustomFieldId);
    Task<AcContactCustomField> GetContactCustomFieldByTitleAsync(string title);
    Task<AcContactCustomField> PostContactCustomFieldAsync(AcContactCustomField contactField);

    Task<AcContactCustomFieldValue> GetContactCustomFieldValueAsync(long contactCustomFieldValueId);
    Task<AcContactCustomFieldValue> GetContactCustomFieldValueByContactFieldAsync(long contactId, long fieldId);
    Task<AcContactCustomFieldValue> PostContactCustomFieldValueAsync(AcContactCustomFieldValue contactFieldValue);

    Task<IReadOnlyList<AcContactTag>> GetContactTagsAsync(long contactId);
    Task<AcContactTag> PostContactTagAsync(AcContactTag contactTag);
    Task DeleteContactTagAsync(long contactTagId);

    Task<AcTag> GetTagAsync(long tagId);
    Task<AcTag> GetTagByNameAsync(string tagName);
    Task<AcTag> PostTagAsync(AcTag tag);

    Task<AcEventTrackingEvent> GetEventTrackingEventAsync(string eventName);

    IAsyncEnumerable<IReadOnlyList<AcAutomation>> GetAutomationsAsync();
    IAsyncEnumerable<IEnumerable<AcAutomation>> GetAutomationsContainingAsync(string nameContaining);
    Task PostContactAutomationAsync(AcContactAutomation contactAutomation);
}
