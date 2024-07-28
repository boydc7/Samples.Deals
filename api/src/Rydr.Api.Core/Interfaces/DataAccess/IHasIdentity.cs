using Rydr.Api.Dto.Interfaces;
using ServiceStack.Model;

namespace Rydr.Api.Core.Interfaces.DataAccess;

public interface IHasIdentity : IHasId<int>
{
    new int Id { get; set; }
}

public interface IHasLongIdentity : IHasSettableId { }

public class Int64Id : IHasSettableId
{
    public long Id { get; set; }

    public static Int64Id FromValue(long value) => new()
                                                   {
                                                       Id = value
                                                   };
}
