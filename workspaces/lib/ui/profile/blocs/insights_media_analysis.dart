import 'dart:async';
import 'package:rxdart/rxdart.dart';
import 'package:rydrworkspaces/models/enums/publisher_media.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';
import 'package:rydrworkspaces/models/publisher_media.dart';
import 'package:rydrworkspaces/models/publisher_media_vision.dart';
import 'package:rydrworkspaces/models/requests/publisher_media_analysis.dart';
import 'package:rydrworkspaces/models/responses/base.dart';
import 'package:rydrworkspaces/models/responses/publisher_media_analysis.dart';
import 'package:rydrworkspaces/models/responses/publisher_media_vision.dart';
import 'package:rydrworkspaces/services/publisher_account.dart';
import 'package:rydrworkspaces/services/publisher_media_analysis.dart';
import 'package:rydrworkspaces/services/publisher_media_vision.dart';

class InsightsMediaAnalysisBloc {
  final _optedInToAi = BehaviorSubject<bool>();
  final _analysisResponse =
      BehaviorSubject<PublisherAccountMediaVisionResponse>();
  final _mediaQueryResponse =
      BehaviorSubject<PublisherMediaAnalysisQueryResponse>();
  final _mediaLoading = BehaviorSubject<bool>();
  final _query = BehaviorSubject<String>();

  int _profileId;
  List<PublisherMedia> _mediaLoaded;

  InsightsMediaAnalysisBloc(PublisherAccount profile) {
    _profileId = profile.id;

    /// TODO:
    //_optedInToAi.sink.add(appState.isAiAvailable(profile));
    _optedInToAi.sink.add(true);
  }

  dispose() {
    _optedInToAi.close();
    _analysisResponse.close();
    _mediaQueryResponse.close();
    _mediaLoading.close();
    _query.close();
  }

  List<PublisherMedia> get mediaLoaded => _mediaLoaded;

  Stream<bool> get optedInToAi => _optedInToAi.stream;
  Stream<PublisherAccountMediaVisionResponse> get analysisResponse =>
      _analysisResponse.stream;
  Stream<PublisherMediaAnalysisQueryResponse> get mediaQueryResponse =>
      _mediaQueryResponse.stream;
  Stream<bool> get mediaLoading => _mediaLoading.stream;
  Stream<String> get query => _query.stream;

  Future<bool> setAcceptedTerms() async {
    /// TODO:
    int profileId = 0;
    final BaseResponse res =
        await PublisherAccountService.optInToAi(true, profileId);

    if (res.error == null) {
      _optedInToAi.sink.add(true);

      /// TODO:
      /// update the current profile in state
      //appState.currentProfile.optInToAi = true;

      loadData();
      return true;
    }

    return false;
  }

  void setMediaLoading(bool val) => _mediaLoading.sink.add(val);

  Future<void> loadData() async {
    if (_optedInToAi.value == true) {
      _analysisResponse.sink.add(
          await PublisherAccountMediaVisionService.getPublisherMediaVision(
              _profileId));
    }
  }

  void querySection(PublisherAccountMediaVisionSectionSearchDescriptor request,
      [PublisherContentType contentType]) async {
    _query.sink.add(null);
    _mediaQueryResponse.sink.add(
        await PublisherMediaAnalysisService.queryPublisherMediaAnalysis(
            _profileId,
            PublisherMediaAnalysisQuery.fromSearchDescriptor(
                request, contentType)));

    /// keep the list of loaded media in a local prop so we can use it
    /// to pass to the overlay viewer for viewing media details
    _mediaLoaded = _mediaQueryResponse.value?.medias;
  }

  void queryTag(String tag, [PublisherContentType contentType]) async {
    _query.sink.add(tag);

    _mediaQueryResponse.sink.add(null);

    _mediaQueryResponse.sink
        .add(await PublisherMediaAnalysisService.queryPublisherMediaAnalysis(
            _profileId,
            PublisherMediaAnalysisQuery(
              query: tag,
              contentType: contentType,
            )));

    /// keep the list of loaded media in a local prop so we can use it
    /// to pass to the overlay viewer for viewing media details
    _mediaLoaded = _mediaQueryResponse.value?.medias;
  }
}
