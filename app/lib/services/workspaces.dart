import 'dart:async';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/models/publisher_account.dart';

class WorkspacesService {
  /// main call for when the app loads, we load all workspaces and their associated profiles
  /// for the given master user that is logged into their device with the main FB/Firebase account
  /// we should always be asking for a refreshed copy here and not cache it
  static Future<WorkspacesResponse> getWorkspaces() async {
    final ApiResponse apiResponse = await AppApi.instance
        .get('workspaces', queryParams: {"forceRefresh": "true"});

    return WorkspacesResponse.fromApiResponse(apiResponse);
  }

  /// Load/reload a single workspace and all its settings
  static Future<WorkspaceResponse> getWorkspace(int workspaceId,
      [bool forceRefresh = false]) async {
    final String path = 'workspaces/$workspaceId';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        options: AppApi.instance.cacheConfig(path, forceRefresh: forceRefresh));

    return WorkspaceResponse.fromApiResponse(apiResponse);
  }

  /// Paged list of users that currently have access to the active workspace
  static Future<WorkspaceUsersResponse> getWorkspaceUsers(
    int workspaceId, {
    int skip,
    int take,
    bool forceRefresh = false,
  }) async {
    final String path = 'workspaces/$workspaceId/users';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        queryParams: {
          "skip": skip ?? 0,
          "take": take ?? 50,
        },
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
          includeWorkspaceInKey: false,
          includeProfileInKey: false,
        ));

    return WorkspaceUsersResponse.fromApiResponse(apiResponse);
  }

  /// Page list of profiles a given workspace user currently has access to (is linked to)
  /// can also do the inverse and return any that are not yet linkedk to the user
  /// from the list of profiles available in the workspace itself
  ///
  /// can filter by type of publisher (business/creator), and by a specific id
  /// which enforces that this user (me) does indeed have access to the pubaccount via linked in their workspace
  static Future<WorkspacePublisherAccountInfoResponse>
      getWorkspaceUserPublisherAccounts(
    int workspaceId, {
    int userId,
    int skip,
    int take,
    bool unlinked = false,
    bool forceRefresh = false,
    RydrAccountType rydrAccountType,
    int publisherAccountId,
    String usernamePrefix,
  }) async {
    final String path =
        "workspaces/$workspaceId/users/${userId ?? 'me'}/publisheraccts";

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        queryParams: {
          "skip": skip ?? 0,
          "take": take ?? 50,
          "unlinked": unlinked,
          "rydrAccountType": rydrAccountTypeToInt(rydrAccountType),
          "publisherAccountId": publisherAccountId,
          "usernamePrefix": usernamePrefix,
        },
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
          includeProfileInKey: false,
          includeWorkspaceInKey: false,
        ));

    return WorkspacePublisherAccountInfoResponse.fromApiResponse(apiResponse);
  }

  /// this will use the endpoint above and try to specifically return a single publisher
  /// by id, and either return that publisher account or null which would indicate its
  /// not/no longer linked to the user - we use this to validate switching profiles for example
  static Future<PublisherAccount> getWorkspacePublisherAccount(
    int workspaceId,
    int publisherAccountId,
  ) async {
    final WorkspacePublisherAccountInfoResponse publisherAccountsResponse =
        await getWorkspaceUserPublisherAccounts(
      workspaceId,
      publisherAccountId: publisherAccountId,
    );

    return publisherAccountsResponse.error == null &&
            publisherAccountsResponse.models != null &&
            publisherAccountsResponse.models.isNotEmpty
        ? publisherAccountsResponse.models[0]
        : null;
  }

  /// creates a non-personal workspace (paid workspace)
  static Future<WorkspaceResponse> createWorkspace(
    String name,
    List<PublisherAccount> publisherAccountsToLink,
  ) async {
    final ApiResponse apiResponse = await AppApi.instance.post(
      'workspaces',
      body: {
        "model": {
          "id": 0,
          "name": name,
        },
        "linkAccounts": publisherAccountsToLink != null
            ? publisherAccountsToLink
                .map(
                    (PublisherAccount account) => {"id": account.id.toString()})
                .toList()
            : null
      },
    );

    return WorkspaceResponse.fromApiResponse(apiResponse);
  }

  /// link an user to a workspace
  /// this would be called in response to accepting an invite to join from a user account
  /// it will remove the access request record and link the user to the active workspace
  static Future<BasicVoidResponse> linkUserToWorkspace(
      int workspaceId, int userId) async {
    final ApiResponse apiResponse =
        await AppApi.instance.put('workspaces/$workspaceId/users/$userId');

    /// if successful, then refresh cache of workspace users and access requests
    if (apiResponse.error == null) {
      await getWorkspaceUsers(workspaceId, forceRefresh: true);
    }

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  /// unlink an user from a workspace
  static Future<BasicVoidResponse> unlinkUserFromWorkspace(
      int workspaceId, int userId) async {
    final ApiResponse apiResponse =
        await AppApi.instance.delete('workspaces/$workspaceId/users/$userId');

    /// if successful, then refresh cache of workspace users
    if (apiResponse.error == null) {
      await getWorkspaceUsers(workspaceId, forceRefresh: true);
    }

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  /// given an invite code entered by a user on a device, this will create an invite request
  /// sent to the owner of the workspace they're looking to join...
  static Future<BasicVoidResponse> requestAccess(String inviteCode) async {
    final ApiResponse apiResponse =
        await AppApi.instance.post('workspaces/$inviteCode/requests');

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  /// links an accepted 'user' (master user FB/Firebase account) with a publisher account (profile)
  /// part of the active workspace, e.g. give an employee access to a profile in my workspace
  static Future<BasicVoidResponse> linkProfileToWorkspaceUser(
    int workspaceId,
    int userId,
    int profileId,
  ) async {
    final ApiResponse apiResponse = await AppApi.instance
        .put('workspaces/$workspaceId/users/$userId/publisheraccts/$profileId');

    /// if successful, then refresh cache of workspace user and their profiles
    if (apiResponse.error == null) {
      await getWorkspaceUserPublisherAccounts(
        workspaceId,
        userId: userId,
        forceRefresh: true,
      );
      await getWorkspaceUserPublisherAccounts(
        workspaceId,
        userId: userId,
        forceRefresh: true,
        unlinked: true,
      );
    }

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  /// remove a member of my workspace from having access to a publisher account (profile)
  static Future<BasicVoidResponse> unlinkProfileFromWorkspaceUser(
    int workspaceId,
    int userId,
    int profileId,
  ) async {
    final ApiResponse apiResponse = await AppApi.instance.delete(
      'workspaces/$workspaceId/users/$userId/publisheraccts/$profileId',
    );

    /// if successful, then refresh cache of workspace user and their profiles
    if (apiResponse.error == null) {
      await getWorkspaceUserPublisherAccounts(
        workspaceId,
        userId: userId,
        forceRefresh: true,
      );
      await getWorkspaceUserPublisherAccounts(
        workspaceId,
        userId: userId,
        forceRefresh: true,
        unlinked: true,
      );
    }

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  /// gets a list of all pending access requests (requests to join my workspace)
  /// those should never be cached as we have no control over new ones coming in
  static Future<WorkspaceAccessRequestsResponse> getAccessRequests(
    int workspaceId, {
    int skip,
    int take,
  }) async {
    final String path = 'workspaces/$workspaceId/requests';

    final ApiResponse apiResponse =
        await AppApi.instance.get(path, queryParams: {
      "skip": skip ?? 0,
      "take": take ?? 50,
    });

    return WorkspaceAccessRequestsResponse.fromApiResponse(apiResponse);
  }

  /// delete a request to join a workspace
  static Future<BasicVoidResponse> deleteAccessRequest(
      int workspaceId, int userId) async {
    final ApiResponse apiResponse = await AppApi.instance.delete(
      'workspaces/$workspaceId/requests/$userId',
    );

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }
}
