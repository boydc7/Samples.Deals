using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Messages;

public class CompositeServerNotificationService : IServerNotificationService
{
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IRecordTypeRecordService _recordTypeRecordService;
    private readonly List<IServerNotificationService> _serverNotificationServices;

    public CompositeServerNotificationService(IEnumerable<IServerNotificationService> serverNotificationServices,
                                              IPublisherAccountService publisherAccountService,
                                              IRecordTypeRecordService recordTypeRecordService)
    {
        _publisherAccountService = publisherAccountService;
        _recordTypeRecordService = recordTypeRecordService;
        _serverNotificationServices = serverNotificationServices.AsList();
    }

    public async Task NotifyAsync(ServerNotification message, RecordTypeId notifyRecordId = null)
    {
        Guard.AgainstArgumentOutOfRange(message == null || (notifyRecordId == null && message.To == null), "Message and notification info invalid");
        Guard.AgainstArgumentOutOfRange(message.ServerNotificationType == ServerNotificationType.Unspecified, "ServerNotificationType");

        await DoNotifyMemberAsync(message, notifyRecordId);
    }

    public async Task SubscribeAsync(long userId, string token, string oldTokenHash)
    {
        foreach (var serverNotificationService in _serverNotificationServices)
        {
            await serverNotificationService.SubscribeAsync(userId, token, oldTokenHash);
        }
    }

    public async Task UnsubscribeAsync(string tokenHash)
    {
        foreach (var serverNotificationService in _serverNotificationServices)
        {
            await serverNotificationService.UnsubscribeAsync(tokenHash);
        }
    }

    private async Task DoNotifyMemberAsync(ServerNotification message, RecordTypeId notifyRecordId)
    {
        if (message.To == null)
        {
            var toPublisherAccountId = notifyRecordId != null && notifyRecordId.Type == RecordType.PublisherAccount
                                           ? notifyRecordId.Id
                                           : 0;

            var toPublisherAccount = toPublisherAccountId > 0
                                         ? await _publisherAccountService.TryGetPublisherAccountAsync(toPublisherAccountId)
                                         : null;

            if (toPublisherAccount == null || toPublisherAccount.IsDeleted())
            {
                return;
            }

            message.To = toPublisherAccount.ToPublisherAccountInfo();
        }

        // Just a performance shortcut - NOTE the missing use of .GetContextWorkspaceId() here
        if (message.To.RydrAccountType.IsInfluencer())
        {
            message.InWorkspaceId = 0;
        }
        else if (message.InWorkspaceId <= 0 && message.ForRecord != null)
        { // Need to determine what the to workpsace should be
            var forRecord = await _recordTypeRecordService.GetRecordAsync<ICanBeRecordLookup>(message.ForRecord, isInternal: true);

            message.InWorkspaceId = forRecord.WorkspaceId;
        }

        if (message.InWorkspaceId > 0)
        {
            message.InWorkspaceId = message.To.GetContextWorkspaceId(message.InWorkspaceId);
        }

        message.Title = message.Title?
                               .Replace(ServerNotificationTokens.FromPublisherAccountUserName, message.From?.UserName ?? string.Empty)
                               .Replace(ServerNotificationTokens.ToPublisherAccountUserName, message.To?.UserName ?? string.Empty)
                               .Replace(ServerNotificationTokens.WhiteGreenCheckMark, EmojiCodePairs.WhiteGreenCheckMark)
                               .Replace(ServerNotificationTokens.EmojiPartyPopper, EmojiCodePairs.PartyPopper)
                               .Replace(ServerNotificationTokens.EmojiHandshake, EmojiCodePairs.Handshake)
                               .Replace(ServerNotificationTokens.WarningSign, EmojiCodePairs.WarningSign)
                               .Replace(ServerNotificationTokens.HeavyExclamation, EmojiCodePairs.HeavyExclamation);

        message.Message = message.Message?
                                 .Replace(ServerNotificationTokens.FromPublisherAccountUserName, message.From?.UserName ?? string.Empty)
                                 .Replace(ServerNotificationTokens.ToPublisherAccountUserName, message.To?.UserName ?? string.Empty)
                                 .Replace(ServerNotificationTokens.WhiteGreenCheckMark, EmojiCodePairs.WhiteGreenCheckMark)
                                 .Replace(ServerNotificationTokens.EmojiPartyPopper, EmojiCodePairs.PartyPopper)
                                 .Replace(ServerNotificationTokens.EmojiHandshake, EmojiCodePairs.Handshake)
                                 .Replace(ServerNotificationTokens.WarningSign, EmojiCodePairs.WarningSign)
                                 .Replace(ServerNotificationTokens.HeavyExclamation, EmojiCodePairs.HeavyExclamation);

        foreach (var serverNotificationService in _serverNotificationServices)
        {
            await serverNotificationService.NotifyAsync(message, notifyRecordId);
        }
    }
}
