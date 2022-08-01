import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/services/publisher_media.dart';

class RequestUserProfileCardBloc {
  final _mediaResponse = BehaviorSubject<PublisherMediasResponse>();

  RequestUserProfileCardBloc(Deal deal) {
    /// if we're gonna show the full creator details, then load up
    /// their most recent media to show as part of their stats
    if (deal.request.canViewRequestersProfile) {
      PublisherMediaService.getPublisherMedias(
        forUserId: deal.request.publisherAccount.id,
        contentTypes: [PublisherContentType.post],
        limit: 6,
      ).then((res) {
        _mediaResponse.sink.add(res);
      });
    }
  }

  dispose() {
    _mediaResponse.close();
  }

  BehaviorSubject<PublisherMediasResponse> get mediaResponse =>
      _mediaResponse.stream;
}
