import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';
import 'package:rydrworkspaces/models/responses/publisher_media_analysis.dart';
import 'package:rydrworkspaces/services/publisher_media_analysis.dart';

class InsightsMediaAnalysisItemBloc {
  final _optedInToAi = BehaviorSubject<bool>();
  final _mediaResponse = BehaviorSubject<PublisherMediaAnalysisResponse>();
  final _mediaLoading = BehaviorSubject<bool>();
  final _mediaCaptionExpanded = BehaviorSubject<bool>();

  bool _aiEnabled;

  InsightsMediaAnalysisItemBloc(PublisherAccount profile) {
    /// use appstate current profile if the profile we want is
    /// the current user him/herself, and then if its a business viewing this profile
    /// then they must have a subscription in order to view AI for any profiles

    /// TODO:
    //_aiEnabled = appState.isAiAvailable(profile);
    _aiEnabled = true;

    _optedInToAi.sink.add(_aiEnabled == true);
  }

  dispose() {
    _optedInToAi.close();
    _mediaResponse.close();
    _mediaLoading.close();
    _mediaCaptionExpanded.close();
  }

  bool get aiEnabled => _aiEnabled;
  Stream<bool> get optedInToAi => _optedInToAi.stream;
  Stream<PublisherMediaAnalysisResponse> get mediaResponse =>
      _mediaResponse.stream;
  Stream<bool> get mediaLoading => _mediaLoading.stream;
  Stream<bool> get mediaCaptionExpanded => _mediaCaptionExpanded.stream;

  void setMediaCaptionExpanded(bool val) => _mediaCaptionExpanded.sink.add(val);

  void loadMedia(int mediaId) async {
    if (_mediaResponse.value == null) {
      try {
        _mediaLoading.sink.add(true);
        _mediaResponse.sink.add(
            await PublisherMediaAnalysisService.getPublisherMediaAnalysis(
                mediaId));
        _mediaLoading.sink.add(false);
      } catch (ex) {
        /// supress
      }
    }
  }
}
