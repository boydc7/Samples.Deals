using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynDailyStatSnapshot : DynDailyStatBase
    {
        private static readonly int _edgePrefixIndex = DynItemType.DailyStatSnapshot.ToString().Length + 1;

        public DynDailyStatSnapshot()
        {
            TypeId = (int)DynItemType.DailyStatSnapshot;
        }

        protected override int EdgePrefixIndex => _edgePrefixIndex;

        public static string BuildEdgeId(long dayTimestamp) => string.Concat(DynItemType.DailyStatSnapshot, "|", dayTimestamp);
    }

    public class DynDailyStat : DynDailyStatBase
    {
        private static readonly int _edgePrefixIndex = DynItemType.DailyStat.ToString().Length + 1;

        public DynDailyStat()
        {
            TypeId = (int)DynItemType.DailyStat;
        }

        protected override int EdgePrefixIndex => _edgePrefixIndex;

        public static string BuildEdgeId(long dayTimestamp) => string.Concat(DynItemType.DailyStat, "|", dayTimestamp);
    }

    public abstract class DynDailyStatBase : DynItem, IHasPublisherAccountId
    {
        // Hash/Id = Id of the thing the stat is associated with (i.e. PublisherAccount.Id, PublisherMedia.Id, etc.)
        // Range/Edge = Prefix of DynItemType.DailyStat, |, and Unix timestamp of the day the stat info is for
        // RefId = null
        // OwnerId = PublisherAccountId of the thing the stat is forj
        // Expires = 65 days from creation

        protected abstract int EdgePrefixIndex { get; }

        public static string BuildEdgeId<T>(long dayTimestamp)
            where T : DynDailyStatBase
        {
            switch (typeof(T).Name)
            {
                case nameof(DynDailyStatSnapshot):
                    return DynDailyStatSnapshot.BuildEdgeId(dayTimestamp);

                case nameof(DynDailyStat):
                    return DynDailyStat.BuildEdgeId(dayTimestamp);

                default:
                    throw new ArgumentOutOfRangeException(nameof(T));
            }
        }

        private long _dayTimestamp;

        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long DayTimestamp => _dayTimestamp > 0
                                        ? _dayTimestamp
                                        : (_dayTimestamp = EdgeId.Substring(EdgePrefixIndex).ToLong());

        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PublisherAccountId
        {
            get => OwnerId;
            set => OwnerId = value;
        }

        public RecordType AssociatedType { get; set; }

        public Dictionary<string, DailyStatValue> Stats { get; set; }
    }
}
