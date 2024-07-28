using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Publishers;
using ServiceStack;

namespace Rydr.Api.Services.Services;

[RequiredRole("Admin")]
public class PublisherAppService : BaseAdminApiService
{
    public async Task<OnlyResultResponse<PublisherApp>> Get(GetPublisherApp request)
    {
        var dynPublisherApp = await _dynamoDb.GetPublisherAppAsync(request.Id, true);

        return dynPublisherApp.ToPublisherApp()
                              .AsOnlyResultResponse();
    }

    public async Task<LongIdResponse> Post(PostPublisherApp request)
    {
        var newPublisherApp = await request.ToDynPublisherAppAsync();

        if (request.WorkspaceId > 0 && request.WorkspaceId != UserAuthInfo.AdminWorkspaceId && request.WorkspaceId != UserAuthInfo.RydrWorkspaceId)
        {
            newPublisherApp.WorkspaceId = request.WorkspaceId;
            newPublisherApp.DedicatedWorkspaceId = request.WorkspaceId;
            newPublisherApp.OwnerId = request.WorkspaceId;
        }

        await _dynamoDb.PutItemAsync(newPublisherApp);

        return newPublisherApp.ToLongIdResponse();
    }

    public async Task<LongIdResponse> Put(PutPublisherApp request)
        => (await _dynamoDb.UpdateFromRefRequestAsync<PutPublisherApp, DynPublisherApp>(request, request.Id, DynItemType.PublisherApp,
                                                                                        (r, x) => r.ToDynPublisherAppAsync(x).GetAwaiter().GetResult())
           ).ToLongIdResponse();

    public Task Delete(DeletePublisherApp request)
        => _dynamoDb.SoftDeleteByRefIdAsync<DynPublisherApp>(request.Id, DynItemType.PublisherApp, request);
}
