using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynWorkspacePublisherSubscription : DynItem, IHasPublisherAccountId
    {
        // Hash / Id: DynWorkspace.Id the publisher subscription applies to
        // Range / Edge: static typeId of DynItemType.WorkspacePublisherSubscription | PublisherAccountId the subscription applies to within the workspace
        // RefId: Unixtimestamp of when the subscription was created/updated
        // OwnerId: DynWorkspaceSubscription.Id for the related subscription
        // WorkspaceId: DynWorkspace.Id the subscription applies to
        // StatusId: SubscriptionType (as a string)

        public static readonly string EdgeStartsWith = string.Concat((int)DynItemType.WorkspacePublisherSubscription, "|");

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long SubscriptionWorkspaceId
        {
            get => Id;
            set
            {
                Id = value;
                WorkspaceId = value;
            }
        }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PublisherAccountId
        {
            get => GetFinalEdgeSegment(EdgeId).ToLong();
            set => EdgeId = BuildEdgeId(value);
        }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long DynWorkspaceSubscriptionId
        {
            get => OwnerId;
            set => OwnerId = value;
        }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public SubscriptionType SubscriptionType
        {
            get => StatusId.TryToEnum(SubscriptionType.None);
            set => StatusId = value.ToString();
        }

        [ExcludeNullValue]
        public string StripeCustomerId { get; set; }

        [ExcludeNullValue]
        public string ActiveCampaignCustomerId { get; set; }

        [ExcludeNullValue]
        public string StripeSubscriptionId { get; set; }

        [ExcludeNullValue]
        public int? CustomMonthlyFeeCents { get; set; }

        [ExcludeNullValue]
        public int? CustomPerPostFeeCents { get; set; }

        public override AccessIntent DefaultAccessIntent() => AccessIntent.ReadOnly;

        public static string BuildEdgeId(long forPublisherAccountId) => string.Concat(EdgeStartsWith, forPublisherAccountId);

        // Ephemeral value used to determine if this needs to be written or not
        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public bool IsDirty { get; set; }

        public void Dirty() => IsDirty = true;
    }
}
