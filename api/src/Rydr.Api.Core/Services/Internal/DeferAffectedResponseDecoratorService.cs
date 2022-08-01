using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Internal
{
    public class DeferAffectedResponseDecoratorService : IDecorateResponsesService
    {
        private readonly IRequestStateManager _requestStateManager;
        private readonly IDeferRequestsService _deferRequestsService;

        public DeferAffectedResponseDecoratorService(IRequestStateManager requestStateManager, IDeferRequestsService deferRequestsService)
        {
            _requestStateManager = requestStateManager;
            _deferRequestsService = deferRequestsService;
        }

        public Task DecorateManyAsync<TRequest, T>(TRequest request, ICollection<T> results)
            where TRequest : class, IHasUserAuthorizationInfo
            where T : class
        {
            if (results == null || results.Count <= 0 || request == null)
            {
                return Task.CompletedTask;
            }

            DecorateManyInternal(request, results);

            return Task.CompletedTask;
        }

        private void DecorateManyInternal<TRequest>(TRequest request, IEnumerable<object> results)
            where TRequest : class, IHasUserAuthorizationInfo
        {
            if (!(request is IDeferAffected ida))
            {
                return;
            }

            var entityType = RecordType.Unknown;
            var requestState = _requestStateManager.GetState();

            results.Select(r => ida.GetAffected(r))
                   .SelectMany(t =>
                               {
                                   if (entityType == RecordType.Unknown && t.Type != RecordType.Unknown)
                                   {
                                       entityType = t.Type;
                                   }

                                   return t.Ids;
                               })
                   .Where(i => i > 0)
                   .Distinct()
                   .ToLazyBatchesOf(20)
                   .Each(b =>
                         {
                             if (entityType == RecordType.Unknown)
                             {
                                 return;
                             }

                             _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                                  {
                                                                      Ids = b.AsList(),
                                                                      Type = entityType,
                                                                      OriginatingRequestId = requestState.RequestId
                                                                  });
                         });
        }

        public Task DecorateOneAsync<TRequest, T>(TRequest request, T response)
            where TRequest : class, IHasUserAuthorizationInfo
            where T : class
        {
            if (request == null || !(request is IDeferAffected ida))
            {
                return Task.CompletedTask;
            }

            var (ids, type) = ida.GetAffected(response);

            if (ids == null || type == RecordType.Unknown)
            {
                return Task.CompletedTask;
            }

            var requestState = _requestStateManager.GetState();

            ids.Where(i => i > 0)
               .ToLazyBatchesOf(20)
               .Each(b =>
                     {
                         var msg = new PostDeferredAffected
                                   {
                                       Ids = b.AsList(),
                                       Type = type,
                                       OriginatingRequestId = requestState.RequestId
                                   };

                         _deferRequestsService.PublishMessage(msg);
                     });

            return Task.CompletedTask;
        }
    }
}
