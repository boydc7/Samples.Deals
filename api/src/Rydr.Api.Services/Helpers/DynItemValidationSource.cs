using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Services.Helpers;

public class DynItemValidationSource : DynItemValidationSource<DynItem>
{
    public DynItemValidationSource(IRequestBase request, long id, string edgeId,
                                   DynItemType type, string referenceId = null,
                                   ApplyToBehavior treatLike = ApplyToBehavior.Default,
                                   bool skipAccessChecks = false)
        : base(request, id, edgeId, type, referenceId, treatLike, skipAccessChecks) { }
}

// ReSharper disable once UnusedTypeParameter
public class DynItemValidationSource<T>
{
    public DynItemValidationSource(IRequestBase request, long id, string edgeId,
                                   DynItemType type, string referenceId = null,
                                   ApplyToBehavior treatLike = ApplyToBehavior.Default,
                                   bool skipAccessChecks = false,
                                   Func<T, bool> alsoMust = null)
    {
        Request = request;
        Id = id;
        EdgeId = edgeId;
        Type = type;
        TreatLike = treatLike;
        SkipAccessChecks = skipAccessChecks;
        ReferenceId = referenceId;
        AlsoMust = alsoMust;
    }

    public IRequestBase Request { get; }
    public long Id { get; }
    public string EdgeId { get; }
    public DynItemType Type { get; }
    public string ReferenceId { get; }
    public ApplyToBehavior TreatLike { get; }
    public bool SkipAccessChecks { get; }
    public Func<T, bool> AlsoMust { get; }
}

public enum ApplyToBehavior
{
    Default,
    MustExistNotDeleted,
    MustNotExist,
    CanExistNotDeleted,
    MustExistCanBeDeleted
}
