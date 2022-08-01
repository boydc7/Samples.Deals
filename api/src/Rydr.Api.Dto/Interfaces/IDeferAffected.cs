using System.Collections.Generic;
using Rydr.Api.Dto.Enums;
using ServiceStack;

namespace Rydr.Api.Dto.Interfaces
{
    public interface IDeferAffected<T> : IReturn<T>, IDeferAffected { }

    public interface IDeferAffected
    {
        (IEnumerable<long> Ids, RecordType Type) GetAffected(object result);
    }
}
