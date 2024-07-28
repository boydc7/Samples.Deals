namespace Rydr.Api.Core.Models.Supporting;

public class FileUploadInfo
{
    public long FileId { get; set; }
    public string Url { get; set; }
}

public class FileDownloadInfo
{
    public long FileId { get; set; }
    public string Url { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}
