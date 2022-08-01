using System.Linq;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.QueryDto;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class GetDealRequestValidator : BaseRydrValidator<GetDealRequest>
    {
        public GetDealRequestValidator()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            Include(new IsValidPublisherAccountIdOrRequestPublisherIdValidator<GetDealRequest>());

            RuleFor(e => e.PublisherAccountId)
                .MustAsync(async (r, pid, t) =>
                           {
                               // To get a specific request, have to be the publisher in question, or the deal has to have been created by the current user
                               var myPublisherAccountId = r.RequestPublisherAccountId;

                               if (pid == myPublisherAccountId)
                               {
                                   return true;
                               }

                               // Get the deal and see if the deal was created by this user or workspace
                               var deal = await DealExtensions.DefaultDealService.GetDealAsync(r.DealId, true);

                               return deal != null && (deal.CreatedBy == r.UserId || deal.WorkspaceId == r.WorkspaceId ||
                                                       deal.DealContextWorkspaceId == r.GetContextWorkspaceId());
                           })
                .When(e => e.PublisherAccountId > 0 && e.DealId > 0 && !e.IsSystemRequest)
                .WithErrorCode(ErrorCodes.MustBeAuthorized);

            // Deal for the request must exist and be accessible
            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.Default))
                .IsValidDynamoItem();

            RuleFor(e => e.ToDynItemValidationSource(e.DealId, e.ToPublisherAccountId(), DynItemType.DealRequest, ApplyToBehavior.MustExistCanBeDeleted, false))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);
        }
    }

    public class GetDealRequestReportExternalLinkValidator : BaseRydrValidator<GetDealRequestReportExternalLink>
    {
        public GetDealRequestReportExternalLinkValidator()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Duration)
                .LessThanOrEqualTo(435_000)
                .WithErrorCode(ErrorCodes.MustBeValid);

            Include(new IsValidPublisherAccountIdOrRequestPublisherIdValidator<GetDealRequestReportExternalLink>());

            RuleFor(e => e.PublisherAccountId)
                .MustAsync(async (r, pid, t) =>
                           {
                               // To get a specific request, have to be the publisher in question, or the deal has to have been created by the current user
                               var myPublisherAccountId = r.RequestPublisherAccountId;

                               if (pid == myPublisherAccountId)
                               {
                                   return true;
                               }

                               // Get the deal and see if the deal was created by this user or workspace
                               var deal = await DealExtensions.DefaultDealService.GetDealAsync(r.DealId, true);

                               return deal != null && (deal.CreatedBy == r.UserId || deal.WorkspaceId == r.WorkspaceId ||
                                                       deal.DealContextWorkspaceId == r.GetContextWorkspaceId());
                           })
                .When(e => e.PublisherAccountId > 0 && e.DealId > 0 && !e.IsSystemRequest)
                .WithErrorCode(ErrorCodes.MustBeAuthorized);

            // Deal for the request must exist and be accessible
            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.Default))
                .IsValidDynamoItem();

            RuleFor(e => e.ToDynItemValidationSource(e.DealId, e.ToPublisherAccountId(), DynItemType.DealRequest, ApplyToBehavior.MustExistCanBeDeleted, false))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);
        }
    }

    public class GetDealRequestReportExternalValidator : AbstractValidator<GetDealRequestReportExternal>
    {
        public GetDealRequestReportExternalValidator()
        {
            RuleFor(e => e.DealRequestReportId)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.InvalidState)
                .WithName("State")
                .OverridePropertyName("State")
                .WithMessage("State invalid - code [xdrli]");

            RuleFor(e => e.DealRequestReportId)
                .MustAsync(async (ri, t) =>
                           {
                               var dealMap = await MapItemService.DefaultMapItemService.TryGetMapByHashedEdgeAsync(DynItemType.DealRequest, ri);

                               if (dealMap?.ReferenceNumber == null ||
                                   dealMap.ReferenceNumber.Value <= 0 ||
                                   dealMap.MappedItemEdgeId.IsNullOrEmpty() ||
                                   dealMap.IsExpired())
                               {
                                   return false;
                               }

                               // Deal and deal request must be valid
                               var deal = await DealExtensions.DefaultDealService
                                                              .GetDealAsync(dealMap.ReferenceNumber.Value, true);

                               var dealRequest = await DealExtensions.DefaultDealRequestService
                                                                     .GetDealRequestAsync(dealMap.ReferenceNumber.Value, dealMap.MappedItemEdgeId.ToLong());

                               return deal != null && !deal.IsDeleted() &&
                                      dealRequest != null && !dealRequest.IsDeleted();
                           })
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithName("State")
                .OverridePropertyName("State")
                .WithMessage("State invalid - code [xdrlidnx]");
        }
    }

    public class QueryDealRequestsValidator : BaseRydrValidator<QueryDealRequests>
    {
        public QueryDealRequestsValidator()
        {
            // Admins can query everything in the system
            // Workspace owners can query everything in the workspace
            // Workspace users can query the workspace for publishers assigned to them
            // Everyone else queries over a specific worksapce/publisher context

            RuleFor(e => e.Status)
                .Must(a => a.All(drs => drs != DealRequestStatus.Unknown))
                .When(e => e.Status != null && e.Status.Length > 0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.Id)
                .GreaterThan(0)
                .When(e => e.Id.HasValue)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.EdgeId.ToLong(0))
                .GreaterThan(0)
                .When(e => e.EdgeId.HasValue())
                .WithErrorCode(ErrorCodes.MustBeValid);

            // Id == deal.id
            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.Id.Value.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.Default))
                .IsValidDynamoItem()
                .When(e => e.Id.HasValue && e.Id.Value > 0);

            // EdgeId == deal request publisherAccountId
            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.EdgeId.ToLong(0), e.EdgeId.ToLong(0).ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.Default))
                .IsValidDynamoItem()
                .When(e => e.EdgeId.HasValue());

            // OwnerId == deal creator publisherAccountId
            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.OwnerId.Value, e.OwnerId.Value.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.Default))
                .IsValidDynamoItem()
                .When(e => e.OwnerId.HasValue);

            Unless(e => e.IsSystemRequest,
                   () =>
                   {
                       // Always have to have a workspace unless an admin
                       RuleFor(e => e)
                           .SetValidator(new IsFromValidRequestWorkspaceValidator<QueryDealRequests>());

                       // Admins and teams with valid subscriptions can query across the workspace, otherwise there has to be
                       // a valid request publisher account specified
                       RuleFor(e => e)
                           .SetValidator(new IsFromValidRequestPublisherAccountValidator<QueryDealRequests>())
                           .UnlessAsync((e, t) => e.IsSubscribedTeamWorkspaceAsync());
                   });
        }
    }

    public class QueryRequestedDealsValidator : BaseRydrValidator<QueryRequestedDeals>
    {
        public QueryRequestedDealsValidator()
        {
            RuleFor(e => e.Status)
                .Must(a => a.All(drs => drs != DealRequestStatus.Unknown))
                .When(e => e.Status != null && e.Status.Length > 0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.Id)
                .GreaterThan(0)
                .When(e => e.Id.HasValue)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.Id.Value.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.Default))
                .IsValidDynamoItem()
                .When(e => e.Id.HasValue && e.Id.Value > 0);

            RuleFor(e => e.RequestedOnBefore.Value)
                .IsValidDateTime("RequestedOnBefore")
                .When(e => e.RequestedOnBefore.HasValue);

            // EdgeId == publisherAccountId
            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.EdgeId.ToLong(0), e.EdgeId.ToLong(0).ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.Default))
                .IsValidDynamoItem()
                .When(e => e.EdgeId.HasValue());

            // If no valid publisherAccountId include, must be from a valid publisher in the request
            RuleFor(e => e)
                .SetValidator(new IsFromValidRequestPublisherAccountValidator<QueryRequestedDeals>())
                .When(e => !e.EdgeId.HasValue());
        }
    }

    public class DealRequestedLowValidator : DealRequestedValidatorBase<DealRequestedLow> { }
    public class DealRequestedValidator : DealRequestedValidatorBase<DealRequested> { }

    public abstract class DealRequestedValidatorBase<T> : BaseRydrValidator<T>
        where T : DealRequestedLow
    {
        protected DealRequestedValidatorBase()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0);

            RuleFor(e => e.RequestedByPublisherAccountId)
                .GreaterThan(0);

            // Deal has to exist
            RuleFor(e => e.ToDynItemValidationSource(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistCanBeDeleted, true))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);

            // Publisher has to exist
            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.RequestedByPublisherAccountId, e.RequestedByPublisherAccountId, DynItemType.PublisherAccount, ApplyToBehavior.MustExistCanBeDeleted, true))
                .IsValidDynamoItem()
                .When(e => e.RequestedByPublisherAccountId > 0);

            // Request must exist
            RuleFor(e => e.ToDynItemValidationSource(e.DealId, e.RequestedByPublisherAccountId, DynItemType.DealRequest, ApplyToBehavior.MustExistCanBeDeleted, true))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0 && e.RequestedByPublisherAccountId > 0);
        }
    }

    public class DealRequestUpdatedValidator : DealRequestStatusUpdatedValidatorBase<DealRequestUpdated> { }
    public class DealRequestStatusUpdatedValidator : DealRequestStatusUpdatedValidatorBase<DealRequestStatusUpdated> { }

    public abstract class DealRequestStatusUpdatedValidatorBase<T> : BaseRydrValidator<T>
        where T : DealRequestStatusUpdated
    {
        protected DealRequestStatusUpdatedValidatorBase()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0);

            RuleFor(e => e.UpdatedByPublisherAccountId)
                .GreaterThan(0);

            // Deal has to exist
            RuleFor(e => e.ToDynItemValidationSource(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistCanBeDeleted, true))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);

            // Publishers have to exist
            Include(new IsValidPublisherAccountIdValidator<DealRequestStatusUpdated>());

            RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.UpdatedByPublisherAccountId, e.UpdatedByPublisherAccountId, DynItemType.PublisherAccount, ApplyToBehavior.MustExistCanBeDeleted, true))
                .IsValidDynamoItem()
                .When(e => e.UpdatedByPublisherAccountId > 0);
        }
    }

    public class DealRequestCompletionMediaSubmittedValidator : BaseRydrValidator<DealRequestCompletionMediaSubmitted>
    {
        public DealRequestCompletionMediaSubmittedValidator()
        {
            Include(new IsValidPublisherAccountIdValidator<DealRequestCompletionMediaSubmitted>());

            RuleFor(e => e.DealId)
                .GreaterThan(0);

            RuleFor(e => e.CompletionMediaPublisherMediaIds)
                .NotEmpty()
                .When(e => e.CompletionRydrMediaIds.IsNullOrEmpty());

            RuleFor(e => e.CompletionRydrMediaIds)
                .NotEmpty()
                .When(e => e.CompletionMediaPublisherMediaIds.IsNullOrEmpty());

            // Deal has to exist
            RuleFor(e => e.ToDynItemValidationSource(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistNotDeleted, true))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);

            // Deal request must exist
            RuleFor(e => e.ToDynItemValidationSource(e.DealId, e.PublisherAccountId, DynItemType.DealRequest, ApplyToBehavior.MustExistNotDeleted, true))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0 && e.PublisherAccountId > 0);
        }
    }

    public class PostDealRequestValidator : BaseRydrValidator<PostDealRequest>
    {
        public PostDealRequestValidator()
        {
            Include(new IsValidPublisherAccountIdOrRequestPublisherIdValidator<PostDealRequest>());

            RuleFor(e => e.DealId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            // Deal has to exist and be accessible
            RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem();

            // Deal request must not already exist
            RuleFor(e => e.ToDynItemValidationSource(e.DealId, e.ToPublisherAccountId(), DynItemType.DealRequest, ApplyToBehavior.MustNotExist, false))
                .IsValidDynamoItem();

            // Must be able to be requested by the person in question or at all (exprired, over limit, etc.)
            RuleFor(e => e.DealId)
                .MustAsync((e, did, t) => DealExtensions.DefaultDealRequestService.CanBeRequestedAsync(did, e.ToPublisherAccountId()))
                .WithErrorCode(ErrorCodes.LimitReached)
                .WithName("DealLimit")
                .WithMessage(e => (ValidationExtensions._requestStateManager.GetState()?.ValidationMessage).Coalesce("RYDR requested has reached limit of requests, has already been requested, is past expiration date, is not currently published, or is otherwise not able to be requested currently"));
        }
    }

    public class PutDealRequestCompletionMediaValidator : BaseRydrValidator<PutDealRequestCompletionMedia>
    {
        public PutDealRequestCompletionMediaValidator()
        {
            Include(new IsFromValidNonTokenRequestPublisherAccountValidator());

            RuleFor(e => e.DealId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.CompletionMediaIds)
                .NotEmpty()
                .When(e => e.CompletionRydrMediaIds.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.CompletionRydrMediaIds)
                .NotEmpty()
                .When(e => e.CompletionMediaIds.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleForEach(e => e.CompletionRydrMediaIds.Select(fid => e.ToDynItemValidationSource<DynPublisherMedia>(e.RequestPublisherAccountId, fid.ToEdgeId(),
                                                                                                                   DynItemType.PublisherMedia, null, ApplyToBehavior.MustExistNotDeleted, false, null)))
                .IsValidDynamoItem()
                .Unless(e => e.CompletionRydrMediaIds.IsNullOrEmpty())
                .WithName("CompletionRydrMediaIds")
                .WithMessage("Values must be valid publisher media records")
                .WithErrorCode(ErrorCodes.MustBeValid)
                .OverridePropertyName("CompletionRydrMediaIds");

            // Deal request from this account to the specified deal must exist
            RuleFor(e => e.ToDynItemValidationSource(e.DealId, e.RequestPublisherAccountId, DynItemType.DealRequest, ApplyToBehavior.MustExistNotDeleted, false))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);

            // Existing deal request must be completed
            RuleFor(e => e.DealId)
                .MustAsync(async (r, did, t) =>
                           {
                               var dealRequest = await DealExtensions.DefaultDealRequestService
                                                                     .GetDealRequestAsync(did, r.RequestPublisherAccountId);

                               return dealRequest != null && !dealRequest.IsDeleted() && dealRequest.RequestStatus == DealRequestStatus.Completed;
                           })
                .WithErrorCode(ErrorCodes.InvalidState)
                .WithMessage("Only completed requests can have completion media attached");
        }
    }

    public class PutDealRequestValidator : BaseRydrValidator<PutDealRequest>
    {
        public PutDealRequestValidator()
        {
            // PUTs here must currently come from a valid publisher account, as we track/logic to the biz/influencer based on who is making the change
            // In order to change this, have to include validation and params to pass the account that is doing the updating...i.e. if we want an admin to be able to do this
            Include(new IsFromValidNonTokenRequestPublisherAccountValidator());
            Include(new PutDealRequestValidatorShared());

            RuleFor(e => e.CompletionMediaIds)
                .Empty()
                .Unless(r => r.Model.Status == DealRequestStatus.Completed)
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("Completion media can only be included on an update when completing the Pact");

            RuleFor(e => e.CompletionRydrMediaIds)
                .Empty()
                .Unless(r => r.Model.Status == DealRequestStatus.Completed)
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("Completion media can only be included on an update when completing the Pact");

            RuleForEach(e => e.CompletionRydrMediaIds.Select(fid => e.ToDynItemValidationSource<DynPublisherMedia>(e.RequestPublisherAccountId, fid.ToEdgeId(),
                                                                                                                   DynItemType.PublisherMedia, null, ApplyToBehavior.MustExistNotDeleted, false, null)))
                .IsValidDynamoItem()
                .Unless(e => e.CompletionRydrMediaIds.IsNullOrEmpty())
                .WithName("CompletionRydrMediaIds")
                .WithMessage("Values must be valid publisher media records")
                .WithErrorCode(ErrorCodes.MustBeValid)
                .OverridePropertyName("CompletionRydrMediaIds");
        }
    }

    public class UpdateDealRequestValidator : BaseRydrValidator<UpdateDealRequest>
    {
        public UpdateDealRequestValidator()
        {
            Include(e => new PutDealRequestValidatorShared(e.UpdatedByPublisherAccountId, e.ForceAllowStatusChange));

            RuleFor(e => e.UpdatedByPublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class CheckDealRequestAllowancesValidator : BaseRydrValidator<CheckDealRequestAllowances>
    {
        public CheckDealRequestAllowancesValidator()
        {
            RuleFor(e => e.DealId)
                .GreaterThan(0);

            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class PutDealRequestValidatorShared : AbstractValidator<UpdateDealRequestBase>
    {
        public PutDealRequestValidatorShared(long updatedByPublisherId = 0, bool forceAllowStatusChange = false)
        {
            RuleFor(e => e.Model)
                .NotNull()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Model)
                .SetValidator(e => new DealRequestValidator(e, true, updatedByPublisherId, forceAllowStatusChange))
                .When(e => e.Model != null);

            RuleFor(e => e.Reason)
                .MaximumLength(1000);

            RuleFor(e => e.DealId)
                .Equal(e => e.Model == null
                                ? int.MinValue
                                : e.Model.DealId)
                .WithMessage("DealId in the put-url must match the DealId specified in the PUT model");
        }
    }

    public class DeleteDealRequestValidator : BaseRydrValidator<DeleteDealRequest>
    {
        public DeleteDealRequestValidator()
        {
            Include(new IsValidPublisherAccountIdOrRequestPublisherIdValidator<DeleteDealRequest>());

            RuleFor(e => e.DealId)
                .GreaterThanOrEqualTo(0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.Reason)
                .MaximumLength(1000);

            // DealRequest has to exist and be non-deleted
            RuleFor(e => e.ToDynItemValidationSource(e.DealId, e.ToPublisherAccountId(), DynItemType.DealRequest, ApplyToBehavior.MustExistNotDeleted, false))
                .IsValidDynamoItem();

            RuleFor(e => e)
                .MustAsync((d, t) => DealExtensions.DefaultDealRequestService.CanBeCancelledAsync(d.DealId, d.ToPublisherAccountId()))
                .WithErrorCode(ErrorCodes.CannotChangeState)
                .WithName("DealRequest")
                .OverridePropertyName("DealRequest")
                .WithMessage(e => (ValidationExtensions._requestStateManager.GetState()?.ValidationMessage).Coalesce("Request cannot be deleted/cancelled."));
        }
    }

    public class DeleteDealRequestInternalValidator : BaseRydrValidator<DeleteDealRequestInternal>
    {
        public DeleteDealRequestInternalValidator()
        {
            Include(new IsValidPublisherAccountIdValidator<DeleteDealRequestInternal>());

            RuleFor(e => e.DealId)
                .GreaterThanOrEqualTo(0)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.Reason)
                .MaximumLength(1000);

            // DealRequest has to exist and be non-deleted
            RuleFor(e => e.ToDynItemValidationSource(e.DealId, e.PublisherAccountId, DynItemType.DealRequest, ApplyToBehavior.MustExistNotDeleted, false))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0 && e.PublisherAccountId > 0);
        }
    }

    public class DealRequestPostOnlyValidator : AbstractValidator<DealRequest>
    {
        public DealRequestPostOnlyValidator(IRequestBase request)
        {
            RuleFor(e => e.Status)
                .Equal(DealRequestStatus.Unknown)
                .Unless(d => Request.IsRydrRequest() && (d.Status == DealRequestStatus.InProgress ||
                                                         d.Status == DealRequestStatus.Cancelled)) // In-process msg q's are used to approve auto-approved deal requests
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            // Influencers request deals, so they cannot specify anything here
            RuleFor(e => e.HoursAllowedInProgress)
                .LessThanOrEqualTo(0)
                .When(e => !Request.IsRydrRequest())
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            RuleFor(e => e.HoursAllowedRedeemed)
                .LessThanOrEqualTo(0)
                .When(e => !Request.IsRydrRequest())
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            // Deal request must not already exist
            RuleFor(e => request.ToDynItemValidationSource(e.DealId, e.PublisherAccountId.Gz(request.RequestPublisherAccountId),
                                                           DynItemType.DealRequest, ApplyToBehavior.MustNotExist, false))
                .IsValidDynamoItem()
                .Unless(d => Request.IsRydrRequest() && (d.Status == DealRequestStatus.InProgress ||
                                                         d.Status == DealRequestStatus.Cancelled)); // In-process msg q's are used to approve auto-approved deal requests

            // Must be able to be requested by the person in question or at all (exprired, over limit, etc.)
            RuleFor(e => e.DealId)
                .MustAsync((e, did, t) => DealExtensions.DefaultDealRequestService.CanBeRequestedAsync(e.DealId, e.PublisherAccountId.Gz(request.RequestPublisherAccountId)))
                .Unless(d => Request.IsRydrRequest() && (d.Status == DealRequestStatus.InProgress ||
                                                         d.Status == DealRequestStatus.Cancelled)) // In-process msg q's are used to approve auto-approved deal requests
                .WithErrorCode(ErrorCodes.LimitReached)
                 .WithName("DealLimit")
                 .WithMessage(e => (ValidationExtensions._requestStateManager.GetState()?.ValidationMessage).Coalesce("Pact requested has reached limit of requests, past expiration date, is not currently published, or is otherwise not able to be requested currently"));
        }
    }

    public class DealRequestPutOnlyValidator : AbstractValidator<DealRequest>
    {
        public DealRequestPutOnlyValidator(IRequestBase request, long updatedByPublisherId = 0, bool forceAllowStatusChange = false)
        {
            // Deal request must already exist
            RuleFor(e => request.ToDynItemValidationSource(e.DealId, e.PublisherAccountId.Gz(request.RequestPublisherAccountId),
                                                           DynItemType.DealRequest, ApplyToBehavior.MustExistNotDeleted, false))
                .IsValidDynamoItem();

            RuleFor(e => e.Status)
                .NotEqual(DealRequestStatus.Invited)
                .NotEqual(DealRequestStatus.Requested)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.Status)
                .MustAsync(async (r, newStatus, ctx, t) =>
                           {
                               var myPublisherId = updatedByPublisherId.Gz(request.RequestPublisherAccountId);

                               var requestPublisherId = r.PublisherAccountId > 0
                                                            ? r.PublisherAccountId
                                                            : myPublisherId;

                               var existingDealRequest = await ValidationExtensions._dynamoDb.GetItemAsync<DynDealRequest>(r.DealId, requestPublisherId.ToEdgeId(), true);

                               if (existingDealRequest == null || existingDealRequest.IsDeleted() ||
                                   existingDealRequest.RequestStatus == DealRequestStatus.Completed ||
                                   existingDealRequest.RequestStatus == newStatus ||
                                   (existingDealRequest.RequestStatus == DealRequestStatus.Cancelled && (!Request.IsRydrRequest() || !forceAllowStatusChange))) // We can force un-cancellation of a request as an admin over an admin in-process gateway
                               { // Cannot update if the existing is deleted or in a final state, or if the specified
                                   // status to update to is the same as the existing status
                                   ctx.MessageFormatter.AppendArgument("RydrValidationMsg", $"Pact status is currently [{existingDealRequest?.RequestStatus.ToString() ?? "NotFound"}] and cannot be changed to [{newStatus.ToString()}].");

                                   return false;
                               }

                               // Update must come from the influencer (which is always the request's publisherId)
                               // OR the workspace context the deal originated from (if the biz is doing the update)
                               if (myPublisherId != requestPublisherId &&
                                   !request.IsSystemRequest &&
                                   !forceAllowStatusChange &&
                                   existingDealRequest.DealWorkspaceId != request.WorkspaceId &&
                                   existingDealRequest.DealContextWorkspaceId != request.GetContextWorkspaceId())
                               {
                                   ctx.MessageFormatter.AppendArgument("RydrValidationMsg", "You are not authorized to modify the status of this Pact.");

                                   return false;
                               }

                               var dynDeal = await DealExtensions.DefaultDealService.GetDealAsync(r.DealId);

                               // Only deal owners can change/specify delinquent days
                               if (myPublisherId != dynDeal.PublisherAccountId && (r.HoursAllowedInProgress > 0 || r.HoursAllowedRedeemed > 0) && !request.IsSystemRequest && !Request.IsRydrRequest())
                               {
                                   ctx.MessageFormatter.AppendArgument("RydrValidationMsg", "You are not authorized to modify the delinquency/inprogress thresholds of this Pact.");

                                   return false;
                               }

                               // Deal can be cancelled at any time by any party for any reason, so if moving to that, nothing to do
                               // Otherwise:
                               //     - For an existing status of invited, the influencer must make the change
                               //     - Either party can make transitions to Completed, Redeemed, or Delinquent
                               //     - For all others, the deal owner must make the change
                               if (newStatus != DealRequestStatus.Cancelled)
                               {
                                   if ((existingDealRequest.RequestStatus == DealRequestStatus.Invited && myPublisherId == dynDeal.PublisherAccountId) ||
                                       (existingDealRequest.RequestStatus != DealRequestStatus.Invited &&
                                        newStatus != DealRequestStatus.Completed && newStatus != DealRequestStatus.Redeemed && newStatus != DealRequestStatus.Delinquent &&
                                        myPublisherId != dynDeal.PublisherAccountId))
                                   {
                                       ctx.MessageFormatter.AppendArgument("RydrValidationMsg", "You are not authorized to modify the status of this Pact.");

                                       return false;
                                   }
                               }

                               var dealRequestService = DealExtensions.DefaultDealRequestService;

                               var result = newStatus switch
                               {
                                   DealRequestStatus.InProgress when !(await dealRequestService.CanBeApprovedAsync(existingDealRequest, forceAllowStatusChange)) => false,
                                   DealRequestStatus.Completed when !(await dealRequestService.CanBeCompletedAsync(existingDealRequest)) => false,
                                   DealRequestStatus.Denied when !(await dealRequestService.CanBeDeniedAsync(existingDealRequest)) => false,
                                   DealRequestStatus.Cancelled when !(await dealRequestService.CanBeCancelledAsync(existingDealRequest)) => false,
                                   DealRequestStatus.Redeemed when !(await dealRequestService.CanBeRedeemedAsync(existingDealRequest)) => false,
                                   DealRequestStatus.Delinquent when !(await dealRequestService.CanBeDelinquentAsync(existingDealRequest, ignoreTimeConstraint: true)) => false,
                                   _ => true
                               };

                               if (!result)
                               {
                                   ctx.MessageFormatter.AppendArgument("RydrValidationMsg",
                                                                       (ValidationExtensions._requestStateManager.GetState()?.ValidationMessage).Coalesce("Request cannot be changed to the status requested - it is either in an unmodifiable status (cancelled, expired, etc.), has reached a request limit, is in a state that must be modified by someone else, or is otherwise unable to be honored."));
                               }

                               return result;
                           })
                .When(e => e.Status != DealRequestStatus.Unknown)
                .WithErrorCode(ErrorCodes.CannotChangeState)
                .WithName("DealRequestStatus")
                .OverridePropertyName("DealRequestStatus")
                .WithMessage("{RydrValidationMsg}");
        }
    }

    public class DealRequestValidator : AbstractValidator<DealRequest>
    {
        public DealRequestValidator(IRequestBase request, bool forceHandleAsPut = false, long updatedByPublisherId = 0, bool forceAllowStatusChange = false)
        {
            RuleFor(e => e)
                .SetValidator(new DealRequestPostOnlyValidator(request))
                .When(e => !forceHandleAsPut &&
                           Request.RequestAttributes.HasFlag(RequestAttributes.HttpPost));

            RuleFor(e => e)
                .SetValidator(new DealRequestPutOnlyValidator(request, updatedByPublisherId, forceAllowStatusChange))
                .When(e => forceHandleAsPut ||
                           Request.RequestAttributes.HasFlag(RequestAttributes.HttpPut));

            RuleFor(e => request.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.PublisherAccountId.Gz(request.RequestPublisherAccountId),
                                                                                     e.PublisherAccountId.Gz(request.RequestPublisherAccountId).ToStringInvariant(),
                                                                                     DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem();

            RuleFor(e => e.PublisherAccountId)
                .MustAsync(async (r, pid, t) =>
                           {
                               // To update a specific request, have to be the publisher in question, or the deal has to have been created by the current user,
                               // or be an admin
                               if (Request.IsRydrRequest())
                               {
                                   return true;
                               }

                               if (pid == request.RequestPublisherAccountId)
                               {
                                   return true;
                               }

                               // Get the deal and see if the deal was created by this user
                               var deal = await DealExtensions.DefaultDealService.GetDealAsync(r.DealId, true);

                               return deal != null && (deal.CreatedBy == request.UserId || deal.WorkspaceId == request.WorkspaceId ||
                                                       deal.DealContextWorkspaceId == request.GetContextWorkspaceId());
                           })
                .When(e => e.PublisherAccountId > 0 && e.DealId > 0)
                .WithErrorCode(ErrorCodes.MustBeAuthorized);

            // Deal has to exist and be accessible
            RuleFor(e => request.ToDynItemValidationSource<DynDeal>(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistNotDeleted))
                .IsValidDynamoItem()
                .When(e => e.DealId > 0);

            RuleFor(e => e.DealId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Title)
                .Empty()
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            RuleFor(e => e.PublisherAccount)
                .Null()
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            RuleFor(e => e.RequestedOn)
                .Null()
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            RuleFor(e => e.CompletionMedia)
                .Null()
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            RuleFor(e => e.StatusChanges)
                .Null()
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            RuleFor(e => e.LastMessage)
                .Null()
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            RuleFor(e => e.ReceiveType)
                .Null()
                .WithErrorCode(ErrorCodes.CannotBeSpecified);
        }
    }
}
