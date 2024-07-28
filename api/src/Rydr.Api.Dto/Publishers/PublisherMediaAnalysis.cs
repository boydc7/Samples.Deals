using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Search;
using Rydr.Api.Dto.Shared;
using ServiceStack;

namespace Rydr.Api.Dto.Publishers;

[Route("/publisheracct/{PublisherIdentifier}/mediavision", "GET")]
public class GetPublisherAccountMediaVision : RequestBase, IGet, IReturn<OnlyResultResponse<PublisherAccountMediaVision>>, IHasPublisherAccountIdentifier
{
    public string PublisherIdentifier { get; set; }
}

[Route("/publisheracct/{PublisherIdentifier}/mediaanalysis", "GET")]
public class GetPublisherAccountMediaAnalysis : RequestBase, IGet, IReturn<OnlyResultResponse<PublisherAccountMediaAnalysis>>, IHasPublisherAccountIdentifier
{
    public string PublisherIdentifier { get; set; }
}

[Route("/publisheracct/{PublisherIdentifier}/mediasearch", "GET")]
public class GetPublisherAccountMediaSearch : BaseSearch, IGet, IReturn<OnlyResultsResponse<PublisherMedia>>, IHasPublisherAccountIdentifier, IHasSkipTake
{
    public string PublisherIdentifier { get; set; }
    public int Skip { get; set; }
    public PublisherContentType ContentType { get; set; }
    public List<string> Sentiments { get; set; }
    public LongRange FacesRange { get; set; }
    public LongRange FacesAvgAgeRange { get; set; }
    public LongRange FacesMalesRange { get; set; }
    public LongRange FacesFemalesRange { get; set; }
    public LongRange FacesSmilesRange { get; set; }
    public LongRange FacesBeardsRange { get; set; }
    public LongRange FacesMustachesRange { get; set; }
    public LongRange FacesEyeglassesRange { get; set; }
    public LongRange FacesSunglassesRange { get; set; }
    public bool SortRecent { get; set; }
}

[Route("/analyze/medias", "POST")]
public class PostAnalyzePublisherMedias : RequestBase, IPost, IReturn<StatusSimpleResponse>
{
    public List<long> PublisherAccountIds { get; set; }
}

[Route("/analyze/{publisheraccountid}/media", "POST")]
public class PostAnalyzePublisherMedia : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public bool ProcessTopics { get; set; }
    public bool Force { get; set; }
    public bool Rebuild { get; set; }
    public bool Reanalyze { get; set; }

    public static string GetRecurringJobId(long publisherAccountId)
        => string.Concat("AnalyzePublisherMedia|", publisherAccountId);
}

[Route("/internal/analyze/{publisheraccountid}/text", "POST")]
public class PostAnalyzePublisherText : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public long PublisherMediaId { get; set; }
    public string Path { get; set; }
    public bool Reanalyze { get; set; }
}

[Route("/internal/analyze/{publisheraccountid}/image", "POST")]
public class PostAnalyzePublisherImage : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public long PublisherMediaId { get; set; }
    public string Path { get; set; }
    public bool Reanalyze { get; set; }
}

[Route("/internal/analyze/{publisheraccountid}/aggstats", "POST")]
public class PostAnalyzePublisherAggStats : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public string EdgeId { get; set; }
    public long CountTextQueued { get; set; }
    public long CountImageQueued { get; set; }
    public long CountPostsAnalyzed { get; set; }
    public long CountStoriesAnalyzed { get; set; }
}

[Route("/internal/analyze/{publisheraccountid}/topics", "POST")]
public class PostAnalyzePublisherTopics : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public string Path { get; set; }
}

[Route("/internal/analyze/medialabels", "PUT")]
public class PutUpdateMediaLabel : RequestBase, IPut, IReturn<OnlyResultResponse<ValueWithConfidence>>
{
    public string Name { get; set; }
    public string ParentName { get; set; }
    public bool? Ignore { get; set; }
    public string RewriteName { get; set; }
    public string RewriteParent { get; set; }
}

[Route("/internal/analyze/updated", "POST")]
public class PublisherMediaAnalysisUpdated : RequestBase, IPost, IReturnVoid
{
    public long PublisherMediaId { get; set; }
    public string PublisherMediaAnalysisEdgeId { get; set; }
}

public class PublisherAccountMediaVision
{
    public long TotalPostsAnalyzed { get; set; }
    public long TotalStoriesAnalyzed { get; set; }
    public long TodayPostsAnalyzed { get; set; }
    public long TodayStoriesAnalyzed { get; set; }
    public long PostDailyLimit { get; set; }
    public long StoryDailyLimit { get; set; }
    public PublisherAccountMediaVisionSection Notable { get; set; } // trim media
    public PublisherAccountMediaVisionSection Stories { get; set; }
    public PublisherAccountMediaVisionSection Posts { get; set; }
    public PublisherAccountMediaVisionSection Captions { get; set; }
    public List<PublisherMedia> RecentPosts { get; set; }
    public List<PublisherMedia> RecentStories { get; set; }
}

public class PublisherAccountMediaVisionSection
{
    public string Title { get; set; }
    public long TotalCount { get; set; }
    public List<PublisherAccountMediaVisionSectionItem> Items { get; set; }
}

public class PublisherAccountMediaVisionSectionItem
{
    public string Title { get; set; }
    public string SubTitle { get; set; }
    public long Count { get; set; }
    public IReadOnlyList<PublisherMediaInfo> Medias { get; set; }
    public PublisherAccountMediaVisionSectionSearchDescriptor SearchDescriptor { get; set; }
    public IReadOnlyList<string> SearchTags { get; set; }
}

public class PublisherAccountMediaVisionSectionSearchDescriptor : IHasSkipTake
{
    public string Query { get; set; }
    public PublisherContentType ContentType { get; set; }
    public List<string> Sentiments { get; set; }
    public LongRange FacesRange { get; set; }
    public IntRange FacesAvgAgeRange { get; set; }
    public IntRange FacesMalesRange { get; set; }
    public IntRange FacesFemalesRange { get; set; }
    public IntRange FacesSmilesRange { get; set; }
    public IntRange FacesBeardsRange { get; set; }
    public IntRange FacesMustachesRange { get; set; }
    public IntRange FacesEyeglassesRange { get; set; }
    public IntRange FacesSunglassesRange { get; set; }
    public bool SortRecent { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public long PublisherAccountId { get; set; }
}

public class PublisherAccountMediaAnalysis
{
    public PublisherAccountMediaAnalysisResult AccountAnalysis { get; set; }
    public PublisherAccountMediaAnalysisResult StoryAnalysis { get; set; }
    public PublisherAccountMediaAnalysisResult PostAnalysis { get; set; }
}

public class PublisherAccountMediaAnalysisResult
{
    public PublisherContentType? ContentType { get; set; }

    public long PostsAnalyzed { get; set; }
    public long StoriesAnalyzed { get; set; }

    public long ImageCount { get; set; }
    public long ImagesQueued { get; set; }
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

    public long TextCount { get; set; }
    public long TextsQueued { get; set; }
    public List<ValueWithConfidence> TextEntities { get; set; }

    public double TextPositiveSentimentPercentage { get; set; }
    public double TextNegativeSentimentPercentage { get; set; }
    public double TextNeutralSentimentPercentage { get; set; }
    public double TextMixedSentimentPercentage { get; set; }
    public long TotalSentimentOccurrences { get; set; }
}
