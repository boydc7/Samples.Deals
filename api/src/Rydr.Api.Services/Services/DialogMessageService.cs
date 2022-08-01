using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Services.Services
{
    [RydrCacheResponse(900, "deals")]
    public class DialogMessageService : BaseAuthenticatedApiService
    {
        private readonly IDialogMessageService _messageService;
        private readonly IDialogService _dialogService;
        private readonly IAssociationService _associationService;

        public DialogMessageService(IDialogMessageService messageService,
                                    IDialogService dialogService,
                                    IAssociationService associationService)
        {
            _messageService = messageService;
            _dialogService = dialogService;
            _associationService = associationService;
        }

        public Task<OnlyResultsResponse<DialogMessage>> Get(GetDialogMessages request)
            => _messageService.GetDialogMessagesAsync(request.DialogId, request.SentBefore, request.SentAfter, request.SentBeforeId, request.SentAfterId)
                              .Skip(request.Skip)
                              .Take(request.Take.Gz(50))
                              .AsOnlyResultsResponseAsync();

        public async Task<OnlyResultResponse<Dialog>> Get(GetDialog request)
        {
            var dialogId = request.Id > 0
                               ? request.Id
                               : (await _dynamoDb.GetItemEdgeIndexAsync(DynItemType.Dialog,
                                                                        new[]
                                                                        {
                                                                            request.From, request.To
                                                                        }.ToDialogKey(),
                                                                        true)
                                 ).Id;

            var dialog = await _dialogService.GetDialogAsync(dialogId);

            dialog.ForRecords = await _associationService.GetAssociationsToAsync(dialogId, toRecordType: RecordType.Dialog)
                                                         .Select(a => new RecordTypeId(a.IdRecordType, a.Id))
                                                         .ToList();

            return dialog.AsOnlyResultResponse();
        }

        public async Task<OnlyResultsResponse<Dialog>> Get(GetDialogs request)
        {
            var results = new List<Dialog>(request.Take.Gz(50));

            await foreach (var dialog in _dialogService.GetDialogsAsync(request.ForRecord, request.Type, request.ForWorkspaceId,
                                                                        request.Skip, request.Take))
            {
                dialog.ForRecords = await _associationService.GetAssociationsToAsync(dialog.Id, toRecordType: RecordType.Dialog)
                                                             .Select(a => new RecordTypeId(a.IdRecordType, a.Id))
                                                             .ToList();

                results.Add(dialog);
            }

            return results.OrderByDescending(r => r.LastMessageSentOn ?? r.CreatedOn)
                          .AsOnlyResultsResponse();
        }

        public async Task<OnlyResultResponse<DialogMessageIds>> Post(PostMessage request)
        {
            var sendMessage = await request.ToSendMessageAsync();

            var sendResponse = await _messageService.SendMessageAsync(sendMessage);

            return sendResponse.AsOnlyResultResponse();
        }

        public async Task<LongIdResponse> Post(PostDialogMessage request)
        {
            var result = await _messageService.SendMessageAsync(new SendMessage
                                                                {
                                                                    Message = request.Message,
                                                                    DialogId = request.DialogId
                                                                });

            return result.Id.ToLongIdResponse();
        }

        public Task Put(PutMessageRead request)
            => _messageService.MarkMessageReadAsync(request.DialogId, request.Id);

        public async Task Put(PutDialogRead request)
        {
            await _dialogService.MarkDialogReadAsync(request.Id);

            if (request.RequestPublisherAccountId > 0)
            {
                // Delete any notification for the dialog as well, since it's been read
                await _dynamoDb.DeleteItemAsync<DynNotification>(request.RequestPublisherAccountId,
                                                                 DynNotification.BuildEdgeId(ServerNotificationType.Dialog,
                                                                                             RecordTypeId.GetIdString(RecordType.Dialog, request.Id)));
            }
        }
    }
}
