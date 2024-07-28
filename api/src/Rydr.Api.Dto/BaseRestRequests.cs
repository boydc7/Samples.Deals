using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Model;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Rydr.Api.Dto;

public abstract class BaseGetRequest<T> : BaseGetRequest, IReturn<OnlyResultResponse<T>>
    where T : class { }

public abstract class BaseGetRequest : BaseGetManyRequest, IHasLongId
{
    public long Id { get; set; }
}

public abstract class BaseGetManyRequest<T> : BaseGetManyRequest, IReturn<OnlyResultsResponse<T>>
    where T : class { }

public abstract class BaseGetManyRequest : RequestBase, IGet { }

public abstract class BasePostRequest<T> : BaseSetRequest<T>, IPost
    where T : class, IHasLongId { }

public abstract class BasePutRequest<T> : BaseSetRequest<T>, IPut, IHasLongId
    where T : class, IHasLongId
{
    public long Id { get; set; }
}

public abstract class BaseDeleteRequest : RequestBaseDeferAffectedVoid, IDelete, IHasLongId
{
    public long Id { get; set; }

    protected abstract RecordType GetRecordType();

    protected override (IEnumerable<long> Ids, RecordType Type) GetMyAffected()
        => (Id.AsEnumerable(), GetRecordType());
}

public abstract class BaseSetRequest<T> : RequestBaseDeferAffected<LongIdResponse>, IRequestBaseWithModel<T>
    where T : class, IHasLongId
{
    public T Model { get; set; }

    protected abstract RecordType GetRecordType();

    protected override (IEnumerable<long> Ids, RecordType Type) GetMyAffected(LongIdResponse result)
        => (result.Id.Gz(Model.Id).AsEnumerable(), GetRecordType());
}

public abstract class BaseSetResultRequest<T> : RequestBaseDeferAffected<OnlyResultResponse<LongIdResponse>>, IRequestBaseWithModel<T>
    where T : class, IHasLongId
{
    public T Model { get; set; }

    protected abstract RecordType GetRecordType();

    protected override (IEnumerable<long> Ids, RecordType Type) GetMyAffected(OnlyResultResponse<LongIdResponse> result)
        => result.Result == null
               ? (Enumerable.Empty<long>(), GetRecordType())
               : (result.Result.Id.Gz(Model.Id).AsEnumerable(), GetRecordType());
}
