import 'package:rxdart/rxdart.dart';
import 'package:rydrworkspaces/models/enums/publisher_media.dart';
import 'package:rydrworkspaces/models/responses/publisher_media.dart';
import 'package:rydrworkspaces/services/publisher_media.dart';

class ProfileCardBloc {
  final _mediaResponse = BehaviorSubject<PublisherMediaResponse>();

  dispose() {
    _mediaResponse.close();
  }

  BehaviorSubject<PublisherMediaResponse> get mediaResponse =>
      _mediaResponse.stream;

  void loadMedia(int profileId) async {
    _mediaResponse.sink.add(
      await PublisherMediaService.getPublisherMedia(
        forUserId: profileId,
        contentTypes: [PublisherContentType.post],
        limit: 12,
      ),
    );
  }
}
