using System.Linq;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using Rydr.Api.QueryDto;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class GetDealValidator : BaseGetRequestValidator<GetDeal>
    {
        public GetDealValidator()
        {
            Unless(e => e.IsSystemRequest,
                   () =>
                   {
                       Include(new IsFromValidRequestWorkspaceValidator<GetDeal>());

                       // Active multi-team subscribers can query cross-workspace
                       RuleFor(e => e)
                           .SetValidator(new IsFromValidRequestPublisherAccountValidator<GetDeal>())
                           .UnlessAsync((e, t) => e.IsSubscribedTeamWorkspaceAsync());
                   });

            RuleFor(e => e.ToDynItemValidationSource(e.Id, e.RequestedPublisherAccountId, DynItemType.DealRequest, ApplyToBehavior.Default, false))
                .IsValidDynamoItem()
                .When(e => e.RequestedPublisherAccountId > 0);

            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.Id.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.Default))
                .IsValidDynamoItem();
        }
    }

    public class GetDealInvitesValidator : BaseGetRequestValidator<GetDealInvites>
    {
        public GetDealInvitesValidator()
        {
            Include(new IsValidSkipTakeValidator());

            RuleFor(e => e)
                .SetValidator(new IsFromValidRequestWorkspaceValidator<GetDealInvites>())
                .Unless(e => e.IsSystemRequest);

            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.Id.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.Default))
                .IsValidDynamoItem();
        }
    }

    public class GetDealExternalLinkValidator : BaseGetRequestValidator<GetDealExternalLink>
    {
        public GetDealExternalLinkValidator()
        {
            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.Id.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.Default))
                .IsValidDynamoItem();
        }
    }

    public class GetDealByLinkValidator : BaseRydrValidator<GetDealByLink>
    {
        public GetDealByLinkValidator()
        {
            RuleFor(e => e.DealLink)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.InvalidState);

            RuleFor(e => e.DealLink)
                .MustAsync(async (l, t) =>
                           {
                               var dealLinkAssociation = await AssociationExtensions.DefaultAssociationService
                                                                                    .GetAssociationsToAsync(l, RecordType.Deal, RecordType.DealLink)
                                                                                    .FirstOrDefaultAsync();

                               if (dealLinkAssociation == null || dealLinkAssociation.IsDeleted())
                               {
                                   return false;
                               }

                               var deal = await DealExtensions.DefaultDealService.GetDealAsync(dealLinkAssociation.FromRecordId, true);

                               return deal != null && !deal.IsDeleted();
                           })
                .WithErrorCode(ErrorCodes.MustExist);
        }
    }

    public class GetDealExternalHtmlValidator : AbstractValidator<GetDealExternalHtml>
    {
        public GetDealExternalHtmlValidator()
        {
            RuleFor(e => e.DealLink)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.InvalidState)
                .WithName("State")
                .OverridePropertyName("State")
                .WithMessage("State invalid - code [xdli]");

            RuleFor(e => e.DealLink)
                .MustAsync(async (l, t) =>
                           {
                               var dealLinkAssociation = await AssociationExtensions.DefaultAssociationService
                                                                                    .GetAssociationsToAsync(l, RecordType.Deal, RecordType.DealLink)
                                                                                    .FirstOrDefaultAsync();

                               if (dealLinkAssociation == null || dealLinkAssociation.IsDeleted())
                               {
                                   return false;
                               }

                               var deal = await DealExtensions.DefaultDealService.GetDealAsync(dealLinkAssociation.FromRecordId, true);

                               return deal != null && !deal.IsDeleted();
                           })
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithName("State")
                .OverridePropertyName("State")
                .WithMessage("State invalid - code [xdldnx]");
        }
    }

    public class QueryPublisherDealsValidator : BaseRydrValidator<QueryPublisherDeals>
    {
        public QueryPublisherDealsValidator()
        {
            // Admins can query everything in the system
            // Workspace owners can query everything in the workspace
            // Workspace users can query the workspace for publishers assigned to them
            // Everyone else queries over a specific worksapce/publisher context

            // Always have to have a workspace unless an admin
            Unless(e => e.IsSystemRequest,
                   () =>
                   {
                       RuleFor(e => e)
                           .SetValidator(new IsFromValidRequestWorkspaceValidator<QueryPublisherDeals>());

                       // Admins and teams with valid subscriptions can query across the workspace, otherwise there has to be
                       // a valid request publisher account specified, or a specific publisherId (the Id property on a deal)
                       RuleFor(e => e)
                           .SetValidator(new IsFromValidRequestPublisherAccountValidator<QueryPublisherDeals>())
                           .UnlessAsync(async (e, t) => e.Id > 0 ||
                                                        (await e.IsSubscribedTeamWorkspaceAsync()));
                   });

            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.Id, e.Id.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.Default))
                .IsValidDynamoItem()
                .When(e => e.Id > 0);
        }
    }

    public class QueryPublishedDealsValidator : BaseRydrValidator<QueryPublishedDeals>
    {
        public QueryPublishedDealsValidator()
        {
            // Can only pass a specific publisherAccount if coming from a Rydr account
            RuleFor(e => e)
                .SetValidator(new IsFromValidRequestPublisherAccountValidator<QueryPublishedDeals>())
                .When(e => e.Id > 0 && !e.IsSystemRequest);

            RuleFor(e => e.Sort)
                .Must((e, s) => e.Latitude.HasValue && e.Longitude.HasValue)
                .When(e => e.Sort == DealSort.Closest)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("When using closest sort value, must include valid lat/lon and distance");

            RuleFor(e => e.PlaceId)
                .GreaterThan(0)
                .When(e => e.PlaceId.HasValue)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.FollowerCount)
                .SetValidator(v => new LongRangeValidator())
                .When(e => e.FollowerCount != null);

            RuleFor(e => e.EngagementRating)
                .SetValidator(v => new DoubleRangeValidator())
                .When(e => e.EngagementRating != null);

            RuleFor(e => e.Value)
                .SetValidator(v => new DoubleRangeValidator())
                .When(e => e.Value != null);

            Include(new GeoQueryValidator());
        }
    }

    public class DealPostedLowValidator : DealPostedValidatorBase<DealPostedLow> { }
    public class DealPostedValidator : DealPostedValidatorBase<DealPosted> { }

    public abstract class DealPostedValidatorBase<T> : BaseRydrValidator<T>
        where T : DealPostedLow
    {
        protected DealPostedValidatorBase()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0);

            // Deal has to exist
            RuleFor(e => e.ToDynItemValidationSource(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistCanBeDeleted, true))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);
        }
    }

    public class UpdateExternalDealValidator : BaseRydrValidator<UpdateExternalDeal>
    {
        public UpdateExternalDealValidator()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0);

            // Deal has to exist
            RuleFor(e => e.ToDynItemValidationSource(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistCanBeDeleted, true))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);
        }
    }

    public class DealStatIncrementValidator : BaseRydrValidator<DealStatIncrement>
    {
        public DealStatIncrementValidator()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0);

            RuleFor(e => e.FromPublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class DealStatIncrementedValidator : BaseRydrValidator<DealStatIncremented>
    {
        public DealStatIncrementedValidator()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0);

            RuleFor(e => e.FromPublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class DealUpdatedValidator : DealStatusUpdatedValidatorBase<DealUpdated> { }
    public class DealUpdatedLowValidator : DealStatusUpdatedValidatorBase<DealUpdatedLow> { }
    public class DealStatusUpdatedValidator : DealStatusUpdatedValidatorBase<DealStatusUpdated> { }

    public abstract class DealStatusUpdatedValidatorBase<T> : BaseRydrValidator<T>
        where T : DealStatusUpdated
    {
        protected DealStatusUpdatedValidatorBase()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0);

            // Deal has to exist
            RuleFor(e => e.ToDynItemValidationSource(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistCanBeDeleted, true))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);
        }
    }

    public class PostDealValidator : BasePostRequestValidator<PostDeal, Deal>
    {
        public PostDealValidator(IPocoDynamo dynamoDb)
            : base(r => new DealValidator(r, dynamoDb))
        {
            RuleFor(e => e)
                .UpdateStateIntentTo(AccessIntent.Write);

            RuleFor(e => e.WorkspaceId)
                .MustAsync(async (e, w, t) =>
                           {
                               var defaultPublisherAccount = await WorkspaceService.DefaultWorkspaceService.TryGetDefaultPublisherAccountAsync(e.WorkspaceId);

                               if (defaultPublisherAccount == null || !defaultPublisherAccount.IsRydrSystemPublisherAccount())
                               { // No default account, or not a rydr system account
                                   return true;
                               }

                               // Cannot create new deals for accounts that are soft-associated to the workspace - soft linked is fine, as the account is still anonymous
                               // and has no proper token account connected anywhere.  Soft-associated however is not, as that means there has been a valid user signup and
                               // link the account with a proper token. Once that happens, we can still manage existing deals, but cannot create new ones on behalf of the
                               // business unless they properly permission our account(s) via FB BizMgr
                               var dealPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                                   .GetPublisherAccountAsync(e.Model.PublisherAccountId);

                               if (dealPublisherAccount.IsRydrSoftLinkedAccount())
                               {
                                   return true;
                               }

                               var softMapExists = await MapItemService.DefaultMapItemService.MapExistsAsync(defaultPublisherAccount.PublisherAccountId,
                                                                                                             dealPublisherAccount.ToRydrSoftLinkedAssociationId());

                               return !softMapExists;
                           })
                .When(e => e.WorkspaceId > 0 && !e.IsSystemRequest)
                .WithErrorCode(ErrorCodes.InvalidState)
                .WithMessage("Cannot create new deals for a soft-associated publishers - continue to manage existing deals, request proper account access to continue creating new deals.");

            Include(new IsFromValidRequestWorkspaceValidator<PostDeal>());
        }
    }

    public class PutDealValidator : BasePutRequestValidator<PutDeal, Deal>
    {
        public PutDealValidator(IPocoDynamo dynamoDb)
            : base(r => new DealValidator(r, dynamoDb))
        {
            RuleFor(e => e)
                .UpdateStateIntentTo(AccessIntent.Write);

            RuleFor(e => e.Reason)
                .MaximumLength(1000);
        }
    }

    public class PutDealInvitesValidator : BaseRydrValidator<PutDealInvites>
    {
        public PutDealInvitesValidator()
        {
            RuleFor(e => e)
                .UpdateStateIntentTo(AccessIntent.Write);

            RuleFor(e => e.DealId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.PublisherAccounts)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleForEach(e => e.PublisherAccounts)
                .SetValidator(e => new PublisherAccountValidator(e, isUpsert: true, allTypesAllowed: true))
                .When(e => !e.PublisherAccounts.IsNullOrEmpty());

            RuleFor(e => e.PublisherAccounts)
                .Must(i => i.Count <= 250)
                .When(e => !e.PublisherAccounts.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.TooLong)
                .WithMessage("No more than 250 invites are currently allowed per deal");

            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);

            RuleFor(e => e.DealId)
                .MustAsync(async (e, di, t) =>
                           {
                               var deal = await DealExtensions.DefaultDealService.GetDealAsync(di);

                               return (deal.WorkspaceId == e.WorkspaceId || deal.DealContextWorkspaceId == e.GetContextWorkspaceId()) &&
                                      deal.DealStatus == DealStatus.Published;
                           })
                .When(e => e.DealId > 0 && !e.IsSystemRequest)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("Additional invites can only be added to Pacts that are InProgress and originate from the proper workspace");
        }
    }

    public class DeleteDealValidator : BaseDeleteRequestValidator<DeleteDeal>
    {
        public DeleteDealValidator()
        {
            RuleFor(e => e)
                .UpdateStateIntentTo(AccessIntent.Write);

            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.Id.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.Default))
                .IsValidDynamoItem();

            RuleFor(e => e.Reason)
                .MaximumLength(1000);

            RuleFor(e => e)
                .MustAsync((d, t) => DealExtensions.DefaultDealService.CanBeDeletedAsync(d.Id))
                .WithErrorCode(ErrorCodes.CannotChangeState)
                .WithName("Deal")
                .OverridePropertyName("Deal")
                .WithMessage(e => (ValidationExtensions._requestStateManager.GetState()?.ValidationMessage).Coalesce("Pact cannot be deleted when outstanding InProgress requests exist, if the deal is already deleted, or if it does not exist"));
        }
    }

    public class DeleteDealInternalValidator : BaseRydrValidator<DeleteDealInternal>
    {
        public DeleteDealInternalValidator()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistCanBeDeleted))
                .IsValidDynamoItem();

            RuleFor(e => e.Reason)
                .MaximumLength(1000);
        }
    }

    public class DealRestrictionValidator : AbstractValidator<DealRestriction>
    {
        public DealRestrictionValidator()
        {
            RuleFor(d => d.Type)
                .NotEqual(DealRestrictionType.Unknown);

            RuleFor(d => d.Value)
                .NotEmpty();
        }
    }

    public class DealStatValidator : AbstractValidator<DealStat>
    {
        public DealStatValidator()
        {
            RuleFor(d => d.Type)
                .NotEqual(DealStatType.Unknown);

            RuleFor(d => d.Value)
                .NotEmpty();
        }
    }

    public class DealStatusValidator : AbstractValidator<Deal>
    {
        public DealStatusValidator()
        {
            RuleSet(ApplyTo.Post,
                    () =>
                    {
                        RuleFor(e => e.Status)
                            .Must(s => s == DealStatus.Draft || s == DealStatus.Published)
                            .WithErrorCode(ErrorCodes.MustBeValid)
                            .WithMessage("Posting a new Pact must include a valid status (Draft or Published only)");
                    });

            RuleFor(e => e.Status)
                .NotEqual(DealStatus.Unknown)
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Status)
                .MustAsync(async (r, s, ctx, t) =>
                           {
                               var xd = await DealExtensions.DefaultDealService.GetDealAsync(r.Id);

                               if (xd.DealStatus == DealStatus.Deleted)
                               { // Existing deal is deleted, cannot modify anything here
                                   ctx.MessageFormatter.AppendArgument("RydrValidationMsg", "Pact is deleted and cannot be modified.");

                                   return false;
                               }

                               if (r.Status != DealStatus.Unknown && (int)xd.DealStatus > (int)r.Status &&
                                   (xd.DealStatus != DealStatus.Paused || r.Status != DealStatus.Published))
                               { // Existing status is "after" requested status, cannot do that (i.e. move backwards) with one exception (from paused back to published)
                                   ctx.MessageFormatter.AppendArgument("RydrValidationMsg", $"Pact cannot transition from existing status of [{xd.DealStatus.ToString()}] to [{r.Status}].");

                                   return false;
                               }

                               if (r.MaxApprovals.HasValue && xd.DealStatus != DealStatus.Draft)
                               { // Can only change max approvals if deal has not yet been published
                                   ctx.MessageFormatter.AppendArgument("RydrValidationMsg", "Max approvals cannot be changed once published.");

                                   return false;
                               }

                               if (r.Status == DealStatus.Paused && (xd.DealStatus != DealStatus.Paused && xd.DealStatus != DealStatus.Published))
                               { // Can only pause a published deal
                                   ctx.MessageFormatter.AppendArgument("RydrValidationMsg", "Can only pause Pacts that are currently published.");

                                   return false;
                               }

                               // If published, or being published
                               if (xd.DealStatus == DealStatus.Published &&
                                   (r.Status == DealStatus.Published || r.Status == DealStatus.Unknown) &&

                                   // Is a private deal or will become one
                                   (xd.IsPrivateDeal || r.Restrictions == null) &&

                                   // Incoming invited accounts must be null (i.e. will not be updated) or include invites
                                   (r.InvitedPublisherAccounts != null && r.InvitedPublisherAccounts.Count <= 0))
                               { // When published or being published, a private deal must include invites - either existing invites that will remain
                                   // and not be updated, or be updated to 1 or more invites
                                   ctx.MessageFormatter.AppendArgument("RydrValidationMsg", "Private Pacts must include invitees.");

                                   return false;
                               }

                               return true;
                           })
                .When(e => e.Id > 0 && (e.Status != DealStatus.Unknown ||
                                        e.MaxApprovals.HasValue ||
                                        e.InvitedPublisherAccounts != null))
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithName("NewDealStatus")
                .OverridePropertyName("NewDealStatus")
                .WithMessage("{RydrValidationMsg}");

            RuleFor(e => e.Status)
                .MustAsync((r, ns, t) => DealExtensions.DefaultDealService.CanBeDeletedAsync(r.Id))
                .When(e => e.Status == DealStatus.Deleted)
                .WithErrorCode(ErrorCodes.CannotChangeState)
                .WithName("Deal")
                .OverridePropertyName("Deal")
                .WithMessage(e => (ValidationExtensions._requestStateManager.GetState()?.ValidationMessage).Coalesce("Pacts cannot be deleted when outstanding InProgress requests exist, if the deal is already deleted, or if it does not exist"));
        }
    }

    public class DealMetaDataValidator : AbstractValidator<DealMetaData>
    {
        public DealMetaDataValidator()
        {
            RuleFor(e => e.Type)
                .NotEqual(DealMetaType.Unknown)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.Value)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);
        }
    }

    public class DealValidator : AbstractValidator<Deal>
    {
        public DealValidator(IRequestBase request, IPocoDynamo dynamoDb)
        {
            RuleSet(ApplyTo.Post | ApplyTo.Put,
                    () =>
                    {
                        // PUT - don't use the PUT verb filter, as we can use this entity validator as attribute validators from other models
                        // when those enclosing models may be doing a POST of that model but including an existing sub-model
                        RuleFor(e => request.ToDynItemValidationSource<DynDeal>(e.Id.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistNotDeleted))
                            .IsValidDynamoItem()
                            .When(e => e.Id > 0);

                        RuleFor(e => e.Id)
                            .MustAsync(async (e, i, t) =>
                                       {
                                           var deal = await DealExtensions.DefaultDealService.GetDealAsync(i, true);

                                           return deal != null && (deal.WorkspaceId == request.WorkspaceId || deal.DealContextWorkspaceId == request.GetContextWorkspaceId());
                                       })
                            .When(e => e.Id > 0 && !request.IsSystemRequest)
                            .WithErrorCode(ErrorCodes.MustBeAuthorized)
                            .WithMessage("Pact can only be modified by the owning workspace context");

                        // POST - Id <= 0
                        RuleForEach(e => e.PublisherMedias)
                            .SetValidator(new PublisherMediaValidator(request))
                            .When(e => !e.PublisherMedias.IsNullOrEmpty());

                        RuleFor(e => request.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.PublisherAccountId, e.PublisherAccountId.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
                            .IsValidDynamoItem()
                            .When(e => e.PublisherAccountId > 0);
                    });

            // Normally you may consider putting these inside a POST ruleset, but since the validator here can be used as a sub-attribute on other request
            // models, the sub-model may need to act like a POST when the outer model was PUT'ed, or act as a PUT when the outer was POST'ed.  So, we leave
            // the POST and PUT specific checks (i.e. Id must be <= 0 on a POST, or Id must be > 0 on a PUT) for the Post/Put specific request validators
            // and use the WHEN filters here

            // POST-like rules only (i.e. Id <= 0)
            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0)
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Restrictions)
                .NotNull() // Using NotNull vs. NullOrEmpty purposely - null indicates a private deal, that's what we're protecting here
                .When(e => e.Id <= 0 && e.InvitedPublisherAccounts.IsNullOrEmpty() && e.Status == DealStatus.Published)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("A private Pact (Restrictions == null) must include InvitedPublisherAccounts when being published.");

            RuleForEach(e => e.Restrictions)
                .SetValidator(new DealRestrictionValidator())
                .When(e => !e.Restrictions.IsNullOrEmpty());

            RuleFor(e => e.Title)
                .NotEmpty()
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.ReceiveType)
                .Must(t => t.All(l => l.Type != PublisherContentType.Unknown))
                .When(e => e.Id <= 0 && !e.ReceiveType.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("ReceiveTypes specified must all include a valid contentType and quantity");

            RuleFor(e => e.Place)
                .NotNull()
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            Include(new DealStatusValidator());

            RuleFor(e => e.PublisherAccountId)
                .Equal(e => request.RequestPublisherAccountId)
                .When(e => e.Id <= 0 && !request.IsSystemRequest && e.PublisherAccountId > 0)
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("Pact can only be created for the account logged in to (PublisherAccountId is invalid)");

            // PUT-like rules only (i.e. Id > 0)
            RuleFor(e => e.PublisherAccountId)
                .Empty()
                .When(e => e.Id > 0)
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("You cannot update the PublisherAccountId once created");

            RuleFor(e => e.DealType)
                .Equal(DealType.Unknown)
                .When(e => e.Id > 0 && !request.IsSystemRequest)
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("DealType cannot be changed after it has been created.");

            // ALL operation rules
            RuleFor(e => e.ExpirationDate.Value)
                .IsValidDateTime("ExpirationDate")
                .When(e => e.ExpirationDate.HasValue);

            RuleForEach(e => e.MetaData)
                .SetValidator(new DealMetaDataValidator())
                .When(e => !e.MetaData.IsNullOrEmpty());

            RuleFor(e => e.Title)
                .MaximumLength(250)
                .WithErrorCode(ErrorCodes.TooLong);

            RuleFor(e => e.Description)
                .MaximumLength(10000)
                .WithErrorCode(ErrorCodes.TooLong);

            RuleFor(e => e.ApprovalNotes)
                .MaximumLength(10000)
                .WithErrorCode(ErrorCodes.TooLong);

            RuleFor(e => e.ReceiveNotes)
                .MaximumLength(100000)
                .WithErrorCode(ErrorCodes.TooLong);

            RuleFor(e => e.Place)
                .SetValidator(new PlaceValidator(request, true))
                .When(e => e.Place != null);

            RuleFor(e => e.ReceivePlace)
                .SetValidator(new PlaceValidator(request))
                .When(e => e.ReceivePlace != null);

            RuleForEach(e => e.ReceiveHashtags)
                .SetValidator(new HashtagValidator(request, dynamoDb, true))
                .When(e => !e.ReceiveHashtags.IsNullOrEmpty());

            RuleForEach(e => e.ReceivePublisherAccounts)
                .SetValidator(new PublisherAccountValidator(request, isUpsert: true))
                .When(e => !e.ReceivePublisherAccounts.IsNullOrEmpty());

            RuleForEach(e => e.InvitedPublisherAccounts)
                .SetValidator(new PublisherAccountValidator(request, isUpsert: true, allTypesAllowed: true))
                .When(e => !e.InvitedPublisherAccounts.IsNullOrEmpty());

            RuleFor(e => e.InvitedPublisherAccounts)
                .Must(i => i.Count <= 250)
                .When(e => !e.InvitedPublisherAccounts.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.TooLong)
                .WithMessage("No more than 250 invites are currently allowed per deal");

            RuleFor(e => e.MaxApprovals)
                .GreaterThan(0)
                .When(e => e.MaxApprovals.HasValue && e.MaxApprovals != 0)
                .WithMessage("MaxApprovals cannot be a negative number");

            RuleFor(e => e.Value)
                .GreaterThanOrEqualTo(0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.DealWorkspaceId)
                .Empty()
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            RuleFor(e => e.PublisherApprovedMediaIds)
                .Must((e, l) => l.Count <= 30 && l.All(i => i > 0))
                .When(e => !e.PublisherApprovedMediaIds.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.LimitReached)
                .WithMessage("You may only include up to 30 approved media IDs per deal and they must be valid values.");

            RuleForEach(e => e.PublisherApprovedMediaIds
                              .Select(i => request.ToDynItemValidationSource<DynPublisherApprovedMedia>(i.ToEdgeId(),
                                                                                                        DynItemType.ApprovedMedia,
                                                                                                        ApplyToBehavior.MustExistNotDeleted,
                                                                                                        a => request.IsSystemRequest ||
                                                                                                             a.PublisherAccountId == e.PublisherAccountId.Gz(request.RequestPublisherAccountId))))
                .IsValidDynamoItem()
                .When(e => !e.PublisherApprovedMediaIds.IsNullOrEmpty());
        }
    }
}
