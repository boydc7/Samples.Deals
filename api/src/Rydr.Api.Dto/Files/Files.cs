using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Model;

namespace Rydr.Api.Dto.Files;

[Route("/files/{id}", "GET")]
public class GetFile : BaseGetRequest<FileDto> { }

[Route("/files/{id}/preview", "GET")]
public class GetFileUrl : FileConvertInfoRequest, IReturn<OnlyResultResponse<TransferFileResponse>>, IGet
{
    public long Id { get; set; }
    public bool ForDownload { get; set; }
}

[Route("/files/{id}/download", "GET")]
public class DownloadFile : FileConvertInfoRequest, IReturnVoid, IGet
{
    public long Id { get; set; }
    public bool IsPreview { get; set; }
}

[Route("/files/{fileid}/conversion", "GET")]
public class GetConvertFile : FileConvertInfoRequest, IReturn<OnlyResultResponse<StatusSimpleResponse>>, IGet
{
    public long FileId { get; set; }
}

[Route("/files", "POST")]
public class PostFile : RequestBase, IRequestBaseWithModel<FileDto>, IReturn<OnlyResultResponse<TransferFileResponse>>
{
    public FileDto Model { get; set; }
}

[Route("/files/{id}/uploaded", "POST")]
public class PostConfirmFileUpload : RequestBase, IReturnVoid, IPost
{
    public long Id { get; set; }
}

[Route("/files/{id}", "DELETE")]
public class DeleteFile : RequestBase, IReturnVoid, IDelete
{
    public long Id { get; set; }
}

[Route("/internal/files/processmedia", "POST")]
public class ProcessRelatedMediaFiles : RequestBase, IReturnVoid, IPost
{
    public long PublisherAccountId { get; set; }
    public List<long> PublisherMediaIds { get; set; }
    public bool StoreAsPermanentMedia { get; set; }
    public bool IsCompletionMedia { get; set; }
}

[Route("/internal/files/processpublisherprofile", "POST")]
public class ProcessPublisherAccountProfilePic : RequestBase, IReturnVoid, IPost
{
    public long PublisherAccountId { get; set; }
    public string ProfilePicKey { get; set; }
}

[Route("/internal/files/{fileid}/convert", "POST")]
public class ConvertFile : FileConvertInfoRequest, IReturnVoid, IPost
{
    public long FileId { get; set; }
}

public class FileDto : BaseDateTimeDeleteTrackedDtoModel, IHasLongId
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string FileExtension { get; set; }
    public FileType FileType { get; set; }
    public long ContentLength { get; set; }
    public string OriginalFileName { get; set; }

    // Returned-only data
    public bool? IsConverted { get; set; }
    public FileConvertStatus? ConvertStatus { get; set; }
}

public class TransferFileResponse
{
    public long Id { get; set; }
    public string Url { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string MimeType { get; set; }
}

public abstract class FileConvertInfoRequest : RequestBase
{
    public int Width { get; set; }
    public int Height { get; set; }
    public ImageResizeMode ResizeMode { get; set; }
    public FileConvertType ConvertType { get; set; }
}
