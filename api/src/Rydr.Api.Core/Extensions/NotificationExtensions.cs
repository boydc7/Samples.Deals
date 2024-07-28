using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using ServiceStack;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Extensions;

public static class NotificationExtensions
{
    private static readonly IDeferRequestsService _deferRequestsService = RydrEnvironment.Container.Resolve<IDeferRequestsService>();
    private static readonly ILog _log = LogManager.GetLogger("NotificationExtensions");

    public static async Task TrySendAppNotificationAsync(this IOpsNotificationService service, string subject, string message)
    {
        try
        {
            await service.SendAppNotificationAsync(subject, message);
        }
        catch(Exception ex)
        {
            _log.Exception(ex);
        }
    }

    public static async Task TrySendApiNotificationAsync(this IOpsNotificationService service, string subject, string message)
    {
        try
        {
            await service.SendApiNotificationAsync(subject, message);
        }
        catch(Exception ex)
        {
            _log.Exception(ex);
        }
    }

    public static async Task TrackEventNotificationAsync(this IWorkspaceService workspaceService, long workspaceId,
                                                         string eventName, string eventData, params ExternalCrmUpdateItem[] updateItems)
        => TrackEventNotification(await workspaceService.TryGetWorkspaceAsync(workspaceId),
                                  await workspaceService.TryGetWorkspacePrimaryEmailAddressAsync(workspaceId),
                                  eventName, eventData, updateItems);

    private static void TrackEventNotification(DynWorkspace dynWorkspace, string toUserEmail,
                                               string eventName, string eventData,
                                               params ExternalCrmUpdateItem[] updateItems)
    {
        if (dynWorkspace == null || !dynWorkspace.WorkspaceType.HasExternalCrmIntegration() || toUserEmail.IsNullOrEmpty())
        {
            return;
        }

        _deferRequestsService.DeferFifoRequest(new PostTrackEventNotification
                                               {
                                                   EventName = eventName,
                                                   EventData = eventData,
                                                   UserEmail = toUserEmail,
                                                   RelatedUpdateItems = updateItems.IsNullOrEmpty()
                                                                            ? null
                                                                            : updateItems.ToList()
                                               });
    }
}
