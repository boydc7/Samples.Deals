using System.Runtime.Serialization;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Internal;

public class RydrQueryResponse<T> : QueryResponse<T>, IHaveResults<T>, IHaveResults
    where T : class
{
    [Ignore]
    [IgnoreDataMember]
    public IReadOnlyList<object> ResultObjs => Results;

    public new IReadOnlyList<T> Results { get; set; }

    public int ResultCount => Results?.Count ?? 0;
}
