using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Funq;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Auth;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Model;

// ReSharper disable UnusedMember.Local

namespace Rydr.Api.Core.Configuration
{
    public class DeferredProcessingConfiguration : IAppHostConfigurer
    {
        private readonly Dictionary<Type, IAuthorizer> _dynItemTypeValidators = new Dictionary<Type, IAuthorizer>();

        public void Apply(ServiceStackHost appHost, Container container)
        {
            container.RegisterAutoWired<GenericDeferredRequestService>();

            container.Register<IDeferRequestsService>(c => c.Resolve<GenericDeferredRequestService>())
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IDeferredRequestProcessingService>(c => c.Resolve<GenericDeferredRequestService>())
                     .ReusedWithin(ReuseScope.Hierarchy);

            // Register models, filter maps, etc.

            // Dynamo stored records...
            RegisterDynGetRecordsService<DynFile>(container, RecordType.File, (i, s, d) => i.ToItemDynamoId(), null, null, DynItemType.File);

            RegisterDynGetRecordsService<DynPublisherApprovedMedia, RydrPublisherApprovedMedia, long>(container, RecordType.ApprovedMedia, null, (i, s, d) => d.GetItemByEdgeIntoAsync<DynPublisherApprovedMedia>(DynItemType.ApprovedMedia, i),
                                                                                                      null, d => d.ToRydrPublisherApprovedMedia(), DynItemType.ApprovedMedia);

            RegisterDynGetRecordsService(container, RecordType.Place, null, (i, s, d) => d.GetPlaceAsync(i), null, DynItemType.Place);
            RegisterDynGetRecordsService(container, RecordType.Hashtag, null, (i, s, d) => d.GetHashtagAsync(i), null, DynItemType.Hashtag);

            RegisterDynGetRecordsService(container, RecordType.Deal, null,
                                         (i, s, d) => s.HasValue()
                                                          ? DealExtensions.DefaultDealService.GetDealAsync(i, s.ToLong())
                                                          : DealExtensions.DefaultDealService.GetDealAsync(i),
                                         null, d => d.ToRydrDeal(), DynItemType.Deal);

            RegisterDynGetRecordsService(container, RecordType.Dialog, null, (i, s, d) => d.GetItemByRefAsync<DynDialog>(i, DynItemType.Dialog),
                                         new Func<DynDialog, IRequestState, AuthorizerResult>[]
                                         { // For dialogs, current user must have created or be a member of the dialog
                                             (d, s) => AuthorizerResult.UnauthorizedIf(!s.IsSystemRequest && d.CreatedBy != s.UserId &&
                                                                                       !d.Members.Contains(new RecordTypeId(RecordType.User, s.UserId)) &&
                                                                                       (s.RequestPublisherAccountId <= 0 || !d.Members.Contains(new RecordTypeId(RecordType.PublisherAccount, s.RequestPublisherAccountId))))
                                         },
                                         DynItemType.Dialog);

            RegisterDynGetRecordsService(container, RecordType.PublisherMedia, null, (i, s, d) => d.GetItemAsync<DynPublisherMedia>(i, s), null, DynItemType.PublisherMedia);

            // Those that do not need filter lookups...
            RegisterDynGetRecordsService<DynDealRequest, RydrDealRequest, string>(container, RecordType.DealRequest, null,
                                                                                  (i, s, d) => d.GetItemAsync<DynDealRequest>(i, s),
                                                                                  null, d => d.ToRydrDealRequest(), DynItemType.DealRequest);

            RegisterDynGetRecordsService<DynWorkspace, RydrWorkspace, long>(container, RecordType.Workspace, null, (i, s, d) => WorkspaceService.DefaultWorkspaceService.TryGetWorkspaceAsync(i), null, w => w.ToRydrWorkspace(), DynItemType.Workspace);
            RegisterDynGetRecordsService<DynWorkspaceSubscription, RydrWorkspaceSubscription, long>(container, RecordType.WorkspaceSubscription, null, (i, s, d) => WorkspaceService.DefaultWorkspaceSubscriptionService.TryGetWorkspaceSubscriptionConsistentAsync(i, s), null, w => w.ToRydrWorkspaceSubscription(), DynItemType.WorkspaceSubscription);

            RegisterDynGetRecordsService<DynWorkspacePublisherSubscription, RydrWorkspacePublisherSubscription, string>(container, RecordType.WorkspacePublisherSubscription, null, (i, s, d) => WorkspaceService.DefaultWorkspacePublisherSubscriptionService.TryGetPublisherSubscriptionConsistentAsync(i, s), null, w => w.ToRydrWorkspacePublisherSubscription(),
                                                                                                                        DynItemType.WorkspacePublisherSubscription);

            RegisterDynGetRecordsService<DynUser, RydrUser, long>(container, RecordType.User, null, (i, s, d) => UserExtensions.DefaultUserService.GetUserAsync(i), null, u => u.ToRydrUsers(), DynItemType.User);

            RegisterDynGetRecordsService(container, RecordType.Message, null,
                                         (i, s, d) => d.GetItemByEdgeIntoAsync<DynDialogMessage>(DynItemType.Message, i),
                                         null, DynItemType.Message);

            RegisterDynGetRecordsService<DynPublisherAccount, RydrPublisherAccount, long>(container, RecordType.PublisherAccount, null,
                                                                                          (i, s, d) => PublisherExtensions.DefaultPublisherAccountService.TryGetPublisherAccountAsync(i),
                                                                                          new Func<DynPublisherAccount, IRequestState, AuthorizerResult>[]
                                                                                          {
                                                                                              (dpa, state) => dpa.PublisherAccountId == state.RequestPublisherAccountId ||
                                                                                                              dpa.PublisherAccountId == state.RoleId
                                                                                                                  ? AuthorizerResult.ExplicitlyAuthorized
                                                                                                                  : AuthorizerResult.Unspecified
                                                                                          },
                                                                                          dpa => dpa.ToRydrPublisherAccounts(),
                                                                                          DynItemType.PublisherAccount);

            RegisterDynGetRecordsService(container, RecordType.PublisherApp, null, (i, s, d) => d.GetPublisherAppAsync(i), null, DynItemType.PublisherApp);

            RegisterDynGetRecordsService<DynPublisherMediaStat, RydrPublisherMediaStat, string>(container, RecordType.PublisherMediaStat, null, (i, s, d) => d.GetItemAsync<DynPublisherMediaStat>(i, s), null, d => d.ToRydrPublisherMediaStats(), DynItemType.PublisherMediaStat);
            RegisterDynGetRecordsService<DynDailyStatSnapshot, RydrDailyStatSnapshot, string>(container, RecordType.DailyStatSnapshot, null, (i, s, d) => d.GetItemAsync<DynDailyStatSnapshot>(i, s), null, d => d.ToRydrDailyStatSnapshots(), DynItemType.DailyStatSnapshot);
            RegisterDynGetRecordsService<DynDailyStat, RydrDailyStat, string>(container, RecordType.DailyStat, null, (i, s, d) => d.GetItemAsync<DynDailyStat>(i, s), null, d => d.ToRydrDailyStats(), DynItemType.DailyStat);

            // Register the map of item types to validations (used later in auth config)
            container.Register("DynItemTypeValidationAuthorizations", _dynItemTypeValidators);

            // Common deferred processing services...
            container.Register<IDeferredAffectedProcessingService>(c => new CompositeDeferredAffectedProcessingService(GetProcessingServices(c)))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IDecorateSqlExpressionService>(c => new CompositeSqlExpressionDecorator(new[]
                                                                                                       {
                                                                                                           new AssociatedIdsSqlExpressionDecorator(c.Resolve<IRydrDataService>(),
                                                                                                                                                   c.Resolve<IAssociationService>())
                                                                                                       }))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IDecorateDynamoExpressionService>(c => new CompositeDynamoExpressionDecorator(new IDecorateDynamoExpressionService[]
                                                                                                             {
                                                                                                                 new UnDeletedDynamoExpressionDecorator(), new OwnerWorkspaceIdDynamoExpressionDecorator(), new ZeroIdDynamoExpressionDecorator()
                                                                                                             }))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IDecorateResponsesService>(c => new CompositeResponseDecoratorService(new IDecorateResponsesService[]
                                                                                                     {
                                                                                                         new DeferAffectedResponseDecoratorService(c.Resolve<IRequestStateManager>(),
                                                                                                                                                   c.Resolve<IDeferRequestsService>()),
                                                                                                         new PublisherAccountResponseDecoratorService(c.Resolve<IPublisherAccountService>())
                                                                                                     }))
                     .ReusedWithin(ReuseScope.Hierarchy);

            // Record services
            var entityTypeRecordService = new DefaultRecordTypeRecordService(container);

            container.Register<IRecordTypeRecordService>(entityTypeRecordService);
            container.Register<IGetRecordServiceFactory>(entityTypeRecordService);
        }

