using System.Collections.Generic;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Messages;

namespace Rydr.Api.Core.Models.Supporting
{
    public class DealRequestExtended
    {
        public DynDealRequest DealRequest { get; set; }
        public List<DynPublisherMedia> CompletionMedia { get; set; }
        public List<DynDealRequestStatusChange> StatusChanges { get; set; }
        public Dictionary<long, DynPublisherMediaStat> LifetimeStats { get; set; }
        public DialogMessage LastMessage { get; set; }
    }
}
