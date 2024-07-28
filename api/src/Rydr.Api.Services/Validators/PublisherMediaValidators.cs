using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class GetPublisherMediaValidator : BaseGetRequestValidator<GetPublisherMedia>
{
    public GetPublisherMediaValidator()
    {
        RuleFor(e => e.ToDynItemValidationSource(e.Id.ToEdgeId(), DynItemType.PublisherMedia, ApplyToBehavior.Default))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

public class GetPublisherMediaAnalysisValidator : BaseGetRequestValidator<GetPublisherMediaAnalysis>
{
    public GetPublisherMediaAnalysisValidator()
    { // Correctly validating that the media exists, and not the analysis object directly
        RuleFor(e => e.ToDynItemValidationSource<DynPublisherMedia>(e.Id.ToEdgeId(), DynItemType.PublisherMedia, ApplyToBehavior.Default))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

public class GetRecentMediaValidator : BaseRydrValidator<GetRecentMedia>
{
    public GetRecentMediaValidator()
    {
        Include(new IsValidPublisherIdentifierValidator<GetRecentMedia>());

        RuleFor(e => e)
            .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
            .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

        RuleFor(e => e.ToDynItemValidationSourceByRef(e.PublisherAppId, e.PublisherAppId, DynItemType.PublisherApp))
            .IsValidDynamoItem()
            .When(e => e.PublisherAppId > 0);

        RuleFor(e => e.Limit)
            .InclusiveBetween(1, 100)
            .When(e => e.Limit != 0)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class GetRecentPublisherMediaValidator : BaseRydrValidator<GetRecentPublisherMedia>
{
    public GetRecentPublisherMediaValidator()
    {
        Include(new IsValidPublisherIdentifierValidator<GetRecentPublisherMedia>());

        RuleFor(e => e)
            .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
            .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

        RuleFor(e => e.ToDynItemValidationSourceByRef(e.PublisherAppId, e.PublisherAppId, DynItemType.PublisherApp))
            .IsValidDynamoItem()
            .When(e => e.PublisherAppId > 0);

        RuleFor(e => e.Limit)
            .InclusiveBetween(1, 100)
            .When(e => e.Limit != 0)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class GetRecentSyncedMediaValidator : BaseRydrValidator<GetRecentSyncedMedia>
{
    public GetRecentSyncedMediaValidator()
    {
        // Media is basically public (as it is on ig), but only liveMedia is so...(i.e. stories that are live on ig)
        // Can only skip non-me access checks if asking for live media only
        Include(e => new IsValidPublisherIdentifierValidator<GetRecentSyncedMedia>(e.LiveMediaOnly));

        RuleFor(e => e)
            .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
            .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

        RuleFor(e => e.CreatedAfter.Value)
            .IsValidDateTime("CreatedAfter")
            .When(e => e.CreatedAfter.HasValue);

        RuleFor(e => e.ContentTypes)
            .Must(ct => ct.All(t => t != PublisherContentType.Unknown))
            .When(e => !e.ContentTypes.IsNullOrEmpty())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("ContentTypes specified must all be valid PublisherContentType values");

        RuleFor(e => e.Limit)
            .InclusiveBetween(1, 200)
            .When(e => e.Limit != 0)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class GetPublisherApprovedMediaValidator : BaseGetRequestValidator<GetPublisherApprovedMedia>
{
    public GetPublisherApprovedMediaValidator()
    {
        Include(new IsFromValidSubscribedRequestWorkspace<GetPublisherApprovedMedia>());

        RuleFor(e => e.ToDynItemValidationSource<DynPublisherApprovedMedia>(e.Id.ToEdgeId(), DynItemType.ApprovedMedia, ApplyToBehavior.MustExistCanBeDeleted))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

public class GetPublisherApprovedMediasValidator : BaseRydrValidator<GetPublisherApprovedMedias>
{
    public GetPublisherApprovedMediasValidator()
    {
        RuleFor(e => e)
            .SetValidator(new IsFromValidSubscribedRequestWorkspace<GetPublisherApprovedMedias>())
            .Unless(e => e.IsSystemRequest);

        RuleFor(e => e)
            .SetValidator(new IsValidPublisherIdentifierValidator<GetPublisherApprovedMedias>())
            .When(e => e.DealId <= 0 &&
                       (e.PublisherIdentifier.HasValue() || e.RequestPublisherAccountId <= 0));

        RuleFor(e => e.ToDynItemValidationSource(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistCanBeDeleted))
            .IsValidDynamoItem()
            .When(e => e.DealId > 0);
    }
}

public class PostPublisherMediaValidator : BasePostRequestValidator<PostPublisherMedia, PublisherMedia>
{
    public PostPublisherMediaValidator() : base(r => new PublisherMediaValidator(r)) { }
}

public class PostPublisherApprovedMediaValidator : BasePostRequestValidator<PostPublisherApprovedMedia, PublisherApprovedMedia>
{
    public PostPublisherApprovedMediaValidator() : base(r => new PublisherApprovedMediaValidator(r)) { }
}

public class PutPublisherApprovedMediaValidator : BasePutRequestValidator<PutPublisherApprovedMedia, PublisherApprovedMedia>
{
    public PutPublisherApprovedMediaValidator() : base(r => new PublisherApprovedMediaValidator(r)) { }
}

public class PutPublisherMediaAnalysisPriorityValidator : BaseRydrValidator<PutPublisherMediaAnalysisPriority>
{
    public PutPublisherMediaAnalysisPriorityValidator()
    {
        RuleFor(e => e.Id)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.ToDynItemValidationSource(e.Id.ToEdgeId(), DynItemType.PublisherMedia, ApplyToBehavior.Default))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

public class PostPublisherMediaStatsReceivedValidator : BaseRydrValidator<PostPublisherMediaStatsReceived>
{
    public PostPublisherMediaStatsReceivedValidator()
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.PublisherMediaId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.Stats)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}

public class PostTriggerSyncRecentPublisherAccountMediaValidator : BaseRydrValidator<PostTriggerSyncRecentPublisherAccountMedia>
{
    public PostTriggerSyncRecentPublisherAccountMediaValidator()
    {
        Include(new IsValidPublisherAccountIdValidator<PostTriggerSyncRecentPublisherAccountMedia>());

        RuleFor(e => e.ToDynItemValidationSource(e.WithWorkspaceId, DynItemType.Workspace, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.WithWorkspaceId > 0);

        RuleFor(e => e.PublisherAccountId)
            .MustAsync((e, i, t) => AuthorizationExtensions.DefaultAuthorizeService.IsAuthorizedAsync(e.WithWorkspaceId, e.PublisherAccountId))
            .When(e => e.WithWorkspaceId > 0 && e.PublisherAccountId > 0)
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("Publisher account and workspace included are invalid or are not authorized for use");

        RuleFor(e => e.ToDynItemValidationSourceByRef(e.PublisherAppId, e.PublisherAppId, DynItemType.PublisherApp, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.PublisherAppId > 0);
    }
}

public class PostSyncRecentPublisherAccountMediaValidator : BaseRydrValidator<PostSyncRecentPublisherAccountMedia>
{
    public PostSyncRecentPublisherAccountMediaValidator()
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        // NOTE: Do not put record validations here - this is only called from an MQ, and if the account/app is now invalid, we check that in the POST
        // handler and add a notification, disable the job, etc.
    }
}

public class PublisherAccountSyncEnableValidator : BaseRydrValidator<PublisherAccountSyncEnable>
{
    public PublisherAccountSyncEnableValidator()
    {
        Include(new IsValidPublisherAccountIdValidator<PublisherAccountSyncEnable>());
    }
}

public class PostPublisherMediaUpsertValidator : BaseUpsertRequestValidator<PostPublisherMediaUpsert, PublisherMedia>
{
    public PostPublisherMediaUpsertValidator()
        : base((r, u) => new PublisherMediaValidator(r, u))
    {
        RuleFor(e => e.Model.PublisherAccountId)
            .GreaterThan(0)
            .When(e => e.RequestPublisherAccountId <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e)
            .SetValidator(new IsFromValidRequestPublisherAccountValidator<PostPublisherMediaUpsert>())
            .When(e => e.Model.PublisherAccountId <= 0);
    }
}

public class PostPublisherMediaReceivedValidator : BaseRydrValidator<PostPublisherMediaReceived>
{
    public PostPublisherMediaReceivedValidator()
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.PublisherMediaId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}

public class PublisherApprovedMediaValidator : AbstractValidator<PublisherApprovedMedia>
{
    public PublisherApprovedMediaValidator(IRequestBase request)
    {
        RuleSet(ApplyTo.Post | ApplyTo.Put,
                () =>
                {
                    // PUT like operation
                    RuleFor(e => request.ToDynItemValidationSource<DynPublisherApprovedMedia>(e.PublisherAccountId.Gz(request.RequestPublisherAccountId),
                                                                                              e.Id.ToEdgeId(), DynItemType.ApprovedMedia,
                                                                                              null, ApplyToBehavior.MustExistNotDeleted, false, null))
                        .IsValidDynamoItem()
                        .When(e => e.Id > 0);

                    // ALL operations
                    RuleFor(e => request.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.PublisherAccountId, e.PublisherAccountId.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
                        .IsValidDynamoItem()
                        .When(e => e.PublisherAccountId > 0);

                    RuleFor(e => request.ToDynItemValidationSource(e.MediaFileId, DynItemType.File, ApplyToBehavior.MustExistNotDeleted))
                        .IsValidDynamoItem()
                        .When(e => e.MediaFileId > 0);
                });

        // POST-like rules only (i.e. Id <= 0)
        RuleFor(e => e.ContentType)
            .NotEqual(PublisherContentType.Unknown)
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        // ALL operation rules
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0)
            .When(e => request.RequestPublisherAccountId <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.MediaFileId)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.MediaUrl)
            .Empty()
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.ThumbnailUrl)
            .Empty()
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.Caption)
            .MaximumLength(2200)
            .When(e => e.Caption.HasValue())
            .WithErrorCode(ErrorCodes.TooLong);
    }
}

public class PublisherMediaValidator : AbstractValidator<PublisherMedia>
{
    public PublisherMediaValidator(IRequestBase request, bool isUpsert = false)
    {
        RuleSet(ApplyTo.Post | ApplyTo.Put,
                () =>
                {
                    // PUT like operation
                    RuleFor(e => request.ToDynItemValidationSource<DynPublisherMedia>(e.PublisherAccountId.Gz(request.RequestPublisherAccountId), e.Id.ToEdgeId(),
                                                                                      DynItemType.PublisherMedia, null, ApplyToBehavior.MustExistNotDeleted, false, null))
                        .IsValidDynamoItem()
                        .When(e => e.Id > 0);

                    // POST - Id <= 0
                    RuleFor(e => request.ToDynItemValidationSourceByRef<DynPublisherMedia>(e.PublisherAccountId.Gz(request.RequestPublisherAccountId),
                                                                                           DynPublisherMedia.BuildRefId(e.PublisherType, e.MediaId),
                                                                                           DynItemType.PublisherMedia, ApplyToBehavior.Default))
                        .IsValidDynamoItem()
                        .When(e => e.Id <= 0 && e.ContentType != PublisherContentType.Media &&
                                   e.PublisherType != PublisherType.Unknown && e.MediaId.HasValue() && !isUpsert);

                    RuleFor(e => request.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.PublisherAccountId, e.PublisherAccountId.ToStringInvariant(), DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
                        .IsValidDynamoItem()
                        .When(e => e.PublisherAccountId > 0);

                    // When posting a media of publisher-type RYDR, the mediaId has to exist and it has to be a valid existing file
                    RuleFor(e => request.ToDynItemValidationSource(e.MediaId.ToLong(0), DynItemType.File, ApplyToBehavior.MustExistNotDeleted))
                        .IsValidDynamoItem()
                        .When(e => e.MediaId.HasValue() && e.PublisherType == PublisherType.Rydr);
                });

        // POST-like rules only (i.e. Id <= 0)
        RuleFor(e => e.MediaId)
            .NotEmpty()
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.PublisherType)
            .NotEqual(PublisherType.Unknown)
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.ContentType)
            .NotEqual(PublisherContentType.Unknown)
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.MediaUrl)
            .NotEmpty()
            .When(e => e.Id <= 0 && e.PublisherType != PublisherType.Rydr)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.PublisherUrl)
            .NotEmpty()
            .When(e => e.Id <= 0 && e.PublisherType != PublisherType.Rydr)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.Caption)
            .NotEmpty()
            .When(e => e.Id <= 0 && e.PublisherType != PublisherType.Rydr)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.CreatedAt)
            .IsValidDateTime("CreatedAt")
            .When(e => e.CreatedAt >= DateTimeHelper.MinApplicationDate);

        // ALL operation rules
        RuleFor(e => e.LifetimeStats)
            .Empty()
            .WithErrorCode(ErrorCodes.CannotBeSpecified);
    }
}
