using System.Collections.Generic;
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Dto.Users
{
    [Route("/humanloops/process", "POST")]
    public class PostProcessHumanLoop : RequestBase, IReturnVoid, IPost
    {
        public string LoopIdentifier { get; set; }
        public int HoursBack { get; set; }

        public static string GetRecurringJobId(string categoryIdentifier)
            => string.Concat("PostProcessHumanLoop|", categoryIdentifier);
    }

    [Route("/internal/humanloops/categorizebusiness", "POST")]
    public class PostHumanCategorizeBusiness : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
    }

    [Route("/internal/humanloops/businesscategory", "POST")]
    public class PostProcessHumanBusinessCategoryResponse : ProcessHumanResponseBase, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
    }

    [Route("/internal/humanloops/categorizecreator", "POST")]
    public class PostHumanCategorizeCreator : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
    }

    [Route("/internal/humanloops/creatorcategory", "POST")]
    public class PostProcessHumanCreatorCategoryResponse : ProcessHumanResponseBase, IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
    }

    [Route("/internal/humanloops/imagemoderation", "POST")]
    public class PostProcessHumanImageModerationResponse : ProcessHumanResponseBase
    {
        public long PublisherMediaId { get; set; }
    }

    public abstract class ProcessHumanResponseBase : RequestBase, IReturnVoid, IPost
    {
        public string HumanS3Uri { get; set; }
        public List<ValueWithConfidence> Inputs { get; set; }
        public List<ValueWithConfidence> Answers { get; set; }
        public int HumanResponders { get; set; }
    }
}
