using Rydr.ActiveCampaign;
using Rydr.ActiveCampaign.Models;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Messages;
using ServiceStack;

namespace Rydr.Api.Services.Services;

public class NotificationServiceInternal : BaseInternalOnlyApiService
{
    private readonly IOpsNotificationService _opsNotificationService;
    private readonly IDeferRequestsService _deferRequestsService;

    public NotificationServiceInternal(IOpsNotificationService opsNotificationService,
                                       IDeferRequestsService deferRequestsService)
    {
        _opsNotificationService = opsNotificationService;
        _deferRequestsService = deferRequestsService;
    }

    public async Task Post(PostTrackEventNotification request)
    {
        if (request.UserEmail.IsNullOrEmpty() || request.EventName.IsNullOrEmpty())
        {
            return;
        }

        await _opsNotificationService.SendTrackEventNotificationAsync(request.EventName, request.UserEmail, request.EventData);

        if (!request.RelatedUpdateItems.IsNullOrEmpty())
        {
            _deferRequestsService.DeferRequest(new PostExternalCrmContactUpdate
                                               {
                                                   UserEmail = request.UserEmail,
                                                   Items = request.RelatedUpdateItems
                                               });
        }
    }

    public async Task Post(PostExternalCrmContactUpdate request)
    {
        if (request.UserEmail.IsNullOrEmpty() || request.Items.IsNullOrEmpty())
        {
            return;
        }

        var acClient = ActiveCampaignClientFactory.Instance.GetOrCreateRydrClient();

        var acContact = await acClient.GetContactByEmailAsync(request.UserEmail);

        if (acContact == null)
        {
            _log.WarnFormat("No ActiveCampaign contact found for email [{0}]", request.UserEmail);

            return;
        }

        foreach (var updateItem in request.Items)
        {
            var acField = await acClient.GetContactCustomFieldByTitleAsync(updateItem.FieldName);

            if ((acField?.Id).IsNullOrEmpty())
            {
                _log.WarnFormat("No ActiveCampaign contact field found named [{0}]", updateItem.FieldName);

                continue;
            }

            var existingValue = await acClient.GetContactCustomFieldValueByContactFieldAsync(acContact.Id.ToLong(0), acField.Id.ToLong(0));

            if (updateItem.Remove && existingValue.TryRemoveDelimitedValue(updateItem.FieldValue, out var newRemovedFieldValue))
            {
                await acClient.PostContactCustomFieldValueAsync(new AcContactCustomFieldValue
                                                                {
                                                                    Contact = acContact.Id,
                                                                    Field = acField.Id,
                                                                    Value = newRemovedFieldValue
                                                                });
            }
            else if (!updateItem.Remove && existingValue.TryAddDelimitedValue(updateItem.FieldValue, out var newAddedFieldValue))
            {
                await acClient.PostContactCustomFieldValueAsync(new AcContactCustomFieldValue
                                                                {
                                                                    Contact = acContact.Id,
                                                                    Field = acField.Id,
                                                                    Value = newAddedFieldValue
                                                                });
            }
        }
    }
}
