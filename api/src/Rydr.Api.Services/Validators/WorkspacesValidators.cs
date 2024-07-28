using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class GetWorkspaceValidator : BaseGetRequestValidator<GetWorkspace>
{
    public GetWorkspaceValidator()
    {
        RuleFor(e => e)
            .UpdateStateIntentTo(AccessIntent.ReadOnly);

        RuleFor(e => e.ToDynItemValidationSource(e.Id, DynItemType.Workspace, ApplyToBehavior.Default))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

public class GetWorkspaceAccessRequestsValidator : BaseRydrValidator<GetWorkspaceAccessRequests>
{
    public GetWorkspaceAccessRequestsValidator()
    {
        RuleFor(e => e)
            .UpdateStateIntentTo(AccessIntent.ReadOnly);

        Include(new IsValidWorkspaceIdentifierValidator<GetWorkspaceAccessRequests>(ApplyToBehavior.MustExistNotDeleted));
        Include(new IsAuthorizedToAlterWorkspace<GetWorkspaceAccessRequests>());
    }
}

public class GetWorkspacesValidator : BaseRydrValidator<GetWorkspaces>
{
    public GetWorkspacesValidator()
    {
        // UserIdentifer can be passed to override getting "my" workspaces, but only if the user is an admin, or the UserIdentifier specified is the userId themself...
        RuleFor(e => e.UserIdentifier)
            .Must((e, i) => i.EqualsOrdinalCi("me") || (e.IsSystemRequest && i.ToLong(0) > 0) ||
                            (i.ToLong(0) > 0 && i.ToLong(0) == e.UserId))
            .When(e => e.UserIdentifier.HasValue())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("UserIdentifier must be a valid value");
    }
}

public class GetWorkspaceUsersValidator : BaseRydrValidator<GetWorkspaceUsers>
{
    public GetWorkspaceUsersValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<GetWorkspaceUsers>(ApplyToBehavior.MustExistNotDeleted));

        Include(new IsAuthorizedToAlterWorkspace<GetWorkspaceUsers>());
    }
}

public class GetWorkspacePublisherAccountsValidator : BaseRydrValidator<GetWorkspacePublisherAccounts>
{
    public GetWorkspacePublisherAccountsValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<GetWorkspacePublisherAccounts>(ApplyToBehavior.MustExistNotDeleted));
        Include(new IsAuthorizedToAlterWorkspace<GetWorkspacePublisherAccounts>());
    }
}

public class GetWorkspacePublisherAccountUsersValidator : BaseRydrValidator<GetWorkspacePublisherAccountUsers>
{
    public GetWorkspacePublisherAccountUsersValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<GetWorkspacePublisherAccountUsers>(ApplyToBehavior.MustExistNotDeleted));
        Include(new IsAuthorizedToAlterWorkspace<GetWorkspacePublisherAccountUsers>());
        Include(new IsValidPublisherIdentifierValidator<GetWorkspacePublisherAccountUsers>());
    }
}

public class GetWorkspaceUserPublisherAccountsValidator : BaseRydrValidator<GetWorkspaceUserPublisherAccounts>
{
    public GetWorkspaceUserPublisherAccountsValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<GetWorkspaceUserPublisherAccounts>(ApplyToBehavior.MustExistNotDeleted));

        RuleFor(e => e.WorkspaceUserIdentifier)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.WorkspaceUserIdentifier)
            .Must((e, i) => i.EqualsOrdinalCi("me") || i.ToLong(0) > 0)
            .When(e => e.WorkspaceUserIdentifier.HasValue())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("WorkspaceUserIdentifier must be a valid value");

        // Skip access checks directly here, we verify access by the workspace to the user below
        RuleFor(e => e.ToDynItemValidationSourceByRef<DynUser>(e.WorkspaceUserIdentifier.ToLong(0), e.WorkspaceUserIdentifier.ToLong(0),
                                                               DynItemType.User, ApplyToBehavior.MustExistNotDeleted, true))
            .IsValidDynamoItem()
            .When(e => e.WorkspaceUserIdentifier.HasValue() && !e.WorkspaceUserIdentifier.EqualsOrdinalCi("me") &&
                       e.WorkspaceUserIdentifier.ToLong(0) != e.UserId);

        // User must exist in the workspace and have appropriate access depending on what was requested
        RuleFor(e => e.WorkspaceUserIdentifier)
            .MustAsync(async (e, u, ctx, t) =>
                       {
                           var workspace = await WorkspaceService.DefaultWorkspaceService
                                                                 .TryGetWorkspaceAsync(e.GetWorkspaceIdFromIdentifier());

                           if (workspace == null)
                           {
                               return false;
                           }

                           if (await WorkspaceService.DefaultWorkspaceService.IsWorkspaceAdmin(workspace, e.UserId))
                           {
                               return true;
                           }

                           // To view a list of unlinked profiles, have to be an admin/owner
                           if (e.Unlinked && !e.IsSystemRequest)
                           {
                               ctx.MessageFormatter.AppendArgument("rydrvmsg", "rwupavunlownsysoy");

                               return false;
                           }

                           var workspaceUserId = await WorkspaceService.DefaultWorkspaceService
                                                                       .GetWorkspaceUserIdAsync(e.GetWorkspaceIdFromIdentifier(),
                                                                                                e.WorkspaceUserIdentifier.EqualsOrdinalCi("me")
                                                                                                    ? e.UserId
                                                                                                    : e.WorkspaceUserIdentifier.ToLong(0));

                           if (workspaceUserId <= 0)
                           {
                               ctx.MessageFormatter.AppendArgument("rydrvmsg", "rwupavwudnx");

                               return false;
                           }

                           return true;
                       })
            .When(e => e.WorkspaceUserIdentifier.HasValue())
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("WorkspaceUser specified does not exist, does not have permission to perform this operation, or you do not have access to it - code [{rydrvmsg}]");
    }
}

