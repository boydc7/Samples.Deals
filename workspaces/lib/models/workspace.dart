import 'package:rydrworkspaces/app/utils.dart';
import 'package:rydrworkspaces/models/enums/workspace.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';

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

    this.publisherAccountInfo = json['publisherAccountInfo'] != null
        ? List<PublisherAccount>.from(json['publisherAccountInfo']
            .map((user) => PublisherAccount.fromProfileJson(user))
            .toList())
        : [];
  }
}

class WorkspaceUser {
  final int userId;
  final String name;
  final String userName;
  final String avatar;
  final String userEmail;
  final List<PublisherAccount> publisherAccountInfo;
  final WorkspaceRole workspaceRole;

  WorkspaceUser.fromJson(Map<String, dynamic> json)
      : this.userId = json['userId'],
        this.name = json['name'],
        this.userName = json['userName'],
        this.avatar = json['avatar'],
        this.userEmail = json['userEmail'],
        this.workspaceRole = workspaceRoleFromString(json['workspaceRole']),
        this.publisherAccountInfo = json['publisherAccountInfo'] != null
            ? List<PublisherAccount>.from(json['publisherAccountInfo']
                .map((user) => PublisherAccount.fromProfileJson(user))
                .toList())
            : [];
}

class WorkspaceAccessRequest {
  final WorkspaceUser user;
  final DateTime requestedOn;

  WorkspaceAccessRequest.fromJson(Map<String, dynamic> json)
      : this.user = WorkspaceUser.fromJson(json),
        this.requestedOn = DateTime.parse(json['requestedOn']);

  String get requestedOnDisplay => Utils.formatAgo(this.requestedOn.toLocal());
}
