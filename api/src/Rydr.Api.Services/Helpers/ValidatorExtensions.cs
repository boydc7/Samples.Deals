using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EnumsNET;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Services.Validators;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.FluentValidation;
using ServiceStack.Web;

namespace Rydr.Api.Services.Helpers
{
    public static class ValidationExtensions
    {
        internal static readonly IPocoDynamo _dynamoDb = RydrEnvironment.Container.Resolve<IPocoDynamo>();
        internal static readonly IRequestStateManager _requestStateManager = RydrEnvironment.Container.Resolve<IRequestStateManager>();

        public static bool IsRydrRequest(this IRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.RequestAttributes.HasAnyFlags(RequestAttributes.MessageQueue |
                                                      RequestAttributes.RydrInternalRequest |
                                                      RequestAttributes.InProcess))
            {
                return true;
            }

#if LOCAL
            return false;
#endif

#pragma warning disable 162
            return request.RequestAttributes.HasFlag(RequestAttributes.Localhost);
#pragma warning restore 162
        }

        /// <summary>
        ///     Return true for workspaces that have an active subscription and allow multiple users on their account (i.e. teams,
        ///     admins)
        /// </summary>
        /// <param name="request"></param>
        /// <returns>True if the request comes from a workspace with an active subscription in a team workspace</returns>
        public static async Task<bool> IsSubscribedTeamWorkspaceAsync(this IRequestBase request)
        {
            if (request == null || request.WorkspaceId <= 0)
            {
                return false;
            }

            if (request.IsSystemRequest)
            {
                return true;
            }

            var workspace = await WorkspaceService.DefaultWorkspaceService
                                                  .GetWorkspaceAsync(request.WorkspaceId);

            if (workspace == null || workspace.IsDeleted() || !workspace.WorkspaceType.AllowsMultipleUsers())
            {
                return false;
            }

            var activeSubscriptionType = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                               .GetActiveWorkspaceSubscriptionTypeAsync(workspace);

            return activeSubscriptionType.IsActiveSubscriptionType();
        }

        /// <summary>
        ///     Returns true for workspaces that have an active subscription period - does NOT require workspaces be multi-user
        ///     accounts (i.e.
        ///     personal workspaces with one or more subscribed profiles will return true here)
        /// </summary>
        /// <param name="request"></param>
        /// <returns>True for any request that comes from a workspace with an active subscription of any kind</returns>
        public static async Task<bool> IsSubscribedWorkspaceAsync(this IRequestBase request)
        {
            if (request == null || request.WorkspaceId <= 0)
            {
                return false;
            }

            if (request.IsSystemRequest)
            {
                return true;
            }

            var workspace = await WorkspaceService.DefaultWorkspaceService
                                                  .GetWorkspaceAsync(request.WorkspaceId);

            if (workspace == null || workspace.IsDeleted())
            {
                return false;
            }

            var activeSubscriptionType = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                               .GetActiveWorkspaceSubscriptionTypeAsync(workspace);

            return activeSubscriptionType.IsActiveSubscriptionType();
        }

        public static void UpdateStateIntentTo<T>(this IRuleBuilder<T, IDateTimeTracked> ruleBuilder, AccessIntent toIntent)
            => ruleBuilder.Must(d =>
                                {
                                    _requestStateManager.UpdateStateIntent(toIntent);

                                    return true;
                                })
                          .WithName("StateIntent")
                          .OverridePropertyName("StateIntent")
                          .WithErrorCode(ErrorCodes.InvalidState)
                          .WithMessage("UpdateStateIntent invalid or unavailable");

        public static void UpdateStateIntentTo<T>(this IRuleBuilder<T, IHasUserAuthorizationInfo> ruleBuilder, AccessIntent toIntent)
            => ruleBuilder.Must(d =>
                                {
                                    _requestStateManager.UpdateStateIntent(toIntent);

                                    return true;
                                })
                          .WithName("StateIntent")
                          .OverridePropertyName("StateIntent")
                          .WithErrorCode(ErrorCodes.InvalidState)
                          .WithMessage("UpdateStateIntent invalid or unavailable");

        public static IRuleBuilderOptions<T, RecordTypeId> IsValidRecordTypeId<T>(this IRuleBuilder<T, RecordTypeId> ruleBuilder, string fieldName = null)
            => ruleBuilder.SetValidator(new RecordTypeIdValidator(fieldName));

        public static IRuleBuilderOptions<T, List<long>> IsValidIdList<T>(this IRuleBuilder<T, List<long>> ruleBuilder, string fieldName)
            => ruleBuilder.SetValidator(new IdListValidIfNotNullOrEmptyValidator(fieldName));

        public static IRuleBuilderOptions<T, DateTime> IsValidDateTime<T>(this IRuleBuilder<T, DateTime> ruleBuilder, string fieldName, bool canBeEmpty = false)
            => ruleBuilder.SetValidator(new DateTimeAttributeValidator(fieldName, canBeEmpty));

