import 'dart:async';
import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/publisher_media_vision.dart';
import 'package:rydr_app/models/requests/publisher_media_analysis.dart';
import 'package:rydr_app/models/responses/publisher_media_analysis.dart';
import 'package:rydr_app/models/responses/publisher_media_vision.dart';
import 'package:rydr_app/services/publisher_account.dart';
import 'package:rydr_app/services/publisher_media_analysis.dart';
import 'package:rydr_app/services/publisher_media_vision.dart';

class InsightsMediaAnalysisBloc {
  final _analysisResponse =
      BehaviorSubject<PublisherAccountMediaVisionResponse>();
  final _mediaQueryResponse =
      BehaviorSubject<PublisherMediaAnalysisQueryResponse>();
  final _mediaLoading = BehaviorSubject<bool>();
  final _query = BehaviorSubject<String>();

  PublisherAccount _profile;
  List<PublisherMedia> _mediaLoaded;

  InsightsMediaAnalysisBloc(PublisherAccount profile) {
    _profile = profile;
  }

  dispose() {
    _analysisResponse.close();
    _mediaQueryResponse.close();
    _mediaLoading.close();
    _query.close();
  }

  List<PublisherMedia> get mediaLoaded => _mediaLoaded;

  Stream<PublisherAccountMediaVisionResponse> get analysisResponse =>
      _analysisResponse.stream;
  Stream<PublisherMediaAnalysisQueryResponse> get mediaQueryResponse =>
      _mediaQueryResponse.stream;
  Stream<bool> get mediaLoading => _mediaLoading.stream;
  Stream<String> get query => _query.stream;

  Future<bool> setAcceptedTerms() async {
    final res = await PublisherAccountService.optInToAi(true);

    if (res.error == null) {
      /// update the current profile in state & stream
      appState.setCurrentProfileOptInToAi(true);

      loadData();
      return true;
    }

    return false;
  }

  void setMediaLoading(bool val) => _mediaLoading.sink.add(val);

  Future<void> loadData([bool forceRefresh = false]) async {
    /// if the user him/herself is viewing their own Ai then check the stream flag
    /// on their own/current profile, otherwise check appstate function to see if
    /// this business has access to the creators ai based on biz subscription and creator setting
    if ((_profile.id == appState.currentProfile.id &&
            appState.currentProfileOptInToAi.value == true) ||
        appState.isAiAvailable(_profile)) {
      _analysisResponse.sink.add(
        await PublisherAccountMediaVisionService.getPublisherMediaVision(
          _profile.id,
          forceRefresh: forceRefresh,
        ),
      );
    }
  }

  void querySection(PublisherAccountMediaVisionSectionSearchDescriptor request,
      [PublisherContentType contentType]) async {
    _query.sink.add(null);
    _mediaQueryResponse.sink.add(
        await PublisherMediaAnalysisService.queryPublisherMediaAnalysis(
            _profile.id,
            PublisherMediaAnalysisQuery.fromSearchDescriptor(
                request, contentType)));

    /// keep the list of loaded media in a local prop so we can use it
    /// to pass to the overlay viewer for viewing media details
    _mediaLoaded = _mediaQueryResponse.value?.models;
  }

  void queryTag(String tag, [PublisherContentType contentType]) async {
    _query.sink.add(tag);

    _mediaQueryResponse.sink.add(null);

    _mediaQueryResponse.sink
        .add(await PublisherMediaAnalysisService.queryPublisherMediaAnalysis(
            _profile.id,
            PublisherMediaAnalysisQuery(
              query: tag,
              contentType: contentType,
            )));

    /// keep the list of loaded media in a local prop so we can use it
    /// to pass to the overlay viewer for viewing media details
    _mediaLoaded = _mediaQueryResponse.value?.models;
  }
}
