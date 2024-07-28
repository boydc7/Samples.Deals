using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class GetMyPublisherAccountValidator : BaseRydrValidator<GetMyPublisherAccount>
{
    public GetMyPublisherAccountValidator()
    {
        Include(new IsFromValidRequestPublisherAccountValidator<GetMyPublisherAccount>());
    }
}

public class GetBusinessReportExternalLinkValidator : BaseRydrValidator<GetBusinessReportExternalLink>
{
    public GetBusinessReportExternalLinkValidator()
    {
        Include(new IsValidPublisherIdentifierValidator<GetBusinessReportExternalLink>());

        RuleFor(e => e.Duration)
            .LessThanOrEqualTo(435_000)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.CompletedOnStart)
            .IsValidDateTime("CompletedOnStart");

        RuleFor(e => e.CompletedOnEnd)
            .IsValidDateTime("CompletedOnEnd");
    }
}

public class GetBusinessReportExternalValidator : AbstractValidator<GetBusinessReportExternal>
{
    public GetBusinessReportExternalValidator()
    {
        RuleFor(e => e.BusinessReportId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.InvalidState)
            .WithName("State")
            .OverridePropertyName("State")
            .WithMessage("State invalid - code [xbrli]");

        RuleFor(e => e.BusinessReportId)
            .MustAsync(async (ri, t) =>
                       {
                           var businessMap = await MapItemService.DefaultMapItemService
                                                                 .TryGetMapByHashedEdgeAsync(DynItemType.PublisherAccount, ri);

                           if (businessMap?.ReferenceNumber == null ||
                               businessMap.ReferenceNumber.Value <= 0 ||
                               businessMap.IsExpired() ||
                               businessMap.Items.IsNullOrEmptyRydr() ||
                               !businessMap.Items.ContainsKey("CompletedOnStart") ||
                               !businessMap.Items.ContainsKey("CompletedOnEnd"))
                           {
                               return false;
                           }

                           // Business must be valid
                           var business = await PublisherExtensions.DefaultPublisherAccountService
                                                                   .TryGetPublisherAccountAsync(businessMap.ReferenceNumber.Value);

                           return business != null && !business.IsDeleted();
                       })
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithName("State")
            .OverridePropertyName("State")
            .WithMessage("State invalid - code [xbrlidnx]");
    }
}

public class GetPublisherAccountsValidator : BaseRydrValidator<GetPublisherAccounts>
{
    public GetPublisherAccountsValidator()
    {
        RuleFor(e => e)
            .SetValidator(new IsValidPublisherAccountIdValidator<GetPublisherAccounts>())
            .When(e => e.PublisherAccountId > 0);

        RuleFor(e => e)
            .SetValidator(new IsFromValidRequestWorkspaceValidator<GetPublisherAccounts>())
            .When(e => !e.IsSystemRequest);
    }
}

public class GetPublisherAccountValidator : BaseGetRequestValidator<GetPublisherAccount>
{
    public GetPublisherAccountValidator()
    {
        RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.Id, e.Id.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.Default))
            .IsValidDynamoItem();
    }
}

public class GetPublisherAccountStatsValidator : BaseRydrValidator<GetPublisherAccountStats>
{
    public GetPublisherAccountStatsValidator()
    {
        Include(new IsFromValidRequestWorkspaceValidator<GetPublisherAccountStats>());

        // Only active multi-user team workspaces can cross-query the entire workspace
        RuleFor(e => e)
            .SetValidator(new IsValidPublisherIdentifierValidator<GetPublisherAccountStats>())
            .UnlessAsync((e, t) => e.IsSubscribedTeamWorkspaceAsync());
    }
}

public class GetPublisherAccountStatsWithValidator : BaseRydrValidator<GetPublisherAccountStatsWith>
{
    public GetPublisherAccountStatsWithValidator()
    {
        Include(new IsValidPublisherIdentifierValidator<GetPublisherAccountStatsWith>());

        RuleFor(e => e.DealtWithPublisherAccountId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.DealtWithPublisherAccountId, e.DealtWithPublisherAccountId.ToStringInvariant(),
                                                                           DynItemType.PublisherAccount, ApplyToBehavior.MustExistCanBeDeleted))
            .IsValidDynamoItem()
            .When(e => e.DealtWithPublisherAccountId > 0);
    }
}

