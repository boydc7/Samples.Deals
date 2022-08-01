import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/notification.dart';
import 'package:rydr_app/models/responses/notifications.dart';
import 'package:rydr_app/services/notifications.dart';

class NotificationsBloc {
  final _notificationsResponse = BehaviorSubject<NotificationsResponse>();

  int _skip = 0;
  int _take = 25;
  bool _isLoading = false;
  bool _hasMore = false;

  dispose() {
    _notificationsResponse.close();
  }

  bool get hasMore => _hasMore;
  bool get isLoading => _isLoading;

  Stream<NotificationsResponse> get notificationsResponse =>
      _notificationsResponse.stream;

  /// make this a 'future' to be compatible with refreshindicator list view widget
  Future<void> loadList(bool showForAll, [bool reset = false]) async {
    if (_isLoading) {
      return;
    }

    /// set loading flag and either reset or use existing skip
    _isLoading = true;
    _skip = reset ? 0 : _skip;

    NotificationsResponse res = await NotificationService.queryNotifications(
      skip: _skip * _take,
      take: _take,
      forceRefresh: reset,
    );

    /// set the hasMore flag if we have >=take records coming back
    _hasMore = res.models != null &&
        res.models.isNotEmpty &&
        res.models.length == _take;

    if (_skip > 0 && res.error == null) {
      final List<AppNotification> existing =
          _notificationsResponse.value.models;
      existing.addAll(res.models);

      /// update the requests on the response before adding to stream
      res = NotificationsResponse.fromModels(existing);
    }

    if (!_notificationsResponse.isClosed) {
      _notificationsResponse.sink.add(res);
    }

    /// increment skip if we have no error, and set loading to false
    _skip = res.error == null && _hasMore ? _skip + _take : _skip;
    _isLoading = false;
  }

  void markAllAsRead() {
    /// mark all notifications as read on the server
    NotificationService.markAsRead();
  }
}
