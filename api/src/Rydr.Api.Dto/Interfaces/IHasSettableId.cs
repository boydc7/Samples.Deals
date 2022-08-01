using ServiceStack.Model;

namespace Rydr.Api.Dto.Interfaces
{
    public interface IHasSettableId : IHasLongId
    {
        new long Id { get; set; }
    }

    public interface IHasCompositeId : IHasId<string> { }
}
