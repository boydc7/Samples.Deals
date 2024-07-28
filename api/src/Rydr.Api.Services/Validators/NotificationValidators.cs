using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class ServerNotificationSubscribeValidator : BaseRydrValidator<ServerNotificationSubscribe>
{
    public ServerNotificationSubscribeValidator()
    {
        RuleFor(e => e.Token)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}

public class ServerNotificationUnSubscribeValidator : BaseRydrValidator<ServerNotificationUnSubscribe>
{
    public ServerNotificationUnSubscribeValidator()
    {
        RuleFor(e => e.TokenHash)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}

public class DeleteServerNotificationValidator : BaseRydrValidator<DeleteServerNotification>
{
    public DeleteServerNotificationValidator()
    {
        RuleFor(e => e.TokenHash)
            .NotEmpty();
    }
}

public class PostServerDealMatchNotificationValidator : BaseRydrValidator<PostServerDealMatchNotification>
{
    public PostServerDealMatchNotificationValidator()
    {
        RuleFor(e => e.ToPublisherAccountIds)
            .NotEmpty()
            .IsValidIdList("ToPublisherAccountIds");

        RuleForEach(e => e.ToPublisherAccountIds
                          .Select(p => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(p, p.ToStringInvariant(),
                                                                                             DynItemType.PublisherAccount,
                                                                                             ApplyToBehavior.MustExistNotDeleted)))
            .SetValidator(new DynItemExistsValidator<DynPublisherAccount>(ValidationExtensions._dynamoDb))
            .When(e => !e.ToPublisherAccountIds.IsNullOrEmpty())
            .WithName("ToPublisherAccountIds");

        RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem();

        RuleFor(e => e.DealId)
            .MustAsync(async (dealId, t) =>
                       {
                           var deal = await DealExtensions.DefaultDealService.GetDealAsync(dealId, true);

                           return deal != null && deal.DealStatus == DealStatus.Published;
                       })
            .When(e => e.DealId > 0)
            .WithMessage("Deal must exist and be in a published state to send match notifications about");

        RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.FromPublisherAccountId, e.FromPublisherAccountId.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.FromPublisherAccountId > 0);

        RuleFor(e => e.Title)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.Message)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}

public class PostServerNotificationValidator : BaseRydrValidator<PostServerNotification>
{
    public PostServerNotificationValidator()
    {
        RuleFor(e => e.Notification)
            .NotNull()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.Notification)
            .SetValidator(e => new ServerNotificationValidator(e.IsSystemRequest || Request.IsRydrRequest()))
            .When(e => e.Notification != null);
    }
}

public class GetNotificationSubscriptionsValidator : BaseRydrValidator<GetNotificationSubscriptions>
{
    public GetNotificationSubscriptionsValidator()
    {
        Include(new IsFromValidRequestWorkspaceValidator<GetNotificationSubscriptions>());
        Include(new IsFromValidRequestPublisherAccountValidator<GetNotificationSubscriptions>());
    }
}

public class PutNotificationSubscriptionValidator : BaseRydrValidator<PutNotificationSubscription>
{
    public PutNotificationSubscriptionValidator()
    {
        RuleFor(e => e.NotificationType)
            .NotEqual(ServerNotificationType.Unspecified)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        Include(new IsFromValidRequestWorkspaceValidator<PutNotificationSubscription>());
        Include(new IsFromValidRequestPublisherAccountValidator<PutNotificationSubscription>());
    }
}

public class DeleteNotificationSubscriptionValidator : BaseRydrValidator<DeleteNotificationSubscription>
{
    public DeleteNotificationSubscriptionValidator()
    {
        RuleFor(e => e.NotificationType)
            .NotEqual(ServerNotificationType.Unspecified)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        Include(new IsFromValidRequestWorkspaceValidator<DeleteNotificationSubscription>());
        Include(new IsFromValidRequestPublisherAccountValidator<DeleteNotificationSubscription>());
    }
}

// Internal, simple
public class PostTrackEventNotificationValidator : BaseRydrValidator<PostTrackEventNotification> { }
public class PostExternalCrmContactUpdateValidator : BaseRydrValidator<PostExternalCrmContactUpdate> { }

