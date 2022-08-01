using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class GetPublisherAppValidator : BaseGetRequestValidator<GetPublisherApp>
    {
        public GetPublisherAppValidator()
        {
            RuleFor(e => e.ToDynItemValidationSourceByRef(e.Id, e.Id, DynItemType.PublisherApp))
                .IsValidDynamoItem();
        }
    }

    public class PostPublisherAppValidator : BasePostRequestValidator<PostPublisherApp, PublisherApp>
    {
        public PostPublisherAppValidator()
            : base(r => new PublisherAppValidator(r)) { }
    }

    public class PutPublisherAppValidator : BasePutRequestValidator<PutPublisherApp, PublisherApp>
    {
        public PutPublisherAppValidator()
            : base(r => new PublisherAppValidator(r)) { }
    }

    public class DeletePublisherAppValidator : BaseDeleteRequestValidator<DeletePublisherApp>
    {
        public DeletePublisherAppValidator()
        {
            RuleFor(e => e)
                .UpdateStateIntentTo(AccessIntent.Write);

            RuleFor(e => e.ToDynItemValidationSourceByRef(e.Id, e.Id, DynItemType.PublisherApp))
                .IsValidDynamoItem();
        }
    }

    public class PublisherAppValidator : AbstractValidator<PublisherApp>
    {
        public PublisherAppValidator(IRequestBase request)
        {
            RuleSet(ApplyTo.Post | ApplyTo.Put,
                    () =>
                    {
                        RuleFor(e => e)
                            .UpdateStateIntentTo(AccessIntent.Write);

                        // POST - don't use the PUT verb filter, as we can use this entity validator as attribute validators from other models
                        // when those enclosing models may be doing a POST of that model but including an existing sub-model
                        RuleFor(e => request.ToDynItemValidationSource(DynPublisherApp.BuildEdgeId(e.Type, e.AppId), DynItemType.PublisherApp, ApplyToBehavior.MustNotExist))
                            .IsValidDynamoItem()
                            .When(e => e.Id <= 0);

                        // PUT - don't use the PUT verb filter, as we can use this entity validator as attribute validators from other models
                        // when those enclosing models may be doing a POST of that model but including an existing sub-model
                        RuleFor(e => request.ToDynItemValidationSourceByRef(e.Id, e.Id, DynItemType.PublisherApp, ApplyToBehavior.MustExistNotDeleted))
                            .IsValidDynamoItem()
                            .When(e => e.Id > 0);
                    });

            // Normally you may consider putting these inside a POST ruleset, but since the validator here can be used as a sub-attribute on other request
            // models, the sub-model may need to act like a POST when the outer model was PUT'ed, or act as a PUT when the outer was POST'ed.  So, we leave
            // the POST and PUT specific checks (i.e. Id must be <= 0 on a POST, or Id must be > 0 on a PUT) for the Post/Put specific request validators
            // and use the WHEN filters here

            // POST-like rules only (i.e. Id <= 0)
            RuleFor(e => e.Type)
                .NotEqual(PublisherType.Unknown)
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.AppId)
                .NotEmpty()
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.AppSecret)
                .NotEmpty()
                .When(e => e.Id <= 0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            // PUT-like rules only (i.e. Id > 0)
            RuleFor(e => e.AppId)
                .Null()
                .When(e => e.Id > 0)
                .WithErrorCode(ErrorCodes.CannotBeSpecified)
                .WithMessage("You cannot update the AppId once registered");

            // ALL operation rules
            RuleFor(e => e.AppId)
                .MaximumLength(150)
                .When(e => e.AppId.HasValue())
                .WithErrorCode(ErrorCodes.TooLong);

            RuleFor(e => e.AppSecret)
                .MaximumLength(150)
                .When(e => e.AppSecret.HasValue())
                .WithErrorCode(ErrorCodes.TooLong);
        }
    }
}
