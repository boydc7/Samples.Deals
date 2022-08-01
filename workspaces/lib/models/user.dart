import 'package:rydrworkspaces/utils/enum_values.dart';

enum UserType {
  unknown,
  user,
  admin,
}

final userTypeValues = EnumValues.fromValues(UserType.values);

class User {
  int id;
  UserType userType;
  String fullName;
  String avatar;
  String email;
  bool isEmailVerified;
  String authProviderUserName;
  String authProviderUid;
  int defaultWorkspaceId;

  User.fromJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.userType = userTypeValues.map[json['userType']];
    this.fullName = json['fullName'];
    this.avatar = json['avatar'];
    this.isEmailVerified = json['isEmailVerified'];
    this.authProviderUserName = json['authProviderUserName'];
    this.authProviderUid = json['authProviderUid'];
    this.defaultWorkspaceId = json['defaultWorkspaceId'];
  }
}
