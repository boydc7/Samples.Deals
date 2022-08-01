using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynWorkspacePublisherSubscriptionDiscount : DynItem, IHasPublisherAccountId
    {
        // Hash / Id: DynWorkspaceSubscription.DynWorkspaceSubscriptionId the discount is for
        // Range / Edge: static typeId of DynItemType.WorkspacePublisherSubscriptionDiscount | PublisherAccountId the subscription applies to within the workspace | SubscriptionUsageType the discount applies to
        // RefId: Unixtimestamp of when the discount was created/updated
        // OwnerId:
        // WorkspaceId: DynWorkspace.Id the subscription applies to
        // StatusId:

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long WorkspaceSubscriptionId
        {
            get => Id;
            set => Id = value;
        }

        public long PublisherAccountId { get; set; }
        public SubscriptionUsageType UsageType { get; set; }
        public int PercentOff { get; set; }
        public long StartsOn { get; set; }
        public long EndsOn { get; set; }

        public override AccessIntent DefaultAccessIntent() => AccessIntent.ReadOnly;

        public static string BuildEdgeId(long forPublisherAccountId, SubscriptionUsageType usageType)
            => string.Concat((int)DynItemType.WorkspacePublisherSubscriptionDiscount, "|", forPublisherAccountId, "|", (int)usageType);
    }
}
