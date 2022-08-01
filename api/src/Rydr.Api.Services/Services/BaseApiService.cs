using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Services.Filters;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;
using ServiceStack.Logging;

// ReSharper disable InconsistentNaming

namespace Rydr.Api.Services.Services
{
    [CompressResponse]
    [RequiresRequestState]
    public abstract class BaseApiService : Service
    {
        protected static readonly Func<IAdminServiceGateway> _adminServiceGatewayFactory = () => RydrEnvironment.Container.Resolve<IAdminServiceGateway>();

        protected readonly ILog _log;

        protected BaseApiService()
        {
            _log = LogManager.GetLogger(GetType());
        }

        public IDateTimeProvider _dateTimeProvider { get; set; }
        public IPocoDynamo _dynamoDb { get; set; }

        public ILog GetLogger() => _log;

        public static bool InShutdown { get; set; }

        protected async Task PostUpsertModelsAsync<TPost, TModel>(IEnumerable<TModel> models, IRequestBase request, bool allowZeroResponse = false)
            where TModel : class, IHasSettableId
            where TPost : IPost, IHaveModel<TModel>, IRequestBase, IReturn<LongIdResponse>, new()
        {
            foreach (var model in models)
            {
                await PostUpsertModelAsync<TPost, TModel>(model, request, allowZeroResponse);
            }
        }

        protected async Task PostUpsertModelAsync<TPost, TModel>(TModel model, IRequestBase request, bool allowZeroResponse = false)
            where TModel : class, IHasSettableId
            where TPost : IPost, IHaveModel<TModel>, IRequestBase, IReturn<LongIdResponse>, new()
        {
            var post = new TPost
                       {
                           Model = model
                       }.PopulateWithRequestInfo(request);

            post.IsSystemRequest = true;

            var response = await Gateway.SendAsync(post);

            if (!allowZeroResponse)
            {
                Guard.Against<HttpError>(response == null || response.Id <= 0, $"Invalid response id - Code [{response.ResponseStatus?.ErrorCode}], Message [{response.ResponseStatus?.Message}], ErrorCode [{response.ResponseStatus?.ErrorCode}]");
            }

            model.Id = response.Id;
        }

        protected async Task PutOrPostModelAsync<TPut, TPost, TModel>(TModel model, IRequestBase request)
            where TModel : class, IHasSettableId
            where TPut : BasePutRequest<TModel>, new()
            where TPost : BasePostRequest<TModel>, new()
        {
            LongIdResponse response = null;

            if (model.Id > 0)
            {
                var put = new TPut
                          {
                              Id = model.Id,
                              Model = model
                          }.PopulateWithRequestInfo(request);

                put.IsSystemRequest = true;

                response = await Gateway.SendAsync(put);
            }
            else
            {
                var post = new TPost
                           {
                               Model = model
                           }.PopulateWithRequestInfo(request);

                post.IsSystemRequest = true;

                response = await Gateway.SendAsync(post);
            }

            Guard.Against<HttpError>(response == null || response.Id <= 0, $"Invalid response id - Code [{response.ResponseStatus?.ErrorCode}], Message [{response.ResponseStatus?.Message}], ErrorCode [{response.ResponseStatus?.ErrorCode}]");

            model.Id = response.Id;
        }

        [Obsolete]
        public QueryResponse<From> Exec<From>(IQueryDb<From> dto)
            => throw new ApplicationException("Do not use this method, use IAutoQueryService service instead");

        [Obsolete]
        public QueryResponse<Into> Exec<From, Into>(IQueryDb<From, Into> dto)
            => throw new ApplicationException("Do not use this method, use IAutoQueryService service instead");

        [Obsolete]
        public Task<QueryResponse<From>> ExecAsync<From>(IQueryDb<From> dto)
            => throw new ApplicationException("Do not use this method, use IAutoQueryService service instead");

        [Obsolete]
        public Task<QueryResponse<Into>> ExecAsync<From, Into>(IQueryDb<From, Into> dto)
            => throw new ApplicationException("Do not use this method, use IAutoQueryService service instead");

        [Obsolete]
        public QueryResponse<From> Exec<From>(IQueryData<From> dto)
            => throw new ApplicationException("Do not use this method, use IAutoQueryService service instead");

        [Obsolete]
        public QueryResponse<Into> Exec<From, Into>(IQueryData<From, Into> dto)
            => throw new ApplicationException("Do not use this method, use IAutoQueryService service instead");
    }

    [Authenticate]
    public abstract class BaseAuthenticatedApiService : BaseApiService { }

    [RequiredRole("Admin")]
    [Restrict(VisibleLocalhostOnly = true)]
    [ExcludeMetadata]
    public abstract class BaseAdminApiService : BaseAuthenticatedApiService { }

    [Restrict(new[]
              {
                  RequestAttributes.RydrInternalRequest,
                  RequestAttributes.Localhost,
                  RequestAttributes.InProcess,
                  RequestAttributes.MessageQueue,
                  RequestAttributes.MessageQueue | RequestAttributes.HttpPost,
                  RequestAttributes.InProcess | RequestAttributes.AnyHttpMethod,
                  RequestAttributes.Localhost | RequestAttributes.AnyHttpMethod
              },
              new[]
              {
                  RequestAttributes.Localhost
              })]
    [ExcludeMetadata]
    public abstract class BaseInternalOnlyApiService : BaseAuthenticatedApiService { }
}
