import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/services/publisher_media.dart';

class ProfileMediaBloc {
  final _mediaResponse = BehaviorSubject<ProfileMediaBlocResponse>();

  dispose() {
    _mediaResponse.close();
  }

  Stream<ProfileMediaBlocResponse> get mediaResponse => _mediaResponse.stream;

  void loadMedia(int profileId) async {
    final PublisherMediasResponse mediaResponse =
        await PublisherMediaService.getPublisherMedias(
      forUserId: profileId,
      liveMediaOnly: true,
    );

    if (mediaResponse.error == null &&
        mediaResponse.models != null &&
        mediaResponse.models.isEmpty == false) {
      _mediaResponse.sink.add(ProfileMediaBlocResponse(
          mediaResponse,
          mediaResponse.models
              .where((m) => m.contentType == PublisherContentType.post)
              .toList(),
          mediaResponse.models
              .where((m) => m.contentType == PublisherContentType.story)
              .toList()));
    } else {
      _mediaResponse.sink.add(ProfileMediaBlocResponse(mediaResponse, [], []));
    }
  }
}

class ProfileMediaBlocResponse {
  PublisherMediasResponse response;
  List<PublisherMedia> posts;
  List<PublisherMedia> stories;

  ProfileMediaBlocResponse(
    this.response,
    this.posts,
    this.stories,
  );
}
