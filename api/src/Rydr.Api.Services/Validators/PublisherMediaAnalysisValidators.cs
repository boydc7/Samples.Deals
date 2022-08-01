using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Services.Helpers;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators
{
    public class GetPublisherAccountMediaVisionValidator : BaseRydrValidator<GetPublisherAccountMediaVision>
    {
        public GetPublisherAccountMediaVisionValidator()
        {
            Include(new IsValidPublisherIdentifierValidator<GetPublisherAccountMediaVision>());

            RuleFor(e => e)
                .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
                .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

            RuleFor(e => e.PublisherIdentifier)
                .MustAsync(async (e, i, t) =>
                           {
                               var publisherAccountId = e.GetPublisherIdFromIdentifier();

                               var publisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                               .TryGetPublisherAccountAsync(publisherAccountId);

                               return publisherAccount != null && !publisherAccount.IsDeleted() &&
                                      publisherAccount.OptInToAi;
                           })
                .When(e => !e.IsSystemRequest)
                .WithName("Publisher")
                .OverridePropertyName("Publisher")
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithMessage("Publisher requested either does not exist or you do not have permission to view results - code [gamaynoin]");
        }
    }

    public class GetPublisherAccountMediaAnalysisValidator : BaseRydrValidator<GetPublisherAccountMediaAnalysis>
    {
        public GetPublisherAccountMediaAnalysisValidator()
        {
            Include(new IsValidPublisherIdentifierValidator<GetPublisherAccountMediaAnalysis>());

            RuleFor(e => e)
                .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
                .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               var publisherAccountId = e.GetPublisherIdFromIdentifier();

                               var publisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                               .TryGetPublisherAccountAsync(publisherAccountId);

                               return publisherAccount != null && !publisherAccount.IsDeleted() &&
                                      publisherAccount.OptInToAi;
                           })
                .When(e => !e.IsSystemRequest)
                .WithName("Publisher")
                .OverridePropertyName("Publisher")
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithMessage("Publisher requested either does not exist or you do not have permission to view results - code [gamaynoin]");
        }
    }

    public class GetPublisherAccountMediaSearchValidator : BaseRydrValidator<GetPublisherAccountMediaSearch>
    {
        public GetPublisherAccountMediaSearchValidator()
        {
            Include(new IsValidPublisherIdentifierValidator<GetPublisherAccountMediaSearch>());

            RuleFor(e => e)
                .SetValidator(new IsFromValidNonTokenRequestPublisherAccountValidator())
                .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               var publisherAccountId = e.GetPublisherIdFromIdentifier();

                               var publisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                               .TryGetPublisherAccountAsync(publisherAccountId);

                               return publisherAccount != null && !publisherAccount.IsDeleted() &&
                                      publisherAccount.OptInToAi;
                           })
                .When(e => !e.IsSystemRequest)
                .WithName("Publisher")
                .OverridePropertyName("Publisher")
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithMessage("Publisher requested either does not exist or you do not have permission to view results - code [gamaynoin]");
        }
    }

    public class PostAnalyzePublisherTopicsValidator : BaseRydrValidator<PostAnalyzePublisherTopics>
    {
        public PostAnalyzePublisherTopicsValidator()
        {
            Include(new IsValidPublisherAccountIdValidator<PostAnalyzePublisherTopics>());
        }
    }

    public class PostAnalyzePublisherMediasValidator : BaseRydrValidator<PostAnalyzePublisherMedias>
    {
        public PostAnalyzePublisherMediasValidator()
        { // Admin only endpoint, keep it simple
            RuleFor(e => e.PublisherAccountIds)
                .IsValidIdList("PublisherAccountIds")
                .When(e => e.PublisherAccountIds != null)
                .WithErrorCode(ErrorCodes.MustBeValid);
        }
    }

    public class PostAnalyzePublisherMediaValidator : BaseRydrValidator<PostAnalyzePublisherMedia>
    {
        public PostAnalyzePublisherMediaValidator()
        {
            // Admin only, scheduled DTO, simple validation only...

            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0);
        }
    }

    public class PostAnalyzePublisherTextValidator : BaseRydrValidator<PostAnalyzePublisherText>
    {
        public PostAnalyzePublisherTextValidator(IFileStorageProvider fileStorageProvider)
        {
            Include(new IsValidPublisherAccountIdValidator<PostAnalyzePublisherText>());

            RuleFor(e => e.PublisherMediaId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Path)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Path)
                .MustAsync(async (p, ctx) =>
                           {
                               var fileMeta = new FileMetaData(p);

                               var result = await fileStorageProvider.ExistsAsync(fileMeta);

                               return result;
                           })
                .When(e => e.Path.HasValue())
                .WithErrorCode(ErrorCodes.MustExist)
                .WithMessage(e => $"Path to text [{e.Path}] does not exist");
        }
    }

    public class PutUpdateMediaLabelValidator : BaseRydrValidator<PutUpdateMediaLabel>
    {
        public PutUpdateMediaLabelValidator()
        {
            RuleFor(e => e.Name)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Ignore)
                .Equal(false)
                .When(e => e.Ignore.HasValue && e.ParentName.EqualsOrdinalCi("*"))
                .WithErrorCode(ErrorCodes.MustBeValid)
                .WithMessage("Cannot ignore a wildcard name");

            RuleFor(e => e.RewriteName)
                .NotEmpty()
                .When(e => !e.Ignore.GetValueOrDefault())
                .WithErrorCode(ErrorCodes.MustBeSpecified);
        }
    }

    public class PostAnalyzePublisherAggStatsValidator : BaseRydrValidator<PostAnalyzePublisherAggStats>
    {
        public PostAnalyzePublisherAggStatsValidator()
        {
            RuleFor(e => e.PublisherAccountId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.EdgeId)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);
        }
    }

    public class PublisherMediaAnalysisUpdatedValidator : BaseRydrValidator<PublisherMediaAnalysisUpdated>
    {
        public PublisherMediaAnalysisUpdatedValidator()
        {
            RuleFor(e => e.PublisherMediaId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.PublisherMediaAnalysisEdgeId)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);
        }
    }

    public class PostAnalyzePublisherImageValidator : BaseRydrValidator<PostAnalyzePublisherImage>
    {
        public PostAnalyzePublisherImageValidator(IFileStorageProvider fileStorageProvider)
        {
            Include(new IsValidPublisherAccountIdValidator<PostAnalyzePublisherImage>());

            RuleFor(e => e.PublisherMediaId)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Path)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.Path)
                .MustAsync(async (p, ctx) =>
                           {
                               var fileMeta = new FileMetaData(p);

                               var response = await fileStorageProvider.ExistsAsync(fileMeta);

                               return response;
                           })
                .When(e => e.Path.HasValue())
                .WithErrorCode(ErrorCodes.MustExist)
                .WithMessage(e => $"Path to image [{e.Path}] does not exist");
        }
    }
}
