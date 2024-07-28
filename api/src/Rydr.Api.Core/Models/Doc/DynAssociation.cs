using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynAssociation : DynItem, IEquatable<DynAssociation>
{
    // Hash / Id : FromRecordId
    // Range / Edge: ToRecordId
    // RefId:
    // OwnerId:
    // WorkspaceId:
    // StatusId:

    [Required]
    public int DynIdRecordType { get; set; }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public RecordType IdRecordType
    {
        get => DynIdRecordType.TryToEnum(RecordType.Unknown);
        set => DynIdRecordType = (int)value;
    }

    [Required]
    public int DynEdgeRecordType { get; set; }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public RecordType EdgeRecordType
    {
        get => DynEdgeRecordType.TryToEnum(RecordType.Unknown);
        set => DynEdgeRecordType = (int)value;
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long FromRecordId
    {
        get => Id;
        set => Id = value;
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long ToRecordId
    {
        get => EdgeId.ToLong(0);
        set => EdgeId = value.ToEdgeId();
    }

    public bool Equals(DynAssociation other)
        => other != null && Id == other.Id && EdgeId.EqualsOrdinal(other.EdgeId);

    public override bool Equals(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is DynAssociation oobj && Equals(oobj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int)2166136261;

            // ReSharper disable once NonReadonlyMemberInGetHashCode
            hashCode = (hashCode * 16777619) ^ Id.GetHashCode();
            hashCode = (hashCode * 16777619) ^ EdgeId.GetHashCode();

            return hashCode;
        }
    }
}