public class PostWorkspaceValidator : BaseRydrValidator<PostWorkspace>
{
    public PostWorkspaceValidator()
    {
        RuleFor(e => e.Model)
            .NotNull()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.Model.Id)
            .Empty()
            .When(e => e.Model != null)
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.Model)
            .SetValidator(e => new WorkspaceValidator(e))
            .When(e => e.Model != null);

        RuleFor(e => e)
            .SetValidator(e => new IsFromValidRequestWorkspaceValidator<PostWorkspace>())
            .When(e => !e.IsSystemRequest);

        RuleForEach(e => e.LinkAccounts)
            .SetValidator(r => new LinkingPublisherAccountValidator(r))
            .When(e => !e.LinkAccounts.IsNullOrEmpty());
    }
}

public class PutLinkWorkspaceTokenAccountValidator : BaseRydrValidator<PutLinkWorkspaceTokenAccount>
{
    public PutLinkWorkspaceTokenAccountValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<PutLinkWorkspaceTokenAccount>());
        Include(new IsAuthorizedToAlterWorkspace<PutLinkWorkspaceTokenAccount>());

        RuleFor(e => e.TokenAccount)
            .NotNull()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.TokenAccount.AccessToken)
            .NotEmpty()
            .When(e => e.TokenAccount != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.TokenAccount.AccountType)
            .Must(t => t.IsUserAccount())
            .When(e => e.TokenAccount != null && e.TokenAccount.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("Only user accounts can be linked as token accounts to workspaces");

        RuleFor(e => e.TokenAccount.RydrAccountType)
            .Must(rat => rat.HasFlag(RydrAccountType.TokenAccount))
            .When(e => e.TokenAccount != null && e.TokenAccount.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("Accounts linked as token accounts to workspaces must include the TokenAccount flag");

        RuleFor(e => e.TokenAccount)
            .SetValidator(e => new PublisherAccountValidator(e, isUpsert: true))
            .When(e => e.TokenAccount != null);
    }
}

public class DeleteWorkspaceAccessRequestValidator : BaseRydrValidator<DeleteWorkspaceAccessRequest>
{
    public DeleteWorkspaceAccessRequestValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<DeleteWorkspaceAccessRequest>());
        Include(new IsAuthorizedToAlterWorkspace<DeleteWorkspaceAccessRequest>());

        RuleFor(e => e.RequestedUserId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}

public class PostRequestWorkspaceAccessValidator : BaseRydrValidator<PostRequestWorkspaceAccess>
{
    private static readonly HashSet<char> _validInviteCodeValues = TimestampedWorkspaceService.InviteCodeCharacters.AsHashSet();

