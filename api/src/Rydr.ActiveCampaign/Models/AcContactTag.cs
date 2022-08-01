using System.Collections.Generic;

namespace Rydr.ActiveCampaign.Models
{
    public class AcContactTag
    {
        public string Contact { get; set; }
        public string Tag { get; set; }
        public string Id { get; set; }
    }

    public class GetAcContactTags : AcCollectionBase<AcContactTag>
    {
        public IReadOnlyList<AcContactTag> ContactTags { get; set; }
        public override IReadOnlyList<AcContactTag> Data => ContactTags;
    }

    public class PostAcContactTag
    {
        public AcContactTag ContactTag { get; set; }
    }
}
