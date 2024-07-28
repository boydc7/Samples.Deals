using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using ServiceStack;
using GetFile = Rydr.Api.Dto.Files.GetFile;

namespace Rydr.Api.Services.Services;

public class FileService : BaseAuthenticatedApiService
{
    private readonly IFileStorageService _fileStorageService;

    public FileService(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    public async Task<OnlyResultResponse<FileDto>> Get(GetFile request)
    {
        var dynFile = await _fileStorageService.TryGetFileAsync(request.Id);

        if (dynFile.FileType == FileType.Video && !dynFile.IsFinalStatus())
        {
            await _fileStorageService.RefreshConvertStatusAsync(dynFile, VideoConvertGenericTypeArguments.Instance);
        }

        var file = dynFile.ToFileDto();

        return file.AsOnlyResultResponse();
    }

    public async Task<OnlyResultResponse<TransferFileResponse>> Get(GetFileUrl request)
    {
        var existingFile = await _dynamoDb.GetItemAsync<DynFile>(request.Id.ToItemDynamoId());

        var convertArgs = request.ToConvertArguments();

        var transferInfo = await _fileStorageService.GetDownloadInfoAsync(request.Id, existingFile,
                                                                          !request.ForDownload, convertArgs);

        return new TransferFileResponse
               {
                   Id = request.Id,
                   Url = transferInfo.Url,
                   Width = transferInfo.Width,
                   Height = transferInfo.Height,
                   MimeType = MimeTypes.GetMimeType(existingFile.FileExtension.Coalesce(existingFile.OriginalFileName))
               }.AsOnlyResultResponse();
    }

    public async Task Get(DownloadFile request)
    {
        var convertArgs = request.ToConvertArguments();

        var transferInfo = await _fileStorageService.GetDownloadInfoAsync(request.Id, isPreview: request.IsPreview, convertArguments: convertArgs);
        Response.RedirectToUrl(transferInfo.Url);
    }

    public async Task<OnlyResultResponse<StatusSimpleResponse>> Get(GetConvertFile request)
    {
        var existingFile = await _dynamoDb.GetItemAsync<DynFile>(request.FileId.ToItemDynamoId());

        if (existingFile.IsFinalStatus())
        {
            return new StatusSimpleResponse(existingFile.ConvertStatus.ToString()).AsOnlyResultResponse();
        }

        var convertArgs = request.ToConvertArguments();

        var status = await _fileStorageService.RefreshConvertStatusAsync(existingFile, convertArgs);

        return new StatusSimpleResponse(status.ToString()).AsOnlyResultResponse();
    }

    public async Task<OnlyResultResponse<TransferFileResponse>> Post(PostFile request)
    {
        var dynFile = request.Model.ToDynFile();

        await _dynamoDb.PutItemAsync(dynFile);

        var transferInfo = await _fileStorageService.GetUploadInfoAsync(dynFile.Id, dynFile);

        return new TransferFileResponse
               {
                   Id = dynFile.Id,
                   Url = transferInfo.Url,
                   MimeType = MimeTypes.GetMimeType(dynFile.FileExtension.Coalesce(dynFile.OriginalFileName))
               }.AsOnlyResultResponse();
    }

    public Task Post(PostConfirmFileUpload request)
        => _fileStorageService.ConfirmUploadAsync(request.Id);

    public async Task Delete(DeleteFile request)
    {
        await _fileStorageService.DeleteFileAsync(request.Id);
    }

    public async Task<object> Get(DownloadRydrFile request)
    {
        var localFile = await _fileStorageService.DownloadAsync(request.Id);

        var response = new HttpResult(new FileInfo(localFile.FullName), !request.IsPreview);

        if (request.IsPreview)
        {
            response.Headers["Content-Disposition"] = "inline";
        }

        return response;
    }

    public async Task Put(UploadRydrFile request)
    {
        if (Request.Files?.Length > 0)
        {
            await _fileStorageService.UploadAsync(Request.Files.Single(), request.Id);
        }
        else
        {
            await _fileStorageService.UploadAsync(Request.InputStream, request.Id);
        }
    }
}