    public PostRequestWorkspaceAccessValidator()
    {
        RuleFor(e => e.InviteToken)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(15)
            .MustAsync(async (e, ic, t) =>
                       {
                           if (ic.IsNullOrEmpty() || ic.Any(c => !_validInviteCodeValues.Contains(c)))
                           { // Invalid characters
                               return false;
                           }

                           var inviteTokenMap = await MapItemService.DefaultMapItemService
                                                                    .TryGetMapByHashedEdgeAsync(DynItemType.InviteToken, ic);

                           if (inviteTokenMap?.ReferenceNumber == null)
                           {
                               return false;
                           }

                           // Has to be an active workspace
                           var workspace = await WorkspaceService.DefaultWorkspaceService
                                                                 .TryGetWorkspaceAsync(inviteTokenMap.ReferenceNumber.Value);

                           return workspace != null && !workspace.IsDeleted() &&
                                  workspace.OwnerId != e.GetUserIdFromIdentifier();
                       })
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("InviteToken must be valid and cannot be used by the workspace owner");

        RuleFor(e => e.UserIdentifier)
            .Must((e, i) => i.EqualsOrdinalCi("me") || (e.IsSystemRequest && i.ToLong(0) > 0) ||
                            (i.ToLong(0) > 0 && i.ToLong(0) == e.UserId))
            .When(e => e.UserIdentifier.HasValue())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("UserIdentifier must be a valid value");

        RuleFor(e => e.UserIdentifier)
            .MustAsync(async (e, i, t) =>
                       {
                           // If the user is already part of the workspace, cannot request again
                           var inviteTokenMap = await MapItemService.DefaultMapItemService
                                                                    .TryGetMapByHashedEdgeAsync(DynItemType.InviteToken, e.InviteToken);

                           if (inviteTokenMap?.ReferenceNumber == null)
                           { // Returning true here correctly, want to bascially ignore this validation in this case, it'll have been properly
                               // invalidated by the MustBeValid validation above
                               return true;
                           }

                           // If the user is already part of the workspace, invalid request (i.e. no need to request again)
                           var workspaceUserId = await WorkspaceService.DefaultWorkspaceService
                                                                       .GetWorkspaceUserIdAsync(inviteTokenMap.ReferenceNumber.Value,
                                                                                                e.GetUserIdFromIdentifier());

                           return workspaceUserId <= 0;
                       })
            .When(e => e.InviteToken.HasValue())
            .WithErrorCode(ErrorCodes.AlreadyExists)
            .WithMessage("You or the UserIdentifier specified are already part of the workspace requested");
    }
}

public class PutLinkWorkspaceUserValidator : BaseRydrValidator<PutLinkWorkspaceUser>
{
    public PutLinkWorkspaceUserValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<PutLinkWorkspaceUser>(ApplyToBehavior.MustExistNotDeleted));

        Include(new IsAuthorizedToAlterWorkspace<PutLinkWorkspaceUser>());

        RuleFor(e => e.LinkUserId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        // Skip access checks as we are linking a new user into a workspace, a workspace admin is always allowed to do so
        RuleFor(e => e.ToDynItemValidationSourceByRef<DynUser>(e.LinkUserId, e.LinkUserId, DynItemType.User, ApplyToBehavior.MustExistNotDeleted, true))
            .IsValidDynamoItem()
            .When(e => e.LinkUserId > 0);

        // Must be an open invite request if this is not an admin
        RuleFor(e => e.LinkUserId)
            .MustAsync(async (e, i, t) =>
                       {
                           var inviteMap = await MapItemService.DefaultMapItemService
                                                               .TryGetMapAsync(e.GetWorkspaceIdFromIdentifier(),
                                                                               DynItemMap.BuildEdgeId(DynItemType.InviteRequest, i.ToStringInvariant()));

                           return inviteMap?.ReferenceNumber != null && inviteMap.ReferenceNumber.Value == i;
                       })
            .When(e => e.LinkUserId > 0 && !e.IsSystemRequest)
            .WithErrorCode(ErrorCodes.MustExist)
            .WithMessage("User being linked must have requested access");
    }
}

public class DeleteLinkedWorkspaceUserValidator : BaseRydrValidator<DeleteLinkedWorkspaceUser>
{
    public DeleteLinkedWorkspaceUserValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<DeleteLinkedWorkspaceUser>(ApplyToBehavior.MustExistNotDeleted));

        Include(new IsAuthorizedToAlterWorkspace<DeleteLinkedWorkspaceUser>((e, w) => w.OwnerId != e.LinkedUserId));

        RuleFor(e => e.LinkedUserId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        // Not bothering to validate the LinkedUserId being sent as valid, as we dont need to/care - even if an invalid user id is linked, still want to delink it
    }
}

