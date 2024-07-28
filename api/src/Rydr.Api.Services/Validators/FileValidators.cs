using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using Rydr.Api.Services.Helpers;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class ProcessRelatedMediaFilesValidator : BaseRydrValidator<ProcessRelatedMediaFiles>
{
    public ProcessRelatedMediaFilesValidator()
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0);

        RuleFor(e => e.PublisherMediaIds)
            .NotEmpty();
    }
}

public class ProcessPublisherAccountProfilePicValidator : BaseRydrValidator<ProcessPublisherAccountProfilePic>
{
    public ProcessPublisherAccountProfilePicValidator()
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0);

        RuleFor(e => e.ProfilePicKey)
            .NotEmpty();
    }
}

public class GetConvertFileValidator : BaseRydrValidator<GetConvertFile>
{
    public GetConvertFileValidator()
    {
        RuleFor(e => e.FileId)
            .GreaterThan(0);

        RuleFor(e => e.ToDynItemValidationSource(e.FileId, DynItemType.File, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.FileId > 0);
    }
}

public class GetFileValidator : BaseGetRequestValidator<GetFile>
{
    public GetFileValidator()
    {
        RuleFor(e => e.ToDynItemValidationSource(e.Id, DynItemType.File, ApplyToBehavior.MustExistCanBeDeleted))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

public class DownloadFileValidator : BaseRydrValidator<DownloadFile>
{
    public DownloadFileValidator()
    {
        RuleFor(e => e.Id)
            .GreaterThan(0);

        RuleFor(e => e.ToDynItemValidationSource(e.Id, DynItemType.File, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

public class GetFileUrlValidator : BaseRydrValidator<GetFileUrl>
{
    public GetFileUrlValidator()
    {
        RuleFor(e => e.Id)
            .GreaterThan(0);

        RuleFor(e => e.ToDynItemValidationSource(e.Id, DynItemType.File, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

public class PostFileValidator : BaseRydrValidator<PostFile>
{
    public PostFileValidator()
    {
        RuleFor(e => e.Model)
            .NotNull()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.Model.Id)
            .Equal(0)
            .When(e => e.Model != null)
            .WithErrorCode(ErrorCodes.CannotBeSpecified);

        RuleFor(e => e.Model.Name)
            .NotEmpty()
            .When(e => e.Model != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.Model.FileType)
            .Must((e, t) =>
                  {
                      var fileExtensionType = e.Model.Name.HasValue()
                                                  ? new FileMetaData(e.Model.Name).FileExtension.ToFileType()
                                                  : FileType.Unknown;

                      // If the user did not include a specific type, we have to be able to infer it from the extension
                      if (e.Model.FileType == FileType.Unknown)
                      {
                          return fileExtensionType != FileType.Unknown &&
                                 fileExtensionType != FileType.Other;
                      }

                      // If the user did include a specific type, has to match
                      return e.Model.FileType == fileExtensionType;
                  })
            .When(e => e.Model != null)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("A FileType could not be inferred, or the specified FileType does not match the inferred type of the file extension");
    }
}

public class PostConfirmFileUploadValidator : BaseRydrValidator<PostConfirmFileUpload>
{
    public PostConfirmFileUploadValidator()
    {
        RuleFor(e => e.Id)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.ToDynItemValidationSource(e.Id, DynItemType.File, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

public class DeleteFileValidator : BaseRydrValidator<DeleteFile>
{
    public DeleteFileValidator()
    {
        RuleFor(e => e.Id)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.ToDynItemValidationSource(e.Id, DynItemType.File, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

// Internal messages

public class ConvertFileValidator : BaseRydrValidator<ConvertFile>
{
    public ConvertFileValidator()
    {
        RuleFor(e => e.FileId)
            .GreaterThan(0);

        RuleFor(e => e.ConvertType)
            .Equal(FileConvertType.VideoGenericMp4)
            .WithMessage("The only supported conversion target currently is Generic MP4");
    }
}
