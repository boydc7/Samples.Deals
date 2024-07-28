using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IAssociationService : IAssociationService<DynAssociation> { }

public interface IAssociationService<T>
    where T : DynAssociation
{
    IAsyncEnumerable<string> GetAssociatedIdsAsync(long fromRecordId, RecordType toRecordType, RecordType? fromRecordType = null);
    IAsyncEnumerable<T> GetAssociationsAsync(long fromRecordId, long minToId, long maxToId, params RecordType[] toRecordTypes);
    IAsyncEnumerable<T> GetAssociationsToAsync(string toRecordId, RecordType? fromRecordType = null, RecordType? toRecordType = null);

    Task<T> GetAssociationAsync(long fromRecordId, string toRecordId);

    Task<bool> IsAssociatedAsync(long fromRecordId, string toRecordId);

    Task<T> AssociateAsync(T association, bool sourceRecordUpdatable = false);

    Task DeleteAssociationAsync(RecordType fromRecordType, long fromRecordId, RecordType toRecordType, string toRecordId);
    Task DeleteAssociationAsync(T association);

    Task DeleteAssociationsToAsync(RecordType toRecordType, string toRecordId, RecordType fromRecordType = RecordType.Unknown);
    Task DeleteAssociationsFromAsync(RecordType fromRecordType, long fromRecordId, RecordType toRecordType = RecordType.Unknown);

    Task<bool> UnDeleteAssociationAsync(T association);
}
