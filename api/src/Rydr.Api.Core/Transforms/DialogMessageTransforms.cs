using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Transforms;

public static class DialogMessageTransforms
{
    public static Dialog ToDialog(this DynDialog source)
    {
        if (source == null)
        {
            throw new RecordNotFoundException();
        }

        var result = source.ConvertTo<Dialog>();

        result.Id = source.DialogId;

        // Members on the server are RecordTypeId combinations...on the client currently we want PublisherAccountIds of each of those contacts...
        result.Members = source.PublisherAccountIds?
                               .Select(pid => new DialogMember
                                              {
                                                  Record = new RecordTypeId(RecordType.PublisherAccount, pid)
                                              })
                               .AsList();

        return result;
    }

    public static async Task<DialogMessage> ToDialogMessageAsync(this DynDialogMessage source)
    {
        if (source == null)
        {
            throw new RecordNotFoundException();
        }

        var result = source.ConvertTo<DialogMessage>();

        result.Id = source.MessageId;
        result.DialogId = source.DialogId;
        result.Message = source.Message.ToString();
        result.IsRead = true;
        result.SentOn = source.CreatedOn;

        if (source.SentByPublisherAccountId <= 0)
        {
            return result;
        }

        var dynPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                           .TryGetPublisherAccountAsync(source.SentByPublisherAccountId);

        result.PublisherAccount = dynPublisherAccount.ToPublisherAccountInfo();

        return result;
    }

    public static DynDialogMessage ToDynDialogMessage(this SendMessage source, DynDialogMessage existingBeingUpdated = null, ISequenceSource sequenceSource = null)
    {
        var to = source.ConvertTo<DynDialogMessage>();

        if (existingBeingUpdated == null)
        { // New one
            to.DynItemType = DynItemType.Message;
            to.UpdateDateTimeTrackedValues();
        }
        else
        {
            to.DynItemType = existingBeingUpdated.DynItemType;
            to.DialogId = existingBeingUpdated.DialogId;
            to.UpdateDateTimeDeleteTrackedValues(existingBeingUpdated);
        }

        // MessageId is the edge, Dialog is id
        if (to.MessageId <= 0)
        {
            to.MessageId = existingBeingUpdated != null && existingBeingUpdated.MessageId > 0
                               ? existingBeingUpdated.MessageId
                               : (sequenceSource ?? Sequences.Provider).Next();
        }

        to.SentByPublisherAccountId = source.SentByPublisherAccountId.Gz(existingBeingUpdated?.SentByPublisherAccountId ?? 0);
        to.Message = source.Message;

        return to;
    }

    public static DynDialogMember ToDialogMember(this RecordTypeId memberRecord, long dialogId)
    {
        var dynDialogMember = new DynDialogMember
                              {
                                  MemberId = memberRecord.Id,
                                  IdRecordType = memberRecord.Type,
                                  DialogId = dialogId,
                                  EdgeRecordType = RecordType.Dialog,
                                  DynItemType = DynItemType.DialogMember,
                                  ReferenceId = DateTimeHelper.UtcNowTs.ToStringInvariant(),
                              };

        dynDialogMember.UpdateDateTimeTrackedValues();

        return dynDialogMember;
    }

    public static async Task<SendMessage> ToSendMessageAsync(this PostMessage source)
    {
        var members = new HashSet<RecordTypeId>();
        var sentByPublisherAccountId = 0L;
        RecordTypeId forRecord = null;
        var forRecordAccountsAdded = false;

        // Froms that are user/pub accounts become part of the membership - otherwise, imply the from as the current context state
        if (source.From == null || source.From.IsUserOrAccountRecordType())
        {
            var fromMember = source.From ?? (source.RequestPublisherAccountId > 0
                                                 ? new RecordTypeId(RecordType.PublisherAccount, source.RequestPublisherAccountId)
                                                 : new RecordTypeId(RecordType.User, source.UserId));

            members.Add(fromMember);

            if (fromMember.Type == RecordType.PublisherAccount)
            {
                sentByPublisherAccountId = fromMember.Id;
            }
        }
        else
        {
            forRecord = source.From;

            if (source.From != null)
            {
                // If we can get but a single accountId from the From record, the becomes the sentBy...
                var forRecordPublisherAccountId = await GetRecordTypePublisherAccountIdAsync(forRecord);

                if (forRecordPublisherAccountId > 0)
                {
                    members.Add(new RecordTypeId(RecordType.PublisherAccount, forRecordPublisherAccountId));
                    sentByPublisherAccountId = forRecordPublisherAccountId;
                    forRecordAccountsAdded = true;
                }
            }
        }

        // If the To record is a user/publisher, it's part of the membership
        // If not a usre/publisher, and the From record didn't handle to forRecord, use the To record (if not a user/pub)
        if (source.To.IsUserOrAccountRecordType())
        {
            members.Add(source.To);
        }
        else if (forRecord == null)
        {
            forRecord = source.To;
        }

        // Add in the reference record if appropriate and any associated publisher accountids
        if (forRecord != null)
        {
            if (!forRecordAccountsAdded)
            {
                var recordTypeId = await GetRecordTypePublisherAccountIdAsync(forRecord);

                if (recordTypeId > 0)
                {
                    members.Add(new RecordTypeId(RecordType.PublisherAccount, recordTypeId));
                }
            }

            members.Add(forRecord);
        }

        var sendMessage = new SendMessage
                          {
                              Message = source.Message,
                              Members = members,
                              ForRecord = forRecord,
                              SentByPublisherAccountId = sentByPublisherAccountId.Gz(source.RequestPublisherAccountId)
                          };

        return sendMessage;
    }

    public static bool IsUserOrAccountRecordType(this RecordTypeId source)
        => (source != null && source.Type == RecordType.User) || source.Type == RecordType.PublisherAccount;

    public static async Task<long> GetRecordTypePublisherAccountIdAsync(this RecordTypeId source)
    {
        if (source == null)
        {
            return 0;
        }

        switch (source.Type)
        {
            case RecordType.Deal:
                var deal = await DealExtensions.DefaultDealService
                                               .GetDealAsync(source.Id, true);

                if (deal != null && deal.PublisherAccountId > 0)
                {
                    return deal.PublisherAccountId;
                }

                break;

            default:
                return 0;
        }

        return 0;
    }

    public static string ToDialogKey(this IEnumerable<RecordTypeId> members)
        => ToDialogKey(members.Select(m => m.Id));

    public static string ToDialogKey(this IEnumerable<long> members)
        => string.Join(":", members.Distinct()
                                   .OrderBy(i => i))
                 .ToShaBase64();
}
