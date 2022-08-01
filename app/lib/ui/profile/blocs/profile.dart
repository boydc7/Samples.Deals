import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/publisher_account.dart';
import 'package:rydr_app/models/tag.dart';

import 'package:rydr_app/services/publisher_account.dart';

import 'package:rydr_app/app/state.dart';

class ProfileBloc {
  final _userResponse = BehaviorSubject<PublisherAccountResponse>();
  final _tags = BehaviorSubject<List<Tag>>();

  int _profileId;

  dispose() {
    _userResponse.close();
    _tags.close();
  }

  BehaviorSubject<PublisherAccountResponse> get userResponse => _userResponse;
  BehaviorSubject<List<Tag>> get tags => _tags.stream;

  Future<void> loadProfile(
    int profileIdToLoad, [
    bool forceRefresh = false,
  ]) async {
    _profileId = profileIdToLoad ?? appState.currentProfile.id;
    PublisherAccountResponse res;

    res = await PublisherAccountService.getPubAccount(
      _profileId,
      forceRefresh: forceRefresh,
    );

    /// if successful, and we have a profile that we should be synching
    /// but if we as of yet don't have a last synched date - and - this was not
    /// a full refresh, then let's refresh it here again to see if we're done synching now
    if (!res.hasError &&
        res.model != null &&
        res.model.isAccountFull &&
        res.model.lastSyncedOnDisplay == null &&
        !forceRefresh) {
      res = await PublisherAccountService.getPubAccount(
        _profileId,
        forceRefresh: true,
      );
    }

    _userResponse.sink.add(res);
    _tags.sink.add(!res.hasError && res.model != null ? res.model.tags : []);
  }

  Future<bool> updateTags(List<Tag> tags) async {
    final BasicVoidResponse res =
        await PublisherAccountService.updateTags(_profileId, tags);

    if (!res.hasError) {
      _tags.sink.add(tags);
    }

    return !res.hasError;
  }
}