public class PutWorkspaceUserRoleValidator : BaseRydrValidator<PutWorkspaceUserRole>
{
    public PutWorkspaceUserRoleValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<PutWorkspaceUserRole>(ApplyToBehavior.MustExistNotDeleted));

        Include(new IsAuthorizedToAlterWorkspace<PutWorkspaceUserRole>());

        RuleFor(e => e.UserIdentifier)
            .Must((e, i) => i.EqualsOrdinalCi("me") || (e.IsSystemRequest && i.ToLong(0) > 0) ||
                            (i.ToLong(0) > 0 && i.ToLong(0) == e.UserId))
            .When(e => e.UserIdentifier.HasValue())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("UserIdentifier must be a valid value");

        RuleFor(e => e.WorkspaceRole)
            .NotEqual(WorkspaceRole.Unknown)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class PutLinkWorkspaceUserPublisherAccountValidator : BaseRydrValidator<PutLinkWorkspaceUserPublisherAccount>
{
    public PutLinkWorkspaceUserPublisherAccountValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<PutLinkWorkspaceUserPublisherAccount>(ApplyToBehavior.MustExistNotDeleted));

        Include(new IsAuthorizedToAlterWorkspace<PutLinkWorkspaceUserPublisherAccount>());

        RuleFor(e => e.WorkspaceUserId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        Include(new IsValidPublisherAccountIdValidator<PutLinkWorkspaceUserPublisherAccount>());

        // Skip access checks directly here, we verify access by the workspace to the user below
        RuleFor(e => e.ToDynItemValidationSourceByRef<DynUser>(e.WorkspaceUserId, e.WorkspaceUserId, DynItemType.User,
                                                               ApplyToBehavior.MustExistNotDeleted, true))
            .IsValidDynamoItem()
            .When(e => e.WorkspaceUserId > 0);

        // User must exist in the workspace
        RuleFor(e => e.WorkspaceUserId)
            .MustAsync(async (e, u, t) =>
                       {
                           var workspaceUserId = await WorkspaceService.DefaultWorkspaceService
                                                                       .GetWorkspaceUserIdAsync(e.GetWorkspaceIdFromIdentifier(), u);

                           return workspaceUserId > 0;
                       })
            .When(e => e.WorkspaceUserId > 0)
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("User specified does not exist or you do not have access to it");
    }
}

