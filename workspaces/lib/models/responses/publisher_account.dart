import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';

class PublisherAccountResponse {
  final PublisherAccount user;
  final DioError error;

  PublisherAccountResponse(this.user, this.error);

  PublisherAccountResponse.fromResponse(Map<String, dynamic> json)
      : user = PublisherAccount.fromJson(json['result']),
        error = null;

  PublisherAccountResponse.withError(DioError error)
      : user = null,
        error = error;
}

class PublisherAccountLinkResponse {
  final PublisherAccount user;
  final DioError error;

  PublisherAccountLinkResponse(this.user, this.error);

  PublisherAccountLinkResponse.fromResponse(PublisherAccount linkedUser)
      : user = linkedUser,
        error = null;

  PublisherAccountLinkResponse.withError(DioError error)
      : user = null,
        error = error;
}
