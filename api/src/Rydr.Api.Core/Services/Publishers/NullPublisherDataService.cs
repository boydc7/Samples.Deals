using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;

namespace Rydr.Api.Core.Services.Publishers;

public class NullPublisherDataService : IPublisherDataService
{
    private static readonly DynPublisherApp _nullPublisherApp = new()
                                                                {
                                                                    Id = GlobalItemIds.NullPublisherAppId,
                                                                    EdgeId = DynPublisherApp.BuildEdgeId(PublisherType.Unknown, "GetRydrNullInstancePublisherApp-InvalidForUse"),
                                                                    ReferenceId = GlobalItemIds.NullPublisherAppId.ToStringInvariant(),
                                                                    OwnerId = GlobalItemIds.AuthRydrWorkspaceId,
                                                                    WorkspaceId = GlobalItemIds.AuthRydrWorkspaceId,
                                                                    AppId = "GetRydrNullInstancePublisherApp-InvalidForUse",
                                                                    AppSecret = Guid.NewGuid().ToString(),
                                                                    Name = "RYDR Null app instance representation",
                                                                    DedicatedWorkspaceId = GlobalItemIds.AuthRydrWorkspaceId
                                                                };

    private static readonly Task<List<PublisherMedia>> _emptyMediaTask = Task.FromResult(new List<PublisherMedia>());

    private NullPublisherDataService() { }

    public static NullPublisherDataService Instance { get; } = new();

    public Task<DynPublisherApp> GetDefaultPublisherAppAsync() => Task.FromResult<DynPublisherApp>(null);

    public Task<List<PublisherMedia>> GetRecentMediaAsync(long tokenPublisherAccountId, long publisherAccountId, long publisherAppId = 0, int limit = 50)
        => _emptyMediaTask;

    public Task<DynPublisherApp> GetPublisherAppOrDefaultAsync(long publisherAppId, AccessIntent accessIntent = AccessIntent.Unspecified, long workspaceId = 0)
        => Task.FromResult(_nullPublisherApp);

    public Task PutAccessTokenAsync(long publisherAccountId, string accessToken, int expiresIn = 0, long workspaceId = 0, long publisherAppId = 0)
        => Task.CompletedTask;
}