public class GetPublisherAccountExternalValidator : BaseGetRequestValidator<GetPublisherAccountExternal>
{
    public GetPublisherAccountExternalValidator()
    {
        RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.Id, e.Id, DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted, true))
            .IsValidDynamoItem();
    }
}

public class PostPublisherAccountValidator : BasePostRequestValidator<PostPublisherAccount, PublisherAccount>
{
    public PostPublisherAccountValidator()
        : base(r => new PublisherAccountValidator(r)) { }
}

public class PostPublisherAccountUpsertValidator : BaseUpsertRequestValidator<PostPublisherAccountUpsert, PublisherAccount>
{
    public PostPublisherAccountUpsertValidator()
        : base((r, u) => new PublisherAccountValidator(r, isUpsert: u)) { }
}

public class PutPublisherAccountTokenValidator : BaseRydrValidator<PutPublisherAccountToken>
{
    public PutPublisherAccountTokenValidator()
    {
        Include(new IsValidPublisherAccountIdValidator<PutPublisherAccountToken>());

        RuleFor(e => e.AccessToken)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.ToDynItemValidationSourceByRef(e.PublisherAppId, e.PublisherAppId, DynItemType.PublisherApp, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.PublisherAppId > 0);
    }
}

public class PutPublisherAccountTagValidator : BaseRydrValidator<PutPublisherAccountTag>
{
    public PutPublisherAccountTagValidator()
    {
        RuleFor(e => e.Id)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.Id, e.Id.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem();

        RuleFor(e => e.Tag)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}

public class PutPublisherAccountValidator : BasePutRequestValidator<PutPublisherAccount, PublisherAccount>
{
    public PutPublisherAccountValidator()
        : base(r => new PublisherAccountValidator(r)) { }
}

public class PutPublisherAccountAdminValidator : BaseRydrValidator<PutPublisherAccountAdmin>
{
    public PutPublisherAccountAdminValidator()
    {
        Include(new IsValidPublisherAccountIdValidator<PutPublisherAccountAdmin>());
    }
}

public class DeletePublisherAccountValidator : BaseDeleteRequestValidator<DeletePublisherAccount>
{
    public DeletePublisherAccountValidator()
    {
        RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.Id, e.Id.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.MustExistCanBeDeleted))
            .IsValidDynamoItem();
    }
}

// Internal/deferred...
public class DeletePublisherAccountInternalValidator : BaseRydrValidator<DeletePublisherAccountInternal>
{
    public DeletePublisherAccountInternalValidator()
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0);
    }
}

public class PublisherAccountUpdatedValidator : BaseRydrValidator<PublisherAccountUpdated>
{
    public PublisherAccountUpdatedValidator()
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0);
    }
}

public class ProcessPublisherAccountTagsValidator : BaseRydrValidator<ProcessPublisherAccountTags>
{
    public ProcessPublisherAccountTagsValidator()
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0);
    }
}

public class PublisherAccountDeletedValidator : BaseRydrValidator<PublisherAccountDeleted>
{
    public PublisherAccountDeletedValidator()
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0);
    }
}

