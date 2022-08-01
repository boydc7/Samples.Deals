using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Core.Transforms
{
    public static class SharedTransforms
    {
        private static readonly IRequestStateManager _requestStateManager = HostContext.AppHost.Container.Resolve<IRequestStateManager>();

        public static T PopulateWithRequestInfo<T>(this T target, IRequestBase source)
            where T : IRequestBase
        {
            target.UserId = source.UserId;
            target.WorkspaceId = source.WorkspaceId;
            target.RoleId = source.RoleId;
            target.RequestPublisherAccountId = source.RequestPublisherAccountId;
            target.IsSystemRequest = source.IsSystemRequest;

            return target;
        }

        public static T WithAdminRequestInfo<T>(this T target)
            where T : IRequestBase
        {
            target.UserId = UserAuthInfo.AdminUserId;
            target.WorkspaceId = UserAuthInfo.AdminWorkspaceId;
            target.RoleId = target.RoleId;
            target.RequestPublisherAccountId = 0;
            target.IsSystemRequest = true;

            return target;
        }

        public static void UpdateDateTimeDeleteTrackedValuesOnly<TTarget>(this TTarget target, IHasUserAuthorizationInfo state = null)
            where TTarget : IDateTimeDeleteTracked
            => DoUpdateDeleteTrackedValues((IDateTimeDeleteTracked)null, target, state);

        public static void UpdateDateTimeDeleteTrackedValues<TTarget, TSource>(this TTarget target, TSource source, IHasUserAuthorizationInfo state = null)
            where TTarget : IDateTimeDeleteTracked, IDateTimeTracked
            where TSource : IDateTimeDeleteTracked, IDateTimeTracked
        {
            Guard.AgainstNullArgument(source == null, "Source cannot be null - only use this method to update both created/modified AND delete modifications from a source record");

            DoUpdateNonDeleteTrackedValues(source, target, state);
            DoUpdateDeleteTrackedValues(source, target, state);
        }

        public static void UpdateDateTimeTrackedValues<TTarget, TSource>(this TTarget target, TSource source, IHasUserAuthorizationInfo state = null)
            where TTarget : IDateTimeTracked
            where TSource : IDateTimeTracked
            => DoUpdateNonDeleteTrackedValues(source, target, state);

        public static void UpdateDateTimeTrackedValues<TSource>(this TSource target, IHasUserAuthorizationInfo state = null)
            where TSource : class, IDateTimeTracked
            => DoUpdateNonDeleteTrackedValues<TSource, TSource>(null, target, state);

        private static void DoUpdateNonDeleteTrackedValues<TSource, TTarget>(TSource source, TTarget target, IHasUserAuthorizationInfo state = null)
            where TSource : IDateTimeTracked
            where TTarget : IDateTimeTracked
        {
            var utcNow = DateTimeHelper.UtcNow;

            var currentState = state ?? _requestStateManager.GetState();

            var stateUserId = state != null && state.UserId > 0
                                  ? state.UserId
                                  : currentState != null && currentState.UserId > 0
                                      ? currentState.UserId
                                      : 0;

            var stateWorkspaceId = state != null && state.WorkspaceId > 0
                                       ? state.WorkspaceId
                                       : currentState != null && currentState.WorkspaceId > 0
                                           ? currentState.WorkspaceId
                                           : 0;

            target.ModifiedOn = utcNow;

            target.ModifiedBy = stateUserId > 0
                                    ? stateUserId
                                    : (source?.ModifiedBy ?? 0).Nz(target.ModifiedBy).Nz(-1);

            target.ModifiedWorkspaceId = stateWorkspaceId > 0
                                             ? stateWorkspaceId
                                             : (source?.ModifiedWorkspaceId ?? 0).Nz(target.ModifiedWorkspaceId).Nz(-1);

            if (target.CreatedOn <= DateTimeHelper.MinApplicationDate)
            {
                var sourceCreatedOn = source?.CreatedOn;

                target.CreatedOn = sourceCreatedOn.HasValue && sourceCreatedOn.Value > DateTimeHelper.MinApplicationDate
                                       ? sourceCreatedOn.Value
                                       : utcNow;
            }

            if (target.CreatedBy <= 0)
            {
                target.CreatedBy = source != null && source.CreatedBy > 0
                                       ? source.CreatedBy
                                       : stateUserId > 0
                                           ? stateUserId
                                           : target.CreatedBy.Nz(-1);

                target.CreatedWorkspaceId = source != null && source.CreatedWorkspaceId > 0
                                                ? source.CreatedWorkspaceId
                                                : stateWorkspaceId > 0
                                                    ? stateWorkspaceId
                                                    : target.CreatedWorkspaceId.Nz(-1);
            }

            if (!(target is IHasWorkspaceId targetWithOrg))
            {
                return;
            }

            if (targetWithOrg.WorkspaceId <= 0 && source != null && source is IHasWorkspaceId sourceWithOrg)
            {
                targetWithOrg.WorkspaceId = sourceWithOrg.WorkspaceId;
            }

            if (targetWithOrg.WorkspaceId <= 0)
            {
                targetWithOrg.WorkspaceId = currentState.WorkspaceId;
            }

            if (targetWithOrg.WorkspaceId <= 0 && target.CreatedWorkspaceId > 0)
            {
                targetWithOrg.WorkspaceId = target.CreatedWorkspaceId;
            }
        }

        private static void DoUpdateDeleteTrackedValues<TSource, TTarget>(TSource source, TTarget target, IHasUserAuthorizationInfo state = null)
            where TSource : IDateTimeDeleteTracked
            where TTarget : IDateTimeDeleteTracked
        {
            if (target == null || target.DeletedBy.GetValueOrDefault() > 0 && target.DeletedOn.HasValue)
            {
                return;
            }

            if (!target.DeletedOn.HasValue)
            {
                target.DeletedOn = source == null
                                       ? DateTimeHelper.UtcNow
                                       : source.DeletedOn;
            }

            if (!target.DeletedBy.HasValue && source != null && !source.DeletedBy.HasValue)
            {
                return;
            }

            if (target.DeletedBy.GetValueOrDefault() <= 0)
            {
                var currentState = _requestStateManager.GetState();

                target.DeletedBy = source != null && source.DeletedBy.HasValue
                                       ? source.DeletedBy
                                       : state != null && state.UserId > 0
                                           ? state.UserId
                                           : currentState.UserId.Nz(-1);

                target.DeletedByWorkspaceId = source != null && source.DeletedByWorkspaceId.HasValue
                                                  ? source.DeletedByWorkspaceId
                                                  : state != null && state.WorkspaceId > 0
                                                      ? state.WorkspaceId
                                                      : currentState.WorkspaceId.Nz(-1);
            }
        }
    }
}
