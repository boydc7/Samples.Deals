using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Admin;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class PostGetPublisherAppAccountsValidator : BaseRydrValidator<PostGetPublisherAppAccounts>
    {
        public PostGetPublisherAppAccountsValidator()
        {
            Include(new IsValidPublisherAccountIdValidator<PostGetPublisherAppAccounts>());
        }
    }

    public class RemapPublisherAccountValidator : BaseRydrValidator<RemapSoftBasicPublisherAccount>
    {
        public RemapPublisherAccountValidator()
        {
            RuleFor(e => e.BasicPublisherAccountId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("Valid publisher account identifier is required");

            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.BasicPublisherAccountId, e.BasicPublisherAccountId.ToStringInvariant(),
                                                                               DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.BasicPublisherAccountId > 0);

            RuleFor(e => e.SoftLinkedPublisherAccountId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("Valid publisher account identifier is required");

            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.SoftLinkedPublisherAccountId, e.SoftLinkedPublisherAccountId.ToStringInvariant(),
                                                                               DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.SoftLinkedPublisherAccountId > 0);

            RuleFor(e => e.SoftLinkedPublisherAccountId)
                .MustAsync(async (i,t) =>
                           {
                               var softLinkedPublisher = await PublisherExtensions.DefaultPublisherAccountService.TryGetPublisherAccountAsync(i);

                               return softLinkedPublisher != null && !softLinkedPublisher.IsDeleted() &&
                                      softLinkedPublisher.IsRydrSoftLinkedAccount();
                           })
                .When(e => e.SoftLinkedPublisherAccountId > 0)
                .WithMessage("SoftLinkedPublisherAccountId must be valid and an existing SoftLinked account");


            RuleFor(e => e.BasicPublisherAccountId)
                .MustAsync(async (i, t) =>
                           {
                               var fromPublisher = await PublisherExtensions.DefaultPublisherAccountService.TryGetPublisherAccountAsync(i);

                               return fromPublisher != null && !fromPublisher.IsDeleted() &&
                                      fromPublisher.IsBasicLink;
                           })
                .When(e => e.BasicPublisherAccountId > 0)
                .WithMessage("BasicPublisherAccountId must be valid and an existing BasicLinked account");
        }
    }

    public class RemapUserValidator : BaseRydrValidator<RemapUser>
    {
        public RemapUserValidator()
        {
            RuleFor(e => e.FromUserFirebaseId)
                .NotEmpty();

            RuleFor(e => e.ToUserFirebaseId)
                .NotEmpty();

            RuleFor(e => e.FromUserFirebaseId)
                .MustAsync(async (i, t) => await UserExtensions.DefaultUserService.GetUserByAuthUidAsync(i) != null)
                .WithErrorCode(ErrorCodes.MustExist);

            RuleFor(e => e.ToUserFirebaseId)
                .MustAsync(async (i, t) => await UserExtensions.DefaultUserService.GetUserByAuthUidAsync(i) != null)
                .WithErrorCode(ErrorCodes.MustExist);
        }
    }

    public class AuditCurrentPublisherAccountStatsValidator : BaseRydrValidator<AuditCurrentPublisherAccountStats>
    {
        public AuditCurrentPublisherAccountStatsValidator()
        {
            // negative value for either indicates ALL of the things...
            RuleFor(e => e.PublisherAccountId)
                .NotEqual(0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.InWorkspaceId)
                .NotEqual(0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e)
                .SetValidator(new IsValidPublisherAccountIdValidator<AuditCurrentPublisherAccountStats>())
                .When(e => e.PublisherAccountId > 0);

            RuleFor(e => e.ToDynItemValidationSource<DynWorkspace>(e.InWorkspaceId, DynItemType.Workspace, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.InWorkspaceId > 0);
        }
    }

    public class SoftLinkMapRydrValidator : BaseRydrValidator<SoftLinkMapRydr>
    {
        public SoftLinkMapRydrValidator()
        {
            RuleFor(e => e.ToWorkspaceId)
                .GreaterThan(0)
                .Must(wid => WorkspaceExtensions.RydrWorkspaceIds.Contains(wid))
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("ToWorkspaceId must be a valid RYDR workspace");

            RuleFor(e => e.ToPublisherAccountId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.ToPublisherAccountId, e.ToPublisherAccountId.ToStringInvariant(),
                                                                               DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.ToPublisherAccountId > 0);
        }
    }

    public class RebuildEsCreatorsValidator : BaseRydrValidator<RebuildEsCreators>
    {
        public RebuildEsCreatorsValidator()
        {
            // negative value for either indicates ALL of the things...
            RuleFor(e => e.PublisherAccountId)
                .NotEqual(0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e)
                .SetValidator(new IsValidPublisherAccountIdValidator<RebuildEsCreators>((r, p) => p.IsInfluencer()))
                .When(e => e.PublisherAccountId > 0);
        }
    }

    public class PutUncancelDealRequestValidator : BaseRydrValidator<PutUncancelDealRequest>
    {
        public PutUncancelDealRequestValidator()
        {
            Include(new IsValidPublisherAccountIdValidator<PutUncancelDealRequest>((r, p) => p.IsInfluencer()));

            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem();

            RuleFor(e => e.ToDynItemValidationSource<DynDealRequest>(e.DealId, e.PublisherAccountId.ToEdgeId(), DynItemType.DealRequest, null, ApplyToBehavior.MustExistCanBeDeleted, false,
                                                                     dr => dr.RequestStatus == DealRequestStatus.Cancelled))
                .IsValidDynamoItem();
        }
    }

    public class RebuildEsBusinessesValidator : BaseRydrValidator<RebuildEsBusinesses>
    {
        public RebuildEsBusinessesValidator()
        {
            // negative value for either indicates ALL of the things...
            RuleFor(e => e.PublisherAccountId)
                .NotEqual(0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e)
                .SetValidator(new IsValidPublisherAccountIdValidator<RebuildEsBusinesses>((r, p) => p.IsBusiness()))
                .When(e => e.PublisherAccountId > 0);
        }
    }

    public class RebuildEsMediasValidator : BaseRydrValidator<RebuildEsMedias>
    {
        public RebuildEsMediasValidator()
        {
            // negative value for either indicates ALL of the things...
            RuleFor(e => e.PublisherAccountId)
                .NotEqual(0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e)
                .SetValidator(new IsValidPublisherAccountIdValidator<RebuildEsMedias>())
                .When(e => e.PublisherAccountId > 0);
        }
    }

    public class RebuildEsDealsValidator : BaseRydrValidator<RebuildEsDeals>
    {
        public RebuildEsDealsValidator()
        {
            // negative value for either indicates ALL of the things...
            RuleFor(e => e.PublisherAccountId)
                .NotEqual(0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e)
                .SetValidator(new IsValidPublisherAccountIdValidator<RebuildEsDeals>())
                .When(e => e.PublisherAccountId > 0);
        }
    }

    public class MqRetryValidator : BaseRydrValidator<MqRetry>
    {
        public MqRetryValidator()
        {
            // negative value for either indicates ALL of the things...
            RuleFor(e => e.TypeName)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);
        }
    }

    public class RebalanceRecurringJobsValidator : BaseRydrValidator<RebalanceRecurringJobs> { }

    public class PostPayInvoiceValidator : BaseRydrValidator<PostPayInvoice>
    {
        public PostPayInvoiceValidator()
        {
            RuleFor(e => e.InvoiceId)
                .NotEmpty();
        }
    }

    public class ChargeCompletedUsageValidator : BaseRydrValidator<ChargeCompletedUsage>
    {
        public ChargeCompletedUsageValidator()
        {
            Include(new IsValidWorkspaceIdentifierValidator<ChargeCompletedUsage>(ApplyToBehavior.MustExistNotDeleted));
            Include(new IsValidPublisherIdentifierValidator<ChargeCompletedUsage>(alsoMust: (r, p) => p.IsBusiness()));

            RuleFor(e => e.StartDate)
                .IsValidDateTime("StartDate");

            RuleFor(e => e.EndDate)
                .IsValidDateTime("EndDate");

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
                .WithMessage("Only paid, managing workspaces can be specified for usage charging");

            // Any of the publishers included cannot already have an active paid subscription
            RuleFor(e => e.PublisherIdentifier)
                .MustAsync(async (e, pidf, t) =>
                           {
                               var pid = e.GetPublisherIdFromIdentifier();

                               var workspacePublisherSubscription = await WorkspaceService.DefaultWorkspacePublisherSubscriptionService
                                                                                          .GetPublisherSubscriptionAsync(e.GetWorkspaceIdFromIdentifier(), pid);

                               return workspacePublisherSubscription != null && !workspacePublisherSubscription.IsDeleted() &&
                                      workspacePublisherSubscription.SubscriptionType.IsManagedSubscriptionType();
                           })
                .Unless(e => e.PublisherIdentifier.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.MustExist)
                .WithMessage("The publisher requested does not have a valid active managed subscription in this workspace");
        }
    }

    public class SubscribeWorksapceUnlimittedValidator : BaseRydrValidator<SubscribeWorksapceUnlimitted>
    {
        public SubscribeWorksapceUnlimittedValidator()
        {
            // negative value for either indicates ALL of the things...
            RuleFor(e => e.SubscribeWorkspaceId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.ToDynItemValidationSource(e.SubscribeWorkspaceId, DynItemType.Workspace, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.SubscribeWorkspaceId > 0);

            RuleFor(e => e.SubscribeWorkspaceId)
                .MustAsync(async (r, i, t) =>
                           {
                               var existingActiveSubscription = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                                                      .TryGetActiveWorkspaceSubscriptionAsync(i);

                               return !existingActiveSubscription.IsPaidSubscription();
                           })
                .When(e => e.SubscribeWorkspaceId > 0)
                .WithErrorCode(ErrorCodes.InvalidState)
                .WithMessage("Workspace to add unlimitted subscription for must not have an existing active paid subscription");
        }
    }
}