        private IEnumerable<IDeferredAffectedProcessingService> GetProcessingServices(Container c) =>
            new IDeferredAffectedProcessingService[]
            {
                new RydrSqlDeferredAffectedLookupsService(c.Resolve<IRecordTypeRecordService>())
            };

        private void RegisterDynGetRecordsService<T, TRydr>(Container container, RecordType type,
                                                            Func<long, string, IPocoDynamo, DynamoId> idResolver,
                                                            Func<long, string, IPocoDynamo, Task<T>> recordResolver, // One or the other of this or idResolver, one can be null
                                                            IEnumerable<Func<T, IRequestState, AuthorizerResult>> validations,
                                                            Func<T, IEnumerable<TRydr>> rydrTransform,
                                                            params DynItemType[] itemTypes)
            where T : DynItem, ICanBeRecordLookup, IHasName
            where TRydr : IHasLongId
        {
            RegisterDynGetRecordsService<T, TRydr, long>(container, type, idResolver, recordResolver, validations, rydrTransform, itemTypes);
        }

        private void RegisterRydrGetRecordsService<T>(Container container, RecordType type)
            where T : BaseDataModel, ICanBeRecordLookup
        {
            container.Register<IGetRecordsService<T>>(c => new GenericRydrGetRecordsService<T>(type,
                                                                                               c.Resolve<IRydrDataService>(),
                                                                                               c.Resolve<IAuthorizationService>(),
                                                                                               c.Resolve<IRequestStateManager>()))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IGetRecordsService>(type.ToString(), c => c.Resolve<IGetRecordsService<T>>())
                     .ReusedWithin(ReuseScope.Hierarchy);
        }

