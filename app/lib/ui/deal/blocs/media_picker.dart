import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/services/public_api.dart';
import 'package:rydr_app/services/publisher_media.dart';

class DealMediaPickerBloc {
  final _mediaResponse = BehaviorSubject<PublisherMediasResponse>();

  dispose() {
    _mediaResponse.close();
  }

  Stream<PublisherMediasResponse> get mediaResponse => _mediaResponse.stream;

  void loadMedia() async => _mediaResponse.sink
          .add(await PublisherMediaService.getPublisherMedias(contentTypes: [
        PublisherContentType.post,
        PublisherContentType.media,
      ]));

  Future<PublisherMedia> getImageFromPostUrl(String postUrl) async {
    /// parse the image from the desired post and return back a temporary
    /// publisher media object filled with the base props & vars to then create
    /// an actual one on the server to then link to the deal
    PublisherMedia tmpMedia = await PublicApiService.getIgMediaFromPost(
      appState.currentProfile.id,
      appState.currentProfile.userName,
      postUrl,
    );

    if (tmpMedia != null) {
      /// create publisher media on the server, gets back id
      /// which we'll add to the temp object and return back
      final IntIdResponse idResponse =
          await PublisherMediaService.addPublisherMedia(tmpMedia);

      if (!idResponse.hasError) {
        return tmpMedia..id = idResponse.model;
      }
    }

    return null;
  }
}