public class PublisherAccountValidator : AbstractValidator<PublisherAccount>
{
    public PublisherAccountValidator(IRequestBase request, bool skipRecordExistsChecks = false,
                                     bool isUpsert = false, bool allTypesAllowed = false)
    {
        RuleSet(ApplyTo.Post | ApplyTo.Put,
                () =>
                {
                    RuleFor(e => e.Type)
                        .Must(t => t.IsWritablePublisherType())
                        .When(e => !request.IsSystemRequest && !allTypesAllowed && e.Type != PublisherType.Unknown)
                        .WithErrorCode(ErrorCodes.MustBeValid)
                        .WithMessage("Type specified is not allowed for write operations currently");

                    // POST - don't use the PUT verb filter, as we can use this entity validator as attribute validators from other models
                    // when those enclosing models may be doing a POST of that model but including an existing sub-model
                    RuleFor(e => request.ToDynItemValidationSource(DynPublisherAccount.BuildEdgeId(e.Type, e.AccountId), DynItemType.PublisherAccount,
                                                                   isUpsert
                                                                       ? ApplyToBehavior.CanExistNotDeleted
                                                                       : ApplyToBehavior.MustNotExist,
                                                                   isUpsert))
                        .IsValidDynamoItem()
                        .When(e => e.Id <= 0 && !skipRecordExistsChecks);

                    // Cannot if the writable alternative (i.e. facebook for instagram for example) exists (in which case the post'ed account is a lower-priority
                    // type account than the existing one, i.e. we have a full facebook account for a basic ig posting)
                    RuleFor(e => request.ToDynItemValidationSource(DynPublisherAccount.BuildEdgeId(e.Type.WritableAlternateAccountType(),
                                                                                                   PublisherTransforms.ToRydrSoftLinkedAccountId(e.Type, e.AccountId)),
                                                                   DynItemType.PublisherAccount,
                                                                   isUpsert
                                                                       ? ApplyToBehavior.CanExistNotDeleted
                                                                       : ApplyToBehavior.MustNotExist,
                                                                   isUpsert))
                        .IsValidDynamoItem()
                        .When(e => e.Id <= 0 && !skipRecordExistsChecks && !e.Type.IsWritablePublisherType() &&
                                   e.Type.WritableAlternateAccountType().IsWritablePublisherType());

                    // And have to also check if a down-graded one already exists...in which case at the moment, we have to send through a link-account loop to
                    // get the existing one upgraded to the new one, this isn't available in a straight POST
                    RuleFor(e => request.ToDynItemValidationSource(DynPublisherAccount.BuildEdgeId(e.Type.NonWritableAlternateAccountType(), e.AccountId),
                                                                   DynItemType.PublisherAccount,
                                                                   isUpsert
                                                                       ? ApplyToBehavior.CanExistNotDeleted
                                                                       : ApplyToBehavior.MustNotExist,
                                                                   isUpsert))
                        .IsValidDynamoItem()
                        .When(e => e.Id <= 0 && !skipRecordExistsChecks && e.Type.IsWritablePublisherType() &&
                                   e.Type.NonWritableAlternateAccountType() != PublisherType.Unknown);

                    // Even with skipRecordExistsChecks, certain things must match and/or not exist
                    RuleFor(e => e.Id)
                        .MustAsync(async (e, i, t) =>
                                   {
                                       var existingPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                                               .TryGetPublisherAccountAsync(e.Id)
                                                                      ??
                                                                      await PublisherExtensions.DefaultPublisherAccountService
                                                                                               .TryGetPublisherAccountAsync(e.Type, e.AccountId);

                                       if (existingPublisherAccount == null)
                                       {
                                           return true;
                                       }

                                       // Exists, so certain things have to match
                                       return (e.Id <= 0 || existingPublisherAccount.Id == e.Id) &&
                                              (e.Type == PublisherType.Unknown || existingPublisherAccount.PublisherType == e.Type) &&
                                              (e.AccountId.IsNullOrEmpty() || existingPublisherAccount.AccountId.EqualsOrdinalCi(e.AccountId));
                                   })
                        .WithErrorCode(ErrorCodes.MustBeValid)
                        .WithMessage("Invalid incoming/existing identifier state - code[pubact-idtaid]");

                    // PUT - don't use the PUT verb filter, as we can use this entity validator as attribute validators from other models
                    // when those enclosing models may be doing a POST of that model but including an existing sub-model
                    RuleFor(e => request.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.Id, e.Id, DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted, isUpsert))
                        .IsValidDynamoItem()
                        .When(e => e.Id > 0 && !skipRecordExistsChecks);

                    RuleFor(e => e)
                        .MustAsync(async (e, t) =>
                                   {
                                       var existingAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                                      .GetPublisherAccountAsync(e.Id);

                                       return (e.Type == PublisherType.Unknown || e.Type == existingAccount.PublisherType) &&
                                              (e.AccountType == PublisherAccountType.Unknown || e.AccountType == existingAccount.AccountType);
                                   })
                        .When(e => e.Id > 0 && !skipRecordExistsChecks &&
                                   (e.Type != PublisherType.Unknown || e.AccountType != PublisherAccountType.Unknown))
                        .WithMessage("Type and AccountType cannot be changed once registered.")
                        .WithName("Types")
                        .WithErrorCode(ErrorCodes.CannotChangeState)
                        .OverridePropertyName("Types");
                });

