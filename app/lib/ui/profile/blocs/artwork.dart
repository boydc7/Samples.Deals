import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/publisher_approved_media.dart';
import 'package:rydr_app/services/publisher_approved_media.dart';

class ArtworkBloc {
  final _artworkResponse = BehaviorSubject<PublisherApprovedMediasResponse>();

  int _skip = 0;
  int _take = 25;
  bool _isLoading = false;
  bool _hasMore = false;

  dispose() {
    _artworkResponse.close();
  }

  bool get hasMore => _hasMore;
  bool get isLoading => _isLoading;

  Stream<PublisherApprovedMediasResponse> get artworkResponse =>
      _artworkResponse.stream;

  /// make this a 'future' to be compatible with refreshindicator list view widget
  Future<void> loadList(
    ListPageArguments args, {
    bool reset = false,
    bool forceRefresh = false,
  }) async {
    if (_isLoading) {
      return;
    }

    /// null response will trigger loading ui if we're resetting
    if (reset) {
      _artworkResponse.sink.add(null);
    }

    /// set loading flag and either reset or use existing skip
    _isLoading = true;
    _skip = reset ? 0 : _skip;

    PublisherApprovedMediasResponse res =
        await PublisherApprovedMediaService.getPublisherApprovedMedias(
      skip: _skip,
      take: _take,
      forceRefresh: forceRefresh,
    );

    /// set the hasMore flag if we have >=take records coming back
    _hasMore = res.models != null &&
        res.models.isNotEmpty &&
        res.models.length == _take;

    if (_skip > 0 && res.error == null) {
      final List<PublisherApprovedMedia> existing =
          _artworkResponse.value.models;
      existing.addAll(res.models);

      /// update the requests on the response before adding to stream
      res = PublisherApprovedMediasResponse.fromModels(existing);
    }

    if (!_artworkResponse.isClosed) {
      _artworkResponse.sink.add(res);
    }

    /// increment skip if we have no error, and set loading to false
    _skip = res.error == null && _hasMore ? _skip + _take : _skip;
    _isLoading = false;
  }
}
