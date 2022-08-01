using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Messages;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services
{
    public class AsyncDealMetricService : IDealMetricService
    {
        private static readonly string _dealMetricStreamName = RydrEnvironment.GetAppSetting("DealMetrics.StreamName", "dev_DealMetrics");

        private readonly ILog _log = LogManager.GetLogger("AsyncDealMetricService");
        private readonly IDataStreamProducer _dataStreamProducer;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IWorkspaceService _workspaceService;
        private readonly IDeferRequestsService _deferRequestsService;
        private readonly IUserService _userService;

        public AsyncDealMetricService(IDataStreamProducer dataStreamProducer,
                                      IPublisherAccountService publisherAccountService,
                                      IWorkspaceService workspaceService,
                                      IDeferRequestsService deferRequestsService,
                                      IUserService userService)
        {
            _dataStreamProducer = dataStreamProducer;
            _publisherAccountService = publisherAccountService;
            _workspaceService = workspaceService;
            _deferRequestsService = deferRequestsService;
            _userService = userService;
        }

        public void Measure(DealTrackMetricType type, DealResponse dealResponse, long otherPublisherAccountId = 0,
                            long workspaceId = 0, IHasUserLatitudeLongitude fromLocation = null, long userId = 0)
        {
            try
            {
                LocalAsyncTaskExecuter.DefaultTaskExecuter.ExecAsync(new DealMetricMeasurementData
                                                                     {
                                                                         MetricType = type,
                                                                         Timestamp = DateTimeHelper.UtcNowTsMs,
                                                                         WorkspaceId = workspaceId,
                                                                         DealResponse = dealResponse,
                                                                         OtherPublisherAccount = otherPublisherAccountId,
                                                                         UserLatitude = fromLocation?.UserLatitude,
                                                                         UserLongitude = fromLocation?.UserLongitude,
                                                                         UserId = userId
                                                                     },
                                                                     DoMeasureAsync,
                                                                     maxAttempts: 5);
            }
            catch(Exception ex)
            {
                _log.Exception(ex);
            }
        }

        public void Measure(DealTrackMetricType type, IReadOnlyList<DealResponse> dealResponses, long otherPublisherAccountId = 0,
                            long workspaceId = 0, IHasUserLatitudeLongitude fromLocation = null, long userId = 0)
        {
            try
            {
                LocalAsyncTaskExecuter.DefaultTaskExecuter.ExecAsync(new DealMetricMeasurementData
                                                                     {
                                                                         MetricType = type,
                                                                         Timestamp = DateTimeHelper.UtcNowTsMs,
                                                                         WorkspaceId = workspaceId,
                                                                         DealResponses = dealResponses,
                                                                         OtherPublisherAccount = otherPublisherAccountId,
                                                                         UserLatitude = fromLocation?.UserLatitude,
                                                                         UserLongitude = fromLocation?.UserLongitude,
                                                                         UserId = userId
                                                                     },
                                                                     DoMeasureAsync,
                                                                     maxAttempts: 5);
            }
            catch(Exception ex)
            {
                _log.Exception(ex);
            }
        }

        public void Measure(DealTrackMetricType type, DynDeal dynDeal, long otherPublisherAccountId = 0, long workspaceId = 0, long userId = 0)
        {
            try
            {
                LocalAsyncTaskExecuter.DefaultTaskExecuter.ExecAsync(new DealMetricMeasurementData
                                                                     {
                                                                         MetricType = type,
                                                                         Timestamp = DateTimeHelper.UtcNowTsMs,
                                                                         WorkspaceId = workspaceId,
                                                                         DynDeal = dynDeal,
                                                                         OtherPublisherAccount = otherPublisherAccountId,
                                                                         UserId = userId
                                                                     },
                                                                     DoMeasureAsync,
                                                                     maxAttempts: 5);
            }
            catch(Exception ex)
            {
                _log.Exception(ex);
            }
        }

        private async Task DoMeasureAsync(DealMetricMeasurementData dealMetricData)
        {
            // Send the actual measurement to the pipeline - only one of the deals, deal, dyndeal properties is used...
            if (!dealMetricData.DealResponses.IsNullOrEmptyReadOnly())
            {
                var streamValues = new List<string>(dealMetricData.DealResponses.Count);

                foreach (var dealValues in dealMetricData.DealResponses.Select(GetMetricsFromDealResponse))
                {
                    var streamValue = await GetStreamValueAsync(dealMetricData.Timestamp, dealMetricData.MetricType, dealMetricData.WorkspaceId,
                                                                dealMetricData.OtherPublisherAccount, dealMetricData.UserLatitude, dealMetricData.UserLongitude,
                                                                dealMetricData.UserId, dealValues);

                    streamValues.Add(streamValue);
                }

                await _dataStreamProducer.ProduceAsync(_dealMetricStreamName, streamValues, dealMetricData.DealResponses.Count);
            }
            else if (dealMetricData.DealResponse != null)
            {
                var dealValues = GetMetricsFromDealResponse(dealMetricData.DealResponse);

                var streamValue = await GetStreamValueAsync(dealMetricData.Timestamp, dealMetricData.MetricType, dealMetricData.WorkspaceId,
                                                            dealMetricData.OtherPublisherAccount, dealMetricData.UserLatitude, dealMetricData.UserLongitude,
                                                            dealMetricData.UserId, dealValues);

                await _dataStreamProducer.ProduceAsync(_dealMetricStreamName, streamValue);
            }
            else if (dealMetricData.DynDeal != null)
            {
                var dealValues = GetMetricsFromDynDeal(dealMetricData.DynDeal);

                var streamValue = await GetStreamValueAsync(dealMetricData.Timestamp, dealMetricData.MetricType, dealMetricData.WorkspaceId,
                                                            dealMetricData.OtherPublisherAccount, dealMetricData.UserLatitude, dealMetricData.UserLongitude,
                                                            dealMetricData.UserId, dealValues);

                await _dataStreamProducer.ProduceAsync(_dealMetricStreamName, streamValue);
            }

            // Map the latest location if valid
            if (dealMetricData.OtherPublisherAccount > 0 && dealMetricData.IsValidUserLatLon())
            {
                var locationMapEdgeId = DynItemMap.BuildEdgeId(DynItemType.AccountLocation, "LastLocation");
                var locationMappedItemEdgeId = string.Concat(Math.Round(dealMetricData.UserLatitude.Value, 4), "|", Math.Round(dealMetricData.UserLongitude.Value, 4));

                var existingLocationMap = await MapItemService.DefaultMapItemService.GetMapAsync(dealMetricData.OtherPublisherAccount, locationMapEdgeId);

                // Only write if we need to update the location
                if (existingLocationMap == null || !existingLocationMap.MappedItemEdgeId.EqualsOrdinalCi(locationMappedItemEdgeId))
                {
                    await MapItemService.DefaultMapItemService
                                        .PutMapAsync(new DynItemMap
                                                     {
                                                         Id = dealMetricData.OtherPublisherAccount,
                                                         EdgeId = locationMapEdgeId,
                                                         MappedItemEdgeId = locationMappedItemEdgeId,
                                                         ReferenceNumber = DateTimeHelper.UtcNowTs
                                                     });
                }
            }

            // External CRM tracking - track the primary metric data as sent
            await TrackExternalCrmDataAsync(dealMetricData.WorkspaceId, dealMetricData.UserId, dealMetricData.MetricType, dealMetricData.DynDeal);

            if (dealMetricData.DynDeal != null && dealMetricData.DynDeal.WorkspaceId != dealMetricData.WorkspaceId)
            { // And any inverted impact it may have for the deal creator as well if it's not the same workspace
                await TrackExternalCrmDataAsync(dealMetricData.DynDeal.WorkspaceId, dealMetricData.DynDeal.CreatedBy,
                                                dealMetricData.MetricType.ToDealOwnerInvertedMetricType(), dealMetricData.DynDeal);
            }
        }

        private async Task TrackExternalCrmDataAsync(long workspaceId, long userId, DealTrackMetricType metricType, DynDeal dynDeal)
        {
            if (metricType == DealTrackMetricType.Unknown)
            {
                return;
            }

            if (!metricType.HasExternalCrmIntegration() &&
                (metricType != DealTrackMetricType.Updated || dynDeal == null || dynDeal.DealStatus != DealStatus.Published))
            {
                return;
            }

            // Purposely give the default workspace user priority over the actual user - for team workspaces, roll up that info to the main account
            var userEmail = (workspaceId > GlobalItemIds.MinUserDefinedObjectId
                                 ? await _workspaceService.TryGetWorkspacePrimaryEmailAddressAsync(workspaceId)
                                 : null)
                            ??
                            (userId > GlobalItemIds.MinUserDefinedObjectId
                                 ? (await _userService.TryGetUserAsync(userId))?.Email
                                 : null);

            var eventData = metricType == DealTrackMetricType.Updated
                                ? dynDeal?.DealStatus.ToString()
                                : metricType.ToString();

            if (userEmail.HasValue() && eventData.HasValue())
            {
                _deferRequestsService.DeferFifoRequest(new PostTrackEventNotification
                                                       {
                                                           EventName = "DealTrackMetric",
                                                           EventData = eventData,
                                                           UserEmail = userEmail
                                                       }.WithAdminRequestInfo());
            }
        }

        private (long DealId, long DealPublisherAccountId, double DealValue, long DealPlaceId, long DealReceivePlaceId, DealStatus DealStatus, bool IsPrivateDeal, int DealMinAge, double dealDistanceMiles) GetMetricsFromDealResponse(DealResponse dealResponse)
            => (dealResponse.Deal.Id, dealResponse.Deal.PublisherAccountId, dealResponse.Deal.Value, dealResponse.Deal.Place?.Id ?? 0, dealResponse.Deal.ReceivePlace?.Id ?? 0,
                dealResponse.Deal.Status, dealResponse.Deal.IsPrivateDeal ?? false,
                dealResponse.Deal.Restrictions.IsNullOrEmpty()
                    ? 0
                    : dealResponse.Deal.Restrictions.FirstOrDefault(r => r.Type == DealRestrictionType.MinAge)?.Value.ToInteger().Gz(0) ?? 0,
                dealResponse.DistanceInMiles ?? 0);

        private (long DealId, long DealPublisherAccountId, double DealValue, long DealPlaceId, long DealReceivePlaceId, DealStatus DealStatus, bool IsPrivateDeal, int DealMinAge, double dealDistanceMiles) GetMetricsFromDynDeal(DynDeal deal)
            => (deal.DealId, deal.PublisherAccountId, deal.Value, deal.PlaceId, deal.ReceivePlaceId, deal.DealStatus, deal.IsPrivateDeal,
                deal.Restrictions.IsNullOrEmpty()
                    ? 0
                    : deal.Restrictions.FirstOrDefault(r => r.Type == DealRestrictionType.MinAge)?.Value.ToInteger().Gz(0) ?? 0,
                0);

        private async Task<string> GetStreamValueAsync(long timestamp, DealTrackMetricType type, long workspaceId, long otherPublisherAccountId,
                                                       double? fromLatitude, double? fromLongitude, long userId,
                                                       (long DealId, long DealPublisherAccountId, double DealValue, long DealPlaceId, long DealReceivePlaceId,
                                                           DealStatus DealStatus, bool IsPrivateDeal, int DealMinAge, double DealDistanceMiles) dealValues)
        {
            var dealPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(dealValues.DealPublisherAccountId);

            var otherPublisherAccount = otherPublisherAccountId > 0
                                            ? await _publisherAccountService.TryGetPublisherAccountAsync(otherPublisherAccountId)
                                            : null;

            // The format of the CSV record to send through the pipeline
            // Do not change this without coordinating changes in Kinesis, Glue, Redshift, etc. throughout the pipelines
            var csvString = string.Concat((timestamp > 9999999999
                                               ? timestamp
                                               : timestamp * 1000), ",",
                                          (int)type, ",",
                                          userId, ",",
                                          workspaceId.Gz(0), ",",
                                          fromLatitude.GetValueOrDefault(), ",",
                                          fromLongitude.GetValueOrDefault(), ",",
                                          dealValues.DealId, ",",
                                          dealValues.DealPublisherAccountId, ",",
                                          dealValues.DealValue, ",",
                                          dealValues.DealPlaceId, ",",
                                          dealValues.DealReceivePlaceId, ",",
                                          (int)dealValues.DealStatus, ",",
                                          dealValues.IsPrivateDeal
                                              ? 1
                                              : 0, ",",
                                          dealValues.DealMinAge, ",",
                                          dealValues.DealDistanceMiles, ",",
                                          dealPublisherAccount.AgeRangeMin, ",",
                                          dealPublisherAccount.AgeRangeMax, ",",
                                          (int)dealPublisherAccount.Gender, ",",
                                          otherPublisherAccount?.AgeRangeMin ?? 0, ",",
                                          dealPublisherAccount?.AgeRangeMax ?? 0, ",",
                                          (int?)otherPublisherAccount?.Gender ?? 0, ",",
                                          otherPublisherAccount?.Metrics?.GetValueOrDefault(PublisherMetricName.FollowedBy) ?? 0, ",",
                                          otherPublisherAccount?.Metrics?.GetValueOrDefault(PublisherMetricName.RecentStoryCount) ?? 0, ",",
                                          otherPublisherAccount?.Metrics?.GetValueOrDefault(PublisherMetricName.RecentMediaCount) ?? 0, ",",
                                          otherPublisherAccount?.Metrics?.GetValueOrDefault(PublisherMetricName.RecentStoryImpressions) ?? 0, ",",
                                          otherPublisherAccount?.Metrics?.GetValueOrDefault(PublisherMetricName.RecentMediaImpressions) ?? 0, ",",
                                          otherPublisherAccount?.Metrics?.GetValueOrDefault(PublisherMetricName.RecentStoryReach) ?? 0, ",",
                                          otherPublisherAccount?.Metrics?.GetValueOrDefault(PublisherMetricName.RecentMediaReach) ?? 0, ",",
                                          otherPublisherAccount?.Metrics?.GetValueOrDefault(PublisherMetricName.StoryEngagementRating) ?? 0, ",",
                                          otherPublisherAccount?.Metrics?.GetValueOrDefault(PublisherMetricName.RecentEngagementRating) ?? 0, ",",
                                          otherPublisherAccount?.Metrics?.GetValueOrDefault(PublisherMetricName.RecentTrueEngagementRating) ?? 0, ",",
                                          otherPublisherAccount?.PublisherAccountId ?? 0);

            return csvString;
        }

        private class DealMetricMeasurementData : IHasUserLatitudeLongitude
        {
            public DealTrackMetricType MetricType { get; set; }
            public long Timestamp { get; set; }
            public long WorkspaceId { get; set; }
            public DynDeal DynDeal { get; set; }
            public DealResponse DealResponse { get; set; }
            public IReadOnlyList<DealResponse> DealResponses { get; set; }
            public long OtherPublisherAccount { get; set; }
            public double? UserLatitude { get; set; }
            public double? UserLongitude { get; set; }
            public long UserId { get; set; }
        }
    }
}
