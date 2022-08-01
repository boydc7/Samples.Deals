using System;
using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynWorkspaceSubscription : DynItem
    {
        // Hash / Id: Workspace the subscription applies to (same as the base WorkspaceId)
        // Range / Edge: static typeId of DynItemType.WorkspaceSubscription | Unique StripeId for the subscription
        // RefId: Timestamp the subscription was created/updated
        // OwnerId: Unique Id of the worksapceSubscription
        // WorkspaceId: Workspace the subscription applies to
        // StatusId: SubscriptionType

        public static readonly string EdgeStartsWith = string.Concat((int)DynItemType.WorkspaceSubscription, "|");

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
        public string SubscriptionId
        {
            get => GetFinalEdgeSegment(EdgeId);
            set => EdgeId = value.StartsWithOrdinalCi(EdgeStartsWith)
                                ? value
                                : BuildEdgeId(value);
        }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public SubscriptionType SubscriptionType
        {
            get => Enum.TryParse<SubscriptionType>(StatusId, true, out var status)
                       ? status
                       : SubscriptionType.None;

            set => StatusId = value.ToString();
        }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long DynWorkspaceSubscriptionId
        {
            get => OwnerId;
            set => OwnerId = value;
        }

        public long Quantity { get; set; }
        public long UnitPriceCents { get; set; }

        [ExcludeNullValue]
        public string Interval { get; set; }

        [ExcludeNullValue]
        public long? IntervalCount { get; set; }

        [ExcludeNullValue]
        public string ProductId { get; set; }

        [ExcludeNullValue]
        public string PlanId { get; set; }

        [ExcludeNullValue]
        public string SubscriptionStatus { get; set; }

        [ExcludeNullValue]
        public string SubscriptionEmail { get; set; }

        public long BillingCycleAnchor { get; set; }
        public long SubscriptionStartedOn { get; set; }
        public long SubscriptionEndsOn { get; set; }
        public long SubscriptionTrialStartedOn { get; set; }
        public long SubscriptionTrialEndsOn { get; set; }
        public long SubscriptionCanceledOn { get; set; }

        public bool IsSystemSubscription { get; set; }

        [ExcludeNullValue]
        public string SubscriptionCustomerId { get; set; }

        public override AccessIntent DefaultAccessIntent() => AccessIntent.ReadOnly;

        public DateTime SubscriptionStartDate() => SubscriptionStartedOn >= DateTimeHelper.MinApplicationDateTs
                                                       ? SubscriptionStartedOn.ToDateTime()
                                                       : CreatedOn;

        public static string BuildEdgeId(string subscriptionId)
            => string.Concat(EdgeStartsWith, subscriptionId);
    }
}
