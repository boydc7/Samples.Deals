using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack;

namespace Rydr.Api.Dto
{
    [Route("/places/{id}", "GET")]
    public class GetPlace : BaseGetRequest<Place> { }

    [Route("/places/{publishertype}/{publisherid}", "GET")]
    public class GetPlaceByPublisher : RequestBase, IReturn<OnlyResultResponse<Place>>, IGet
    {
        public PublisherType PublisherType { get; set; }
        public string PublisherId { get; set; }
    }

    [Route("/places", "POST")]
    public class PostPlace : BasePostRequest<Place>
    {
        protected override RecordType GetRecordType() => RecordType.Place;
    }

    [Route("/places/{id}", "PUT")]
    public class PutPlace : BasePutRequest<Place>
    {
        protected override RecordType GetRecordType() => RecordType.Place;
    }

    [Route("/places/{id}", "DELETE")]
    public class DeletePlace : BaseDeleteRequest
    {
        protected override RecordType GetRecordType() => RecordType.Place;
    }

    [Route("/publisheracct/{publisheraccountid}/places", "GET")]
    public class GetPublisherAccountPlaces : BaseGetManyRequest<Place>, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
    }

    [Route("/publisheracct/{publisheraccountid}/places", "POST")]
    public class LinkPublisherAccountPlace : RequestBase, IPost, IReturn<OnlyResultResponse<LongIdResponse>>, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
        public bool? IsPrimary { get; set; }
        public Place Place { get; set; }
    }

    [Route("/publisheracct/{publisheraccountid}/places/{placeid}", "DELETE")]
    public class DeleteLinkedPublisherAccountPlace : RequestBase, IDelete, IReturnVoid, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
        public long PlaceId { get; set; }
    }

    // DEFERRED actions in response to deal actions
    [Route("/internal/places/updated", "POST")]
    public class PlaceUpdated : RequestBase, IReturnVoid, IPost
    {
        public long PlaceId { get; set; }
    }

    public class Place : BaseDateTimeDeleteTrackedDtoModel, IHasSettableId, IHaveAddress
    {
        public long Id { get; set; }
        public string PublisherId { get; set; }
        public PublisherType PublisherType { get; set; }
        public string Name { get; set; }
        public Address Address { get; set; }

        // Response only properties
        public bool? IsPrimary { get; set; }

        public bool HasUpsertData()
            => !string.IsNullOrEmpty(Name) || Address != null || IsPrimary.GetValueOrDefault();
    }
}
