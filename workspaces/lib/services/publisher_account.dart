import 'dart:async';
import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/enums/publisher_account.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';
import 'package:rydrworkspaces/models/responses/base.dart';
import 'package:rydrworkspaces/models/responses/publisher_account.dart';
import 'package:rydrworkspaces/models/responses/users.dart';
import 'package:rydrworkspaces/services/api.dart';

class PublisherAccountService {
  /// gets a list of instagram business / creator profile pages
  /// that are not yet linked to the master user
  static Future<FbIgUsersResponse> getFbIgBusinessAccounts([
    bool forceRefresh,
  ]) async {
    try {
      final Response res = await AppApi.instance.call(
        'facebook/igaccounts',
        queryParams: {
          'forceRefresh': forceRefresh.toString(),
        },
      );

      return FbIgUsersResponse.fromResponse(res.data);
    } catch (error) {
      return FbIgUsersResponse.withError(error);
    }
  }

  /// connect the users personal/primary facebook account
  /// which will become the parent/wrapper for all child publisher accounts
  static Future<BaseResponse> connectAccount({
    String firebaseToken,
    String firebaseId,
    String authProvider,
    String authProviderToken,
    String authProviderId,
    String name,
    String avatar,
    String email,
    String phone,
    bool isEmailVerified,
  }) async {
    final Map<String, dynamic> payload = {
      "firebaseToken": firebaseToken,
      "firebaseId": firebaseId,
      "authProvider": authProvider,
      "authProviderToken": authProviderToken,
      "authProviderId": authProviderId,
      "name": name,
      "avatar": avatar,
      "email": email,
      "phone": phone,
      "isEmailVerified": isEmailVerified.toString(),
    };

    try {
      await AppApi.instance.call(
        'authentication/connect',
        method: 'POST',
        body: payload,
      );

      return BaseResponse.fromResponse();
    } catch (error) {
      return BaseResponse.withError(error);
    }
  }

  static Future<PublisherAccountLinkResponse> linkProfile(
    PublisherAccount userToLink,
    RydrAccountType rydrType,
  ) async {
    /// make sure to link the right types
    userToLink.type = PublisherType.facebook;
    userToLink.accountType = PublisherAccountType.fbIgUser;
    userToLink.rydrPublisherType = rydrType;

    try {
      final Response res = await AppApi.instance
          .call('publisheracct/link', method: 'PUT', body: {
        "linkAccounts": [userToLink.toJson()]
      });

      /// re-create the user now with data from the server
      userToLink =
          PublisherAccount.fromJson(res.data['results'][0]['publisherAccount']);

      /// update the user we linked with the returned pub id
      userToLink.unreadNotifications =
          res.data['results'][0]['unreadNotifications'] ?? 0;

      return PublisherAccountLinkResponse.fromResponse(userToLink);
    } catch (error) {
      return PublisherAccountLinkResponse.withError(error);
    }
  }

  static Future<BaseResponse> unLinkProfile(
      PublisherAccount userToUnlink) async {
    try {
      await AppApi.instance
          .call('publisheracct/link/${userToUnlink.id}', method: 'DELETE');

      return BaseResponse.fromResponse();
    } catch (error) {
      return BaseResponse.withError(error);
    }
  }

  static Future<PublisherAccountResponse> getPubAccount(
    int userId, {
    bool forceRefresh = false,
  }) async {
    try {
      final Response res =
          await AppApi.instance.call('publisheracct/x/$userId');

      return PublisherAccountResponse.fromResponse(res.data);
    } catch (error) {
      return PublisherAccountResponse.withError(error);
    }
  }

  static Future<BaseResponse> optInToAi(bool optIn, int profileId) async {
    try {
      Map<String, dynamic> body = {
        'model': {
          'id': profileId.toString(),
          'optInToAi': optIn,
        },
      };

      if (!optIn) {
        body['unset'] = [
          'optInToAi',
        ];
      }
      await AppApi.instance.call(
        'publisheracct/$profileId',
        method: 'PUT',
        body: body,
      );

      return BaseResponse.fromResponse();
    } catch (error) {
      return BaseResponse.withError(error);
    }
  }
}
