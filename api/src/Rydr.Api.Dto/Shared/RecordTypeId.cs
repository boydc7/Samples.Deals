using System;
using Rydr.Api.Dto.Enums;

// ReSharper disable MemberCanBePrivate.Global

namespace Rydr.Api.Dto.Shared
{
    public class RecordTypeId : IEquatable<RecordTypeId>
    {
        private string _toString;

        public RecordTypeId() { }

        public RecordTypeId(RecordType type, long id)
        {
            Type = type;
            Id = id;
        }

        public RecordType Type { get; set; }
        public long Id { get; set; }

        public override string ToString()
            => _toString ??= GetIdString(Type, Id);

        public static string GetIdString(RecordType forType, long forId)
            => forType != RecordType.Unknown && forId > 0
                   ? string.Concat(forType.ToString(), "-", forId)
                   : null;

        public bool Equals(RecordTypeId other)
            => other != null && Id == other.Id && Type == other.Type;

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

            return obj is RecordTypeId oobj && Equals(oobj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)2166136261;

                hashCode = (hashCode * 16777619) ^ Id.GetHashCode();
                hashCode = (hashCode * 16777619) ^ Type.GetHashCode();

                return hashCode;
            }
        }
    }
}
