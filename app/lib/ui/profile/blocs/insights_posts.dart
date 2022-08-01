import 'dart:async';

import 'package:dio/dio.dart';
import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_insights_media.dart';
import 'package:rydr_app/models/responses/publisher_insights_media.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/services/publisher_insights.dart';
import 'package:rydr_app/ui/profile/widgets/insights_chart.dart';

class InsightsPostsBloc {
  final data = BehaviorSubject<InsightsPostsBlocResponse>();

  String lastPostsIndex0 = '';
  String lastPostsIndex1 = '';
  String lastPostsIndex2 = '';

  PublisherAccount _profile;
  PublisherInsightsMediaResponse _response;
  List<PublisherInsightsMedia> _mediaSorted;
  List<PublisherInsightsMedia> _mediaCurrent;
  List<PublisherInsightsMedia> _media0;
  List<PublisherInsightsMedia> _media1;
  List<PublisherInsightsMedia> _media2;

  dispose() {
    data.close();
  }

  Future<void> loadData(PublisherAccount profile, bool forceRefresh) async {
    _profile = profile;

    _response = await PublisherInsightsService.getMediaInsights(
      profile.id,
      contentType: PublisherContentType.post,
      forceRefresh: forceRefresh,
    );

    if (_response.error == null && _response.models.isEmpty == false) {
      /// keep a sorted list of media
      _mediaSorted = _response.models
        ..sort((a, b) => a.mediaCreatedOn.compareTo(b.mediaCreatedOn));

      /// set names of labels for last 5, 10, 100 depending on the count of posts
      /// we have on file for the current profile
      if (_response.models.length > 0) {
        lastPostsIndex0 = _response.models.length >= 5
            ? "Last 5"
            : _response.models.length == 1
                ? "Latest Post"
                : "Last ${_response.models.length} Posts";

        // we take one more than we need if available, but show only 5. this allows for the fading of the chart on the left
        _media0 = _response.models.length > 5
            ? _mediaSorted.skip(_response.models.length - 6).toList()
            : _mediaSorted.take(_response.models.length).toList();

        if (_response.models.length > 5) {
          lastPostsIndex1 = _response.models.length >= 10
              ? "Last 10"
              : "Last ${_response.models.length}";

          // we take one more than we need if available, but show only 5. this allows for the fading of the chart on the left
          _media1 = _response.models.length > 10
              ? _mediaSorted.skip(_response.models.length - 11).toList()
              : _mediaSorted.take(_response.models.length).toList();
        }

        if (_response.models.length > 10) {
          lastPostsIndex2 = _response.models.length >= 25
              ? "Last 25"
              : "Last ${_response.models.length}";

          // we take one more than we need if available, but show only 5. this allows for the fading of the chart on the left
          _media2 = _response.models.length > 25
              ? _mediaSorted.skip(_response.models.length - 26).toList()
              : _mediaSorted.take(_response.models.length).toList();
        }
      }
      toggleShowIndex(0);
    } else {
      data.sink.add(
        InsightsPostsBlocResponse(
          error: _response.error,
          showIndex: 0,
          hasResults: false,
        ),
      );
    }
  }

  void toggleShowIndex(int index) {
    _mediaCurrent = index == 0 ? _media0 : index == 1 ? _media1 : _media2;

    /// must have a min of 2 posts, otherwise we don't have enough data
    if (_mediaCurrent == null || _mediaCurrent.length < 2) {
      _mediaCurrent = [];

      data.sink.add(
        InsightsPostsBlocResponse(
          error: _response.error,
          showIndex: 0,
          hasResults: false,
        ),
      );

      return;
    }

    var mediaSummary = PublisherInsightsMediaSummary(
      _mediaCurrent,
      _profile.publisherMetrics.followedBy.toInt(),
      PublisherContentType.post,
    );

    data.sink.add(
      InsightsPostsBlocResponse(
        error: _response.error,
        followedBy: _profile.publisherMetrics.followedBy.toInt(),
        totalMedia: _mediaSorted.length,
        hasResults: true,
        dates: _mediaCurrent.map((m) => m.mediaCreatedOn).toList(),
        chartData: [
          ChartData(
            dataColor: chartDataColor.blue,
            data: mediaSummary.flSpotsImpressions,
            maxY: mediaSummary.maxImpressions.total,
            minY: mediaSummary.minImpressions.total,
          ),
          ChartData(
            dataColor: chartDataColor.teal,
            data: mediaSummary.flSpotsReach,
            maxY: mediaSummary.maxReach.total,
            minY: mediaSummary.minReach.total,
          ),
        ],
        mediaSummary: mediaSummary,
        mediaCurrent: _mediaSorted,
        showIndex: index,
      ),
    );
  }
}

class InsightsPostsBlocResponse {
  DioError error;
  List<PublisherInsightsMedia> mediaCurrent;
  PublisherInsightsMediaSummary mediaSummary;
  List<DateTime> dates;
  List<ChartData> chartData;
  int followedBy;
  int totalMedia;
  int showIndex;
  bool hasResults;

  InsightsPostsBlocResponse({
    this.error,
    this.mediaCurrent,
    this.mediaSummary,
    this.dates,
    this.chartData,
    this.followedBy,
    this.totalMedia,
    this.showIndex,
    this.hasResults,
  });
}
