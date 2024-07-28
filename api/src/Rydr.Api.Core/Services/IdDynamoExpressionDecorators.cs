using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Core.Services;

public class OwnerWorkspaceIdDynamoExpressionDecorator : IDecorateDynamoExpressionService
{
    public Task DecorateAsync<TRequest, TFrom>(TRequest request, DataQuery<TFrom> query)
        where TRequest : IQueryDataRequest<TFrom>
        where TFrom : ICanBeRecordLookup
    { // Remove the org condition for now from dyn queries, as we do not actually use it anywhere and it needs to be an IN query anyhow (to account for public org)
        query.Conditions.RemoveAll(x => x.Field.Name.EqualsOrdinalCi("OwnerId") ||
                                        (!request.IncludeWorkspace && x.Field.Name.EqualsOrdinalCi("WorkspaceId")));

        return Task.CompletedTask;
    }
}

public class ZeroIdDynamoExpressionDecorator : IDecorateDynamoExpressionService
{
    public Task DecorateAsync<TRequest, TFrom>(TRequest request, DataQuery<TFrom> query)
        where TRequest : IQueryDataRequest<TFrom>
        where TFrom : ICanBeRecordLookup
    { // If an Id value is <= 0, remove it
        query.Conditions.RemoveAll(d => d.Field.Name.EqualsOrdinalCi("Id") && d.Value is long dvl && dvl <= 0);

        return Task.CompletedTask;
    }
}