        private void RegisterDynGetRecordsService<T>(Container container, RecordType type,
                                                     Func<long, string, IPocoDynamo, DynamoId> idResolver,
                                                     Func<long, string, IPocoDynamo, Task<T>> recordResolver, // One or the other of this or idResolver, one can be null
                                                     IEnumerable<Func<T, IRequestState, AuthorizerResult>> validations,
                                                     params DynItemType[] itemTypes)
            where T : DynItem, ICanBeRecordLookup
            => RegisterDynGetRecordsService<T, T, long>(container, type, idResolver, recordResolver, validations, null, itemTypes);

        private void RegisterDynGetRecordsService<T, TRydr, TRydrIdType>(Container container, RecordType type,
                                                                         Func<long, string, IPocoDynamo, DynamoId> idResolver,
                                                                         Func<long, string, IPocoDynamo, Task<T>> recordResolver, // One or the other of this or idResolver, one can be null
                                                                         IEnumerable<Func<T, IRequestState, AuthorizerResult>> validations,
                                                                         Func<T, IEnumerable<TRydr>> rydrTransform,
                                                                         params DynItemType[] itemTypes)
            where T : DynItem, ICanBeRecordLookup
            where TRydr : IHasId<TRydrIdType>
        {
            container.Register<IGetRecordsService<T>>(c => new GenericDynGetRecordsService<T, TRydr, TRydrIdType>(type,
                                                                                                                  c.Resolve<IPocoDynamo>(),
                                                                                                                  c.Resolve<IAuthorizationService>(),
                                                                                                                  c.Resolve<IRequestStateManager>(),
                                                                                                                  c.Resolve<IRydrDataService>(),
                                                                                                                  idResolver,
                                                                                                                  recordResolver,
                                                                                                                  rydrTransform))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IGetRecordsService>(type.ToString(), c => c.Resolve<IGetRecordsService<T>>())
                     .ReusedWithin(ReuseScope.Hierarchy);

            if (itemTypes == null || itemTypes.Length <= 0)
            {
                return;
            }

            _dynItemTypeValidators.Add(typeof(T), new DynItemTypeValidationAuthorizer<T>(itemTypes, container.Resolve<IRequestStateManager>(), validations));
        }
    }
}
