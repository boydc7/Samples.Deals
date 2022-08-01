using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Services.Helpers;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class PostFacebookConnectUserValidator : BaseRydrValidator<PostFacebookConnectUser>
    {
        public PostFacebookConnectUserValidator()
        {
            RuleFor(e => e.AuthToken)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.AccountId)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Features)
                .Must(f => f == WorkspaceFeature.None || f == WorkspaceFeature.Default)
                .Unless(e => e.IsSystemRequest || Request.IsRydrRequest() || Request.IsLocal)
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("You do not have access to change the specified value(s)");
        }
    }

    public class GetFacebookWebhookValidator : AbstractValidator<GetFacebookWebhook>
    {
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private static readonly string _fbWebHookVerifyToken = RydrEnvironment.GetAppSetting("Facebook.WebHook.VerifyToken");

        public GetFacebookWebhookValidator()
        {
            RuleFor(e => e.VerifyToken)
                .Must(t => t.HasValue() && t.EqualsOrdinal(_fbWebHookVerifyToken))
                .WithErrorCode(ErrorCodes.InvalidArguments)
                .WithName("State")
                .OverridePropertyName("State")
                .WithMessage("Invalid state - code [vt]");

            RuleFor(e => e.Challenge)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.InvalidArguments)
                .WithName("State")
                .OverridePropertyName("State")
                .WithMessage("Invalid state - code [ch]");

            RuleFor(e => e.Mode)
                .Must(t => t.HasValue() && t.EqualsOrdinal("subscribe"))
                .WithErrorCode(ErrorCodes.InvalidArguments)
                .WithName("State")
                .OverridePropertyName("State")
                .WithMessage("Invalid state - code [md]");
        }
    }

    public class PostFacebookWebhookValidator : AbstractValidator<PostFacebookWebhook>
    {
        public PostFacebookWebhookValidator()
        {
            RuleFor(e => e.Object)
                .Must(t => t.HasValue() && t.EqualsOrdinal("instagram"))
                .WithErrorCode(ErrorCodes.InvalidArguments)
                .WithName("State")
                .OverridePropertyName("State")
                .WithMessage("Invalid state - code [obj]");
        }
    }

    public class SearchFbPlacesValidator : BaseRydrValidator<SearchFbPlaces>
    {
        public SearchFbPlacesValidator()
        {
            Include(new IsFromValidRequestWorkspaceValidator<SearchFbPlaces>());

            RuleFor(e => e.Query)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            Include(new GeoQueryValidator());

            RuleFor(e => e.ToDynItemValidationSourceByRef(e.PublisherAppId, e.PublisherAppId, DynItemType.PublisherApp, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.PublisherAppId > 0);
        }
    }

    public class GetFbIgBusinessAccountsValidator : BaseRydrValidator<GetFbIgBusinessAccounts>
    {
        public GetFbIgBusinessAccountsValidator()
        {
            RuleFor(e => e)
                .UpdateStateIntentTo(AccessIntent.ReadOnly);

            Include(new IsFromValidRequestWorkspaceValidator<GetFbIgBusinessAccounts>());

            RuleFor(e => e.ToDynItemValidationSourceByRef(e.PublisherAppId, e.PublisherAppId, DynItemType.PublisherApp, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.PublisherAppId > 0);
        }
    }

    public class GetFbBusinessesValidator : BaseRydrValidator<GetFbBusinesses>
    {
        public GetFbBusinessesValidator()
        {
            Include(new IsFromValidRequestWorkspaceValidator<GetFbBusinesses>());

            RuleFor(e => e.ToDynItemValidationSourceByRef(e.PublisherAppId, e.PublisherAppId, DynItemType.PublisherApp, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.PublisherAppId > 0);
        }
    }
}
