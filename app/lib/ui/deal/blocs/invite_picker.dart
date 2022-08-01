import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/requests/creator_search.dart';
import 'package:rydr_app/models/responses/publisher_account.dart';
import 'package:rydr_app/services/instagram.dart';
import 'package:rydr_app/services/search.dart';
import 'package:rydr_app/ui/deal/widgets/shared/constants.dart';

class InvitePickerBloc {
  final _pendingInvites = BehaviorSubject<List<PublisherAccount>>();
  final _resultsRydr = BehaviorSubject<List<PublisherAccount>>();
  final _resultsInsta = BehaviorSubject<List<PublisherAccount>>();
  final _search = BehaviorSubject<String>();

  int _skip = 0;
  int _take = 25;
  bool _isLoading = false;
  bool _hasMore = false;
  String _query = "";
  int _existingDealId;

  InvitePickerBloc({
    List<PublisherAccount> existingUsers,
    int dealId,
  }) {
    _existingDealId = dealId;

    _addInvites(existingUsers ?? []);
  }

  bool get hasMore => _hasMore;
  bool get isLoading => _isLoading;

  Stream<String> get search => _search.stream;

  BehaviorSubject<List<PublisherAccount>> get pendingInvites =>
      _pendingInvites.stream;
  BehaviorSubject<List<PublisherAccount>> get resultsRydr =>
      _resultsRydr.stream;
  BehaviorSubject<List<PublisherAccount>> get resultsInsta =>
      _resultsInsta.stream;

  int get inviteCount => pendingInvites.value.length;
  bool get maxInvitesReached => inviteCount >= dealMaxInvites;
  List<PublisherAccount> get invitedUsers => pendingInvites.value;

  dispose() {
    _pendingInvites.close();
    _resultsRydr.close();
    _resultsInsta.close();
    _search.close();
  }

  void setSearch(String query) {
    _query = query;
    _search.sink.add(query);
  }

  Future<void> refresh() => query(_query, reset: true, forceRefresh: true);

  Future<void> queryMore() => query(_query, reset: false);

  Future<void> query(
    String query, {
    bool reset = true,
    bool forceRefresh = false,
  }) async {
    List<PublisherAccount> rydrResults = [];
    if (_isLoading) {
      return;
    }

    /// null response will trigger loading ui if we're resetting
    if (reset) {
      _resultsRydr.sink.add(null);
      _resultsInsta.sink.add(null);

      if (query.trim().length > 0) {
        InstagramService.queryPeopleOnInstagram(query)
            .then((res) => _resultsInsta.sink.add(res));
      }
    }

    /// set loading flag and either reset or use existing skip
    _isLoading = true;
    _skip = reset ? 0 : _skip;

    final PublisherAccountsResponse res = await SearchService.queryCreators(
      CreatorSearchRequest(
        skip: _skip,
        take: _take,
        search: query,
        excludeInvitesDealId: _existingDealId,
        // excludePublisherAccountIds: excludePublisherAccountIds,
      ),
      true,
    );

    /// set the hasMore flag if we have >=take records coming back
    _hasMore = res.models != null &&
        res.models.isNotEmpty &&
        res.models.length == _take;

    if (res.error == null) {
      if (_skip > 0) {
        final List<PublisherAccount> existing = _resultsRydr.value;
        existing.addAll(res.models);

        rydrResults = existing;
      } else {
        rydrResults = res.models;
      }
    }

    if (!_resultsRydr.isClosed) {
      _resultsRydr.sink.add(rydrResults);
    }

    /// increment skip if we have no error, and set loading to false
    _skip = res.error == null && _hasMore ? _skip + _take : _skip;
    _isLoading = false;
  }

  void addInvite(PublisherAccount user) {
    List<PublisherAccount> list = List.from(pendingInvites.value);

    if (list
            .where((PublisherAccount u) => u.userName == user.userName)
            .length ==
        0) {
      list.add(user);
    }

    _addInvites(list);
  }

  void removeInvite(PublisherAccount user) {
    _addInvites(List.from(pendingInvites.value)
      ..removeWhere((PublisherAccount u) => u.userName == user.userName));
  }

  void _addInvites(List<PublisherAccount> users) => _pendingInvites.add(users);
}
