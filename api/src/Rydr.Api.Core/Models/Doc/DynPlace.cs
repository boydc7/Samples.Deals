using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynPlace : DynItem, IHasNameAndIsRecordLookup, IHaveAddress
    {
        // Hash/Id = PlaceId
        // Range/Edge = PlaceId
        // RefId = PublisherType / PublisherId combo - if no pubtype/id combo for the place, it's unspecified|placeid
        // OwnerId: PublicOwnerId
        // WorkspaceId:
        // StatusId:

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PlaceId
        {
            get => Id;
            set => Id = value;
        }

        [ExcludeNullValue]
        public string PublisherId { get; set; }

        public PublisherType PublisherType { get; set; }

        [ExcludeNullValue]
        public string Name { get; set; }

        [ExcludeNullValue]
        public Address Address { get; set; }

        public static string BuildRefId(PublisherType type, string publisherIdOrPlaceId) => string.Concat(type, "|", publisherIdOrPlaceId);

        public override bool IsPubliclyReadable() => false;
    }
}
