using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Logging;
using LogExtensions = Rydr.Api.Core.Extensions.LogExtensions;

namespace Rydr.Api.Core.Services.Internal
{
    public class AsyncServiceCacheInvalidator : IServiceCacheInvalidator
    {
        private readonly IServiceCacheInvalidator _serviceCacheInvalidator;

        public AsyncServiceCacheInvalidator(IServiceCacheInvalidator serviceCacheInvalidator)
        {
            _serviceCacheInvalidator = serviceCacheInvalidator;
        }

        public void UpdateGetResponseValidAt<T>(Type serviceType, T request, IHasUserAndWorkspaceId state)
            where T : IRequestBase
        {
            var deferObj = new ServiceCacheInvalidateInfo<T>
                           {
                               ServiceType = serviceType,
                               Request = request,
                               State = state
                           };

            LocalAsyncTaskExecuter.DefaultTaskExecuter.Exec(deferObj, t => _serviceCacheInvalidator.UpdateGetResponseValidAt(t.ServiceType, t.Request, t.State));
        }

        public Task InvalidateWorkspaceAsync(long workspaceId, params string[] urlSegments)
        {
            if (urlSegments.IsNullOrEmpty())
            {
                return Task.CompletedTask;
            }

            LocalAsyncTaskExecuter.DefaultTaskExecuter.ExecAsync(urlSegments, u => _serviceCacheInvalidator.InvalidateWorkspaceAsync(workspaceId, u));

            return Task.CompletedTask;
        }

        public Task InvalidatePublisherAccountAsync(long publisherAccountId, params string[] urlSegments)
        {
            if (urlSegments.IsNullOrEmpty())
            {
                return Task.CompletedTask;
            }

            LocalAsyncTaskExecuter.DefaultTaskExecuter.ExecAsync(urlSegments, u => _serviceCacheInvalidator.InvalidatePublisherAccountAsync(publisherAccountId, u));

            return Task.CompletedTask;
        }

        public void Invalidate(IHasUserAndWorkspaceId state, params string[] urlSegments)
        {
            if (urlSegments.IsNullOrEmpty())
            {
                return;
            }

            LocalAsyncTaskExecuter.DefaultTaskExecuter.Exec(urlSegments, u => _serviceCacheInvalidator.Invalidate(state, u));
        }

        public void Invalidate(IEnumerable<string> urlSegments, IHasUserAndWorkspaceId state)
            => Invalidate(state, urlSegments.ToArray());

        public void Invalidate<T>(Type serviceType, string methodName, T request, IHasUserAndWorkspaceId state)
        {
            var deferObj = new ServiceCacheInvalidateInfo<T>
                           {
                               ServiceType = serviceType,
                               MethodName = methodName,
                               Request = request,
                               State = state
                           };

            LocalAsyncTaskExecuter.DefaultTaskExecuter.Exec(deferObj, t => _serviceCacheInvalidator.Invalidate(t.ServiceType, t.MethodName, t.Request, t.State));
        }

        public bool IsValidAt<T>(T request, string forGetUrl, IHasUserAndWorkspaceId state)
            => _serviceCacheInvalidator.IsValidAt(request, forGetUrl, state);

        private class ServiceCacheInvalidateInfo<T>
        {
            public Type ServiceType { get; set; }
            public string MethodName { get; set; }
            public T Request { get; set; }
            public IHasUserAndWorkspaceId State { get; set; }
        }
    }

    public class NullServiceCacheInvalidator : IServiceCacheInvalidator
    {
        private NullServiceCacheInvalidator() { }

        public static NullServiceCacheInvalidator Instance { get; } = new NullServiceCacheInvalidator();

        public void UpdateGetResponseValidAt<T>(Type serviceType, T request, IHasUserAndWorkspaceId state)
            where T : IRequestBase { }

        public void Invalidate<T>(Type serviceType, string methodName, T request, IHasUserAndWorkspaceId state) { }
        public void Invalidate(IHasUserAndWorkspaceId state, params string[] urlSegments) { }
        public void Invalidate(IEnumerable<string> urlSegments, IHasUserAndWorkspaceId state) { }
        public Task InvalidateWorkspaceAsync(long workspaceId, params string[] urlSegments) => Task.CompletedTask;
        public Task InvalidatePublisherAccountAsync(long publisherAccountId, params string[] urlSegments) => Task.CompletedTask;
        public bool IsValidAt<T>(T request, string forGetUrl, IHasUserAndWorkspaceId state) => false;
        public bool IsValidAt<T>(T request, long atTimestamp, IHasUserAndWorkspaceId state) => false;
    }

    public class DefaultServiceCacheInvalidator : IServiceCacheInvalidator
    {
        private static readonly ILog _log = LogManager.GetLogger("DefaultServiceCacheInvalidator");

        private readonly ILocalDistributedCacheClient _cacheClient;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IRequestStateManager _requestStateManager;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IAssociationService _associationService;

        private static readonly CacheConfig _invalidationCacheConfig = CacheConfig.FromHours(10);

