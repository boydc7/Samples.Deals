using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Nest;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynPublisherMediaStat : DynItem, IEquatable<DynPublisherMediaStat>, IHasPublisherAccountId
    {
        // Hash/Id = DynPublisherMedia.PublisherMediaId
        // Range/Edge = period / endTime combination (i.e. lifetime/0, or day/date)
        // RefId =
        // Expires = PublisherMediaValues.DaysBackToKeepMedia days from creation
        // OwnerId:
        // WorkspaceId:
        // StatusId:

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PublisherMediaId
        {
            get => Id;
            set => Id = value;
        }

        [ExcludeNullValue]
        public string Period { get; set; }

        public long EndTime { get; set; }

        [ExcludeNullValue]
        public HashSet<PublisherStatValue> Stats { get; set; }

        public double EngagementRating { get; set; }
        public double TrueEngagementRating { get; set; }
        public PublisherContentType ContentType { get; set; }
        public long PublisherAccountId { get; set; }

        public override AccessIntent DefaultAccessIntent() => AccessIntent.ReadOnly;

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]

        // Transient stat used in pipeline processing
        public bool IsCompletionMediaStat { get; set; }

        public static string BuildEdgeId(string period, long endTime) => string.Concat(period, "|", endTime);

        public bool Equals(DynPublisherMediaStat other)
            => other != null && Id == other.Id && EdgeId.EqualsOrdinalCi(other.EdgeId);

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is DynPublisherMediaStat oobj && Equals(oobj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)2166136261;

                hashCode = (hashCode * 16777619) ^ Id.GetHashCode();
                hashCode = (hashCode * 16777619) ^ EdgeId.GetHashCode();

                return hashCode;
            }
        }
    }
}
