using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Auth;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Dto.Users;
using Rydr.Api.QueryDto;
using Rydr.Api.Services.Helpers;
using Rydr.Api.Services.Services;
using ServiceStack;
using ServiceStack.Admin;
using ServiceStack.Configuration;
using ServiceStack.Logging;
using ServiceStack.Web;

namespace Rydr.Api.Services.Filters
{
    public interface IRequestExecutor<TRequest>
    {
        Task<object> ExecuteAsync(IRequest req, object instance, TRequest requestDto, Func<IRequest, object, TRequest, Task<object>> innerExecutor);
    }

    public static class RequestHelpers
    {
        public static readonly HashSet<Type> AllowedAnonRequestTypes = new HashSet<Type>
                                                                       {
                                                                           typeof(NoRoute),
                                                                           typeof(Monitor),
                                                                           typeof(Authenticate),
                                                                           typeof(RequestLogs),
                                                                           typeof(PostAuthenticationConnect),
                                                                           typeof(PostUser),
                                                                           typeof(PostDeferredAffected),
                                                                           typeof(PostDeferredMessage),
                                                                           typeof(PostDeferredLowPriMessage),
                                                                           typeof(PostDeferredDealMessage),
                                                                           typeof(PostDeferredPrimaryDealMessage),
                                                                           typeof(PostDeferredFifoMessage),
                                                                           typeof(MqRetry),
                                                                           typeof(GetFacebookWebhook),
                                                                           typeof(PostFacebookWebhook),
                                                                           typeof(GetDealExternalHtml),
                                                                           typeof(GetDealRequestReportExternal),
                                                                           typeof(PostStripeWebhook),
                                                                           typeof(GetInstagramAuthComplete),
                                                                           typeof(PostTrackLinkSource),
                                                                           typeof(GetBusinessReportExternal),
                                                                       };

        public static readonly HashSet<Type> RequestsThatSkipDecoration = new HashSet<Type>
                                                                          {
                                                                              typeof(NoRoute),
                                                                              typeof(Monitor),
                                                                              typeof(Authenticate),
                                                                              typeof(PostAuthenticationConnect),
                                                                              typeof(PostDeferredAffected),
                                                                              typeof(PostDeferredMessage),
                                                                              typeof(PostDeferredLowPriMessage),
                                                                              typeof(PostDeferredDealMessage),
                                                                              typeof(PostDeferredPrimaryDealMessage),
                                                                              typeof(PostDeferredFifoMessage),
                                                                              typeof(MqRetry),
                                                                              typeof(QueryDealRequests),
                                                                              typeof(QueryRequestedDeals),
                                                                              typeof(QueryPublishedDeals),
                                                                              typeof(QueryPublisherDeals),
                                                                              typeof(QueryPublisherAccountConnections),
                                                                              typeof(PutWorkspaceAccountLocation),
                                                                              typeof(GetFacebookWebhook),
                                                                              typeof(PostFacebookWebhook),
                                                                              typeof(PostSyncRecentPublisherAccountMedia),
                                                                              typeof(GetDealCompletionMediaMetrics)
                                                                          };

        public static readonly HashSet<Type> RequestsThatSkipLogging = new HashSet<Type>
                                                                       {
                                                                           typeof(Monitor),
                                                                           typeof(RequestLogs),
                                                                           typeof(PutWorkspaceAccountLocation),
                                                                           typeof(GetFacebookWebhook),
                                                                           typeof(PostDeferredMessage),
                                                                           typeof(PostDeferredLowPriMessage),
                                                                           typeof(PostDeferredDealMessage),
                                                                           typeof(PostDeferredPrimaryDealMessage),
                                                                           typeof(PostDeferredFifoMessage),
                                                                           typeof(PostDeferredAffected),
#if !LOCAL
                                                                           typeof(PostAuthenticationConnect),
                                                                           typeof(PostPublisherMediaReceived),
                                                                           typeof(PublisherMediaAnalysisUpdated),
                                                                           typeof(PostAnalyzePublisherText),
                                                                           typeof(PostAnalyzePublisherImage),
#endif
                                                                       };

        public static readonly HashSet<string> WriteableHttpVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                                                    {
                                                                        "POST",
                                                                        "PUT",
                                                                        "DELETE"
                                                                    };
    }

    public class DefaultRequestExecutor<TRequest> : IRequestExecutor<TRequest>
    {
        private readonly IStats _stats;
        private readonly ICounterAndListService _counterService;
        private readonly IServiceCacheInvalidator _serviceCacheInvalidator;
        private readonly IDecorateResponsesService _decorateResponsesService;

        private ILog _log;

        public DefaultRequestExecutor(IStats stats,
                                      ICounterAndListService counterService,
                                      IServiceCacheInvalidator serviceCacheInvalidator,
                                      IDecorateResponsesService decorateResponsesService)
        {
            _stats = stats;
            _counterService = counterService;
            _serviceCacheInvalidator = serviceCacheInvalidator;
            _decorateResponsesService = decorateResponsesService;
        }