        private static readonly char[] _urlSegmentDelimiters =
        {
            '/', '?'
        };

        public DefaultServiceCacheInvalidator(ILocalDistributedCacheClient cacheClient, IDateTimeProvider dateTimeProvider,
                                              IRequestStateManager requestStateManager, IPublisherAccountService publisherAccountService,
                                              IAssociationService associationService)
        {
            _cacheClient = cacheClient;
            _dateTimeProvider = dateTimeProvider;
            _requestStateManager = requestStateManager;
            _publisherAccountService = publisherAccountService;
            _associationService = associationService;
        }

        public async Task InvalidateWorkspaceAsync(long workspaceId, params string[] urlSegments)
        {
            if (urlSegments.IsNullOrEmpty())
            {
                return;
            }

            var workspace = WorkspaceService.DefaultWorkspaceService.TryGetWorkspace(workspaceId);

            if (workspace == null || workspace.IsDeleted())
            {
                return;
            }

            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"!@! InvalidatingWorkspace [{workspace.Name} ({workspace.Id}))]");
            }

            // Flush all users connected to this workspace, along with the workspace itself
            var distinctIds = new HashSet<long>();

            await foreach (var userAssociation in _associationService.GetAssociationsToAsync(workspaceId, RecordType.User, RecordType.Workspace))
            {
                var user = await UserExtensions.DefaultUserService.TryGetUserAsync(userAssociation.FromRecordId);

                if (user == null || user.IsDeleted())
                {
                    continue;
                }

                distinctIds.Add(user.WorkspaceId.Gz(user.UserId));
            }

            distinctIds.Add(workspaceId);

