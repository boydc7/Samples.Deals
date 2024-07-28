// ReSharper disable RedundantUsingDirective

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Dto.Enums;
using ServiceStack;
using ServiceStack.Logging;

#pragma warning disable 162

namespace Rydr.Api.Core.Services.Internal;

public class SqlDataStreamProducer : IDataStreamProducer
{
    private static readonly string _dealMetricStreamName = RydrEnvironment.GetAppSetting("DealMetrics.StreamName", "dev_DealMetrics").Coalesce("dev_DealMetrics");

    private static readonly HashSet<DealTrackMetricType> _dealMetricsToTrack = new()
                                                                               {
                                                                                   DealTrackMetricType.Impressed,
                                                                                   DealTrackMetricType.Clicked,
                                                                                   DealTrackMetricType.XClicked,
                                                                                   DealTrackMetricType.Created,
                                                                                   DealTrackMetricType.Updated,
                                                                                   DealTrackMetricType.Requested,
                                                                                   DealTrackMetricType.Invited,
                                                                                   DealTrackMetricType.RequestApproved,
                                                                                   DealTrackMetricType.RequestDenied,
                                                                                   DealTrackMetricType.RequestCompleted,
                                                                                   DealTrackMetricType.RequestCancelled,
                                                                                   DealTrackMetricType.RequestRedeemed
                                                                               };

    private static readonly Dictionary<string, SqlStreamProducerInfo> _streamNameSqlInfoMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            {
                _dealMetricStreamName, new SqlStreamProducerInfo
                                       {
                                           TableName = "DealMetrics",
                                           InsertColumns =
                                               "Timestamp, MetricType, UserId, WorkspaceId, Latitude, Longitude, DealId, DealPublisherAccountId, DealValue, DealPlaceId, DealReceivePlaceId, DealStatus, IsPrivateDeal, DealMinAge, DealDistanceMiles, DealPublisherAgeRangeMin, DealPublisherAgeRangeMax, DealPublisherGender, RequestAgeRangeMin, RequestAgeRangeMax, RequestGender, RequestFollowedBy, RequestRecentStories, RequestRecentMedias, RequestRecentStoryImpressions, RequestRecentMediaImpressions, RequestRecentStoryReach, RequestRecentMediaReach, RequestStoryEngagementRating, RequestEngagementRating, RequestTrueEngagementRating, RequestPublisherAccountId",
                                           Predicate = v => _dealMetricsToTrack.Contains(GetMetricTypeFromStreamValue(v))
                                       }
            }
        };

    private readonly IRydrDataService _rydrDataService;

    public SqlDataStreamProducer(IRydrDataService rydrDataService)
    {
        _rydrDataService = rydrDataService;
    }

    public async Task ProduceAsync(string streamName, string value)
    {
        var metricStreamName = IsDealMetricStream(streamName)
                                   ? _dealMetricStreamName
                                   : streamName;

        if (!metricStreamName.HasValue() || !_streamNameSqlInfoMap.ContainsKey(metricStreamName))
        {
            return;
        }

        var streamTrackInfo = _streamNameSqlInfoMap[metricStreamName];

        if (streamTrackInfo.Predicate != null && !streamTrackInfo.Predicate(value))
        {
            return;
        }

        await _rydrDataService.ExecAdHocAsync(string.Concat(@"
INSERT    IGNORE INTO ", streamTrackInfo.TableName, @"
          (", streamTrackInfo.InsertColumns, @")
VALUES    (", value, @");
"));
    }

    public async Task ProduceAsync(string streamName, IEnumerable<string> values, int hintCount = 50)
    {
        var metricStreamName = IsDealMetricStream(streamName)
                                   ? _dealMetricStreamName
                                   : streamName;

        if (!metricStreamName.HasValue() || !_streamNameSqlInfoMap.ContainsKey(metricStreamName))
        {
            return;
        }

        var streamTrackInfo = _streamNameSqlInfoMap[metricStreamName];

        foreach (var valueBatch in values.ToLazyBatchesOf(25, streamTrackInfo.Predicate))
        {
            var insertValues = string.Join("),(", valueBatch);

            if (insertValues.IsNullOrEmpty())
            {
                continue;
            }

            await _rydrDataService.ExecAdHocAsync(string.Concat(@"
INSERT    IGNORE INTO ", streamTrackInfo.TableName, @"
          (", streamTrackInfo.InsertColumns, @")
VALUES    (", insertValues, @");
"));
        }
    }

    private class SqlStreamProducerInfo
    {
        public string TableName { get; set; }
        public string InsertColumns { get; set; }
        public Func<string, bool> Predicate { get; set; }
    }

    private bool IsDealMetricStream(string streamName)
    {
        if (!streamName.HasValue())
        {
#if LOCALDEBUG
            return true;
#endif

            return false;
        }

        return streamName.EndsWithOrdinalCi("_DealMetrics");
    }

    private static DealTrackMetricType GetMetricTypeFromStreamValue(string streamValue)
    {
        var firstIndex = streamValue.IndexOf(',');
        var secondIndex = streamValue.IndexOf(',', firstIndex + 1);

        if (firstIndex > 0 && secondIndex > 0 && secondIndex > firstIndex)
        {
            var typeValue = streamValue.Substring(firstIndex + 1, (secondIndex - firstIndex - 1));

            var typeAsInt = typeValue.ToInt();

            if (typeAsInt > 0)
            {
                return (DealTrackMetricType)typeAsInt;
            }
        }

        return DealTrackMetricType.Unknown;
    }
}
