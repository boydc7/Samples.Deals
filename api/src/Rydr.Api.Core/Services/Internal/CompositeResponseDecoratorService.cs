using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Internal
{
    public class CompositeResponseDecoratorService : IDecorateResponsesService
    {
        private readonly List<IDecorateResponsesService> _services;
        private readonly ConcurrentDictionary<object, BatchRequestIndexInfo> _batchRequestIndexMap = new ConcurrentDictionary<object, BatchRequestIndexInfo>();

        public CompositeResponseDecoratorService(IEnumerable<IDecorateResponsesService> services)
        {
            _services = services.AsList();
        }

        public async Task DecorateManyAsync<TRequest, T>(TRequest request, ICollection<T> response)
            where TRequest : class, IHasUserAuthorizationInfo
            where T : class
        {
            if (request == null || response == null || response.Count <= 0)
            {
                return;
            }

            var req = GetRequest(request);

            foreach (var service in _services)
            {
                await service.DecorateManyAsync(req, response);
            }
        }

        public async Task DecorateOneAsync<TRequest, T>(TRequest request, T response)
            where TRequest : class, IHasUserAuthorizationInfo
            where T : class
        {
            if (request == null || response == null)
            {
                return;
            }

            var req = GetRequest(request);

            foreach (var service in _services)
            {
                await service.DecorateOneAsync(req, response);
            }
        }

        private T GetRequest<T>(T req)
            where T : class
        {
            if (!(req is object[] reqDtoArray))
            {
                return req;
            }

            if (reqDtoArray.Length <= 0)
            {
                return null;
            }

            // Auto batched requests basically iteratively call this same instance for each request object to get the response
            // to each processed one...
            var batchIndex = _batchRequestIndexMap.AddOrUpdate(req, new BatchRequestIndexInfo(),
                                                               (k, t) =>
                                                               {
                                                                   t.Index++;

                                                                   return t;
                                                               });

            if (batchIndex.Index >= reqDtoArray.Length)
            {
                throw new ArgumentOutOfRangeException($"BatchRequest array processing is invalid - index [{batchIndex.Index}], length [{reqDtoArray.Length}]");
            }

            if (batchIndex.Index == (reqDtoArray.Length - 1))
            {
                _batchRequestIndexMap.TryRemove(req, out _);
            }

            return reqDtoArray[batchIndex.Index] as T;
        }

        private class BatchRequestIndexInfo
        {
            public int Index { get; set; }
        }
    }
}
