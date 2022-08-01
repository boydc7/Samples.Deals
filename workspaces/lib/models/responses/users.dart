import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';

class UsersResponse {
  final PublisherAccount currentUser;
  final List<PublisherAccount> users;
  final DioError error;

  UsersResponse(this.currentUser, this.users, this.error);

  UsersResponse.fromResponse(
      PublisherAccount current, List<PublisherAccount> users)
      : currentUser = current,
        users = users,
        error = null;

  UsersResponse.withError(DioError error)
      : currentUser = null,
        users = null,
        error = error;
}

class FbIgUsersResponse {
  final List<PublisherAccount> users;
  final DioError error;

  FbIgUsersResponse(this.users, this.error);

  FbIgUsersResponse.fromResponse(Map<String, dynamic> json)
      : users = json['results'] != null
            ? json['results']
                .map(
                    (dynamic d) => PublisherAccount.fromInstaBusinessAccount(d))
                .cast<PublisherAccount>()
                .toList()
            : [],
        error = null;

  FbIgUsersResponse.withError(DioError error)
      : users = null,
        error = error;
}