        // Normally you may consider putting these inside a POST ruleset, but since the validator here can be used as a sub-attribute on other request
        // models, the sub-model may need to act like a POST when the outer model was PUT'ed, or act as a PUT when the outer was POST'ed.  So, we leave
        // the POST and PUT specific checks (i.e. Id must be <= 0 on a POST, or Id must be > 0 on a PUT) for the Post/Put specific request validators
        // and use the WHEN filters here

        // POST-like rules only (i.e. Id <= 0)
        RuleFor(e => e.Type)
            .NotEqual(PublisherType.Unknown)
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.AccountId)
            .NotEmpty()
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.AccountType)
            .NotEqual(PublisherAccountType.Unknown)
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.RydrAccountType)
            .NotEqual(RydrAccountType.None)
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        // PUT-like rules only (i.e. Id > 0)
        RuleFor(e => e.AccountId)
            .Null()
            .When(e => e.Id > 0 && !skipRecordExistsChecks && !isUpsert)
            .WithErrorCode(ErrorCodes.CannotBeSpecified)
            .WithMessage("You cannot update the AccountId once registered");

        // ALL operation rules
        RuleFor(e => e.AccountId)
            .MaximumLength(150)
            .When(e => e.AccountId.HasValue())
            .WithErrorCode(ErrorCodes.TooLong);

        RuleFor(e => e.RydrAccountType)
            .Must(t => t == RydrAccountType.Business || t == RydrAccountType.Influencer || t == RydrAccountType.TokenAccount)
            .When(e => e.RydrAccountType != RydrAccountType.None)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("RydrAccountType currently must be either Business or Influencer, but not a combination");

        RuleFor(e => e.RydrAccountType)
            .Must(t => t == RydrAccountType.TokenAccount)
            .When(e => e.RydrAccountType != RydrAccountType.None && e.AccountType.IsUserAccount())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("RydrAccountType must be TokenAccount for a user PublisherAccountType");

        RuleFor(e => e.RydrAccountType)
            .Must(t => t == RydrAccountType.Business || t == RydrAccountType.Influencer)
            .When(e => e.RydrAccountType != RydrAccountType.None && !e.AccountType.IsUserAccount())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("RydrAccountType must be Business or Influencer for a linked account");

        RuleFor(e => e.AccessToken)
            .Null()
            .When(e => e.RydrAccountType != RydrAccountType.None && !e.AccountType.IsUserAccount())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("An AccessToken cannot be specified for a non-user account");

        RuleFor(e => e.UserName)
            .MaximumLength(150)
            .When(e => e.UserName.HasValue())
            .WithErrorCode(ErrorCodes.TooLong);

        RuleFor(e => e.FullName)
            .MaximumLength(150)
            .When(e => e.FullName.HasValue())
            .WithErrorCode(ErrorCodes.TooLong);

        RuleFor(e => e.Email)
            .MaximumLength(150)
            .When(e => e.Email.HasValue())
            .WithErrorCode(ErrorCodes.TooLong);

        RuleFor(e => e.Description)
            .MaximumLength(1000)
            .When(e => e.Description.HasValue())
            .WithErrorCode(ErrorCodes.TooLong);

        RuleFor(e => e.AccessToken)
            .MaximumLength(1000)
            .When(e => e.AccessToken.HasValue())
            .WithErrorCode(ErrorCodes.TooLong);

        // NOTE: Leave this out for now, we send metrics from the client in some instances that we want to keep (when linking new accounts for example)
        //            RuleFor(e => e.Metrics)
        //                .Null()
        //                .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.IsSyncDisabled)
            .Equal(false)
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.RecentCompleters)
            .Null()
            .WithErrorCode(ErrorCodes.CannotBeSpecified);
    }
}
