using Rydr.Api.Dto.Enums;
using ServiceStack;
using ServiceStack.Model;

namespace Rydr.Api.Dto.Publishers
{
    [Route("/publisherapps/{id}", "GET")]
    public class GetPublisherApp : BaseGetRequest<PublisherApp> { }

    [Route("/publisherapps", "POST")]
    public class PostPublisherApp : BasePostRequest<PublisherApp>
    {
        protected override RecordType GetRecordType() => RecordType.PublisherApp;
    }

    [Route("/publisherapps/{id}", "PUT")]
    public class PutPublisherApp : BasePutRequest<PublisherApp>
    {
        protected override RecordType GetRecordType() => RecordType.PublisherApp;
    }

    [Route("/publisherapps/{id}", "DELETE")]
    public class DeletePublisherApp : BaseDeleteRequest
    {
        protected override RecordType GetRecordType() => RecordType.PublisherApp;
    }

    public class PublisherApp : BaseDateTimeDeleteTrackedDtoModel, IHasLongId
    {
        public long Id { get; set; }
        public PublisherType Type { get; set; }
        public string AppId { get; set; }
        public string AppSecret { get; set; }
        public string ApiVersion { get; set; }
    }
}
