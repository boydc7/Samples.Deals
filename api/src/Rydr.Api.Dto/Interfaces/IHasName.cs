using ServiceStack.Model;

namespace Rydr.Api.Dto.Interfaces;

public interface IHasName
{
    string Name { get; }
}

public interface IHasNameAndId : IHasName, IHasId<long> { }
public interface IHasNameAndIsRecordLookup : IHasName, ICanBeRecordLookup { }
