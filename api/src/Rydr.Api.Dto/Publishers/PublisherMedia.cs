using System;
using System.Collections.Generic;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Dto.Publishers
{
    [Route("/publishermedia/{id}", "GET")]
    public class GetPublisherMedia : BaseGetRequest<PublisherMedia> { }

    [Route("/publishermedia/{id}/analysis", "GET")]
    public class GetPublisherMediaAnalysis : BaseGetRequest<PublisherMediaAnalysis> { }

    // Get BOTH media we have synced and new publisher media (i.e. at fb/ig)....
    [Route("/publishermedia/{PublisherIdentifier}/media", "GET")]
    public class GetRecentMedia : BaseGetManyRequest<PublisherMedia>, IHasPublisherAccountIdentifier
    {
        public string PublisherIdentifier { get; set; }
        public long PublisherAppId { get; set; }
        public bool LiveMediaOnly { get; set; }
        public int Limit { get; set; }
    }

    // Get only media that we have already synced to rydr...
    [Route("/publishermedia/{PublisherIdentifier}/syncedmedia", "GET")]
    public class GetRecentSyncedMedia : BaseGetManyRequest<PublisherMedia>, IHasPublisherAccountIdentifier
    {
        public string PublisherIdentifier { get; set; }
        public PublisherContentType[] ContentTypes { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public bool LiveMediaOnly { get; set; }
        public int Limit { get; set; }
    }

    // Get ONLY media that exists at the publisher now (i.e. at fb/ig) that we have NOT synced yet
    [Route("/publishermedia/{PublisherIdentifier}/publishermedia", "GET")]
    public class GetRecentPublisherMedia : BaseGetManyRequest<PublisherMedia>, IHasPublisherAccountIdentifier
    {
        public string PublisherIdentifier { get; set; }
        public long PublisherAppId { get; set; }
        public bool LiveMediaOnly { get; set; }
        public int Limit { get; set; }
    }

    [Route("/publishermedia", "POST")]
    public class PostPublisherMedia : RequestBase, IPost, IRequestBaseWithModel<PublisherMedia>, IReturn<LongIdResponse>
    {
        public PublisherMedia Model { get; set; }
    }

    [Route("/publishermedia/{id}/prioritize", "PUT")]
    public class PutPublisherMediaAnalysisPriority : RequestBase, IPut, IReturnVoid
    {
        public long Id { get; set; }
        public int Priority { get; set; }
    }

    [Route("/approvedmedia/{id}", "GET")]
    public class GetPublisherApprovedMedia : BaseGetRequest<PublisherApprovedMedia> { }

    [Route("/approvedmedia", "GET")]
    public class GetPublisherApprovedMedias : BaseGetManyRequest<PublisherApprovedMedia>, IHasPublisherAccountIdentifier, IHasSkipTake
    {
        public string PublisherIdentifier { get; set; }
        public long DealId { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }

    [Route("/approvedmedia", "POST")]
    public class PostPublisherApprovedMedia : BasePostRequest<PublisherApprovedMedia>
    {
        protected override RecordType GetRecordType() => RecordType.ApprovedMedia;
    }

    [Route("/approvedmedia/{id}", "PUT")]
    public class PutPublisherApprovedMedia : BasePutRequest<PublisherApprovedMedia>
    {
        protected override RecordType GetRecordType() => RecordType.ApprovedMedia;
    }

    [Route("/approvedmedia/{id}", "DELETE")]
    public class DeletePublisherApprovedMedia : BaseDeleteRequest
    {
        protected override RecordType GetRecordType() => RecordType.ApprovedMedia;
    }

    [Route("/internal/publishermedia/received", "POST")]
    public class PostPublisherMediaReceived : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
        public long PublisherMediaId { get; set; }
    }

    [Route("/internal/publishermedia/upsert", "POST")]
    public class PostPublisherMediaUpsert : RequestBase, IPost, IHaveModel<PublisherMedia>, IReturn<LongIdResponse>
    {
        public PublisherMedia Model { get; set; }
    }

    [Route("/internal/publishermediastats/received", "POST")]
    public class PostPublisherMediaStatsReceived : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
        public long PublisherMediaId { get; set; }
        public List<PublisherStatValue> Stats { get; set; }
    }

    [Route("/internal/publisheracct/{PublisherAccountId}/media", "POST")]
    public class PostSyncRecentPublisherAccountMedia : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
        public long WithWorkspaceId { get; set; }
        public long PublisherAppId { get; set; }
        public bool Force { get; set; }

        public static string GetRecurringJobId(long publisherAccountId)
            => string.Concat("PublisherAccountSync|", publisherAccountId);
    }

    [Route("/internal/publisheracct/{publisheraccountid}/media/trigger", "POST")]
    public class PostTriggerSyncRecentPublisherAccountMedia : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
        public long WithWorkspaceId { get; set; }
        public long PublisherAppId { get; set; }
        public bool Force { get; set; }
    }

    [Route("/internal/publisheracct/{publisheraccountid}/media/enable", "POST DELETE")]
    public class PublisherAccountSyncEnable : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
    }

    public class PublisherMedia : PublisherMediaInfo, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
        public PublisherType PublisherType { get; set; }
        public PublisherContentType ContentType { get; set; }
        public string Caption { get; set; }
        public string PublisherUrl { get; set; }
        public long ActionCount { get; set; }
        public long CommentCount { get; set; }
        public bool IsPreBizAccountConversionMedia { get; set; }
        public bool IsAnalyzed { get; set; }
        public bool IsCompletionMedia { get; set; }
        public bool IsMediaRydrHosted { get; set; }
        public int AnalyzePriority { get; set; }

        // Response-only attributes
        public PublisherMediaStat LifetimeStats { get; set; }

        // Upsert here occurrs only if something is sent without an ID
        public bool HasUpsertData()
            => PublisherType != PublisherType.Unknown && !string.IsNullOrEmpty(MediaId) && Id <= 0;
    }

    public class PublisherApprovedMedia : IHasSettableId, IHasPublisherAccountId
    {
        public long Id { get; set; }
        public long PublisherAccountId { get; set; }
        public string Caption { get; set; }
        public PublisherContentType ContentType { get; set; }
        public long MediaFileId { get; set; }

        // RESPONSE only properties
        public string MediaUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public FileConvertStatus? ConvertStatus { get; set; }
    }

    public class PublisherMediaInfo : IHasSettableId
    {
        public long Id { get; set; }
        public string MediaId { get; set; }
        public string MediaType { get; set; }
        public DateTime CreatedAt { get; set; }
        public string MediaUrl { get; set; }
        public string ThumbnailUrl { get; set; }
    }

    public class PublisherMediaStat
    {
        public long PublisherMediaId { get; set; }
        public string Period { get; set; }
        public long EndTime { get; set; }
        public DateTime LastSyncedOn { get; set; }
        public List<PublisherStatValue> Stats { get; set; }
    }

    public class PublisherMediaAnalysis
    {
        public long ImageFacesCount { get; set; }
        public List<ValueWithConfidence> ImageLabels { get; set; }
        public List<ValueWithConfidence> ImageModerations { get; set; }
        public Dictionary<string, long> ImageFacesEmotions { get; set; }
        public double ImageFacesAvgAge { get; set; }
        public long ImageFacesMales { get; set; }
        public long ImageFacesFemales { get; set; }
        public long ImageFacesSmiles { get; set; }
        public long ImageFacesBeards { get; set; }
        public long ImageFacesMustaches { get; set; }
        public long ImageFacesEyeglasses { get; set; }
        public long ImageFacesSunglasses { get; set; }

        public List<ValueWithConfidence> TextEntities { get; set; }
        public bool IsPositiveSentiment { get; set; }
        public bool IsNegativeSentiment { get; set; }
        public bool IsNeutralSentiment { get; set; }
        public bool IsMixedSentiment { get; set; }
    }

    public class PublisherStatValue : IEquatable<PublisherStatValue>
    {
        public string Name { get; set; }
        public long Value { get; set; }

        public bool Equals(PublisherStatValue other)
            => other != null && Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);

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

            return obj is PublisherStatValue oobj && Equals(oobj);
        }

        public override int GetHashCode()
            => Name.GetHashCode();
    }
}
