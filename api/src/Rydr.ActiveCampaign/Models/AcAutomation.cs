using System.Collections.Generic;

namespace Rydr.ActiveCampaign.Models
{
    public class AcAutomation
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class AcContactAutomation
    {
        public string Contact { get; set; }
        public string Automation { get; set; }
    }

    public class PostAcContactAutomation
    {
        public AcContactAutomation ContactAutomation { get; set; }
    }

    public class GetAcAutomations : AcCollectionBase<AcAutomation>
    {
        public IReadOnlyList<AcAutomation> Automations { get; set; }

        public override IReadOnlyList<AcAutomation> Data => Automations;
    }
}
