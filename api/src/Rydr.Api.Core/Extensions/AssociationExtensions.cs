using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Extensions
{
    public static class AssociationExtensions
    {
        public static IAssociationService DefaultAssociationService { get; } = RydrEnvironment.Container.Resolve<IAssociationService>();

        public static Task<DynAssociation> AssociateAsync(this IAssociationService service, RecordType fromRecordType, long fromRecordId,
                                                          RecordType toRecordType, long toRecordId)
            => AssociateAsync(service, fromRecordType, fromRecordId, toRecordType, toRecordId.ToEdgeId());

        public static Task<DynAssociation> AssociateAsync(this IAssociationService service, RecordType fromRecordType,
                                                          long fromRecordId, RecordType toRecordType, string toRecordId,
                                                          long? sourceRefRecordId = null, bool sourceRecordUpdatable = false,
                                                          long? expiresAt = null)
            => service.AssociateAsync(new DynAssociation
                                      {
                                          Id = fromRecordId,
                                          EdgeId = toRecordId,
                                          DynItemType = DynItemType.Association,
                                          IdRecordType = fromRecordType,
                                          EdgeRecordType = toRecordType,
                                          ReferenceId = sourceRefRecordId.HasValue && sourceRefRecordId.Value > 0
                                                            ? sourceRefRecordId.ToString()
                                                            : null,
                                          ExpiresAt = expiresAt.NullIf(l => l <= 0)
                                      },
                                      sourceRecordUpdatable);

        public static Task<bool> IsAssociatedAsync(this IAssociationService service, long fromRecordId, long toRecordId)
            => service.IsAssociatedAsync(fromRecordId, toRecordId.ToEdgeId());

        public static IAsyncEnumerable<DynAssociation> GetAssociationsToAsync(this IAssociationService service, long toRecordId,
                                                                              RecordType? fromRecordType = null, RecordType? toRecordType = null)
            => service.GetAssociationsToAsync(toRecordId.ToEdgeId(), fromRecordType, toRecordType);

        public static Task TryDeleteAssociationAsync(this IAssociationService service, RecordType fromRecordType, long fromRecordId,
                                                     RecordType toRecordType, long toRecordId)
            => Try.ExecIgnoreNotFoundAsync(() => service.DeleteAssociationAsync(fromRecordType, fromRecordId, toRecordType, toRecordId.ToEdgeId()));

        public static IAsyncEnumerable<long> GetAssociatedIdsAsync(this IAssociationService service, RecordTypeId fromRecordTypeId, RecordType toRecordType)
            => service.GetAssociatedIdsAsync(fromRecordTypeId.Id, toRecordType, fromRecordTypeId.Type)?
                      .Select(s => s.ToLong())
                      .Where(l => l > 0);

        public static IAsyncEnumerable<DynAssociation> GetAssociationsAsync(this IAssociationService service, RecordType fromRecordType, long fromRecordId, params RecordType[] toRecordTypes)
            => service.GetAssociationsAsync(fromRecordId, 0, 0, toRecordTypes)
                      .Where(a => fromRecordType == RecordType.Unknown || a.IdRecordType == fromRecordType);

        public static async Task DeleteAllAssociationsAsync(this IAssociationService service, RecordType toOrFromRecordType, long toOrFromRecordId)
        {
            await service.DeleteAssociationsToAsync(toOrFromRecordType, toOrFromRecordId.ToEdgeId());
            await service.DeleteAssociationsFromAsync(toOrFromRecordType, toOrFromRecordId);
        }
    }
}
