using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.QueryDto;
using Rydr.Api.Services.Helpers;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class PostUpdateCreatorsMetricsValidator : BaseRydrValidator<PostUpdateCreatorsMetrics>
{
    public PostUpdateCreatorsMetricsValidator()
    { // Admin only endpoint, keep it simple
        RuleFor(e => e.PublisherAccountIds)
            .IsValidIdList("PublisherAccountIds")
            .When(e => e.PublisherAccountIds != null)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class PostUpdateCreatorMetricsValidator : BaseRydrValidator<PostUpdateCreatorMetrics>
{
    public PostUpdateCreatorMetricsValidator()
    {
        // Admin only, deferred DTO processing - simple validation
        RuleFor(e => e)
            .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
            .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));
    }
}

public class QueryCreatorStatsValidator : BaseRydrValidator<QueryCreatorStats>
{
    public QueryCreatorStatsValidator()
    {
        RuleFor(e => e)
            .SetValidator(new IsValidPublisherAccountIdValidator<QueryCreatorStats>())
            .When(e => e.PublisherAccountId > 0);
    }
}

public class GetPublisherContentStatsValidator : BaseRydrValidator<GetPublisherContentStats>
{
    public GetPublisherContentStatsValidator()
    {
        Include(new IsValidPublisherIdentifierValidator<GetPublisherContentStats>());

        RuleFor(e => e)
            .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
            .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

        RuleFor(e => e.ContentType)
            .NotEqual(PublisherContentType.Unknown)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.PublisherType)
            .NotEqual(PublisherType.Unknown)
            .When(e => e.PublisherType.HasValue)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.Limit)
            .InclusiveBetween(1, 100)
            .When(e => e.Limit != 0)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class GetPublisherAudienceLocationsValidator : BaseRydrValidator<GetPublisherAudienceLocations>
{
    public GetPublisherAudienceLocationsValidator()
    {
        Include(new IsValidPublisherIdentifierValidator<GetPublisherAudienceLocations>());

        RuleFor(e => e)
            .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
            .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

        RuleFor(e => e.Limit)
            .InclusiveBetween(1, 100)
            .When(e => e.Limit != 0)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class GetPublisherAudienceAgeGendersValidator : BaseRydrValidator<GetPublisherAudienceAgeGenders>
{
    public GetPublisherAudienceAgeGendersValidator()
    {
        Include(new IsValidPublisherIdentifierValidator<GetPublisherAudienceAgeGenders>());

        RuleFor(e => e)
            .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
            .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

        RuleFor(e => e.Limit)
            .InclusiveBetween(1, 100)
            .When(e => e.Limit != 0)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}

public class GetPublisherAudienceGrowthValidator : BaseRydrValidator<GetPublisherAudienceGrowth>
{
    public GetPublisherAudienceGrowthValidator()
    {
        Include(new IsValidPublisherIdentifierValidator<GetPublisherAudienceGrowth>());

        RuleFor(e => e)
            .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
            .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

        RuleFor(e => e.Limit)
            .InclusiveBetween(1, 100)
            .When(e => e.Limit != 0)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.StartDate.Value)
            .IsValidDateTime("StartDate")
            .When(e => e.StartDate.HasValue);

        RuleFor(e => e.EndDate.Value)
            .IsValidDateTime("EndDate")
            .When(e => e.EndDate.HasValue);
    }
}