        public async Task<object> ExecuteAsync(IRequest req, object instance, TRequest requestDto, Func<IRequest, object, TRequest, Task<object>> innerExecutor)
        {
            var serviceType = instance.GetType();

            _log = instance is BaseApiService baseApiService
                       ? baseApiService.GetLogger() ?? LogManager.GetLogger(serviceType)
                       : LogManager.GetLogger(serviceType);

            var requestDtoAsBase = requestDto as IRequestBase;

            var requestState = GetRequestState(req, requestDtoAsBase, requestDto);

            if (requestState == null)
            {
                return null;
            }

            using(ContextStack.Push(requestState))
            {
                string decoratedMethodName = null;

                var verbName = requestDto.GetType().Name.StartsWith("Query", StringComparison.OrdinalIgnoreCase)
                                   ? "QUERY"
                                   : req.Verb;

                var serviceName = serviceType.Name;

                try
                {
                    decoratedMethodName = PreProcessRequest(requestDto, serviceName, verbName);

                    var response = await innerExecutor(req, instance, requestDto);

                    if (response.IsErrorResponse())
                    {
                        ProcessUnhandledException(response as Exception, serviceName, verbName, decoratedMethodName);

                        return response;
                    }
                    else if (!RequestHelpers.RequestsThatSkipDecoration.Contains(req.Dto.GetType()) &&
                             requestDtoAsBase != null)
                    {
                        await _decorateResponsesService.DecorateAsync(requestDtoAsBase, response);

                        if (RequestHelpers.WriteableHttpVerbs.Contains(verbName))
                        {
                            _serviceCacheInvalidator.Invalidate(serviceType, verbName, req.Dto, requestState);
                        }
                        else if (verbName.EqualsOrdinalCi("GET") && req.Items.ContainsKey(Keywords.CacheInfo))
                        { // Update the GET response with the fetched value on a GET and if the request isn't being fulfilled from cache already
                            _serviceCacheInvalidator.UpdateGetResponseValidAt(serviceType, requestDtoAsBase, requestState);
                        }
                    }

                    if (!RequestHelpers.RequestsThatSkipLogging.Contains(typeof(TRequest)))
                    {
                        var requestTimeString = string.Empty;

                        if (requestDtoAsBase != null)
                        {
                            var requestTime = DateTime.UtcNow - requestDtoAsBase.ReceivedAt;

                            requestTimeString = string.Concat("RequestTime [", (int)requestTime.TotalMinutes, ":", requestTime.Seconds, ".", requestTime.Milliseconds, "]");
                        }

                        _log.LogRequestEnd(serviceName, null, false,
                                           response != null && response is IHaveResults rhr
                                               ? $"{requestTimeString} Results [{rhr.ResultCount}]"
                                               : requestTimeString,
                                           decoratedMethodName);
                    }

                    return response;
                }
                catch(Exception ex) when(ProcessUnhandledException(ex, serviceName, verbName, decoratedMethodName))
                {
                    throw; // Never will run, process unhandled always returns false, will bubble out the exception
                }
                finally
                {
                    PostProcessRequest();
                }
            }
        }

