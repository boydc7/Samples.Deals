using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class GetPlaceValidator : BaseGetRequestValidator<GetPlace>
    {
        public GetPlaceValidator()
        {
            RuleFor(e => e.ToDynItemValidationSource(e.Id, e.Id, DynItemType.Place, ApplyToBehavior.Default, true))
                .IsValidDynamoItem();
        }
    }

    public class GetPlaceByPublisherValidator : BaseRydrValidator<GetPlaceByPublisher>
    {
        public GetPlaceByPublisherValidator()
        {
            RuleFor(e => e.PublisherId)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.PublisherType)
                .NotEqual(PublisherType.Unknown)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.PublisherId)
                .MustAsync(async (m, i, t) =>
                           {
                               var existingPlace = await ValidationExtensions._dynamoDb.TryGetPlaceAsync(m.PublisherType, m.PublisherId);

                               return existingPlace != null;
                           })
                .When(e => e.PublisherId.HasValue() && e.PublisherType != PublisherType.Unknown)
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithMessage("Resource not found, invalid, deleted, or you do not have access to it (xpbp)");
        }
    }

    public class GetPublisherAccountPlacesValidator : BaseRydrValidator<GetPublisherAccountPlaces>
    {
        public GetPublisherAccountPlacesValidator()
        {
            Include(new IsValidPublisherAccountIdValidator<GetPublisherAccountPlaces>());
        }
    }

    public class PostPlaceValidator : BaseRydrValidator<PostPlace>
    {
        public PostPlaceValidator()
        {
            RuleFor(e => e.Model)
                .NotNull()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Model.Id)
                .Empty()
                .When(e => e.Model != null)
                .WithErrorCode(ErrorCodes.CannotBeSpecified);

            RuleFor(e => e.Model)
                .SetValidator(r => new PlaceValidator(r, true))
                .When(e => e.Model != null);
        }
    }

    public class DeleteLinkedPublisherAccountPlaceValidator : BaseRydrValidator<DeleteLinkedPublisherAccountPlace>
    {
        public DeleteLinkedPublisherAccountPlaceValidator()
        {
            Include(new IsValidPublisherAccountIdValidator<DeleteLinkedPublisherAccountPlace>());

            RuleFor(e => e.PlaceId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.ToDynItemValidationSource(e.PlaceId, DynItemType.Place, ApplyToBehavior.MustExistCanBeDeleted))
                .IsValidDynamoItem()
                .When(e => e.PlaceId > 0);
        }
    }

    public class LinkPublisherAccountPlaceValidator : BaseRydrValidator<LinkPublisherAccountPlace>
    {
        public LinkPublisherAccountPlaceValidator()
        {
            Include(new IsValidPublisherAccountIdValidator<LinkPublisherAccountPlace>());

            RuleFor(e => e.Place)
                .NotNull()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Place.Id)
                .GreaterThanOrEqualTo(0) // Upsert, 0 or more
                .When(e => e.Place != null)
                .WithErrorCode(ErrorCodes.MustBeValid);

            RuleFor(e => e.Place)
                .SetValidator(r => new PlaceValidator(r, true))
                .When(e => e.Place != null);
        }
    }

    public class PutPlaceValidator : BasePutRequestValidator<PutPlace, Place>
    {
        public PutPlaceValidator()
            : base(r => new PlaceValidator(r)) { }
    }

    public class DeletePlaceValidator : BaseDeleteRequestValidator<DeletePlace>
    {
        public DeletePlaceValidator()
        {
            RuleFor(e => e)
                .UpdateStateIntentTo(AccessIntent.Write);

            RuleFor(e => e.ToDynItemValidationSource(e.Id, DynItemType.Place, ApplyToBehavior.Default))
                .IsValidDynamoItem();
        }
    }

    // INTERNAL Deferred action - simple validation
    public class PlaceUpdatedValidator : BaseRydrValidator<PlaceUpdated>
    {
        public PlaceUpdatedValidator()
        {
            RuleFor(e => e.PlaceId)
                .GreaterThan(0);
        }
    }

    public class PlaceValidator : AbstractValidator<Place>
    {
        public PlaceValidator(IRequestBase request, bool isUpsert = false)
        {
            RuleSet(ApplyTo.Post | ApplyTo.Put,
                    () =>
                    {
                        When(p => p.HasUpsertData(),
                             () =>
                             {
                                 RuleFor(e => e)
                                     .UpdateStateIntentTo(AccessIntent.Write);
                             });

                        RuleFor(e => e.PublisherId)
                            .MustAsync(async (m, i, t) =>
                                       {
                                           var existingPlace = await ValidationExtensions._dynamoDb.TryGetPlaceAsync(m.PublisherType, m.PublisherId);

                                           return isUpsert
                                                      ? existingPlace == null || !existingPlace.IsDeleted()
                                                      : existingPlace == null;
                                       })
                            .When(e => e.PublisherId.HasValue() && e.PublisherType != PublisherType.Unknown)
                            .WithErrorCode(ErrorCodes.AlreadyExists)
                            .WithMessage("Resource already exists (xpvu)");

                        // PUT - don't use the PUT verb filter, as we can use this entity validator as attribute validators from other models
                        // when those enclosing models may be doing a POST of that model but including an existing sub-model
                        RuleFor(e => request.ToDynItemValidationSource(e.Id, DynItemType.Place, isUpsert
                                                                                                    ? ApplyToBehavior.CanExistNotDeleted
                                                                                                    : ApplyToBehavior.MustExistNotDeleted))
                            .IsValidDynamoItem()
                            .When(e => e.Id > 0);
                    });

            // POST-like rules only (i.e. Id <= 0)
            RuleFor(e => e.PublisherType)
                .NotEqual(PublisherType.Unknown)
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("PublisherType must be included and valid");

            RuleFor(e => e.PublisherId)
                .NotEmpty()
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("PublisherId must be specified");

            RuleFor(e => e.Name)
                .NotEmpty()
                .When(e => e.Id <= 0);

            // PUT-like rules
            RuleFor(e => e.PublisherId)
                .Null()
                .When(e => e.Id > 0)
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("PublisherId cannot be updated");

            RuleFor(e => e.PublisherType)
                .Equal(PublisherType.Unknown)
                .When(e => e.Id > 0)
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("PublisherType cannot be updated");

            RuleFor(e => e.Name)
                .MaximumLength(1000)
                .WithErrorCode(ErrorCodes.TooLong);

            RuleFor(e => e.Address)
                .SetValidator(new AddressValidator())
                .When(e => e.Address != null);

            RuleFor(e => e.IsPrimary)
                .Null()
                .WithErrorCode(ErrorCodes.CannotBeSpecified);
        }
    }
}
