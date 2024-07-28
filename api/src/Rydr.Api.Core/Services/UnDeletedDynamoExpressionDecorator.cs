using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Core.Services;

public class UnDeletedDynamoExpressionDecorator : IDecorateDynamoExpressionService
{
    public Task DecorateAsync<TRequest, TFrom>(TRequest request, DataQuery<TFrom> query)
        where TRequest : IQueryDataRequest<TFrom>
        where TFrom : ICanBeRecordLookup
    {
        if (!request.IncludeDeleted)
        {
            query.And("DeletedOnUtc", EqualsCondition.Instance, null);
        }

        return Task.CompletedTask;
    }
}