public class DeleteLinkWorkspaceUserPublisherAccountValidator : BaseRydrValidator<DeleteLinkWorkspaceUserPublisherAccount>
{
    public DeleteLinkWorkspaceUserPublisherAccountValidator()
    {
        Include(new IsValidWorkspaceIdentifierValidator<DeleteLinkWorkspaceUserPublisherAccount>(ApplyToBehavior.MustExistNotDeleted));

        Include(new IsAuthorizedToAlterWorkspace<DeleteLinkWorkspaceUserPublisherAccount>());

        RuleFor(e => e.WorkspaceUserId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        Include(new IsValidPublisherAccountIdValidator<DeleteLinkWorkspaceUserPublisherAccount>());

        // Skip access checks directly here, we verify access by the workspace to the user below
        RuleFor(e => e.ToDynItemValidationSourceByRef<DynUser>(e.WorkspaceUserId, e.WorkspaceUserId, DynItemType.User, ApplyToBehavior.MustExistNotDeleted, true))
            .IsValidDynamoItem()
            .When(e => e.WorkspaceUserId > 0);

        // User must exist in the workspace
        RuleFor(e => e.WorkspaceUserId)
            .MustAsync(async (e, u, t) =>
                       {
                           var workspaceUserId = await WorkspaceService.DefaultWorkspaceService
                                                                       .GetWorkspaceUserIdAsync(e.GetWorkspaceIdFromIdentifier(), u);

                           return workspaceUserId > 0;
                       })
            .When(e => e.WorkspaceUserId > 0)
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("User specified does not exist or you do not have access to it");
    }
}

public class PutWorkspaceValidator : BasePutRequestValidator<PutWorkspace, Workspace>
{
    public PutWorkspaceValidator()
        : base(r => new WorkspaceValidator(r)) { }
}

public class DeleteWorkspaceValidator : BaseDeleteRequestValidator<DeleteWorkspace>
{
    public DeleteWorkspaceValidator()
    {
        RuleFor(e => e.ToDynItemValidationSource(e.Id, DynItemType.Workspace, ApplyToBehavior.MustExistCanBeDeleted))
            .IsValidDynamoItem();
    }
}

public class WorkspacePostedValidator : BaseRydrValidator<WorkspacePosted>
{
    public WorkspacePostedValidator()
    {
        RuleFor(e => e.Id)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class WorkspaceUpdatedValidator : BaseRydrValidator<WorkspaceUpdated>
{
    public WorkspaceUpdatedValidator()
    {
        RuleFor(e => e.Id)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.ToWorkspaceType)
            .NotEqual(WorkspaceType.Unspecified)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class ValidateWorkspaceSubscriptionValidator : BaseRydrValidator<ValidateWorkspaceSubscription> { }

public class DeleteWorkspaceInternalValidator : BaseRydrValidator<DeleteWorkspaceInternal>
{
    public DeleteWorkspaceInternalValidator()
    {
        RuleFor(e => e.Id)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class WorkspaceDeletedValidator : BaseRydrValidator<WorkspaceDeleted>
{
    public WorkspaceDeletedValidator()
    {
        RuleFor(e => e.Id)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class WorkspaceUserLinkedValidator : BaseRydrValidator<WorkspaceUserLinked>
{
    public WorkspaceUserLinkedValidator()
    {
        RuleFor(e => e.RydrUserId)
            .GreaterThan(0);

        RuleFor(e => e.WorkspaceUserId)
            .GreaterThan(0);

        RuleFor(e => e.InWorkspaceId)
            .GreaterThan(0);
    }
}

public class WorkspaceUserDelinkedValidator : BaseRydrValidator<WorkspaceUserDelinked>
{
    public WorkspaceUserDelinkedValidator()
    {
        RuleFor(e => e.RydrUserId)
            .GreaterThan(0);

        RuleFor(e => e.InWorkspaceId)
            .GreaterThan(0);
    }
}

public class WorkspaceUserPublisherAccountLinkedValidator : BaseRydrValidator<WorkspaceUserPublisherAccountLinked>
{
    public WorkspaceUserPublisherAccountLinkedValidator()
    {
        RuleFor(e => e.RydrUserId)
            .GreaterThan(0);

        RuleFor(e => e.WorkspaceUserId)
            .GreaterThan(0);

        RuleFor(e => e.InWorkspaceId)
            .GreaterThan(0);

        RuleFor(e => e.ToPublisherAccountId)
            .GreaterThan(0);
    }
}

public class WorkspaceUserPublisherAccountDelinkedValidator : BaseRydrValidator<WorkspaceUserPublisherAccountDelinked>
{
    public WorkspaceUserPublisherAccountDelinkedValidator()
    {
        RuleFor(e => e.RydrUserId)
            .GreaterThan(0);

        RuleFor(e => e.WorkspaceUserId)
            .GreaterThan(0);

        RuleFor(e => e.InWorkspaceId)
            .GreaterThan(0);

        RuleFor(e => e.FromPublisherAccountId)
            .GreaterThan(0);
    }
}

public class WorkspaceValidator : AbstractValidator<Workspace>
{
    public WorkspaceValidator(IRequestBase request)
    {
        RuleSet(ApplyTo.Post | ApplyTo.Put,
                () =>
                {
                    // PUT - don't use the PUT verb filter, as we can use this entity validator as attribute validators from other models
                    // when those enclosing models may be doing a POST of that model but including an existing sub-model
                    RuleFor(e => request.ToDynItemValidationSource(e.Id, DynItemType.Workspace, ApplyToBehavior.MustExistNotDeleted))
                        .IsValidDynamoItem()
                        .When(e => e.Id > 0);
                });

        // Normally you may consider putting these inside a POST ruleset, but since the validator here can be used as a sub-attribute on other request
        // models, the sub-model may need to act like a POST when the outer model was PUT'ed, or act as a PUT when the outer was POST'ed.  So, we leave
        // the POST and PUT specific checks (i.e. Id must be <= 0 on a POST, or Id must be > 0 on a PUT) for the Post/Put specific request validators
        // and use the WHEN filters here

        // POST-like rules only (i.e. Id <= 0)
        RuleFor(e => e.Name)
            .NotEmpty()
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        // ALL operation rules
        RuleFor(e => e.WorkspaceFeatures)
            .Must(f => f == WorkspaceFeature.None || f == WorkspaceFeature.Default)
            .When(e => !request.IsSystemRequest)
            .WithErrorCode(ErrorCodes.CannotBeSpecified)
            .WithMessage("You do not have access to change the specified value(s)");

        RuleFor(e => e.WorkspaceType)
            .Equal(WorkspaceType.Unspecified)
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.WorkspaceRole)
            .Equal(WorkspaceRole.Unknown)
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.DefaultPublisherAccountId)
            .Empty()
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.Name)
            .MaximumLength(100)
            .WithErrorCode(ErrorCodes.TooLong);

        RuleFor(e => e.OwnerId)
            .Empty()
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.PublisherAccountInfo)
            .Null()
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.InviteCode)
            .Null()
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.AccessRequests)
            .Empty()
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.SubscriptionType)
            .Equal(SubscriptionType.None)
            .WithErrorCode(ErrorCodes.CannotBeSpecified);
    }
}
