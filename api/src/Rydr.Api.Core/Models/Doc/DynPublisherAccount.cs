using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynPublisherAccount : DynItem, ICanBeAuthorizedById, IHasPublisherAccountId, IHasRydrAccountType, IHaveMappedEdgeId
{
    // Hash/Id = PublisherAccountId
    // Range/EdgeId = PublisherType/AccountId combo (see GetEdgeId below)
    // ReferenceId = PublisherAccountId
    // OwnerId:
    // WorkspaceId: Workspace that created the account
    // StatusId:
    // Map: Id / Id.ToEdgeId() (essentially a map of id and id, to allow GetItems() calls on only id to get back to list of id/edges to get)
    // Map: DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, dynPublisherAccount.EdgeId) and .ToLongHashCode -

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long PublisherAccountId
    {
        get => Id;
        set => Id = value;
    }

    [Required]
    public string AccountId { get; set; }

    [Required]
    public PublisherType PublisherType { get; set; }

    [Required]
    public PublisherAccountType AccountType { get; set; }

    public RydrAccountType RydrAccountType { get; set; }

    [ExcludeNullValue]
    public string FullName { get; set; }

    [ExcludeNullValue]
    public string UserName { get; set; }

    [ExcludeNullValue]
    public string Email { get; set; }

    [ExcludeNullValue]
    public string Description { get; set; }

    [ExcludeNullValue]
    public string AlternateAccountId { get; set; } // AccountId from another platform - i.e. the Instagram accountId for a Facebook biz ig page

    [ExcludeNullValue]
    public string PageId { get; set; }

    [ExcludeNullValue]
    public Dictionary<string, double> Metrics { get; set; }

    [ExcludeNullValue]
    public HashSet<Tag> Tags { get; set; }

    public long LastEngagementMetricsUpdatedOn { get; set; }
    public long LastMediaSyncedOn { get; set; }
    public long LastMediaSyncTransientFailureOn { get; set; }
    public long LastProfileSyncedOn { get; set; }
    public long PrimaryPlaceId { get; set; }

    [ExcludeNullValue]
    public int? MaxDelinquentAllowed { get; set; }

    [ExcludeNullValue]
    public string ProfilePicture { get; set; }

    [ExcludeNullValue]
    public string Website { get; set; }

    public bool IsSyncDisabled { get; set; }
    public bool OptInToAi { get; set; }
    public int FailuresSinceLastSuccess { get; set; }

    public int AgeRangeMin { get; set; }
    public int AgeRangeMax { get; set; }
    public GenderType Gender { get; set; }

    public string GetEdgeId() => BuildEdgeId(PublisherType, AccountId);
    public static string BuildEdgeId(PublisherType type, string accountId) => string.Concat(type, "|", accountId);
    public long AuthorizeId() => PublisherAccountId;

    public bool IsSoftLinked => (AccountId != null && AccountId.StartsWithOrdinalCi("rydr_")) || !PublisherType.IsWritablePublisherType();

    public bool IsBasicLink => !PublisherType.IsWritablePublisherType() && AccountId != null && !AccountId.StartsWithOrdinalCi("rydr_");

    private sealed class DynPublisherAccountIdEqualityComparer : IEqualityComparer<DynPublisherAccount>
    {
        public bool Equals(DynPublisherAccount x, DynPublisherAccount y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null))
            {
                return false;
            }

            if (ReferenceEquals(y, null))
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return x.PublisherAccountId == y.PublisherAccountId;
        }

        public int GetHashCode(DynPublisherAccount obj)
            => obj.PublisherAccountId.GetHashCode();
    }

    public static IEqualityComparer<DynPublisherAccount> DefaultComparer { get; } = new DynPublisherAccountIdEqualityComparer();
}
