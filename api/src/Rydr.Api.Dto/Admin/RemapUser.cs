using ServiceStack;

namespace Rydr.Api.Dto.Admin;

[Route("/internal/admin/remapuser", "POST")]
public class RemapUser : RequestBase, IReturnVoid, IPost
{
    public string FromUserFirebaseId { get; set; }
    public string ToUserFirebaseId { get; set; }
}
