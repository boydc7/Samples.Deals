using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Publishers;

namespace Rydr.Api.Core.Models.Internal;

public class PublisherAccountConnectInfo
{
    public DynPublisherAccount ExistingPublisherAccount { get; set; }
    public DynPublisherAccount NewPublisherAccount { get; set; }
    public PublisherAccount IncomingPublisherAccount { get; set; }
    public bool ConvertExisting { get; set; }
}
