using System;
using System.Collections.Generic;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Services.Internal;
using Rydr.FbSdk.Extensions;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynItemMap : IEquatable<DynItemMap>, IHasLongId
    {
        [Required]
        [HashKey]
        public long Id { get; set; }

        [Required]
        [RangeKey]
        public string EdgeId { get; set; }

        [ExcludeNullValue]
        public string MappedItemEdgeId { get; set; }

        [ExcludeNullValue]
        public long? ReferenceNumber { get; set; }

        [ExcludeNullValue]
        public Dictionary<string, string> Items { get; set; }

        public long ExpiresAt { get; set; }

        private string _toString;

        public override string ToString()
            => _toString ??= Id <= 0 || EdgeId == null
                                 ? null
                                 : string.Concat(Id, "|", EdgeId);

        public bool Equals(DynItemMap other)
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

            return obj is DynItemMap oobj && Equals(oobj);
        }

        public override int GetHashCode()
            => ToString().GetHashCode();

        public bool IsExpired() => ExpiresAt > 0 && ExpiresAt <= DateTimeHelper.UtcNowTs;

        public static string BuildEdgeId(DynItemType dynItemType, string edgeId) => string.Concat((int)dynItemType, "|", edgeId);

        public static string GetFirstEdgeSegment(string edgeId) => edgeId.Left(edgeId.IndexOf('|'));
        public static string GetFinalEdgeSegment(string edgeId) => edgeId.Right(edgeId.Length - edgeId.LastIndexOf('|') - 1);

        public DynamoId GetMappedDynamoId() => new DynamoId(Id, MappedItemEdgeId);
    }
}
