using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class GetHashtagValidator : BaseGetRequestValidator<GetHashtag>
{
    public GetHashtagValidator()
    {
        RuleFor(e => e.ToDynItemValidationSourceByRef(e.Id, e.Id, DynItemType.Hashtag))
            .IsValidDynamoItem();
    }
}

public class PostHashtagValidator : BasePostRequestValidator<PostHashtag, Hashtag>
{
    public PostHashtagValidator(IPocoDynamo dynamoDb)
        : base(r => new HashtagValidator(r, dynamoDb)) { }
}

public class PostHashtagUpsertValidator : BaseUpsertRequestValidator<PostHashtagUpsert, Hashtag>
{
    public PostHashtagUpsertValidator(IPocoDynamo dynamoDb)
        : base((r, u) => new HashtagValidator(r, dynamoDb, u)) { }
}

public class PutHashtagValidator : BasePutRequestValidator<PutHashtag, Hashtag>
{
    public PutHashtagValidator(IPocoDynamo dynamoDb)
        : base(r => new HashtagValidator(r, dynamoDb)) { }
}

public class DeleteHashtagValidator : BaseDeleteRequestValidator<DeleteHashtag>
{
    public DeleteHashtagValidator()
    {
        RuleFor(e => e.ToDynItemValidationSourceByRef(e.Id, e.Id, DynItemType.Hashtag))
            .IsValidDynamoItem();
    }
}

public class MediaStatValidator : AbstractValidator<MediaStat>
{
    public MediaStatValidator()
    {
        RuleFor(d => d.Type)
            .NotEqual(MediaStatType.Unknown);

        RuleFor(d => d.Value)
            .NotEmpty();
    }
}

public class HashtagValidator : AbstractValidator<Hashtag>
{
    public HashtagValidator(IRequestBase request, IPocoDynamo dynamoDb, bool isUpsert = false)
    {
        RuleSet(ApplyTo.Post | ApplyTo.Put,
                () =>
                {
                    // POST - don't use the PUT verb filter, as we can use this entity validator as attribute validators from other models
                    // when those enclosing models may be doing a POST of that model but including an existing sub-model
                    RuleFor(e => e.Name)
                        .MustAsync(async (r, h, t) =>
                                   {
                                       var existingHashtag = await dynamoDb.GetHashtagByNameAsync(h, r.PublisherType, true);

                                       return existingHashtag == null;
                                   })
                        .When(e => e.Name.HasValue())
                        .When(e => e.Id <= 0 && !isUpsert)
                        .WithErrorCode(ErrorCodes.AlreadyExists)
                        .WithMessage("Hashtag with that name already exists for that publisher type");

                    // PUT - don't use the PUT verb filter, as we can use this entity validator as attribute validators from other models
                    // when those enclosing models may be doing a POST of that model but including an existing sub-model
                    RuleFor(e => request.ToDynItemValidationSourceByRef(e.Id, e.Id, DynItemType.Hashtag, ApplyToBehavior.MustExistNotDeleted))
                        .IsValidDynamoItem()
                        .When(e => e.Id > 0);
                });

        // Normally you may consider putting these inside a POST ruleset, but since the validator here can be used as a sub-attribute on other request
        // models, the sub-model may need to act like a POST when the outer model was PUT'ed, or act as a PUT when the outer was POST'ed.  So, we leave
        // the POST and PUT specific checks (i.e. Id must be <= 0 on a POST, or Id must be > 0 on a PUT) for the Post/Put specific request validators
        // and use the WHEN filters here

        // POST-like rules only (i.e. Id <= 0)
        RuleFor(e => e.Name)
            .NotEmpty()
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleForEach(e => e.Stats)
            .SetValidator(new MediaStatValidator())
            .When(e => !e.Stats.IsNullOrEmpty());

        RuleFor(e => e.PublisherType)
            .NotEqual(PublisherType.Unknown)
            .When(e => e.Id <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        // PUT-like rules only (i.e. Id > 0)
        RuleFor(e => e.Name)
            .Null()
            .When(e => e.Id > 0)
            .WithErrorCode(ErrorCodes.CannotBeSpecified)
            .WithMessage("You cannot update the Hashtag name once created");

        // ALL operation rules
        RuleFor(e => e.Name)
            .MaximumLength(100)
            .WithErrorCode(ErrorCodes.TooLong);

        RuleFor(e => e.PublisherId)
            .MaximumLength(250)
            .WithErrorCode(ErrorCodes.TooLong);
    }
}
