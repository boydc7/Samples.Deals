using Rydr.Api.Core.Models.Doc;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IPublisherMediaStorageService
{
    Task StoreAsync(IEnumerable<DynPublisherMediaStat> stats);
}

public interface IPublisherMediaSingleStorageService
{
    Task StoreAsync(DynPublisherMediaStat stat);
}

public interface IPublisherMediaStatDecorator
{
    IAsyncEnumerable<DynPublisherMediaStat> DecorateAsync(IAsyncEnumerable<DynPublisherMediaStat> stats);
}
