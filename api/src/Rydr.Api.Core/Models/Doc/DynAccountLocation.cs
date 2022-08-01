using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynAccountLocation : DynItem, IHasPublisherAccountId
    {
        // Hash/Id: PublisherAccountId of the workspace the lcoation is for (DefaultPublisherAccount)
        // Range/Edge: DynItemType.AccountLocation (as int) | UnixTimestamp of when the location was captured
        // Expires: 95 days after creation
        // RefId:
        // OwnerId:
        // WorkspaceId: WorkspaceId of the workspace the location is for
        // StatusId:

        public static readonly string AccountLocationStartsWith = string.Concat((int)DynItemType.AccountLocation, "|");

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PublisherAccountId
        {
            get => Id;
            set => Id = value;
        }

        public long CapturedAt { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public static string BuildEdgeId(long capturedAt) => string.Concat(AccountLocationStartsWith, capturedAt);
    }
}
