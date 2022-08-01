using System.Linq;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class PostCheckoutSessionValidator : CheckoutSessionSubsriptionBaseValidator<PostCheckoutSession>
    {
        public PostCheckoutSessionValidator()
        {
            // PublisherAccountIds cannot be included if the workspace already has an active subscription (i.e. they want to add publisher accounts to an existing
            // subscription, different endpoint, this is for creating a new subscription, or updating the credit card details of the existing one only)
            RuleFor(e => e.PublisherAccountIds)
                .MustAsync(async (e, pid, t) =>
                           {
                               var existingWorkspaceSubscription = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                                                         .TryGetActiveWorkspaceSubscriptionAsync(e.GetWorkspaceIdFromIdentifier());

                               return !existingWorkspaceSubscription.IsPaidSubscription();
                           })
                .When(e => !e.PublisherAccountIds.IsNullOrEmptyReadOnly())
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("PublisherAccounts cannot be added to an existing subscription via this endpoint");

            // A new subscription must include at least one publisher account
            RuleFor(e => e.PublisherAccountIds)
                .MustAsync(async (e, pid, t) =>
                           {
                               var existingWorkspaceSubscription = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                                                         .TryGetActiveWorkspaceSubscriptionAsync(e.GetWorkspaceIdFromIdentifier());

                               return existingWorkspaceSubscription.IsPaidSubscription();
                           })
                .When(e => e.PublisherAccountIds.IsNullOrEmptyReadOnly())
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("At least 1 PublisherAccount must be included on an initial subscription");
        }
    }

    public class PostCheckoutPublisherSubscriptionValidator : CheckoutSessionSubsriptionBaseValidator<PostCheckoutPublisherSubscription>
    {
        public PostCheckoutPublisherSubscriptionValidator()
        {
            RuleFor(e => e.PublisherAccountIds)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            // To add a publisher subscription, the workspace must have an active/paid subscription
            RuleFor(e => e.PublisherAccountIds)
                .MustAsync(async (e, pid, t) =>
                           {
                               var existingWorkspaceSubscription = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                                                         .TryGetActiveWorkspaceSubscriptionAsync(e.GetWorkspaceIdFromIdentifier());

                               return existingWorkspaceSubscription.IsPaidSubscription();
                           })
                .When(e => !e.PublisherAccountIds.IsNullOrEmptyReadOnly())
                .WithErrorCode(ErrorCodes.MustExist)
                .WithMessage("Publishers cannot be subscribed until the workspace has an active subscription");
        }
    }

    public class PostCheckoutManagedSubscriptionDiscountValidator : BaseRydrValidator<PostCheckoutManagedSubscriptionDiscount>
    {
        public PostCheckoutManagedSubscriptionDiscountValidator()
        {
            Include(new IsValidWorkspaceIdentifierValidator<PostCheckoutManagedSubscriptionDiscount>(ApplyToBehavior.MustExistNotDeleted));
            Include(new IsValidPublisherAccountIdValidator<PostCheckoutManagedSubscriptionDiscount>((r, p) => p.IsBusiness()));

            RuleFor(e => e.Discount)
                .SetValidator(new ManagedSubscriptionDiscountValidator());

            // To add a discount, the workspace must have an active/paid subscription and be an agency type
            RuleFor(e => e.WorkspaceIdentifier)
                .MustAsync(async (e, pid, t) =>
                           {
                               var existingWorkspaceSubscription = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                                                         .TryGetActiveWorkspaceSubscriptionAsync(e.GetWorkspaceIdFromIdentifier());

                               return existingWorkspaceSubscription != null &&
                                      existingWorkspaceSubscription.SubscriptionType.IsAgencySubscriptionType();
                           })
                .Unless(e => e.WorkspaceIdentifier.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.MustExist)
                .WithMessage("Only paid, managing workspaces can manage another publisher");

            // The publisher included must have an active paid subscription
            RuleFor(e => e.PublisherAccountId)
                .MustAsync(async (e, pid, t) =>
                           {
                               var workspacePublisherSubscription = await WorkspaceService.DefaultWorkspacePublisherSubscriptionService
                                                                                          .GetPublisherSubscriptionAsync(e.GetWorkspaceIdFromIdentifier(), pid);

                               if (workspacePublisherSubscription == null || workspacePublisherSubscription.IsDeleted())
                               {
                                   return false;
                               }

                               var isPaidSubscription = workspacePublisherSubscription.SubscriptionType.IsPaidSubscriptionType();

                               return isPaidSubscription;
                           })
                .When(e => e.PublisherAccountId > 0)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("The publisher requested already has an active subscription in this workspace");
        }
    }

    public class PostCheckoutManagedSubscriptionValidator : BaseRydrValidator<PostCheckoutManagedSubscription>
    {
        public PostCheckoutManagedSubscriptionValidator()
        {
            Include(new IsValidWorkspaceIdentifierValidator<PostCheckoutManagedSubscription>(ApplyToBehavior.MustExistNotDeleted));
            Include(new IsValidPublisherAccountIdValidator<PostCheckoutManagedSubscription>((r, p) => p.IsBusiness()));

            RuleFor(e => e.MonthlyFeeDiscount)
                .SetValidator(new ManagedSubscriptionDiscountValidator(SubscriptionUsageType.SubscriptionFee))
                .When(e => e.MonthlyFeeDiscount != null);

            RuleFor(e => e.PerPostDiscount)
                .SetValidator(new ManagedSubscriptionDiscountValidator(SubscriptionUsageType.CompletedRequest))
                .When(e => e.PerPostDiscount != null);

            RuleFor(e => e.SubscriptionType)
                .Must(t => t.IsManagedSubscriptionType())
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("Only managed subscription types can be used here");

            RuleFor(e => e.CustomSubscriptionMonthlyFee)
                .LessThanOrEqualTo(0)
                .Unless(e => e.SubscriptionType.IsManagedCustomPlan())
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("Custom prices can only be specified with custom managed plan types");

            RuleFor(e => e.CustomSubscriptionPerPostFee)
                .LessThanOrEqualTo(0)
                .Unless(e => e.SubscriptionType.IsManagedCustomPlan())
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("Custom prices can only be specified with custom managed plan types");

            RuleFor(e => e.CustomSubscriptionMonthlyFee)
                .GreaterThanOrEqualTo(0)
                .When(e => e.SubscriptionType.IsManagedCustomPlan())
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("Custom prices must be valid when using a custom managed plan type");

            RuleFor(e => e.CustomSubscriptionPerPostFee)
                .GreaterThanOrEqualTo(0)
                .When(e => e.SubscriptionType.IsManagedCustomPlan())
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("Custom prices must be valid when using a custom managed plan type");

            // To add a managed publisher subscription, the workspace must have an active/paid subscription and be an agency type
            RuleFor(e => e.WorkspaceIdentifier)
                .MustAsync(async (e, pid, t) =>
                           {
                               var existingWorkspaceSubscription = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                                                         .TryGetActiveWorkspaceSubscriptionAsync(e.GetWorkspaceIdFromIdentifier());

                               return existingWorkspaceSubscription != null &&
                                      existingWorkspaceSubscription.SubscriptionType.IsAgencySubscriptionType();
                           })
                .Unless(e => e.WorkspaceIdentifier.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.MustExist)
                .WithMessage("Only paid, managing workspaces can manage another publisher");

            // Any of the publishers included cannot already have an active paid subscription
            RuleFor(e => e.PublisherAccountId)
                .MustAsync(async (e, pid, t) =>
                           {
                               var workspacePublisherSubscription = await WorkspaceService.DefaultWorkspacePublisherSubscriptionService
                                                                                          .GetPublisherSubscriptionAsync(e.GetWorkspaceIdFromIdentifier(), pid);

                               if (workspacePublisherSubscription == null || workspacePublisherSubscription.IsDeleted())
                               {
                                   return true;
                               }

                               var isPaidSubscription = workspacePublisherSubscription.SubscriptionType.IsPaidSubscriptionType();

                               return !isPaidSubscription;
                           })
                .When(e => e.PublisherAccountId > 0)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("The publisher requested already has an active subscription in this workspace");

            RuleFor(e => e.BackdateTo.Value)
                .Must(v => v < DateTimeHelper.UtcNow.Date && v > DateTimeHelper.UtcNow.StartOfMonth())
                .When(e => e.BackdateTo.HasValue)
                .WithMessage("BackdateTo must be in the current month and before today");
        }
    }

    // INTERNAL - simple
    public class CheckoutCompletedValidator : BaseRydrValidator<CheckoutCompleted>
    {
        public CheckoutCompletedValidator()
        {
            RuleFor(e => e.ClientReferenceId)
                .NotEmpty();

            RuleFor(e => e.CheckoutSessionId)
                .NotEmpty();
        }
    }

    public class CheckoutCustomerDeletedValidator : BaseRydrValidator<CheckoutCustomerDeleted>
    {
        public CheckoutCustomerDeletedValidator()
        {
            RuleFor(e => e.StripeCustomerId)
                .NotEmpty();
        }
    }

    public class CheckoutCustomerUpdatedValidator : BaseRydrValidator<CheckoutCustomerUpdated>
    {
        public CheckoutCustomerUpdatedValidator()
        {
            RuleFor(e => e.StripeCustomerId)
                .NotEmpty();
        }
    }

    public class CheckoutCustomerCreatedValidator : BaseRydrValidator<CheckoutCustomerCreated>
    {
        public CheckoutCustomerCreatedValidator()
        {
            RuleFor(e => e.StripeCustomerId)
                .NotEmpty();
        }
    }

    public class CheckoutSubscriptionDeletedValidator : BaseRydrValidator<CheckoutSubscriptionDeleted>
    {
        public CheckoutSubscriptionDeletedValidator()
        {
            RuleFor(e => e.StripeSubscriptionId)
                .NotEmpty();
        }
    }

    public class CheckoutSubscriptionUpdatedValidator : BaseRydrValidator<CheckoutSubscriptionUpdated>
    {
        public CheckoutSubscriptionUpdatedValidator()
        {
            RuleFor(e => e.StripeSubscriptionId)
                .NotEmpty();
        }
    }

    public class CheckoutSubscriptionCreatedValidator : BaseRydrValidator<CheckoutSubscriptionCreated>
    {
        public CheckoutSubscriptionCreatedValidator()
        {
            RuleFor(e => e.StripeSubscriptionId)
                .NotEmpty();
        }
    }

    public class CheckoutApplyInvoiceDiscountsValidator : BaseRydrValidator<CheckoutApplyInvoiceDiscounts>
    {
        public CheckoutApplyInvoiceDiscountsValidator()
        {
            RuleFor(e => e.StripeSubscriptionId)
                .NotEmpty();

            // InvoiceId can be empty here (upcoming invoices ids are)
        }
    }

    public class ManagedSubscriptionDiscountValidator : AbstractValidator<ManagedSubscriptionDiscount>
    {
        public ManagedSubscriptionDiscountValidator(SubscriptionUsageType mustBeUsageType = SubscriptionUsageType.None)
        {
            RuleFor(e => e.UsageType)
                .Must(t =>
                      {
                          if (t.GetCreditTypeForUsage() == SubscriptionUsageType.None)
                          {
                              return false;
                          }

                          return mustBeUsageType == SubscriptionUsageType.None || t == mustBeUsageType;
                      })
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("UsageType must be a valid non-credit type");

            RuleFor(e => e.PercentOff)
                .InclusiveBetween(0, 100)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("PercentOff must be a positive integer between 1 and 100");

            RuleFor(e => e.StartsOnInclusive)
                .IsValidDateTime("StartsOnInclusive");

            RuleFor(e => e.EndsOnExclusive)
                .IsValidDateTime("EndsOnExclusive");

            RuleFor(e => e.EndsOnExclusive)
                .GreaterThan(e => e.StartsOnInclusive)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("EndsOnExclusive must be specified and after the start on date");

            RuleFor(e => e.StartsOnInclusive)
                .Must(s => s.Day == 1)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("StartsOnInclusive must be the beginning of a billing cycle, on the first of a month");
        }
    }

    public abstract class CheckoutSessionSubsriptionBaseValidator<T> : BaseRydrValidator<T>
        where T : CheckoutSessionSubsriptionBase
    {
        protected CheckoutSessionSubsriptionBaseValidator()
        {
            Include(new IsValidWorkspaceIdentifierValidator<T>(ApplyToBehavior.MustExistNotDeleted));

            RuleFor(e => e.PublisherAccountIds)
                .Must(i => i.Count <= 100)
                .When(e => !e.PublisherAccountIds.IsNullOrEmptyReadOnly())
                .WithErrorCode(ErrorCodes.TooLong)
                .WithMessage("Too many publisher accounts included for subscription");

            RuleForEach(e => e.PublisherAccountIds
                              .Select(p => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(p, p.ToStringInvariant(),
                                                                                                 DynItemType.PublisherAccount,
                                                                                                 ApplyToBehavior.MustExistNotDeleted)))
                .SetValidator(new DynItemExistsValidator<DynPublisherAccount>(ValidationExtensions._dynamoDb))
                .When(e => !e.PublisherAccountIds.IsNullOrEmptyReadOnly())
                .WithName("PublisherAccountIds");

            // All publishers for subscription must be properly typed (token accounts not allowed
            RuleForEach(e => e.PublisherAccountIds)
                .MustAsync(async (e, pid, t) =>
                           {
                               var publisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                               .TryGetPublisherAccountAsync(pid);

                               return publisherAccount != null && (publisherAccount.IsBusiness() || publisherAccount.IsInfluencer());
                           })
                .When(e => !e.PublisherAccountIds.IsNullOrEmptyReadOnly())
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("Only valid publisher account types can be included for subscription");

            // Any of the publishers included cannot already have an active paid subscription
            RuleForEach(e => e.PublisherAccountIds)
                .MustAsync(async (e, pid, t) =>
                           {
                               var workspacePublisherSubscriptionType = await WorkspaceService.DefaultWorkspacePublisherSubscriptionService
                                                                                              .GetPublisherSubscriptionTypeAsync(e.GetWorkspaceIdFromIdentifier(), pid);

                               var isPaidSubscription = workspacePublisherSubscriptionType.IsPaidSubscriptionType();

                               return !isPaidSubscription;
                           })
                .When(e => !e.PublisherAccountIds.IsNullOrEmptyReadOnly())
                .WithErrorCode(ErrorCodes.AlreadyExists)
                .WithMessage("One or more of the publishers requested already have an active subscription in this workspace");
        }
    }
}