public class GetNotificationsValidator : BaseRydrValidator<GetNotifications>
{
    public GetNotificationsValidator()
    {
        RuleFor(e => e)
            .SetValidator(new IsFromValidRequestWorkspaceValidator<GetNotifications>())
            .When(e => !e.IsSystemRequest);

        RuleFor(e => e)
            .SetValidator(new IsFromValidRequestPublisherAccountValidator<GetNotifications>())
            .When(e => e.ForPublisherAccountIds.IsNullOrEmpty());

        Include(new IsValidSkipTakeValidator());

        RuleFor(e => e.ForPublisherAccountIds)
            .MustAsync(async (r, paids, t) =>
                       {
                           // Must be authorized to each account requests
                           foreach (var paid in paids)
                           {
                               if (await AuthorizationExtensions.DefaultAuthorizeService
                                                                .IsAuthorizedAsync(r.WorkspaceId, paid))
                               {
                                   continue;
                               }

                               // Workspace not authorized, see if a root account is included and associated
                               if (r.RequestPublisherAccountId > 0 &&
                                   await AssociationExtensions.DefaultAssociationService
                                                              .IsAssociatedAsync(r.RequestPublisherAccountId, paid))
                               {
                                   continue;
                               }

                               return false;
                           }

                           return true;
                       })
            .When(e => !e.ForPublisherAccountIds.IsNullOrEmpty() && !e.IsSystemRequest)
            .WithMessage("Accounts specified must exist, be valid, and you must have access to them.")
            .WithErrorCode(ErrorCodes.MustBeAuthorized);
    }
}

public class GetNotificationCountsValidator : BaseRydrValidator<GetNotificationCounts>
{
    public GetNotificationCountsValidator()
    {
        Include(new IsFromValidRequestWorkspaceValidator<GetNotificationCounts>());

        RuleFor(e => e)
            .SetValidator(new IsFromValidRequestPublisherAccountValidator<GetNotificationCounts>())
            .When(e => e.ForPublisherAccountIds.IsNullOrEmpty());

        RuleFor(e => e.ForPublisherAccountIds)
            .MustAsync(async (r, paids, t) =>
                       {
                           // Must be authorized to each account requests
                           foreach (var paid in paids)
                           {
                               if (await AuthorizationExtensions.DefaultAuthorizeService
                                                                .IsAuthorizedAsync(r.WorkspaceId, paid))
                               {
                                   continue;
                               }

                               // Workspace not authorized, see if a root account is included and associated
                               if (r.RequestPublisherAccountId > 0 &&
                                   await AssociationExtensions.DefaultAssociationService
                                                              .IsAssociatedAsync(r.RequestPublisherAccountId, paid))
                               {
                                   continue;
                               }

                               return false;
                           }

                           return true;
                       })
            .When(e => !e.ForPublisherAccountIds.IsNullOrEmpty())
            .WithMessage("Accounts specified must exist, be valid, and you must have access to them.")
            .WithErrorCode(ErrorCodes.MustBeAuthorized);
    }
}

public class DeleteNotificationsValidator : BaseRydrValidator<DeleteNotifications>
{
    public DeleteNotificationsValidator()
    {
        Include(new IsFromValidRequestWorkspaceValidator<DeleteNotifications>());
        Include(new IsFromValidRequestPublisherAccountValidator<DeleteNotifications>());

        RuleFor(e => e.ToDynItemValidationSource<DynNotification>(e.RequestPublisherAccountId, e.Id,
                                                                  DynItemType.Notification, null, ApplyToBehavior.MustExistNotDeleted, false, null))
            .IsValidDynamoItem()
            .When(e => e.Id.HasValue());
    }
}

public class ServerNotificationValidator : AbstractValidator<ServerNotification>
{
    public ServerNotificationValidator(bool isSystemRequest)
    {
        RuleFor(e => e.Title)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.Message)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.To)
            .NotNull()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.To.Id)
            .GreaterThan(0)
            .When(e => e.To != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.To.UserName)
            .NotEmpty()
            .When(e => e.To != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.To.FullName)
            .NotEmpty()
            .When(e => e.To != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.To.Type)
            .NotEqual(PublisherType.Unknown)
            .When(e => e.To != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => new RecordTypeId(RecordType.PublisherAccount, e.To.Id))
            .IsValidRecordTypeId("To")
            .When(e => e.To != null && e.To.Id > 0);

        RuleFor(e => new RecordTypeId(RecordType.PublisherAccount, e.From.Id))
            .IsValidRecordTypeId("From")
            .When(e => e.From != null && e.From.Id > 0);

        RuleFor(e => e.ForRecord)
            .IsValidRecordTypeId("ForRecord")
            .When(e => e.ForRecord != null);

        RuleFor(e => e.From)
            .NotNull()
            .Unless(n => isSystemRequest)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.From.Id)
            .GreaterThan(0)
            .When(e => e.From != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.From.UserName)
            .NotEmpty()
            .When(e => e.From != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.From.FullName)
            .NotEmpty()
            .When(e => e.From != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.From.Type)
            .NotEqual(PublisherType.Unknown)
            .When(e => e.From != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}
