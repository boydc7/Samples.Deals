using System;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;
using ServiceStack.Model;

namespace Rydr.Api.Services.Validators
{
    public abstract class BaseRydrValidator<T> : AbstractValidator<T>
        where T : IHasUserAuthorizationInfo
    {
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        // ReSharper disable once StaticMemberInGenericType
        private static readonly IAuthorizeService _authorizeService = RydrEnvironment.Container.Resolve<IAuthorizeService>();

        protected BaseRydrValidator()
        {
            RuleFor(e => e)
                .Must(e => Request != null)
                .WithName("Request")
                .WithMessage("Request is invalid")
                .WithErrorCode(ErrorCodes.InvalidArguments);

            // Session info should be valid
            RuleFor(e => e.UserId)
                .MustAsync(async (e, u, ctx, t) =>
                           {
                               var rydrUserSession = Request.SessionAs<RydrUserSession>();

                               if (rydrUserSession == null)
                               {
                                   ctx.MessageFormatter.AppendArgument("rydrvmsg", "brvrusnx");

                                   return false;
                               }

                               if (rydrUserSession.IsAdmin)
                               {
                                   return rydrUserSession.IsAuthenticated;
                               }

                               if (Request.IsRydrRequest())
                               {
                                   return true;
                               }

                               if (rydrUserSession.UserId <= 0 ||
                                   rydrUserSession.UserType == UserType.Unknown ||
                                   !rydrUserSession.Id.HasValue())
                               {
                                   ctx.MessageFormatter.AppendArgument("rydrvmsg", "brvuidnx");

                                   return false;
                               }

                               if (e.WorkspaceId > 0 && e.WorkspaceId != e.UserId)
                               { // If the workspace is > 0 but does not match the userId, have to validate that the user has permission to the workspace
                                   if (!(await _authorizeService.IsAuthorizedAsync(e.UserId, e.WorkspaceId)))
                                   {
                                       ctx.MessageFormatter.AppendArgument("rydrvmsg", "brvuidnzwid");

                                       return false;
                                   }

                                   // In this case the workspace must also be a valid team account (i.e. a paid subscription that allows multiple users to access the workspace)
                                   var userHasAccessToWorkspace = await WorkspaceService.DefaultWorkspaceService
                                                                                        .UserHasAccessToWorkspaceAsync(e.WorkspaceId, e.UserId);

                                   if (!userHasAccessToWorkspace)
                                   {
                                       ctx.MessageFormatter.AppendArgument("rydrvmsg", "brvpwsnownr");

                                       return false;
                                   }
                               }

                               if (e.RequestPublisherAccountId > 0)
                               { // If a publsiher account id is specified, must be permissioned to use it (either the workspace or the user directly)
                                   var workspaceAuthorized = e.WorkspaceId > 0 && (await _authorizeService.IsAuthorizedAsync(e.WorkspaceId, e.RequestPublisherAccountId));

                                   if (!workspaceAuthorized &&
                                       !(await _authorizeService.IsAuthorizedAsync(e.UserId, e.RequestPublisherAccountId)
                                        ))
                                   {
                                       ctx.MessageFormatter.AppendArgument("rydrvmsg", "brvrpanztowsp");

                                       return false;
                                   }
                               }

                               return true;
                           })
                .When(e => Request != null)
                .WithName("SessionState")
                .WithMessage("Session state is invalid - code [{rydrvmsg}]")
                .WithErrorCode(ErrorCodes.InvalidArguments);
        }
    }

    public abstract class BaseGetRequestValidator<TRequest> : BaseRydrValidator<TRequest>
        where TRequest : IGet, IHasLongId, IRequestBase
    {
        protected BaseGetRequestValidator(bool idRequired = true)
        {
            RuleFor(e => e.Id)
                .GreaterThan(0)
                .When(e => idRequired)
                .WithErrorCode(ErrorCodes.MustBeSpecified);
        }
    }

    public abstract class BasePostRequestValidator<TRequest, TModel> : BaseRydrValidator<TRequest>
        where TRequest : IRequestBaseWithModel<TModel>, IPost
        where TModel : class, IHasLongId
    {
        protected BasePostRequestValidator(Func<TRequest, AbstractValidator<TModel>> modelValidationFactory)
        {
            RuleFor(e => e.Model)
                .NotNull()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Model.Id)
                .Empty()
                .When(e => e.Model != null)
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            RuleFor(e => e.Model)
                .SetValidator(modelValidationFactory)
                .When(e => e.Model != null);
        }
    }

    public abstract class BaseUpsertRequestValidator<TRequest, TModel> : BaseRydrValidator<TRequest>
        where TRequest : IPost, IHaveModel<TModel>, IReturn<LongIdResponse>, IRequestBase
        where TModel : class, IHasLongId
    {
        protected BaseUpsertRequestValidator(Func<TRequest, bool, AbstractValidator<TModel>> modelValidationFactory)
        {
            RuleFor(e => e.Model)
                .NotNull()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Model.Id)
                .GreaterThanOrEqualTo(0) // Upsert, 0 or more
                .When(e => e.Model != null)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.Model)
                .SetValidator(r => modelValidationFactory(r, true)) // The bool here is a forced flag that this is an upsert
                .When(e => e.Model != null);
        }
    }

    public abstract class BasePutRequestValidator<TRequest, TModel> : BaseRydrValidator<TRequest>
        where TRequest : IRequestBaseWithModel<TModel>, IPut, IHasLongId
        where TModel : class, IHasLongId
    {
        protected BasePutRequestValidator(Func<TRequest, AbstractValidator<TModel>> modelValidationFactory)
        {
            RuleFor(e => e.Model)
                .NotNull()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Model.Id)
                .GreaterThan(0)
                .When(e => e.Model != null)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Id)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Id)
                .Equal(e => e.Model == null
                                ? int.MinValue
                                : e.Model.Id)
                .WithMessage("ID in the put-url must match the ID specified in the PUT model");

            RuleFor(e => e.Model)
                .SetValidator(modelValidationFactory)
                .When(e => e.Model != null);
        }
    }

    public abstract class BaseDeleteRequestValidator<TRequest> : BaseRydrValidator<TRequest>
        where TRequest : BaseDeleteRequest
    {
        protected BaseDeleteRequestValidator()
        {
            RuleFor(e => e.Id)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);
        }
    }
}
