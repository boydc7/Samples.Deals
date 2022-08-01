using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services.Messages
{
    public class DynamoPersistDialogMessageService : IDialogMessageService
    {
        private readonly IDialogService _dialogService;
        private readonly IPocoDynamo _dynamoDb;

        public DynamoPersistDialogMessageService(IDialogService dialogService,
                                                 IPocoDynamo dynamoDb)
        {
            _dialogService = dialogService;
            _dynamoDb = dynamoDb;
        }

        public async Task<DialogMessageIds> SendMessageAsync(SendMessage request)
        {
            var dialog = await _dialogService.GetOrCreateDialogAsync(request.Members, request.DialogId, request.ForRecord);

            request.DialogId = dialog.Id;

            var message = request.ToDynDialogMessage();

            await _dynamoDb.PutItemTrackDeferAsync(message, RecordType.Message, message.MessageId);

            return new DialogMessageIds
                   {
                       DialogId = message.DialogId,
                       Id = message.MessageId,
                       CreatedBy = message.CreatedBy,
                       DialogKey = dialog.DialogKey
                   };
        }

        public Task MarkMessageReadAsync(long dialogId, long messageId) => Task.CompletedTask;

        public Task<DynDialogMessage> GetLastMessageAsync(long dialogId)
            => _dynamoDb.FromQuery<DynDialogMessage>(k => k.Id == dialogId)
                        .Filter(m => m.DeletedOnUtc == null &&
                                     m.TypeId == (int)DynItemType.Message)
                        .ExecAsync()
                        .FirstOrDefaultAsync()
                        .AsTask();

        public Task<DynDialogMessage> GetMessageAsync(long messageId)
            => _dynamoDb.GetItemByEdgeIntoAsync<DynDialogMessage>(DynItemType.Message, messageId.ToEdgeId(), true);

        public async IAsyncEnumerable<DialogMessage> GetDialogMessagesAsync(long dialogId, long sentBefore = 0, long sentAfter = 0, long sentBeforeId = 0, long sentAfterId = 0)
        {
            var dynQuery = _dynamoDb.FromQuery<DynDialogMessage>(m => m.Id == dialogId);

            if (sentBeforeId > 0 && sentAfterId > 0)
            {
                dynQuery.KeyCondition("EdgeId between :sentAfterId and :sentBeforeId", new
                                                                                       {
                                                                                           sentAfterId = sentAfterId.ToEdgeId(),
                                                                                           sentBeforeId = sentBeforeId.ToEdgeId()
                                                                                       });
            }
            else if (sentBeforeId > 0)
            {
                dynQuery.KeyCondition("EdgeId < :sentBeforeId", new
                                                                {
                                                                    sentBeforeId = sentBeforeId.ToEdgeId()
                                                                });
            }
            else if (sentAfterId > 0)
            {
                dynQuery.KeyCondition("EdgeId > :sentAfterId", new
                                                               {
                                                                   sentAfterId = sentAfterId.ToEdgeId()
                                                               });
            }

            dynQuery.Filter(m => m.DeletedOnUtc == null &&
                                 m.TypeId == (int)DynItemType.Message);

            if (sentAfter > 0)
            {
                dynQuery.Filter(m => m.CreatedOnUtc > sentAfter);
            }

            if (sentBefore > 0)
            {
                dynQuery.Filter(m => m.CreatedOnUtc < sentBefore);
            }

            await foreach (var dynDialogMessage in dynQuery.ExecAsync())
            {
                if (sentBeforeId > 0 && sentAfterId > 0)
                {
                    if (dynDialogMessage.MessageId > sentAfterId && dynDialogMessage.MessageId < sentBeforeId)
                    {
                        var filteredDialogMessage = await dynDialogMessage.ToDialogMessageAsync();

                        yield return filteredDialogMessage;
                    }
                    else
                    {
                        continue;
                    }
                }

                var dialogMessage = await dynDialogMessage.ToDialogMessageAsync();

                yield return dialogMessage;
            }
        }
    }
}