        public static IRuleBuilderOptions<T, DynItemValidationSource> IsValidDynamoItem<T>(this IRuleBuilder<T, DynItemValidationSource> ruleBuilder)
            => ruleBuilder.SetValidator(new DynItemExistsValidator<DynItem>(_dynamoDb));

        public static IRuleBuilderOptions<T, DynItemValidationSource<TDynType>> IsValidDynamoItem<T, TDynType>(this IRuleBuilder<T, DynItemValidationSource<TDynType>> ruleBuilder)
            where TDynType : DynItem
            => ruleBuilder.SetValidator(new DynItemExistsValidator<TDynType>(_dynamoDb));

        public static DynItemValidationSource ToDynItemValidationSourceByRef(this IRequestBase request, long hashId, long referenceId, DynItemType type)
            => ToDynItemValidationSource(request, hashId, null, type, referenceId.ToStringInvariant());

        public static DynItemValidationSource<T> ToDynItemValidationSourceByRef<T>(this IRequestBase request, long hashId, string referenceId,
                                                                                   DynItemType type, ApplyToBehavior treatLike)
            => ToDynItemValidationSource<T>(request, hashId, null, type, referenceId, treatLike);

        public static DynItemValidationSource ToDynItemValidationSourceByRef(this IRequestBase request, long hashId, long referenceId,
                                                                             DynItemType type, ApplyToBehavior treatLike)
            => ToDynItemValidationSource(request, hashId, null, type, referenceId.ToStringInvariant(), treatLike);

        public static DynItemValidationSource<T> ToDynItemValidationSourceByRef<T>(this IRequestBase request, long hashId, long referenceId,
                                                                                   DynItemType type, ApplyToBehavior treatLike, bool skipAccessChecks)
            => ToDynItemValidationSource<T>(request, hashId, null, type, referenceId.ToStringInvariant(), treatLike, skipAccessChecks);

        public static DynItemValidationSource<T> ToDynItemValidationSource<T>(this IRequestBase request, string edgeId, DynItemType type, ApplyToBehavior treatLike)
            => ToDynItemValidationSource<T>(request, 0, edgeId, type, treatLike: treatLike);

        public static DynItemValidationSource<T> ToDynItemValidationSource<T>(this IRequestBase request, string edgeId, DynItemType type, ApplyToBehavior treatLike, Func<T, bool> alsoMust)
            => ToDynItemValidationSource(request, 0, edgeId, type, treatLike: treatLike, alsoMust: alsoMust);

        public static DynItemValidationSource ToDynItemValidationSource(this IRequestBase request, string edgeId, DynItemType type, ApplyToBehavior treatLike)
            => ToDynItemValidationSource(request, 0, edgeId, type, treatLike: treatLike);

        public static DynItemValidationSource ToDynItemValidationSource(this IRequestBase request, string edgeId, DynItemType type, ApplyToBehavior treatLike, bool skipAccessChecks)
            => ToDynItemValidationSource(request, 0, edgeId, type, treatLike: treatLike, skipAccessChecks: skipAccessChecks);

        public static DynItemValidationSource ToDynItemValidationSource(this IRequestBase request, long edgeAndHashId, DynItemType type, ApplyToBehavior treatLike)
            => ToDynItemValidationSource(request, edgeAndHashId, edgeAndHashId.ToEdgeId(), type, treatLike: treatLike);

        public static DynItemValidationSource<T> ToDynItemValidationSource<T>(this IRequestBase request, long edgeAndHashId, DynItemType type, ApplyToBehavior treatLike)
            => ToDynItemValidationSource<T>(request, edgeAndHashId, edgeAndHashId.ToEdgeId(), type, treatLike: treatLike);

        public static DynItemValidationSource ToDynItemValidationSource(this IRequestBase request, long id, long edgeId, DynItemType type,
                                                                        ApplyToBehavior treatLike = ApplyToBehavior.Default, bool skipAccessChecks = false)
            => ToDynItemValidationSource(request, id, edgeId.ToEdgeId(), type, treatLike: treatLike, skipAccessChecks: skipAccessChecks);

        private static DynItemValidationSource ToDynItemValidationSource(this IRequestBase request, long id, string edgeId,
                                                                         DynItemType type, string referenceId = null,
                                                                         ApplyToBehavior treatLike = ApplyToBehavior.Default, bool skipAccessChecks = false)
            => new DynItemValidationSource(request, id, edgeId, type, referenceId, treatLike, skipAccessChecks);

        public static DynItemValidationSource<T> ToDynItemValidationSource<T>(this IRequestBase request, long id, string edgeId,
                                                                              DynItemType type, string referenceId = null,
                                                                              ApplyToBehavior treatLike = ApplyToBehavior.Default, bool skipAccessChecks = false,
                                                                              Func<T, bool> alsoMust = null)
            => new DynItemValidationSource<T>(request, id, edgeId, type, referenceId, treatLike, skipAccessChecks,
                                              alsoMust);
    }
}
