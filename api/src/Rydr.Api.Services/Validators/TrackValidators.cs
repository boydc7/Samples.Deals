using Rydr.Api.Core.Enums;
using Rydr.Api.Dto.Users;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class PostProcessTrackLinkSourcesValidator : BaseRydrValidator<PostProcessTrackLinkSources> { }

    public class PostTrackLinkSourceValidator : AbstractValidator<PostTrackLinkSource>
    {
        public PostTrackLinkSourceValidator()
        {
            RuleFor(e => e.LinkUrl)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);
        }
    }
}
