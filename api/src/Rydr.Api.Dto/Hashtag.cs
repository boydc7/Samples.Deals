using System;
using System.Collections.Generic;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack;

namespace Rydr.Api.Dto
{
    [Route("/hashtags/{id}", "GET")]
    public class GetHashtag : BaseGetRequest<Hashtag> { }

    [Route("/hashtags", "POST")]
    public class PostHashtag : BasePostRequest<Hashtag>
    {
        protected override RecordType GetRecordType() => RecordType.Hashtag;
    }

    [Route("/hashtags/upsert", "POST")]
    public class PostHashtagUpsert : BasePostRequest<Hashtag>
    {
        protected override RecordType GetRecordType() => RecordType.Hashtag;
    }

    [Route("/hashtags/{id}", "PUT")]
    public class PutHashtag : BasePutRequest<Hashtag>
    {
        protected override RecordType GetRecordType() => RecordType.Hashtag;
    }

    [Route("/hashtags/{id}", "DELETE")]
    public class DeleteHashtag : BaseDeleteRequest
    {
        protected override RecordType GetRecordType() => RecordType.Hashtag;
    }

    public class Hashtag : BaseDateTimeDeleteTrackedDtoModel, IHasSettableId, IEquatable<Hashtag>
    {
        public long Id { get; set; }
        public string PublisherId { get; set; }
        public string Name { get; set; }
        public PublisherType PublisherType { get; set; }
        public HashtagType HashtagType { get; set; }
        public List<MediaStat> Stats { get; set; }

        private string _toString;

        public override string ToString()
            => _toString ??= PublisherType == PublisherType.Unknown || string.IsNullOrEmpty(Name)
                                 ? null
                                 : string.Concat(PublisherType, "|", Name, "|", HashtagType);

        public bool Equals(Hashtag other)
            => other != null &&
               PublisherType == other.PublisherType &&
               HashtagType == other.HashtagType &&
               Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);

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

            return obj is Hashtag oobj && Equals(oobj);
        }

        public override int GetHashCode()
            => ToString().GetHashCode();
    }
}
