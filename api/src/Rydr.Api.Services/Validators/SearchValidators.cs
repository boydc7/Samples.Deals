using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Search;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class GetCreatorsSearchValidator : BaseRydrValidator<GetCreatorsSearch>
{
    public GetCreatorsSearchValidator()
    {
        Include(new BaseSearchValidator<GetCreatorsSearch>(false));

        // Admins can search over everything all the time - otherwise, the request must have valid identifiers
        Unless(e => e.IsSystemRequest,
               () =>
               {
                   Include(new IsFromValidSubscribedRequestWorkspace<GetCreatorsSearch>());

                   // Only active multi-user team workspaces can cross-query the entire workspace
                   RuleFor(e => e)
                       .SetValidator(new IsFromValidRequestPublisherAccountValidator<GetCreatorsSearch>())
                       .UnlessAsync((e, t) => e.IsSubscribedTeamWorkspaceAsync());
               });
    }
}

public class GetBusinessesSearchValidator : BaseRydrValidator<GetBusinessesSearch>
{
    public GetBusinessesSearchValidator()
    {
        Include(new BaseSearchValidator<GetBusinessesSearch>(false));

        // Admins can search over everything all the time - otherwise, the request must have valid identifiers
        Unless(e => e.IsSystemRequest,
               () =>
               {
                   Include(new IsFromValidSubscribedRequestWorkspace<GetBusinessesSearch>());

                   RuleFor(e => e.Search)
                       .NotEmpty()
                       .When(e => e.Tags.IsNullOrEmptyRydr() && e.Search.IsNullOrEmpty())
                       .UnlessAsync((e, t) => e.IsSubscribedTeamWorkspaceAsync())
                       .WithErrorCode(ErrorCodes.MustBeSpecified)
                       .WithMessage("Search/Query string or Tags must be included");
               });
    }
}

public class BaseSearchValidator<T> : AbstractValidator<T>
    where T : BaseSearch
{
    public BaseSearchValidator(bool requiresQuery = true)
    {
        RuleFor(e => e.Query)
            .NotEmpty()
            .MinimumLength(3)
            .When(e => requiresQuery)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.Take)
            .GreaterThan(0)
            .LessThanOrEqualTo(200)
            .WithErrorCode(ErrorCodes.MustBeValid);
    }
}
