using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Core.Interfaces.Models;

public interface IQueryDataRequest<T> : IQueryData<T>, IRequestBase
    where T : ICanBeRecordLookup
{
    bool IncludeDeleted { get; set; }
    bool IncludeWorkspace { get; set; }
}
