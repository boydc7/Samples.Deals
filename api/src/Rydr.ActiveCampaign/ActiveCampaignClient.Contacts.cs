using System.Globalization;
using Rydr.ActiveCampaign.Models;

namespace Rydr.ActiveCampaign;

public partial class ActiveCampaignClient
{
    public async Task<AcContact> GetContactAsync(long contactId)
    {
        var contact = await GetAsync<GetAcContact>($"contacts/{contactId}").ConfigureAwait(false);

        return contact?.Contact;
    }

    public async Task<AcContact> GetContactByEmailAsync(string email)
    {
        var contact = await GetAsync<GetAcContacts>("contacts", new
                                                                {
                                                                    email
                                                                }).ConfigureAwait(false);

        return contact?.Contacts?.FirstOrDefault(c => c.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AcContact> PostUpsertContactAsync(AcContact contact)
    {
        var response = await PostAsync("contact/sync", bodyContent: new PostAcContact
                                                                    {
                                                                        Contact = contact
                                                                    }).ConfigureAwait(false);

        return response?.Contact;
    }

    public async Task<AcContactCustomField> GetContactCustomFieldAsync(long contactCustomFieldId)
    {
        var contact = await GetAsync<GetAcContactCustomField>($"fields/{contactCustomFieldId}").ConfigureAwait(false);

        return contact?.Field;
    }

    public async Task<AcContactCustomField> GetContactCustomFieldByTitleAsync(string title)
    {
        var contact = await GetAsync<GetAcContactCustomFields>("fields", new
                                                                         {
                                                                             title
                                                                         }).ConfigureAwait(false);

        return contact?.Fields?.FirstOrDefault(f => f.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AcContactCustomField> PostContactCustomFieldAsync(AcContactCustomField contactField)
    {
        var response = await PostAsync("fields", bodyContent: new PostAcContactCustomField
                                                              {
                                                                  Field = contactField
                                                              }).ConfigureAwait(false);

        return response?.Field;
    }

    public async Task<AcContactCustomFieldValue> GetContactCustomFieldValueAsync(long contactCustomFieldValueId)
    {
        var contact = await GetAsync<GetAcContactCustomFieldValue>($"fieldValues/{contactCustomFieldValueId}").ConfigureAwait(false);

        return contact?.FieldValue;
    }

    public async Task<AcContactCustomFieldValue> GetContactCustomFieldValueByContactFieldAsync(long contactId, long fieldId)
    {
        var contact = await GetAsync<GetAcContactCustomFieldValues>($"contacts/{contactId}/fieldValues", new
                                                                                                         {
                                                                                                             fieldid = fieldId
                                                                                                         }).ConfigureAwait(false);

        return contact?.FieldValues?.FirstOrDefault(fv => fv.Field.Equals(fieldId.ToString(CultureInfo.InvariantCulture),
                                                                          StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<AcContactTag>> GetContactTagsAsync(long contactId)
    {
        var tags = await GetAsync<GetAcContactTags>($"contacts/{contactId}/contactTags").ConfigureAwait(false);

        return tags?.ContactTags;
    }

    public async Task<AcContactCustomFieldValue> PostContactCustomFieldValueAsync(AcContactCustomFieldValue contactFieldValue)
    {
        var response = await PostAsync("fieldValues", bodyContent: new PostAcContactCustomFieldValue
                                                                   {
                                                                       FieldValue = contactFieldValue
                                                                   }).ConfigureAwait(false);

        return response?.FieldValue;
    }

    public async Task<AcContactTag> PostContactTagAsync(AcContactTag contactTag)
    {
        var response = await PostAsync("contactTags", bodyContent: new PostAcContactTag
                                                                   {
                                                                       ContactTag = contactTag
                                                                   }).ConfigureAwait(false);

        return response?.ContactTag;
    }

    public Task DeleteContactTagAsync(long contactTagId)
        => DeleteAsync($"contactTags/{contactTagId}");
}
