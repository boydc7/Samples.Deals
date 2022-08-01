import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/services/publisher_media.dart';

class RequestCompleteSelectedCount {
  final int stories;
  final int posts;

  RequestCompleteSelectedCount(this.stories, this.posts);
}

class RequestCompleteBloc {
  final _mediaResponse = BehaviorSubject<PublisherMediasResponse>();
  final _selectedMedia = BehaviorSubject<List<PublisherMedia>>();
  final _userPosts = BehaviorSubject<List<PublisherMedia>>();
  final _userStories = BehaviorSubject<List<PublisherMedia>>();
  final _selectedCount = BehaviorSubject<RequestCompleteSelectedCount>();
  final _errorAddMedia = BehaviorSubject<bool>();

  dispose() {
    _mediaResponse.close();
    _selectedMedia.close();
    _userPosts.close();
    _userStories.close();
    _selectedCount.close();
    _errorAddMedia.close();
  }

  BehaviorSubject<PublisherMediasResponse> get mediaResponse =>
      _mediaResponse.stream;

  BehaviorSubject<List<PublisherMedia>> get selectedMedia =>
      _selectedMedia.stream;
  BehaviorSubject<List<PublisherMedia>> get userPosts => _userPosts.stream;
  BehaviorSubject<List<PublisherMedia>> get userStories => _userStories.stream;
  BehaviorSubject<RequestCompleteSelectedCount> get selectedCount =>
      _selectedCount.stream;
  BehaviorSubject<bool> get errorAddMedia => _errorAddMedia.stream;

  void loadMedia() async {
    final PublisherMediasResponse res =
        await PublisherMediaService.getPublisherMedias(
      syncedOnly: false,
      forceRefresh: true,
    );

    if (res.error == null && res.models.isNotEmpty) {
      _userPosts.sink.add(res.models
          .where((media) => media.contentType == PublisherContentType.post)
          .toList());

      _userStories.sink.add(res.models
          .where((media) => media.contentType == PublisherContentType.story)
          .toList());
    }
    _mediaResponse.sink.add(res);
  }

  void setMedia(PublisherMedia media) {
    if (media.isPreBizAccountConversionMedia) {
      _errorAddMedia.sink.add(true);
    } else {
      /// toggle selected flag on the media
      media.selected = !media.selected;

      List<PublisherMedia> items = selectedMedia.value ?? [];
      int stories = _selectedCount.value?.stories ?? 0;
      int posts = _selectedCount.value?.posts ?? 0;

      /// add/remove media from selected list
      if (media.selected) {
        items.add(media);

        stories += media.contentType == PublisherContentType.story ? 1 : 0;
        posts += media.contentType == PublisherContentType.post ? 1 : 0;
      } else {
        items.removeWhere((PublisherMedia m) => m.mediaId == media.mediaId);

        stories -= media.contentType == PublisherContentType.story ? 1 : 0;
        posts -= media.contentType == PublisherContentType.post ? 1 : 0;
      }

      _selectedMedia.sink.add(items);
      _selectedCount.sink.add(RequestCompleteSelectedCount(stories, posts));

      /// update posts or stories with newly selected media
      if (media.contentType == PublisherContentType.story) {
        List<PublisherMedia> stories = userStories.value;
        stories
            .firstWhere((PublisherMedia m) => m.mediaId == media.mediaId)
            .selected = media.selected;

        _userStories.sink.add(stories);
      } else {
        List<PublisherMedia> posts = userPosts.value;
        posts
            .firstWhere((PublisherMedia m) => m.mediaId == media.mediaId)
            .selected = media.selected;

        _userPosts.sink.add(posts);
      }
    }
  }
}
