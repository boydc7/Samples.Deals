using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Services.Helpers;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class PostDealMetricValidator : BaseRydrValidator<PostDealMetric>
{
    public PostDealMetricValidator()
    {
        Include(new IsFromValidRequestWorkspaceValidator<PostDealMetric>());

        RuleFor(e => e.DealId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.MetricType)
            .NotEqual(DealTrackMetricType.Unknown)
            .WithErrorCode(ErrorCodes.MustBeValid);

        Include(new IsValidPublisherAccountIdOrRequestPublisherIdValidator<PostDealMetric>());

        // NOTE: Purposely not validating auth to the deal here...metric tracking...
        // RuleFor(e => e.ToDynItemValidationSource(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistNotDeleted))
        //     .IsValidDynamoItem()
        //     .When(e => e.DealId > 0);
    }
}

public class GetDelinquentDealRequestsValidator : BaseRydrValidator<GetDelinquentDealRequests>
{
    public GetDelinquentDealRequestsValidator()
    {
        Include(new IsValidPublisherIdentifierValidator<GetDelinquentDealRequests>());
    }
}

public class GetDealCompletionMediaMetricsValidator : BaseRydrValidator<GetDealCompletionMediaMetrics>
{
    public GetDealCompletionMediaMetricsValidator()
    {
        RuleFor(e => e.ToDynItemValidationSource<DynDeal>(e.DealId.ToEdgeId(), DynItemType.Deal, ApplyToBehavior.MustExistCanBeDeleted))
            .IsValidDynamoItem()
            .When(e => e.DealId > 0);

        Include(new IsValidPublisherAccountIdOrRequestPublisherIdValidator<GetDealCompletionMediaMetrics>());

        // Can only specify a publisher account that is not the authenticated account if the user is an admin/in-process basically
        RuleFor(e => e.PublisherAccountId)
            .Equal(e => e.RequestPublisherAccountId)
            .When(e => e.PublisherAccountId > 0 && !e.IsSystemRequest)
            .WithErrorCode(ErrorCodes.MustBeAuthorized);

        RuleFor(e => e.CompletedOnStart.Value)
            .IsValidDateTime("CompletedOnStart")
            .When(e => e.CompletedOnStart.HasValue);

        RuleFor(e => e.CompletedOnEnd.Value)
            .IsValidDateTime("CompletedOnEnd")
            .When(e => e.CompletedOnEnd.HasValue);
    }
}
