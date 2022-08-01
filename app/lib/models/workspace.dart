import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/enums/workspace.dart';

class Workspace {
  int id;
  String name;
  WorkspaceType type;
  WorkspaceRole role;
  int defaultPublisherAccountId;
  int ownerId;
  int accessRequests;
  String inviteCode;
  List<PublisherAccount> publisherAccountInfo;
  int workspaceFeatures;
  int totalPublisherAccounts;

  /// a flag that tells us whether or not this workspace has a proper
  /// facebook token assigned to it or not, we can use this to check and see
  /// if we can make a call to get all unlinked facebook pages or not...
  bool get hasFacebookToken =>
      defaultPublisherAccountId != null && defaultPublisherAccountId > 0;

  Workspace();

  Workspace.fromJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.name = json['name'];
    this.type = workspaceTypeFromString(json['workspaceType']);
    this.role = workspaceRoleFromString(json['workspaceRole']);
    this.defaultPublisherAccountId = json['defaultPublisherAccountId'];
    this.ownerId = json['ownerId'];
    this.accessRequests = json['accessRequests'] ?? 0;
    this.inviteCode = json['inviteCode'];
    this.workspaceFeatures = json['workspaceFeatures'];

    /// This will only be a max of 3 purely for UI display purposes
    this.publisherAccountInfo = json['publisherAccountInfo'] != null
        ? List<PublisherAccount>.from(json['publisherAccountInfo']
            .map((user) => PublisherAccount.fromProfileJson(user))
            .toList())
        : [];
  }

  Map<String, dynamic> toJson() => {
        "id": this.id,
        "name": this.name,
        "workspaceType": workspaceTypeToString(this.type),
        "workspaceRole": workspaceRoleToString(this.role),
        "defaultPublisherAccountId": this.defaultPublisherAccountId,
        "ownerId": this.ownerId,
        "accessRequests": this.accessRequests,
        "inviteCode": this.inviteCode,
        "workspaceFeatures": this.workspaceFeatures,
        "publisherAccountInfo": this.hasLinkedPublishers
            ? this
                .publisherAccountInfo
                .map((PublisherAccount account) => account.toProfileJson())
                .toList()
            : null,
      };

  /// NOTE! this will only ever be up to three - see .fromJson
  bool get hasLinkedPublishers =>
      this.publisherAccountInfo != null && this.publisherAccountInfo.isNotEmpty;
}

class WorkspaceUser {
  final int userId;
  final String name;
  final String userName;
  final String avatar;
  final String userEmail;
  final WorkspaceRole workspaceRole;

  WorkspaceUser.fromJson(Map<String, dynamic> json)
      : this.userId = json['userId'],
        this.name = json['name'],
        this.userName = json['userName'],
        this.avatar = json['avatar'],
        this.userEmail = json['userEmail'],
        this.workspaceRole = workspaceRoleFromString(json['workspaceRole']);
}

class WorkspaceAccessRequest {
  final WorkspaceUser user;
  final DateTime requestedOn;

  WorkspaceAccessRequest.fromJson(Map<String, dynamic> json)
      : this.user = WorkspaceUser.fromJson(json),
        this.requestedOn = DateTime.parse(json['requestedOn']);

  String get requestedOnDisplay => Utils.formatAgo(this.requestedOn.toLocal());
}
