using Rydr.Api.Core.Enums;
using Rydr.Api.Dto.Users;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class PostProcessHumanLoopValidator : BaseRydrValidator<PostProcessHumanLoop>
    {
        public PostProcessHumanLoopValidator()
        {
            RuleFor(e => e.LoopIdentifier)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.HoursBack)
                .LessThanOrEqualTo(750);
        }
    }

    // INTERNAL requests...

    public class PostHumanCategorizeBusinessValidator : BaseRydrValidator<PostHumanCategorizeBusiness>
    {
        public PostHumanCategorizeBusinessValidator()
        {
            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class PostProcessHumanBusinessCategoryResponseValidator : BaseRydrValidator<PostProcessHumanBusinessCategoryResponse>
    {
        public PostProcessHumanBusinessCategoryResponseValidator()
        {
            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class PostHumanCategorizeCreatorValidator : BaseRydrValidator<PostHumanCategorizeCreator>
    {
        public PostHumanCategorizeCreatorValidator()
        {
            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class PostProcessHumanCreatorCategoryResponseValidator : BaseRydrValidator<PostProcessHumanCreatorCategoryResponse>
    {
        public PostProcessHumanCreatorCategoryResponseValidator()
        {
            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class PostProcessHumanImageModerationResponseValidator : BaseRydrValidator<PostProcessHumanImageModerationResponse>
    {
        public PostProcessHumanImageModerationResponseValidator()
        {
            RuleFor(e => e.PublisherMediaId)
                .GreaterThan(0);
        }
    }
}
