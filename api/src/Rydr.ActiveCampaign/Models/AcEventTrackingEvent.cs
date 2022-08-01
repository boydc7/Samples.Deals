using System.Collections.Generic;

namespace Rydr.ActiveCampaign.Models
{
    public class AcEventTrackingEvent
    {
        public string Name { get; set; }
    }

    public class GetAcEventTrackingEvents : AcCollectionBase<AcEventTrackingEvent>
    {
        public IReadOnlyList<AcEventTrackingEvent> EventTrackingEvents { get; set; }
        public override IReadOnlyList<AcEventTrackingEvent> Data => EventTrackingEvents;
    }

    public class PostAcEventTrackingEvent
    {
        public AcEventTrackingEvent EventTrackingEvent { get; set; }
    }
}
