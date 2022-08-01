import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/services/instagram.dart';
import 'package:rydr_app/services/public_api.dart';
import 'package:rydr_app/ui/connect/utils.dart';

class BusinessFinderBloc {
  final _resultsInsta = BehaviorSubject<List<PublisherAccount>>();
  final _search = BehaviorSubject<String>();

  Stream<String> get search => _search.stream;

  BehaviorSubject<List<PublisherAccount>> get resultsInsta =>
      _resultsInsta.stream;

  dispose() {
    _resultsInsta.close();
    _search.close();
  }

  void setSearch(String query) => _search.sink.add(query);

  Future<void> query(String query) async {
    _resultsInsta.sink.add(null);

    if (query.trim().length > 0) {
      InstagramService.queryPeopleOnInstagram(query)
          .then((res) => _resultsInsta.sink.add(res));
    }
  }

  /// TODO: change to use instagram/softconnect
  Future<bool> linkBusiness(String igUsername) async {
    final PublisherAccount igProfile =
        await PublicApiService.getIgProfileToRydr(igUsername);

    if (igProfile != null) {
      final PublisherAccount linkedProfile = await ConnectUtils.linkUser(
        igProfile,
        PublisherType.instagram,
        RydrAccountType.business,
      );

      return linkedProfile != null;
    }

    return false;
  }
}
