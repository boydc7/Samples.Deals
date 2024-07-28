using Rydr.Api.Dto.Publishers;

namespace Rydr.Api.Dto.Interfaces;

public interface IDecorateWithPublisherAccountInfo
{
    long PublisherAccountId { get; }
    PublisherAccountInfo PublisherAccount { get; set; }
}
