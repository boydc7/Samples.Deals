import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/models/responses/publisher_insights_growth.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/services/publisher_insights.dart';

class InsightsProfileBloc {
  final _growthResponse = BehaviorSubject<PublisherInsightsGrowthResponse>();
  final _showIndex = BehaviorSubject<int>();

  dispose() {
    _growthResponse.close();
    _showIndex.close();
  }

  Stream<PublisherInsightsGrowthResponse> get growthResponse =>
      _growthResponse.stream;
  Stream<int> get showIndex => _showIndex.stream;

  void setShowIndex(int value) => _showIndex.sink.add(value);

  Future<void> loadData(PublisherAccount profile,
      [bool forceRefresh = false]) async {
    _growthResponse.sink.add(await PublisherInsightsService.getGrowth(
      profile.id,
      forceRefresh: forceRefresh,
    ));
  }
}
