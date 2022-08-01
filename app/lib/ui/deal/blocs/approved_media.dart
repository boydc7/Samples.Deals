import 'dart:io';

import 'package:rxdart/subjects.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/models/responses/publisher_approved_media.dart';
import 'package:rydr_app/services/publisher_approved_media.dart';

class DealApprovedMediaBloc {
  final _mediaResponse = BehaviorSubject<PublisherApprovedMediasResponse>();

  BehaviorSubject<PublisherApprovedMediasResponse> get mediaResponse =>
      _mediaResponse.stream;

  void dispose() {
    _mediaResponse.close();
  }

  void loadMedia(
    int dealId,
    List<PublisherApprovedMedia> existingMedia, [
    bool forceRefresh = false,
  ]) async {
    /// use existing media if we have it coming in
    if (existingMedia != null && existingMedia.isNotEmpty) {
      _mediaResponse.sink
          .add(PublisherApprovedMediasResponse.fromModels(existingMedia));

      return;
    }

    /// only load if we have a dealId
    if (dealId == null) {
      return;
    }

    _mediaResponse.sink
        .add(await PublisherApprovedMediaService.getPublisherApprovedMedias(
      skip: 0,
      take: 50,
      dealId: dealId,
      forceRefresh: forceRefresh,
    ));
  }

  Future<bool> upload(File file) async {
    final IntIdResponse intIdResponse =
        await PublisherApprovedMediaService.uploadPublisherApprovedMedia(file);

    if (intIdResponse == null || intIdResponse.hasError) {
      return false;
    }

    final PublisherApprovedMediaResponse res =
        await PublisherApprovedMediaService.getPublisherApprovedMedia(
            intIdResponse.model);

    if (_mediaResponse.value != null && _mediaResponse.value.models != null) {
      _mediaResponse.sink.add(
        PublisherApprovedMediasResponse.fromModels(
          List.from(_mediaResponse.value.models)..add(res.model),
        ),
      );
    } else {
      _mediaResponse.sink.add(
        PublisherApprovedMediasResponse.fromModels(
          [res.model],
        ),
      );
    }

    return true;
  }

  Future<bool> updateMedia(PublisherApprovedMedia media) async {
    final BasicVoidResponse publisherApprovedMediaResponse =
        await PublisherApprovedMediaService.updatePublisherApprovedMedia(media);

    if (publisherApprovedMediaResponse.hasError) {
      return false;
    }

    final int index = _mediaResponse.value.models.indexWhere(
      (m) => media.id == media.id,
    );

    _mediaResponse.sink.add(
      PublisherApprovedMediasResponse.fromModels(
        List.from(_mediaResponse.value.models)
          ..replaceRange(
            index,
            index + 1,
            [media],
          ),
      ),
    );

    return true;
  }

  void removeMedia(PublisherApprovedMedia media) {
    _mediaResponse.sink.add(
      PublisherApprovedMediasResponse.fromModels(
        List.from(_mediaResponse.value.models)
          ..removeWhere(
            (el) => el.id == media.id,
          ),
      ),
    );
  }
}
