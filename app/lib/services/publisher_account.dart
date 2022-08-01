import 'dart:async';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/publisher_account.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/fbig_users.dart';
import 'package:rydr_app/models/tag.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/services/workspaces.dart';

class PublisherAccountService {
  static String pathFacebookAccounts = 'facebook/igaccounts';

  /// gets a list of instagram business / creator profile pages
  /// that are not yet linked to the master user
  static Future<FbIgUsersResponse> getFbIgBusinessAccounts([
    bool forceRefresh,
  ]) async {
    final ApiResponse apiResponse =
        await AppApi.instance.get(pathFacebookAccounts,
            queryParams: {'forceRefresh': forceRefresh.toString()},
            options: AppApi.instance.cacheConfig(
              pathFacebookAccounts,
              forceRefresh: forceRefresh,

              /// we include the workspace id because we don't want to have stuff cached
              /// between logged in / logged out and back in fb accounts that are different
              /// but we can exclude the profile id since all profiles to be connected can be
              /// shared within the current workspace
              includeProfileInKey: false,
            ));

    return FbIgUsersResponse.fromApiResponse(apiResponse);
  }

  /// Links a facebook/instagram page to the master account
  static Future<PublisherAccount> linkProfile({
    PublisherAccount userToLink,
    PublisherType publisherType = PublisherType.facebook,
    RydrAccountType rydrType,
  }) async {
    /// make sure to link the right types
    userToLink.type = publisherType;
    userToLink.accountType = PublisherAccountType.fbIgUser;
    userToLink.rydrPublisherType = rydrType;

    final ApiResponse apiResponse =
        await AppApi.instance.put('publisheracct/link', body: {
      "linkAccounts": [userToLink.toJson()]
    });

    if (apiResponse.error == null) {
      /// re-create the user now with data from the server
      userToLink = PublisherAccount.fromJson(
          apiResponse.response.data['results'][0]['publisherAccount']);

      /// update the user we linked with the returned pub id
      userToLink.unreadNotifications =
          apiResponse.response.data['results'][0]['unreadNotifications'] ?? 0;

      /// refresh current workspace to account for newly linked publisher
      await WorkspacesService.getWorkspaceUserPublisherAccounts(
        appState.currentWorkspace.id,
        forceRefresh: true,
      );

      await getFbIgBusinessAccounts(true);

      return userToLink;
    }

    return null;
  }

  static Future<BasicVoidResponse> unLinkProfile(
      int workspaceId, PublisherAccount userToUnlink) async {
    final ApiResponse apiResponse =
        await AppApi.instance.delete('publisheracct/link/${userToUnlink.id}');

    /// if we have no error then reload / clear currently linked profiles
    /// for the given workspace
    if (apiResponse.error == null) {
      WorkspacesService.getWorkspaceUserPublisherAccounts(
        workspaceId,
        forceRefresh: true,
      );
    }

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  static Future<PublisherAccountResponse> getPubAccount(
    int userId, {
    bool enforceLinked = false,
    bool forceRefresh = false,
  }) async {
    final String path =
        enforceLinked ? 'publisheracct/$userId' : 'publisheracct/x/$userId';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        options: AppApi.instance.cacheConfig(
          path,
          duration: const Duration(hours: 3),
          forceRefresh: forceRefresh,
        ));

    return PublisherAccountResponse.fromApiResponse(apiResponse);
  }

  static Future<BasicVoidResponse> updateTags(
    int publisherAccountId,
    List<Tag> tags,
  ) async {
    final ApiResponse apiResponse =
        await AppApi.instance.put('publisheracct/$publisherAccountId', body: {
      "model": {
        "id": publisherAccountId,
        "tags": tags.map((Tag t) => t.toJson()).toList()
      }
    });

    clearProfileCache(apiResponse, publisherAccountId);

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  static Future<BasicVoidResponse> switchAccountType(
      RydrAccountType toAccountType) async {
    final ApiResponse apiResponse = await AppApi.instance
        .put('publisheracct/${appState.currentProfile.id}', body: {
      'model': {
        'id': appState.currentProfile.id.toString(),
        'rydrAccountType': rydrAccountTypeToInt(toAccountType),
      }
    });

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  static Future<BasicVoidResponse> optInToAi(bool optIn) async {
    Map<String, dynamic> body = {
      'model': {
        'id': appState.currentProfile.id.toString(),
        'optInToAi': optIn,
      },
    };

    if (!optIn) {
      body['unset'] = [
        'optInToAi',
      ];
    }

    final ApiResponse apiResponse = await AppApi.instance.put(
      'publisheracct/${appState.currentProfile.id}',
      body: body,
    );

    /// if sucessful, clear the cache for the given profile
    clearProfileCache(apiResponse, appState.currentProfile.id);

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  static void clearFacebookPages() {
    AppApi.instance.clearCacheByPath(AppApi.instance.getCachePrimaryKey(
      pathFacebookAccounts,
      includeProfileInKey: false,
    ));
  }

  static void clearProfileCache(ApiResponse apiResponse, int profileId) {
    if (apiResponse.error == null) {
      AppApi.instance.clearCacheByPath(
          AppApi.instance.getCachePrimaryKey('publisheracct/x/$profileId'));
    }
  }
}
