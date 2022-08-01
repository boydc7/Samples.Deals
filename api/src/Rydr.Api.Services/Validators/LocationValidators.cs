using System;
using Rydr.Api.Core.Enums;
using Rydr.Api.Dto.Users;
using Rydr.Api.Services.Helpers;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class PutWorkspaceAccountLocationValidator : BaseRydrValidator<PutWorkspaceAccountLocation>
    {
        public PutWorkspaceAccountLocationValidator()
        {
            Include(new IsFromValidRequestWorkspaceValidator<PutWorkspaceAccountLocation>());

            RuleFor(e => e.Latitude)
                .InclusiveBetween(-90, 90);

            RuleFor(e => e.Longitude)
                .InclusiveBetween(-180, 180);

            RuleFor(e => e.CapturedAt.Value)
                .IsValidDateTime("CapturedAt")
                .When(e => e.CapturedAt.HasValue);
        }
    }

    public class PutLocationGeoMapValidator : BaseRydrValidator<PutLocationGeoMap>
    {
        public PutLocationGeoMapValidator()
        {
            RuleFor(e => e.Address)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Address)
                .SetValidator(new AddressValidator())
                .When(e => e.Address != null);

            RuleFor(e => e.Address.Latitude)
                .Must(d => d.HasValue && Math.Abs(d.Value) > 0.0001)
                .Unless(e => e.Address == null)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Address.Longitude)
                .Must(d => d.HasValue && Math.Abs(d.Value) > 0.0001)
                .Unless(e => e.Address == null)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Address.City)
                .NotEmpty()
                .Unless(e => e.Address == null)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Address.StateProvince)
                .NotEmpty()
                .Unless(e => e.Address == null)
                .WithErrorCode(ErrorCodes.MustBeSpecified);
        }
    }

    // INTERNAL DTOs, simple validation....
    public class AddLocationMapValidator : BaseRydrValidator<AddLocationMap> { }
}
