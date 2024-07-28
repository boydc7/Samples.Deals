using Rydr.ActiveCampaign;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.Caching;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class GetInstagramAuthUrlValidator : BaseRydrValidator<GetInstagramAuthUrl>
{
    public GetInstagramAuthUrlValidator()
    {
        RuleFor(e => e.ToDynItemValidationSourceByRef(e.PublisherAppId, e.PublisherAppId, DynItemType.PublisherApp, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.PublisherAppId > 0);
    }
}

public class GetInstagramAuthCompleteValidator : AbstractValidator<GetInstagramAuthComplete>
{
    public GetInstagramAuthCompleteValidator()
    {
        RuleFor(e => e.State)
            .NotEmpty();

        RuleFor(e => e.Code)
            .NotEmpty();
    }
}

public class PostInstagramSoftUserRawFeedValidator : BaseRydrValidator<PostInstagramSoftUserRawFeed>
{
    public PostInstagramSoftUserRawFeedValidator()
    {
        // NOTE: Removing this for now, as we use this to send profile info re: basic accounts from the app now in raw-feed form
        // RuleFor(e => e)
        //     .SetValidator(new IsFromValidSubscribedTeamRequestWorkspace<PostInstagramSoftUserRawFeed>())
        //     .Unless(e => e.IsSystemRequest || Request.IsRydrRequest());
        RuleFor(e => e)
            .SetValidator(new IsFromValidRequestWorkspaceValidator<PostInstagramSoftUserRawFeed>())
            .Unless(e => e.IsSystemRequest || Request.IsRydrRequest());

        RuleFor(e => e)
            .SetValidator(new IsValidPublisherAccountIdValidator<PostInstagramSoftUserRawFeed>())
            .When(e => e.PublisherAccountId > 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0)
            .Unless(e => e.UserName.HasValue())
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.UserName)
            .NotEmpty()
            .Unless(e => e.PublisherAccountId > 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.RydrAccountType)
            .Must(t => t == RydrAccountType.Business || t == RydrAccountType.Influencer)
            .Unless(e => e.PublisherAccountId > 0 || e.RydrAccountType == RydrAccountType.None)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.RawFeed)
            .NotEmpty()
            .When(e => e.FeedUrl.IsNullOrEmpty())
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.FeedUrl)
            .NotEmpty()
            .When(e => e.RawFeed.IsNullOrEmpty())
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}

public class PostInstagramSoftUserMediaRawFeedValidator : BaseRydrValidator<PostInstagramSoftUserMediaRawFeed>
{
    public PostInstagramSoftUserMediaRawFeedValidator()
    {
        Include(new IsValidPublisherIdentifierValidator<PostInstagramSoftUserMediaRawFeed>());

        RuleFor(e => e.RawFeed)
            .NotEmpty()
            .When(e => e.FeedUrl.IsNullOrEmpty())
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.FeedUrl)
            .NotEmpty()
            .When(e => e.RawFeed.IsNullOrEmpty())
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}

public class PostInstagramSoftUserValidator : BaseRydrValidator<PostInstagramSoftUser>
{
    public PostInstagramSoftUserValidator()
    {
        RuleFor(e => e)
            .SetValidator(new IsFromValidSubscribedTeamRequestWorkspace<PostInstagramSoftUser>())
            .Unless(e => e.IsSystemRequest || Request.IsRydrRequest());

        RuleFor(e => e.UserName)
            .NotEmpty();

        RuleFor(e => e.RydrAccountType)
            .Must(t => t == RydrAccountType.Business || t == RydrAccountType.Influencer)
            .Unless(e => e.RydrAccountType == RydrAccountType.None)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class PostInstagramSoftUserMediaValidator : BaseRydrValidator<PostInstagramSoftUserMedia>
{
    public PostInstagramSoftUserMediaValidator()
    {
        RuleFor(e => e)
            .SetValidator(new IsFromValidSubscribedTeamRequestWorkspace<PostInstagramSoftUserMedia>())
            .Unless(e => e.IsSystemRequest || Request.IsRydrRequest());

        RuleFor(e => e)
            .SetValidator(new IsValidPublisherAccountIdValidator<PostInstagramSoftUserMedia>())
            .When(e => e.PublisherAccountId > 0);

        RuleFor(e => e.AccountId)
            .NotEmpty()
            .When(e => e.PublisherAccountId <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);
    }
}

public class PostInstagramUserValidator : BaseRydrValidator<PostInstagramUser>
{
    public PostInstagramUserValidator()
    {
        RuleFor(e => e.AccountId)
            .NotEmpty();

        RuleFor(e => e.UserName)
            .NotEmpty();

        RuleFor(e => e.AccessToken)
            .NotEmpty();

        RuleFor(e => e.RydrAccountType)
            .Must(r => r == RydrAccountType.Business || r == RydrAccountType.Influencer)
            .Unless(e => e.RydrAccountType == RydrAccountType.None)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.ToDynItemValidationSourceByRef<DynUser>(e.UserIdentifier.ToLong(0), e.UserIdentifier, DynItemType.User, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.UserIdentifier.HasValue() && e.UserIdentifier.ToLong(0) > 0);

        RuleFor(e => e.Features)
            .Must(f => f == WorkspaceFeature.None || f == WorkspaceFeature.Default)
            .Unless(e => e.IsSystemRequest || Request.IsRydrRequest() || Request.IsLocal)
            .WithErrorCode(ErrorCodes.CannotBeSpecified)
            .WithMessage("You do not have access to change the specified value(s)");
    }
}

public class PostBackInstagramUserValidator : BaseRydrValidator<PostBackInstagramUser>
{
    public PostBackInstagramUserValidator(ICacheClient cacheClient)
    {
        RuleFor(e => e.PostBackId)
            .NotEmpty();

        RuleFor(e => e.RydrAccountType)
            .Must(r => r == RydrAccountType.Business || r == RydrAccountType.Influencer)
            .Unless(e => e.RydrAccountType == RydrAccountType.None)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.PostBackId)
            .Must((e, i) =>
                  {
                      var igRequestKey = i.EndsWithOrdinalCi("#_")
                                             ? i.Left(i.Length - 2)
                                             : i;

                      var postIgRequest = cacheClient.TryGet<PostInstagramUser>(igRequestKey);

                      return postIgRequest != null && postIgRequest.UserId == e.UserId &&
                             postIgRequest.UserIdentifier.ToLong(0) == e.UserId &&
                             (postIgRequest.RydrAccountType == RydrAccountType.None ||
                              postIgRequest.RydrAccountType == e.RydrAccountType);
                  })
            .When(e => e.PostBackId.HasValue())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("The PostBackId must be valid and the RydrAccountType specified must match the existing account type");
    }
}
