import 'dart:convert';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:http/http.dart' as http;
import 'package:rydr_app/models/publisher_media.dart';

class PublicApiService {
  static Future<PublisherAccount> getIgProfileToRydr(String igHandle) async {
    final http.Response response = await http
        .get('https://pubapi.getrydr.com/igprofiletorydr?iguser=$igHandle');

    if (response.reasonPhrase == "OK") {
      return PublisherAccount.fromJson(json.decode(response.body));
    }

    return null;
  }

  static Future<PublisherMedia> getIgMediaFromPost(
    int publisherAccountId,
    String publisherUsername,
    String postUrl,
  ) async {
    final http.Response response = await http
        .get('https://pubapi.getrydr.com/igposttorydr?igposturl=$postUrl');

    if (response.reasonPhrase == "OK") {
      final body = json.decode(response.body);

      return PublisherMedia.fromJson({
        'publisherAccountId': publisherAccountId,
        'publisherType': 'Instagram',
        'contentType': 'media',
        'caption': '_',
        'mediaId': DateTime.now().millisecondsSinceEpoch.toString(),
        'mediaType': 'IMAGE',
        'publisherUrl': 'https://instagram.com/$publisherUsername/',
        'mediaUrl': body['mediaUrl']
      });
    }

    return null;
  }

  static Future<BizInfoResult> getIgBizInfo(String username) async {
    final http.Response response =
        await http.get('https://pubapi.getrydr.com/bizinfo?iguser=$username');

    if (response.reasonPhrase == "OK") {
      final body = json.decode(response.body);

      return BizInfoResult(
        body['isPrivate'],
        body['isBusiness'],
      );
    }

    return null;
  }
}

class BizInfoResult {
  final bool isPrivate;
  final bool isBusiness;

  BizInfoResult(this.isPrivate, this.isBusiness);
}
