using Rydr.Api.Core.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Model;

namespace Rydr.Api.Core.Interfaces.Models
{
    public interface IDynItem : IHaveIdAndEdgeId, IDateTimeDeleteTracked, IDateTimeTracked, ICanBeAuthorized
    {
        public DynItemType DynItemType { get; set; }
    }

    public interface IHaveIdAndEdgeId : IHasLongId
    {
        public string EdgeId { get; set; }
    }

    public interface IHaveMappedEdgeId : IDynItem { }
}
