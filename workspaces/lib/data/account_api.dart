import 'package:rydrworkspaces/models/user.dart';

import 'api_client.dart';

class AccountApi {
  static final AccountApi _instance = AccountApi();

  static AccountApi get instance => _instance;

  Future<User> getMyUser() async {
    final apiResponse = await ApiClient.instance.get('authentication/me');

    return User.fromJson(apiResponse.data['result']);
  }

  Future<void> connectAccount({
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

    await ApiClient.instance.post(
      'authentication/connect',
      body: payload,
    );
  }
}
