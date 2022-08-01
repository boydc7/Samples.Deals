import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/services/api.dart';

class InstagramService {
  static Future<List<PublisherAccount>> queryPeopleOnInstagram(
      String query) async {
    if (query != null && query.trim().isNotEmpty) {
      // remove "@" sign entered by the user
      final _query =
          query.substring(0, 1) == "@" ? query.substring(0, 1) : query;

      final String url =
          "https://www.instagram.com/web/search/topsearch/?context=user&query=%40$_query&rank_token=0.38108914034956043&include_reel=true";

      try {
        final http.Response res = await http.get(url);
        final Map<String, dynamic> resp = json.decode(res.body);
        final List<PublisherAccount> items = [];

        resp["users"].forEach((item) {
          items.add(PublisherAccount.fromInstaJson(item));
        });
        return items;
      } catch (error, stackTrace) {
        AppErrorLogger.instance.reportError('Other', error, stackTrace);

        return [];
      }
    }

    return [];
  }

  static Future<StringIdResponse> getAuthUrl() async {
    final ApiResponse apiResponse =
        await AppApi.instance.get('instagram/authurl');

    return StringIdResponse.fromApiResponse(apiResponse);
  }

  /// this will link the IG basic account to the current workspace (or create a new one)
  /// and set it to the desired rydr account type
  static Future<IntIdResponse> linkProfile(
    String postBackId,
    RydrAccountType rydrAccountType,
  ) async {
    final ApiResponse apiResponse = await AppApi.instance.post(
      'instagram/postbackuser',
      body: {
        "postBackId": postBackId,
        "rydrAccountType": rydrAccountTypeToInt(rydrAccountType),
      },
    );

    return IntIdResponse.fromApiResponse(apiResponse);
  }
}
