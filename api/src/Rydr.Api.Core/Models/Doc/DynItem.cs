using System;
using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.FbSdk.Extensions;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    [References(typeof(DynItemEdgeIdGlobalIndex))]
    [References(typeof(DynItemTypeOwnerSpaceReferenceGlobalIndex))]
    [References(typeof(DynItemIdTypeReferenceGlobalIndex))]
    public class DynItem : BaseDynLongModel, ICanBeRecordLookup, IEquatable<DynItem>, IDynItem
    {
        private string _edgeId;

        private DynItemType? _dynItemType;

        [Required]
        [HashKey]
        public override long Id { get; set; }

        [Required]
        [RangeKey]
        public string EdgeId
        {
            get => _edgeId;
            set
            {
                _edgeId = value;
                _toString = null;
            }
        }

        [Required]
        public int TypeId { get; set; }

        [ExcludeNullValue]
        public string ReferenceId { get; set; }

        private long? _expiresAt;

        [ExcludeNullValue]
        public long? ExpiresAt
        {
            get => _expiresAt;
            set => _expiresAt = value.HasValue && value > 0
                                    ? value
                                    : null;
        }

        [ExcludeNullValue]
        public string StatusId { get; set; }

        [ExcludeNullValue]
        public string TypeOwnerSpace
        {
            get => OwnerId > 0 || WorkspaceId > 0
                       ? BuildTypeOwnerSpaceHash(TypeId, OwnerId.Gz(WorkspaceId))
                       : null;

            // ReSharper disable once ValueParameterNotUsed
            set
            {
                /* Ignore, required for serialization though */
            }
        }

        [Required]
        public string TypeReference
        {
            get => BuildTypeReferenceHash(TypeId, ReferenceId);

            // ReSharper disable once ValueParameterNotUsed
            set
            {
                /* Ignore, required for serialization though */
            }
        }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public DynItemType DynItemType
        {
            get => _dynItemType ?? (_dynItemType = TypeId.TryToEnum(DynItemType.Null)).Value;
            set
            {
                TypeId = (int)value;
                _dynItemType = value;
            }
        }

        private string _toString;

        public override string ToString()
            => _toString ??= Id <= 0 || EdgeId == null
                                 ? null
                                 : string.Concat(Id, "|", EdgeId);

        public bool Equals(DynItem other)
            => other != null && Id == other.Id && EdgeId.Equals(other.EdgeId, StringComparison.OrdinalIgnoreCase);

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

            return obj is DynItem oobj && Equals(oobj);
        }

        public override int GetHashCode()
            => ToString().GetHashCode();

        public static string BuildTypeOwnerSpaceHash(DynItemType dynItemType, long ownerOrWorkspaceId) => BuildTypeOwnerSpaceHash((int)dynItemType, ownerOrWorkspaceId);
        public static string BuildTypeReferenceHash(DynItemType dynItemType, string reference) => BuildTypeReferenceHash((int)dynItemType, reference);

        private static string BuildTypeOwnerSpaceHash(int itemType, long ownerOrWorkspaceId) => string.Concat(itemType, "|", ownerOrWorkspaceId);
        private static string BuildTypeReferenceHash(int itemType, string reference) => string.Concat(itemType, "|", reference);

        public static string GetFirstEdgeSegment(string edgeId) => edgeId.Left(edgeId.IndexOf('|'));
        public static string GetFinalEdgeSegment(string edgeId) => edgeId.Right(edgeId.Length - edgeId.LastIndexOf('|') - 1);
    }
}
