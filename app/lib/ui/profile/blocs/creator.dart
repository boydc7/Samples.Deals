import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/responses/publisher_account_stats.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/services/publisher_account_stats.dart';

import 'package:rydr_app/services/publisher_media.dart';

class CreatorBloc {
  final _accountStatsWithResponse =
      BehaviorSubject<PublisherAccountStatsWithResponse>();
  final _mediaResponse = BehaviorSubject<PublisherMediasResponse>();
  final _showWorkHistory = BehaviorSubject<bool>.seeded(false);
  final _scrollOffset = BehaviorSubject<double>();

  CreatorBloc() {
    /// TO DO:
  }

  dispose() {
    _accountStatsWithResponse.close();
    _mediaResponse.close();
    _showWorkHistory.close();
    _scrollOffset.close();
  }

  BehaviorSubject<PublisherMediasResponse> get mediaResponse =>
      _mediaResponse.stream;
  BehaviorSubject<PublisherAccountStatsWithResponse>
      get accountStatsWithResponse => _accountStatsWithResponse.stream;
  BehaviorSubject<bool> get showWorkHistory => _showWorkHistory.stream;
  BehaviorSubject<double> get scrollOffset => _scrollOffset.stream;

  void toggleWorkHistory() {
    _showWorkHistory.sink.add(!showWorkHistory.value);
  }

  void loadProfile(int profileIdToLoad) async {
    final PublisherMediasResponse mediaResponse =
        await PublisherMediaService.getPublisherMedias(
      forUserId: profileIdToLoad,
      contentTypes: [PublisherContentType.post],
      limit: 10,
    );

    _mediaResponse.sink.add(mediaResponse);
  }

  void loadWorkHistory(int profileIdToLoad) async {
    _accountStatsWithResponse.sink.add(
        await PublisherAccountStatsService.getAccountStatsWith(
            appState.currentProfile.id, profileIdToLoad));
  }

  void setScrollOffset(double value) => _scrollOffset.sink.add(value);
}
