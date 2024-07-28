using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Auth;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class GetAuthenticationTokenValidator : BaseRydrValidator<GetAuthenticationToken>
{
    public GetAuthenticationTokenValidator()
    {
        RuleFor(e => e.ForUserId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.ToDynItemValidationSourceByRef<DynUser>(e.ForUserId, e.ForUserId.ToStringInvariant(), DynItemType.User, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.ForUserId > 0);
    }
}

public class GetAuthenticationPublisherInfoValidator : BaseRydrValidator<GetAuthenticationPublisherInfo>
{
    public GetAuthenticationPublisherInfoValidator()
    {
        RuleFor(e => e.PublisherType)
            .NotEqual(PublisherType.Unknown)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.PublisherId)
            .NotEmpty()
            .When(e => e.PublisherAccountId <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0)
            .When(e => !e.PublisherId.HasValue())
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.ToDynItemValidationSource<DynWorkspace>(e.InWorkspaceId, DynItemType.Workspace, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.InWorkspaceId > 0);

        RuleFor(e => e.WorkspacePublisherAccountId)
            .Empty()
            .Unless(e => e.InWorkspaceId > 0)
            .WithErrorCode(ErrorCodes.CannotBeSpecified)
            .WithMessage("WorkspacePublisherAccountId can only be specified when InWorkspaceId is also included");
    }
}

public class GetAuthenticationConnectInfoValidator : BaseRydrValidator<GetAuthenticationConnectInfo>
{
    public GetAuthenticationConnectInfoValidator()
    {
        RuleFor(e => e.UserIdentifier)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.UserIdentifier.ToLong(0))
            .GreaterThan(0)
            .When(e => !e.UserIdentifier.EqualsOrdinalCi("me"))
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.UserIdentifier.ToLong(0))
            .Equal(e => e.UserId)
            .When(e => !e.UserIdentifier.EqualsOrdinalCi("me") && !e.IsSystemRequest)
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("The UserIdentifier does not exist or you do not have access to it");
    }
}

public class GetAuthenticationUserValidator : BaseRydrValidator<GetAuthenticationUser>
{
    public GetAuthenticationUserValidator()
    {
        RuleFor(e => e.UserIdentifier)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.UserIdentifier.ToLong(0))
            .GreaterThan(0)
            .When(e => !e.UserIdentifier.EqualsOrdinalCi("me"))
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.UserIdentifier.ToLong(0))
            .Equal(e => e.UserId)
            .When(e => !e.UserIdentifier.EqualsOrdinalCi("me") && !e.IsSystemRequest)
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("The UserIdentifier does not exist or you do not have access to it");
    }
}

public class PostApiKeyValidator : BaseRydrValidator<PostApiKey>
{
    public PostApiKeyValidator()
    {
        RuleFor(e => e.ToUserId)
            .GreaterThan(0)
            .When(e => e.Email.IsNullOrEmpty())
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.Email)
            .NotEmpty()
            .When(e => e.ToUserId <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.ToUserId)
            .LessThanOrEqualTo(0)
            .When(e => e.Email.HasValue())
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.ToDynItemValidationSourceByRef<DynUser>(e.ToUserId, e.ToUserId.ToStringInvariant(), DynItemType.User, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.ToUserId > 0);

        RuleFor(e => e.ApiKey)
            .Must(p => p.IsValidPassword())
            .MinimumLength(65)
            .When(e => e.ApiKey.HasValue())
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class PutUpdateUserTypeValidator : BaseRydrValidator<PutUpdateUserType>
{
    public PutUpdateUserTypeValidator()
    {
        RuleFor(e => e.ForUserId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.ToDynItemValidationSourceByRef<DynUser>(e.ForUserId, e.ForUserId.ToStringInvariant(), DynItemType.User, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.ForUserId > 0);

        RuleFor(e => e.ToUserType)
            .NotEqual(UserType.Unknown)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class PostUserValidator : AbstractValidator<PostUser>
{
    public PostUserValidator(IClientTokenAuthorizationService clientTokenAuthorizationService)
    {
        RuleFor(e => e.FirebaseToken)
            .NotEmpty()
            .WithName("RegisterTkn")
            .OverridePropertyName("RegisterTkn")
            .WithMessage("Register invalid - code [gfbtknrn]");

        RuleFor(e => e.FirebaseId)
            .NotEmpty()
            .WithName("RegisterId")
            .OverridePropertyName("RegisterId")
            .WithMessage("Register invalid - code [gfbidrn]");

        RuleFor(e => e.FirebaseId)
            .MustAsync(async (e, i, t) =>
                       {
                           var validatedUid = await clientTokenAuthorizationService.GetUidFromTokenAsync(e.FirebaseToken);

                           return validatedUid.EqualsOrdinal(e.FirebaseId);
                       })
            .When(e => e.FirebaseId.HasValue() && e.FirebaseToken.HasValue())
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithName("Register")
            .OverridePropertyName("Register")
            .WithMessage("Register invalid - code [racgfbauidtknxr]");
    }
}

public class PostAuthenticationConnectValidator : AbstractValidator<PostAuthenticationConnect>
{
    public PostAuthenticationConnectValidator(IClientTokenAuthorizationService clientTokenAuthorizationService)
    {
        RuleFor(e => e.FirebaseToken)
            .NotEmpty()
            .WithName("Connect")
            .OverridePropertyName("Connect")
            .WithMessage("Connect invalid - code [gfbtknidn]");

        RuleFor(e => e.FirebaseId)
            .NotEmpty()
            .WithName("Connect")
            .OverridePropertyName("Connect")
            .WithMessage("Connect invalid - code [gfbidtknn]");

        RuleFor(e => e.AuthProviderToken)
            .NotEmpty()
            .When(e => e.AuthProviderId.HasValue())
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithName("Provider")
            .OverridePropertyName("Provider")
            .WithMessage("Connect invalid - code [aptkn]");

        RuleFor(e => e.AuthProviderId)
            .NotEmpty()
            .When(e => e.AuthProviderToken.HasValue())
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithName("Provider")
            .OverridePropertyName("Provider")
            .WithMessage("Connect invalid - code [apidt]");

        RuleFor(e => e.AuthProvider)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithName("Provider")
            .OverridePropertyName("Provider")
            .WithMessage("Connect invalid - code [aprvdrn]");

        RuleFor(e => e.FirebaseId)
            .MustAsync(async (e, i, t) =>
                       {
                           var validatedUid = await clientTokenAuthorizationService.GetUidFromTokenAsync(e.FirebaseToken);

                           return validatedUid.EqualsOrdinal(e.FirebaseId);
                       })
            .When(e => e.FirebaseId.HasValue() && e.FirebaseToken.HasValue())
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithName("Connect")
            .OverridePropertyName("Connect")
            .WithMessage("Connect invalid - code [racgfbauidtknx]");
    }
}