            foreach (var distinctId in distinctIds)
            {
                DoInvalidateSegments(urlSegments, distinctId.ToStringInvariant());
            }
        }

        public async Task InvalidatePublisherAccountAsync(long publisherAccountId, params string[] urlSegments)
        {
            if (urlSegments.IsNullOrEmpty())
            {
                return;
            }

            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

            if (publisherAccount == null)
            {
                return;
            }

            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"!@! InvalidatingPublisherAccount [{publisherAccount.DisplayName()}]");
            }

            // Have to basically flush all workspaces that a given User publisher account is directly linked to
            // OR
            // If not a User publisher account, get all user publisher accounts the given account is linked to and then flush those workspaces
            await foreach (var invalidateWorkspaceId in WorkspaceService.DefaultWorkspaceService
                                                                        .GetAssociatedWorkspaceIdsAsync(publisherAccount))
            {
                await InvalidateWorkspaceAsync(invalidateWorkspaceId, urlSegments);
            }
        }

        public void Invalidate(IHasUserAndWorkspaceId state, params string[] urlSegments)
            => Invalidate(urlSegments, state);

        public void Invalidate(IEnumerable<string> urlSegments, IHasUserAndWorkspaceId state)
        {
            var stateToUse = state ?? _requestStateManager.GetState();

            DoInvalidateSegments(urlSegments, stateToUse == null
                                                  ? CacheConfig.InvariantSessionKey
                                                  : stateToUse.WorkspaceId.Gz(stateToUse.UserId).ToStringInvariant());
        }

        public void Invalidate<T>(Type serviceType, string methodName, T request, IHasUserAndWorkspaceId state)
        {
            var methodCacheAttribute = methodName.HasValue()
                                           ? serviceType.GetFirstAttribute<RydrCacheResponse>(methodName, request.GetType())
                                           : null;

            var classCacheAttribute = serviceType.GetFirstAttribute<RydrCacheResponse>();

            if (methodCacheAttribute == null && classCacheAttribute == null)
            {
                return;
            }

            var invalidateKeys = new HashSet<string>((methodCacheAttribute?.UrlTargets ?? Enumerable.Empty<string>()).Select(u => GetInvalidationCacheKey(u, state))
                                                                                                                     .Where(k => k.HasValue()),
                                                     StringComparer.OrdinalIgnoreCase);

            (classCacheAttribute?.UrlTargets ?? Enumerable.Empty<string>()).Select(u => GetInvalidationCacheKey(u, state))
                                                                           .Where(k => k.HasValue())
                                                                           .Each(k => invalidateKeys.Add(k));

            var requestInvalidationKey = GetInvalidationCacheKey(request, state);

            if (requestInvalidationKey.HasValue())
            {
                invalidateKeys.Add(requestInvalidationKey);
            }

            if (invalidateKeys.IsNullOrEmpty())
            {
                return;
            }

            var invalidAt = _dateTimeProvider.UtcNowTs + 1;

            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"!!! Invalidating keys [{string.Join(" , ", invalidateKeys)}] at [{invalidAt}]");
            }

            invalidateKeys.Each(ik => _cacheClient.TrySet(new Int64Id
                                                          {
                                                              Id = invalidAt
                                                          },
                                                          ik,
                                                          _invalidationCacheConfig));
        }

        public void UpdateGetResponseValidAt<T>(Type serviceType, T request, IHasUserAndWorkspaceId state)
            where T : IRequestBase
        {
            var cacheAttribute = serviceType.GetFirstAttribute<RydrCacheResponse>("GET", request.GetType())
                                 ??
                                 serviceType.GetFirstAttribute<RydrCacheResponse>();

            if (cacheAttribute == null || cacheAttribute.NoCaching)
            {
                return;
            }

            if (!request.TryGetUrlForCacheKeyFromDto(out var getUrl))
            {
                return;
            }

            var getResponseLastCachedAtKey = string.Concat("WebServiceCachedGet|", getUrl, "|",
                                                           state.WorkspaceId.Gz(state.UserId).ToStringInvariant()).ToShaBase64();

            var validAt = _dateTimeProvider.UtcNowTs;

            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"*** Caching GET response key for [{getResponseLastCachedAtKey}] at [{validAt}]");
            }

            _cacheClient.TrySet(new Int64Id
                                {
                                    Id = validAt
                                },
                                getResponseLastCachedAtKey,
                                _invalidationCacheConfig);
        }

        public bool IsValidAt<T>(T request, string forGetUrl, IHasUserAndWorkspaceId state)
        {
            var getResponseLastCachedAtKey = string.Concat("WebServiceCachedGet|", forGetUrl, "|",
                                                           state.WorkspaceId.Gz(state.UserId).ToStringInvariant()).ToShaBase64();

            var invalidationCacheKey = GetGetInvalidationCacheKey(request, state);

            if (!invalidationCacheKey.HasValue() || !getResponseLastCachedAtKey.HasValue())
            {
                return false;
            }

            var cachedGetAt = _cacheClient.TryGet<Int64Id>(getResponseLastCachedAtKey)?.Id ?? 0;

            if (cachedGetAt <= 0)
            {
                return false;
            }

            var invalidatedRootAt = _cacheClient.TryGet<Int64Id>(invalidationCacheKey)?.Id ?? 0;

            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"??? IsValidAt is [{cachedGetAt > invalidatedRootAt}] for GET response key [{getResponseLastCachedAtKey}] - getAt [{cachedGetAt}] , invalidAt [{invalidatedRootAt}]");
            }

            return cachedGetAt > invalidatedRootAt;
        }

        private void DoInvalidateSegments(IEnumerable<string> urlSegments, string forSessionKey)
        {
            if (urlSegments == null)
            {
                return;
            }

            var invalidateKeys = new HashSet<string>(urlSegments.Select(u => GetInvalidationCacheKey(u, forSessionKey))
                                                                .Where(k => k.HasValue()),
                                                     StringComparer.OrdinalIgnoreCase);

            if (invalidateKeys.IsNullOrEmpty())
            {
                return;
            }

            var invalidAt = _dateTimeProvider.UtcNowTs + 1;

            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"!!! Invalidating keys [{string.Join(" , ", invalidateKeys)}] at [{invalidAt}]");
            }

            invalidateKeys.Each(ik => _cacheClient.TrySet(new Int64Id
                                                          {
                                                              Id = invalidAt
                                                          },
                                                          ik,
                                                          _invalidationCacheConfig));
        }

        private string GetInvalidationCacheKey<T>(T request, IHasUserAndWorkspaceId state)
        {
            var url = Try.Get(() => request.ToPostUrl());

            if (url.HasValue())
            {
                return GetInvalidationCacheKey(url, state);
            }

            url = Try.Get(() => request.ToPutUrl());

            if (url.HasValue())
            {
                return GetInvalidationCacheKey(url, state);
            }

            url = Try.Get(() => request.ToDeleteUrl());

            if (url.HasValue())
            {
                return GetInvalidationCacheKey(url, state);
            }

            url = Try.Get(() => request.ToGetUrl());

            if (url.HasValue())
            {
                return GetInvalidationCacheKey(url, state);
            }

            return null;
        }

        private string GetGetInvalidationCacheKey<T>(T request, IHasUserAndWorkspaceId state)
            => GetInvalidationCacheKey(Try.Get(() => request.ToGetUrl()), state);

        private string GetInvalidationCacheKey(string url, IHasUserAndWorkspaceId state)
        {
            var stateToUse = state ?? _requestStateManager.GetState();

            return GetInvalidationCacheKey(url, stateToUse == null
                                                    ? CacheConfig.InvariantSessionKey
                                                    : stateToUse.WorkspaceId.Gz(stateToUse.UserId).ToStringInvariant());
        }

        private string GetInvalidationCacheKey(string url, string sessionKey)
        {
            if (!url.HasValue() || !sessionKey.HasValue())
            {
                return null;
            }

            var firstUrlSegmentEndIndex = url.IndexOfAny(_urlSegmentDelimiters, 1);

            var firstUrlSegment = (firstUrlSegmentEndIndex <= 1
                                       ? url
                                       : url.Left(firstUrlSegmentEndIndex)).TrimStart(_urlSegmentDelimiters)
                                                                           .Trim();

            return UrnId.Create(string.Concat("WebServiceCacheInvalidAt|", firstUrlSegment), sessionKey);
        }
    }
}
