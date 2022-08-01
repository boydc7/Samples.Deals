using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Extensions
{
    public static class DecorateQueryResponseExtensions
    {
        private static readonly HashSet<Type> _typesToIgnoreDecorationOf = new HashSet<Type>
                                                                           {
                                                                               typeof(string),
                                                                               typeof(object)
                                                                           };

        public static async Task DecorateAsync<TRequest>(this IDecorateResponsesService service, TRequest request, object response)
            where TRequest : class, IHasUserAuthorizationInfo
        {
            var processedResponse = await ProcessResponseDecorationsAsync(service, request, response);

            if (processedResponse)
            {
                return;
            }

            // Mutually exclusive request types
            switch (request)
            {
                case IDeferAffected _:
                    await service.DecorateOneAsync(request, response);

                    break;
            }

            // All other services/types/etc. currently have to manually opt-in to decoration...
        }

        public static Task DecorateAsync<TRequest, T>(this IDecorateResponsesService service, TRequest request, RydrQueryResponse<T> response)
            where T : class
            where TRequest : class, IHasUserAuthorizationInfo
            => DecorateManyResponseObjectsAsync(service, request, response.Results);

        private static async Task<bool> ProcessResponseDecorationsAsync<TRequest>(IDecorateResponsesService service, TRequest request, object response)
            where TRequest : class, IHasUserAuthorizationInfo
        { // Mutually exclusive response types, if one hits then that covers all we need on the response
            switch (response)
            {
                case IHaveResult ihr:
                    await DecorateOneResponseObjectAsync(service, request, ihr.ResultObj);

                    return false;

                case IHaveResults ihrs:
                    await DecorateManyResponseObjectsAsync(service, request, ihrs.ResultObjs);

                    return false;

                case ResponseBase _:
                    await DecorateOneResponseObjectAsync(service, request, response);

                    return true;
            }

            return false;
        }

        private static async Task DecorateOneResponseObjectAsync<TRequest>(IDecorateResponsesService service, TRequest request, object toDecorate, int depth = 1)
            where TRequest : class, IHasUserAuthorizationInfo
        {
            await service.DecorateOneAsync(request, toDecorate);
            await DecorateObjectPropertiesAsync(service, request, toDecorate, depth);
        }

        private static async Task DecorateManyResponseObjectsAsync<TRequest>(IDecorateResponsesService service, TRequest request, IEnumerable<object> toDecorate, int depth = 1)
            where TRequest : class, IHasUserAuthorizationInfo
        {
            if (toDecorate == null)
            {
                return;
            }

            var forDecoration = new List<object>();

            foreach (var objToDecorate in toDecorate.Take(2500))
            {
                await DecorateObjectPropertiesAsync(service, request, objToDecorate, depth);

                forDecoration.Add(objToDecorate);
            }

            await service.DecorateManyAsync(request, forDecoration);
        }

        private static async Task DecorateObjectPropertiesAsync<TRequest>(IDecorateResponsesService service, TRequest request, object toInspectForDecoration, int depth)
            where TRequest : class, IHasUserAuthorizationInfo
        {
            if (depth > 4 || toInspectForDecoration == null)
            {
                return;
            }

            foreach (var propertyInfo in toInspectForDecoration.GetType()
                                                               .GetPublicTypeProperties()
                                                               .Where(p => p.PropertyType.IsClass &&
                                                                           !_typesToIgnoreDecorationOf.Contains(p.PropertyType) &&
                                                                           (p.PropertyType.ImplementsInterface<IEnumerable>() ||
                                                                            p.PropertyType.Namespace.StartsWithOrdinalCi("Rydr.Api"))))
            {
                var propertyValue = Try.Get(() => propertyInfo.GetValue(toInspectForDecoration));

                if (propertyValue == null)
                {
                    continue;
                }

                var myDepth = depth + 1;

                if (propertyValue is IEnumerable propertyValues)
                {
                    if (!propertyInfo.PropertyType.IsGenericType ||
                        propertyInfo.PropertyType.GenericTypeArguments == null ||
                        propertyInfo.PropertyType.GenericTypeArguments.Length != 1)
                    {
                        continue;
                    }

                    var singleTypeArg = propertyInfo.PropertyType.GenericTypeArguments.SingleOrDefault();

                    if (singleTypeArg == null || !singleTypeArg.IsClass ||
                        _typesToIgnoreDecorationOf.Contains(singleTypeArg) ||
                        !singleTypeArg.Namespace.StartsWithOrdinalCi("Rydr.Api"))
                    {
                        continue;
                    }

                    await DecorateManyResponseObjectsAsync(service, request, propertyValues.AsObjectEnumerable(), myDepth);
                }
                else
                {
                    await DecorateOneResponseObjectAsync(service, request, propertyValue, myDepth);
                }
            }
        }
    }
}
