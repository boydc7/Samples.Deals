using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IServiceCacheInvalidator
{
    void Invalidate<T>(Type serviceType, string methodName, T request, IHasUserAndWorkspaceId state);
    void Invalidate(IHasUserAndWorkspaceId state, params string[] urlSegments);
    void Invalidate(IEnumerable<string> urlSegments, IHasUserAndWorkspaceId state);
    Task InvalidateWorkspaceAsync(long workspaceId, params string[] urlSegments);
    Task InvalidatePublisherAccountAsync(long publisherAccountId, params string[] urlSegments);

    void UpdateGetResponseValidAt<T>(Type serviceType, T request, IHasUserAndWorkspaceId state)
        where T : IRequestBase;

    bool IsValidAt<T>(T request, string forGetUrl, IHasUserAndWorkspaceId state);
}
