using ServiceStack;

namespace Rydr.Api.Dto.Users
{
    [Route("/track/linksource", "GET POST")]
    public class PostTrackLinkSource : RequestBase, IReturnVoid
    {
        public string LinkUrl { get; set; }
    }

    [Route("/track/process", "POST")]
    public class PostProcessTrackLinkSources : RequestBase, IReturnVoid
    {
        public bool DeleteOnSuccess { get; set; }
    }
}
