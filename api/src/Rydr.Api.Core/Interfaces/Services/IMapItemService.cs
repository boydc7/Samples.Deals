using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Models.Doc;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IMapItemService
{
    DynItemMap TryGetMap(long id, string edgeId);
    Task<DynItemMap> TryGetMapAsync(long id, string edgeId);
    Task<DynItemMap> GetMapAsync(long id, string edgeId);
    DynItemMap TryGetMapByHashedEdge(DynItemType mapItemType, string hashedEdgeValue);
    Task<DynItemMap> TryGetMapByHashedEdgeAsync(DynItemType mapItemType, string hashedEdgeValue);

    Task<bool> MapExistsAsync(long id, string edgeId);

    void PutMap(DynItemMap mapItem);
    Task PutMapAsync(DynItemMap mapItem);
    void DeleteMap(long id, string edgeId);
    Task DeleteMapAsync(long id, string edgeId);
    void OnMapUpdate(long id, string edgeId);
}