        private IRequestState GetRequestState(IRequest req, IRequestBase requestDtoAsBase, TRequest requestDto)
        {
            var session = req.GetSession();

            var isInternalOrAdminRequest = req.IsRydrRequest();

            RydrUserSession rydrUserSession = null;

            if (session != null && (session is RydrUserSession rus))
            {
                rydrUserSession = rus;
                isInternalOrAdminRequest = isInternalOrAdminRequest || rydrUserSession.IsAdmin || rydrUserSession.UserType == UserType.Admin;
            }

            if (!isInternalOrAdminRequest && session != null &&
                (session.UserName.EqualsOrdinalCi(Keywords.AuthSecret) ||
                 (!session.Roles.IsNullOrEmpty() && session.Roles.Contains(RoleNames.Admin))))
            {
                isInternalOrAdminRequest = true;
            }

            if (!isInternalOrAdminRequest && rydrUserSession == null)
            {
                throw new UnauthorizedException("Invalid SessionState in RequestExecutor");
            }

            var anonAllowed = RequestHelpers.AllowedAnonRequestTypes.Contains(typeof(TRequest));

            var state = new RequestState
                        {
                            IsSystemRequest = isInternalOrAdminRequest,
                            IpAddress = req.RemoteIp.Coalesce(req.UserHostAddress) ?? string.Empty,
                            UserId = (rydrUserSession?.UserId).Nz(isInternalOrAdminRequest
                                                                      ? UserAuthInfo.AdminUserId
                                                                      : -1),
                            WorkspaceId = -1,
                            RequestPublisherAccountId = -1,
                            RoleId = (rydrUserSession?.RoleId).Nz(isInternalOrAdminRequest
                                                                      ? UserAuthInfo.AdminUserId
                                                                      : -1),
                            UserType = rydrUserSession?.UserType ?? (isInternalOrAdminRequest
                                                                         ? UserType.Admin
                                                                         : UserType.Unknown),
                            HttpVerb = req.Verb?.ToUpperInvariant() ?? "GET"
                        };

            if ((requestDto is IHaveOriginalRequestId reqOrigId) && reqOrigId.OriginatingRequestId.HasValue())
            {
                state.RequestId = reqOrigId.OriginatingRequestId;
            }
            else if (state.RequestId.IsNullOrEmpty())
            {
                state.RequestId = Guid.NewGuid().ToStringId();
            }

            if (anonAllowed && !isInternalOrAdminRequest)
            {
                return state;
            }

            if (requestDtoAsBase == null)
            {
                if (isInternalOrAdminRequest)
                {
                    return state;
                }

                throw new UnauthorizedException("Invalid RequestState in RequestExecutor");
            }

            // WorspaceIds and RequestPublisherAccountId are set in the StateAttributeFilter, and they are not part of a session. The filter also has logic to discern setting the
            // workspaceId to the dto vs. header based on auth anyhow, and a session doesn't hold either one...
            state.WorkspaceId = requestDtoAsBase.WorkspaceId;
            state.RequestPublisherAccountId = requestDtoAsBase.RequestPublisherAccountId;

            if (isInternalOrAdminRequest)
            { // If this is an internal/admin request, user/role info is either the incoming value, or admin
                state.UserId = requestDtoAsBase.UserId.Gz(UserAuthInfo.AdminUserId);
                state.RoleId = requestDtoAsBase.RoleId.Gz(UserAuthInfo.AdminUserId);
                state.WorkspaceId = requestDtoAsBase.WorkspaceId.Gz(UserAuthInfo.AdminWorkspaceId);
                requestDtoAsBase.WorkspaceId = state.WorkspaceId;
                req.Items[Keywords.AuthSecret] = RydrEnvironment.AdminKey;
            }

            // Backfill the dto based on the state (if it's an admin, state will be reflected appropriately directly above)
            requestDtoAsBase.UserId = state.UserId;
            requestDtoAsBase.RoleId = state.RoleId;
            requestDtoAsBase.IsSystemRequest = isInternalOrAdminRequest;

            if (!anonAllowed)
            {
                Guard.AgainstUnauthorized(state.UserId <= 0 || requestDtoAsBase.UserId <= 0 || state.UserId != requestDtoAsBase.UserId, "Uid is not valid");
            }

            state.ContextPublisherAccountId = requestDto switch
            {
                IHasPublisherAccountId rdpaid => rdpaid.PublisherAccountId,
                IHasPublisherAccountIdentifier rdpaif => rdpaif.PublisherIdentifier.ToLong(),
                _ => state.RequestPublisherAccountId
            };

            if (state.ContextPublisherAccountId <= 0)
            {
                state.ContextPublisherAccountId = state.RequestPublisherAccountId;
            }

            return state;
        }

        private string PreProcessRequest(TRequest request, string serviceName, string verbName)
        {
            var decoratedMethodName = string.Concat(verbName, "(", typeof(TRequest).Name, ")");

            if (RequestHelpers.RequestsThatSkipLogging.Contains(typeof(TRequest)))
            {
                return decoratedMethodName;
            }

            if (BaseApiService.InShutdown)
            {
                throw new ApplicationInShutdownException();
            }

            _stats.Increment(Stats.AllApiFired);
            _stats.Increment(StatsKey(verbName, StatsKeySuffix.Fired));

            _log.LogRequestStart(serviceName, ContextStack.CurrentState.IpAddress, request, decoratedMethodName);

            // Counter service is local/in-memory (i.e. this API instance only), persisted is distributed (i.e. all API instances)
            _counterService.Increment(Stats.AllApiOpenRequests);

            return decoratedMethodName;
        }

        private void PostProcessRequest()
        {
            if (RequestHelpers.RequestsThatSkipLogging.Contains(typeof(TRequest)))
            {
                return;
            }

            // This effectively is all threads/calls in use for this API instance only
            _counterService.DecrementNonNegative(Stats.AllApiOpenRequests);
        }

        private bool ProcessUnhandledException(Exception ex, string serviceName, string verbName, string extendedMethodName)
        {
            if (ex == null)
            {
                return false;
            }

            var extraInfo = ex is NoRouteException nrx
                                ? nrx.Message ?? "Invalid Route, no message included"
                                : ex is IHttpError htx && (htx.ErrorCode.EqualsOrdinalCi(nameof(NoRouteException)) || htx.StatusDescription.EqualsOrdinalCi(nameof(NoRouteException)))
                                    ? htx.Message ?? "Invalid Route, no message included"
                                    : null;

            // We dont want to bung up the log with a bunch of noRouteExceptions, so ignore those for exception logging
            _log.LogRequestEnd(serviceName, extraInfo == null
                                                ? ex
                                                : null,
                               false, extraInfo, extendedMethodName);

            _stats.Increment(StatsKey(verbName, StatsKeySuffix.Exception));

            return false;
        }

        private static string StatsKey(string serviceName, string metricName)
            => Stats.StatsKey(serviceName, metricName);
    }
}
