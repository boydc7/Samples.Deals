using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Messages
{
    public class DynamoDialogService : IDialogService
    {
        private readonly IPocoDynamo _dynamoDb;
        private readonly IRequestStateManager _requestStateManager;
        private readonly IAssociationService _associationService;
        private readonly IRecordTypeRecordService _recordTypeRecordService;
        private readonly IRydrDataService _rydrDataService;

        public DynamoDialogService(IPocoDynamo dynamoDb, IRequestStateManager requestStateManager,
                                   IAssociationService associationService,
                                   IRecordTypeRecordService recordTypeRecordService,
                                   IRydrDataService rydrDataService)
        {
            _dynamoDb = dynamoDb;
            _requestStateManager = requestStateManager;
            _associationService = associationService;
            _recordTypeRecordService = recordTypeRecordService;
            _rydrDataService = rydrDataService;
        }

        public Task MarkDialogReadAsync(long dialogId) => Task.CompletedTask;

        public async Task<Dialog> GetDialogAsync(long dialogId)
        {
            var result = await _dynamoDb.GetItemByRefAsync<DynDialog>(dialogId, DynItemType.Dialog);

            return result.ToDialog();
        }

        public async Task<DynDialog> TryGetDynDialogAsync(IEnumerable<long> forMembers)
        {
            var dialogKey = forMembers.ToDialogKey();

            var dynDialog = await _dynamoDb.GetItemByEdgeIntoAsync<DynDialog>(DynItemType.Dialog, dialogKey, true);

            return dynDialog;
        }

        public async Task<Dialog> GetOrCreateDialogAsync(HashSet<RecordTypeId> members, long dialogId = 0, RecordTypeId forRecordTypeId = null)
        {
            if (dialogId > 0)
            {
                return await GetDialogAsync(dialogId);
            }

            var dynDialog = await TryGetDynDialogAsync(members.Select(m => m.Id));

            if (dynDialog != null)
            {
                return dynDialog.ToDialog();
            }

            var forRecordId = forRecordTypeId ?? members.FirstOrDefault(m => !m.IsUserOrAccountRecordType());

            var forRecord = await _recordTypeRecordService.TryGetRecordAsync<IHasNameAndIsRecordLookup>(forRecordId);

            var newDialog = new DynDialog
                            {
                                DialogId = Sequences.Next(),
                                DialogKey = members.ToDialogKey(), // EdgeId
                                Name = forRecord?.Name ?? string.Empty,
                                Members = members,
                                PublisherAccountIds = members.Where(m => m.Type == RecordType.PublisherAccount)
                                                             .Select(m => m.Id)
                                                             .AsHashSet(),
                                DialogType = members.Count > 2
                                                 ? DialogType.Group
                                                 : DialogType.OneToOne,
                                DynItemType = DynItemType.Dialog,
                                WorkspaceId = forRecord?.WorkspaceId ?? 0
                            };

            newDialog.ReferenceId = newDialog.DialogId.ToStringInvariant();

            // Only user/account types get entered as a DynDialogMember model
            await _dynamoDb.PutItemsAsync(members.Where(m => m.IsUserOrAccountRecordType())
                                                 .Select(mid => mid.ToDialogMember(newDialog.DialogId)));

            await _dynamoDb.PutItemTrackedAsync(newDialog);

            if (forRecordId != null && forRecordId.Id > 0)
            {
                await _associationService.AssociateAsync(forRecordId.Type, forRecordId.Id, RecordType.Dialog, newDialog.DialogId);
            }

            return newDialog.ToDialog();
        }

        public async IAsyncEnumerable<Dialog> GetDialogsAsync(RecordTypeId forRecordTypeId = null, DialogType type = DialogType.Unknown, long forWorkspaceId = 0,
                                                              int skip = 0, int take = 50)
        {
            if (forRecordTypeId == null || forRecordTypeId.Id <= 0)
            {
                await foreach (var dialog in GetDialogsAsync(forWorkspaceId, skip, take))
                {
                    if (type != DialogType.Unknown && dialog.DialogType != type)
                    {
                        continue;
                    }

                    yield return dialog.ToDialog();
                }
            }
            else
            {
                await foreach (var dialogId in _associationService.GetAssociatedIdsAsync(forRecordTypeId, RecordType.Dialog)
                                                                  .Skip(skip.Gz(0))
                                                                  .Take(take.Gz(50)))
                {
                    var dialog = await _dynamoDb.GetItemByRefAsync<DynDialog>(dialogId, DynItemType.Dialog);

                    if (type != DialogType.Unknown && dialog.DialogType != type)
                    {
                        continue;
                    }

                    yield return dialog.ToDialog();
                }
            }
        }

        public async IAsyncEnumerable<DynDialog> GetDialogsAsync(long forWorkspaceId = 0, int skip = 0, int take = 50)
        {
            var state = forWorkspaceId > 0
                            ? null
                            : _requestStateManager.GetState();

            if (state != null && state.RequestPublisherAccountId > 0)
            {   // Return dialogs this publisher is a member of only
                await foreach (var dialogId in _dynamoDb.FromQuery<DynDialogMember>(m => m.Id == state.RequestPublisherAccountId)
                                                        .Filter(m => m.DeletedOnUtc == null &&
                                                                     m.TypeId == (int)DynItemType.DialogMember)
                                                        .ExecColumnAsync(m => m.EdgeId)
                                                        .Select(e => e.ToLong())
                                                        .Where(l => l > 0)
                                                        .Skip(skip.Gz(0))
                                                        .Take(take.Gz(50)))
                {
                    yield return await _dynamoDb.GetItemByRefAsync<DynDialog>(dialogId, DynItemType.Dialog);
                }
            }
            else
            {
                var workspaceId = forWorkspaceId.Gz(state?.WorkspaceId ?? 0);

                var dialogIdAndEdges = (await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<DynamoItemIdEdge>(@"
SELECT  da.Id AS Id, da.DialogKey AS EdgeId
FROM    DialogActivity da
WHERE   da.WorkspaceId = @WorkspaceId
ORDER BY
        da.LastMessageSentOn DESC, da.Id DESC
LIMIT   @Limit
OFFSET  @Offset;
",
                                                                                                                     new
                                                                                                                     {
                                                                                                                         WorkspaceId = workspaceId,
                                                                                                                         Offset = skip.Gz(0),
                                                                                                                         Limit = take.Gz(50)
                                                                                                                     }))).AsListReadOnly();

                if (dialogIdAndEdges.IsNullOrEmptyReadOnly())
                {
                    yield break;
                }

                // Dialogs for the entire workspace
                await foreach (var dialog in _dynamoDb.GetItemsAsync<DynDialog>(dialogIdAndEdges.Select(ie => new DynamoId(ie.Id, ie.EdgeId))))
                {
                    yield return dialog;
                }
            }
        }

        public async Task<HashSet<RecordTypeId>> GetDialogMembersAsync(long dialogId)
        {
            var dialog = await _dynamoDb.GetItemByRefAsync<DynDialog>(dialogId, DynItemType.Dialog);

            return dialog?.Members;
        }
    }
}
