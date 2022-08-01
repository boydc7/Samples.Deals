using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Web;

namespace Rydr.Api.Core.Models.Internal
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RydrForcedSimpleCacheResponse : CacheResponseAttribute
    {
        // Disable all cache directive overrides everything...even a forced simple...
        private static readonly bool _cacheDisabled = RydrEnvironment.GetAppSetting("Caching.DisableAll", false);
        private static readonly CacheControl _defaultCacheControl = CacheControl.MustRevalidate | CacheControl.Private;
        private static readonly CacheControl _noCacheControl = CacheControl.MustRevalidate | CacheControl.Private | CacheControl.NoCache | CacheControl.NoStore;

        public RydrForcedSimpleCacheResponse(int duration)
        {
            if (_cacheDisabled)
            {
                MaxAge = -1;
                VaryByUser = true;
                Duration = -1;
                LocalCache = true;
                CacheControl = _noCacheControl;
            }
            else
            {
                Duration = duration;
                MaxAge = -1;
                VaryByUser = true;
                CacheControl = _defaultCacheControl;
            }
        }

        public override async Task ExecuteAsync(IRequest req, IResponse res, object requestDto)
        {
            if (_cacheDisabled || !req.Verb.EqualsOrdinalCi("GET") ||
                req.GetParamInRequestHeader("Cache-Control").Coalesce("").Contains("no-cache") ||
                req.IsInProcessRequest())
            {
                return;
            }

            if (!(requestDto is IRequestBase requestDtoBase))
            {
                return;
            }

            if (!requestDtoBase.TryGetUrlForCacheKeyFromDto(out var getUrl))
            {
                return;
            }

            var cacheInfo = new CacheInfo
                            {
                                KeyBase = getUrl,
                                KeyModifiers = requestDtoBase.WorkspaceId.Gz(requestDtoBase.UserId).ToStringInvariant(),
                                ExpiresIn = Duration > 0
                                                ? TimeSpan.FromSeconds(Duration)
                                                : (TimeSpan?)null,
                                MaxAge = MaxAge > 0
                                             ? TimeSpan.FromSeconds(MaxAge)
                                             : (TimeSpan?)null,
                                CacheControl = CacheControl,
                                VaryByUser = VaryByUser,
                                LocalCache = LocalCache,
                                NoCompression = NoCompression
                            };

            if (Duration > 0 && !requestDtoBase.ForceRefresh)
            {
                var validCache = await req.HandleValidCache(cacheInfo);

                if (validCache)
                { // Ensure there is no cacheInfo in the request
                    req.Items.Remove(Keywords.CacheInfo);

                    return;
                }
            }

            req.Items[Keywords.CacheInfo] = cacheInfo;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RydrCacheResponse : CacheResponseAttribute
    {
        private static readonly CacheControl _defaultCacheControl = CacheControl.MustRevalidate | CacheControl.Private;
        private static readonly IServiceCacheInvalidator _serviceCacheValidator = RydrEnvironment.Container.Resolve<IServiceCacheInvalidator>();
        private static readonly CacheControl _noCacheControl = CacheControl.MustRevalidate | CacheControl.Private | CacheControl.NoCache | CacheControl.NoStore;

        private static readonly bool _cacheDisabled = RydrEnvironment.GetAppSetting("Caching.DisableAll", false) ||
                                                      RydrEnvironment.GetAppSetting("Caching.DisableServices", false);

        private bool _noCaching;

        public RydrCacheResponse() : this(false) { }

        public RydrCacheResponse(int duration, params string[] urlTargets) : this(false, urlTargets)
        {
            if (duration < 0 || _noCaching || _cacheDisabled)
            {
                SetNoCache();
            }
            else
            {
                Duration = duration;
                MaxAge = -1;
                VaryByUser = true;
                CacheControl = _defaultCacheControl;
            }
        }

        public RydrCacheResponse(bool noCache, params string[] urlTargets)
        {
            if (noCache || _cacheDisabled || _noCaching)
            {
                SetNoCache();
            }
            else
            {
                Duration = int.MinValue;
                MaxAge = -1;
                VaryByUser = true;
                CacheControl = _defaultCacheControl;
            }

            UrlTargets = urlTargets.Where(u => u.HasValue()).AsHashSet();
        }

        private void SetNoCache()
        {
            _noCaching = true;
            MaxAge = -1;
            VaryByUser = true;
            Duration = -1;
            LocalCache = true;
            CacheControl = _noCacheControl;
        }

        /// <summary>
        ///     Specify the root/first segment to flush along with the request
        ///     NOTE: DO NOT include the leading slash (/), OR a trailing one...just the segment name...
        /// </summary>
        public HashSet<string> UrlTargets { get; set; }

        public bool NoCaching
        {
            get => _noCaching;
            set
            {
                _noCaching = value;

                if (_noCaching)
                {
                    SetNoCache();
                }
            }
        }

        public override async Task ExecuteAsync(IRequest req, IResponse res, object requestDto)
        {
            if (_noCaching || _cacheDisabled || !req.Verb.EqualsOrdinalCi("GET") ||
                req.GetParamInRequestHeader("Cache-Control").Coalesce("").Contains("no-cache") ||
                req.IsInProcessRequest())
            {
                return;
            }

            if (!(requestDto is IRequestBase requestDtoBase))
            {
                return;
            }

            if (!requestDtoBase.TryGetUrlForCacheKeyFromDto(out var getUrl))
            {
                return;
            }

            var cacheInfo = new CacheInfo
                            {
                                KeyBase = getUrl,
                                KeyModifiers = requestDtoBase.WorkspaceId.Gz(requestDtoBase.UserId).ToStringInvariant(),
                                ExpiresIn = Duration > 0
                                                ? TimeSpan.FromSeconds(Duration)
                                                : (TimeSpan?)null,
                                MaxAge = MaxAge > 0
                                             ? TimeSpan.FromSeconds(MaxAge)
                                             : (TimeSpan?)null,
                                CacheControl = CacheControl,
                                VaryByUser = VaryByUser,
                                LocalCache = LocalCache,
                                NoCompression = NoCompression
                            };

            if (Duration > 0 && !requestDtoBase.ForceRefresh)
            {
                if (_serviceCacheValidator.IsValidAt(requestDto, getUrl, requestDtoBase))
                {
                    var validCache = await req.HandleValidCache(cacheInfo);

                    if (validCache)
                    {
                        // Ensure there is no cacheInfo in the request
                        req.Items.Remove(Keywords.CacheInfo);

                        return;
                    }
                }
            }

            req.Items[Keywords.CacheInfo] = cacheInfo;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RydrNeverCacheResponse : RydrCacheResponse
    {
        public RydrNeverCacheResponse(params string[] urlTargets) : base(true, urlTargets) { }
    }
}
