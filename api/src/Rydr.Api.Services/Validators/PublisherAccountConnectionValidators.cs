using System.Linq;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.QueryDto;
using Rydr.Api.Services.Helpers;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class QueryPublisherAccountConnectionsValidator : BaseRydrValidator<QueryPublisherAccountConnections>
    {
        public QueryPublisherAccountConnectionsValidator()
        {
            RuleFor(e => e.FromPublisherAccountId)
                .GreaterThan(0)
                .When(e => e.FromPublisherAccountId.HasValue)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.ToPublisherAccountId)
                .GreaterThan(0)
                .When(e => e.ToPublisherAccountId.HasValue)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.ConnectionTypes)
                .Must(t => t.All(ev => ev != PublisherAccountConnectionType.Unspecified))
                .When(e => e.ConnectionTypes != null && e.ConnectionTypes.Length > 0)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("ConnectionTypes must all be valid if specified");

            RuleFor(e => e.LastConnectedBefore.Value)
                .IsValidDateTime("LastConnectedBefore")
                .When(e => e.LastConnectedBefore.HasValue);

            RuleFor(e => e.LastConnectedAfter.Value)
                .IsValidDateTime("LastConnectedAfter")
                .When(e => e.LastConnectedAfter.HasValue);

            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.FromPublisherAccountId.Value, e.FromPublisherAccountId.Value.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.FromPublisherAccountId.GetValueOrDefault() > 0);

            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.ToPublisherAccountId.Value, e.ToPublisherAccountId.Value.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.ToPublisherAccountId.GetValueOrDefault() > 0);

            Include(new IsFromValidNonTokenRequestPublisherAccountValidator());
        }
    }

    public class LinkPublisherAccountValidator : BaseRydrValidator<LinkPublisherAccount>
    {
        public LinkPublisherAccountValidator()
        {
            RuleFor(e => e.ToPublisherAccountId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.ToWorkspaceId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.ToWorkspaceId)
                .Must(i => WorkspaceService.DefaultWorkspaceService.TryGetWorkspace(i) != null)
                .When(e => e.ToWorkspaceId > 0)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("ToWorkspaceId must be a valid workspace");

            RuleFor(e => e.ToPublisherAccountId)
                .MustAsync(async (pid, t) =>
                           {
                               var linkedToPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                                       .TryGetPublisherAccountAsync(pid);

                               return linkedToPublisherAccount != null &&
                                      linkedToPublisherAccount.AccountType != PublisherAccountType.Unknown &&
                                      !linkedToPublisherAccount.AccountType.IsUserAccount();
                           })
                .When(e => e.ToPublisherAccountId > 0)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("ToPublisherAccountId must be a non-user account");

            RuleFor(e => e.FromPublisherAccountId)
                .MustAsync(async (pid, t) =>
                           {
                               var linkedFromPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                                         .TryGetPublisherAccountAsync(pid);

                               return linkedFromPublisherAccount != null &&
                                      linkedFromPublisherAccount.AccountType.IsUserAccount();
                           })
                .When(e => e.FromPublisherAccountId > 0)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("linkedFromPublisherAccount must be a user account");
        }
    }

    public class DelinkPublisherAccountValidator : BaseRydrValidator<DelinkPublisherAccount>
    {
        public DelinkPublisherAccountValidator()
        {
            RuleFor(e => e.ToPublisherAccountId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.FromPublisherAccountId)
                .GreaterThan(0)
                .When(e => e.FromWorkspaceId <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("One or both of FromWorkspaceId, FromPublisherAccountId is required");

            RuleFor(e => e.FromWorkspaceId)
                .GreaterThan(0)
                .When(e => e.FromPublisherAccountId <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("One or both of FromWorkspaceId, FromPublisherAccountId is required");

            RuleFor(e => e.FromWorkspaceId)
                .Must(i => WorkspaceService.DefaultWorkspaceService.TryGetWorkspace(i) != null)
                .When(e => e.FromWorkspaceId > 0)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("FromWorkspaceId must be a valid workspace");

            RuleFor(e => e.ToPublisherAccountId)
                .MustAsync(async (pid, t) =>
                           {
                               var linkedToPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                                       .TryGetPublisherAccountAsync(pid);

                               return linkedToPublisherAccount != null &&
                                      linkedToPublisherAccount.AccountType != PublisherAccountType.Unknown &&
                                      !linkedToPublisherAccount.AccountType.IsUserAccount();
                           })
                .When(e => e.ToPublisherAccountId > 0)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("ToPublisherAccountId must be a non-user account");

            RuleFor(e => e.FromPublisherAccountId)
                .MustAsync(async (pid, t) =>
                           {
                               var linkedFromPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                                         .TryGetPublisherAccountAsync(pid);

                               return linkedFromPublisherAccount != null &&
                                      linkedFromPublisherAccount.AccountType.IsUserAccount();
                           })
                .When(e => e.FromPublisherAccountId > 0)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("linkedFromPublisherAccount must be a user account");
        }
    }

    public class PutPublisherAccountLinksValidator : BaseRydrValidator<PutPublisherAccountLinks>
    {
        public PutPublisherAccountLinksValidator()
        {
            Include(new IsFromValidRequestWorkspaceValidator<PutPublisherAccountLinks>());

            RuleFor(e => e.LinkAccounts)
                .Must(l => !l.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("Must specify accounts to link");

            RuleForEach(e => e.LinkAccounts)
                .SetValidator(r => new LinkingPublisherAccountValidator(r))
                .When(e => !e.LinkAccounts.IsNullOrEmpty());
        }
    }

    public class DeletePublisherAccountLinkValidator : BaseDeleteRequestValidator<DeletePublisherAccountLink>
    {
        public DeletePublisherAccountLinkValidator()
        {
            Include(new IsFromValidRequestWorkspaceValidator<DeletePublisherAccountLink>());
        }
    }

    public class PublisherAccountUnlinkedValidator : BaseRydrValidator<PublisherAccountUnlinked>
    { // Internal message...simple validation
        public PublisherAccountUnlinkedValidator()
        {
            RuleFor(e => e.ToPublisherAccountId)
                .GreaterThan(0);

            // NOTE: this is correct - the DelinkPublsiherAccount message allows this as optional, but if passed that way,
            // pushes one of these messages for each workspace the account is unlinked from...
            RuleFor(e => e.FromWorkspaceId)
                .GreaterThan(0);
        }
    }

    public class PublisherAccountRecentDealStatsUpdateValidator : BaseRydrValidator<PublisherAccountRecentDealStatsUpdate>
    { // Internal message...simple validation
        public PublisherAccountRecentDealStatsUpdateValidator()
        {
            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0);

            RuleFor(e => e.InWorkspaceId)
                .GreaterThan(0);
        }
    }

    public class PublisherAccountLinkedCompleteValidator : PublisherAccountLinkedCompleteBaseValidator<PublisherAccountLinkedComplete> { }
    public class PublisherAccountLinkedValidator : PublisherAccountLinkedCompleteBaseValidator<PublisherAccountLinked> { }

    public class PublisherAccountDownConvertValidator : BaseRydrValidator<PublisherAccountDownConvert>
    { // Internal message...simple validation
        public PublisherAccountDownConvertValidator()
        {
            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class PublisherAccountUpConvertFacebookValidator : BaseRydrValidator<PublisherAccountUpConvertFacebook>
    { // Internal message...simple validation
        public PublisherAccountUpConvertFacebookValidator()
        {
            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0);

            RuleFor(e => e.WithFacebookAccount)
                .NotEmpty();

            RuleFor(e => e.WithFacebookAccount.InstagramBusinessAccount)
                .NotEmpty()
                .When(e => e.WithFacebookAccount != null);
        }
    }

    public abstract class PublisherAccountLinkedCompleteBaseValidator<T> : BaseRydrValidator<T>
        where T : PublisherAccountLinkedCompleteBase
    { // Internal message...simple validation
        protected PublisherAccountLinkedCompleteBaseValidator()
        {
            RuleFor(e => e.FromWorkspaceId)
                .GreaterThan(0);

            RuleFor(e => e.ToPublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class LinkingPublisherAccountValidator : AbstractValidator<PublisherAccount>
    {
        public LinkingPublisherAccountValidator(IRequestBase request)
        {
            Include(new PublisherAccountValidator(request, isUpsert: true, allTypesAllowed: true));

            RuleFor(e => e.Id)
                .GreaterThan(0)
                .When(e => !e.AccountId.HasValue() || e.AccountType == PublisherAccountType.Unknown)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("Id or AccountId/Type combination must be included and valid");

            RuleFor(e => e.AccountId)
                .NotEmpty()
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("AccountId/Type combination or Id must be included and valid");

            RuleFor(e => e.AccountType)
                .NotEqual(PublisherAccountType.Unknown)
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("AccountId/Type combination or Id must be included and valid");

            RuleFor(e => e)
                .MustAsync(async (r, t) =>
                           {
                               var workspacePublisherAccount = await WorkspaceService.DefaultWorkspaceService.TryGetDefaultPublisherAccountAsync(request.WorkspaceId);

                               if (workspacePublisherAccount == null || workspacePublisherAccount.IsDeleted() || !workspacePublisherAccount.IsTokenAccount())
                               {
                                   return false;
                               }

                               if (r.Type != workspacePublisherAccount.PublisherType &&
                                   r.Type.WritableAlternateAccountType() != workspacePublisherAccount.PublisherType)
                               {
                                   return false;
                               }

                               var linkPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService.TryGetPublisherAccountAsync(r.Type, r.AccountId);

                               // If the publisher account being linked already exists, it cannot be a user account
                               if (linkPublisherAccount == null)
                               {
                                   return true;
                               }

                               return (!linkPublisherAccount.AccountType.IsUserAccount() &&
                                       linkPublisherAccount.AccountType == r.AccountType) &&
                                      linkPublisherAccount.PublisherType == r.Type &&
                                      linkPublisherAccount.RydrAccountType == r.RydrAccountType;
                           })
                .When(e => e.AccountId.HasValue() && e.AccountType != PublisherAccountType.Unknown && e.Type != PublisherType.Unknown)
                .WithName("Types")
                .OverridePropertyName("Types")
                .WithMessage("The linked account specified already exists in a different type state (accountType, type, rydrAccountType must all be the same as the existing account)")
                .WithErrorCode(ErrorCodes.MustBeValid);
        }
    }
}
