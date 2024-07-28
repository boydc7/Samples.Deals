using ServiceStack;
using ServiceStack.Web;

namespace Rydr.Api.Dto.Files;

[Route("/filetransfer/{id}", "GET")]
[Route("/filetransfer/{id}/{hashid}", "GET")]
public class DownloadRydrFile : RequestBase, IReturn<IHttpResult>, IGet
{
    public long Id { get; set; }
    public bool IsPreview { get; set; }
}

[Route("/filetransfer/{id}", "PUT")]
public class UploadRydrFile : RequestBase, IReturnVoid, IPut
{
    public long Id { get; set; }
}
