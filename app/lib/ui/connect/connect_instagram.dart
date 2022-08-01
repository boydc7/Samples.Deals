import 'dart:async';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/ui/connect/blocs/connect_profile.dart';
import 'package:webview_flutter/webview_flutter.dart';

class ConnectInstagramPage extends StatefulWidget {
  final String url;

  ConnectInstagramPage(this.url);

  @override
  _ConnectInstagramPageState createState() => _ConnectInstagramPageState();
}

class _ConnectInstagramPageState extends State<ConnectInstagramPage> {
  final _log = getLogger('ConnectInstagramPage');
  final _cookieManager = CookieManager();

  final Completer<WebViewController> _controller =
      Completer<WebViewController>();

  /// flag we can check to see if we've cleared/reset the cookies
  /// which is only done when the first page wants to load (e.g. initial IG auth flow)
  bool _resetDone = false;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Connect with Instagram'),
      ),
      // We're using a Builder here so we have a context that is below the Scaffold
      // to allow calling Scaffold.of(context) so we can show a snackbar if we wanted
      body: Builder(builder: (BuildContext context) {
        return WebView(
          initialUrl: widget.url,

          /// javascript must be enabled for IG auth to load successfully
          javascriptMode: JavascriptMode.unrestricted,
          onWebViewCreated: (WebViewController webViewController) {
            _controller.complete(webViewController);
          },
          navigationDelegate: (NavigationRequest request) async {
            /// first page we load we clear cache and cookies first, so that we can force
            /// a new login for instagram auth flow vs. showing existing logged in user if available
            if (!_resetDone) {
              _log.i('First page load, clearing cookies');

              await _cookieManager.clearCookies();
              _resetDone = true;
            }

            /// if used in local environment, then intercept ssl localhost redirect
            /// and make http again to have it be able to talk back to load api listening
            if (request.url.startsWith('https://localhost')) {
              _controller.future.then((d) {
                final noLocalhost =
                    request.url.replaceFirst('https://', 'http://');
                d.loadUrl(noLocalhost);
              });

              return NavigationDecision.prevent;
            }

            return NavigationDecision.navigate;
          },
          onPageStarted: (String url) {
            _log.i('Page started loading: $url');
          },
          onPageFinished: (String url) {
            _log.i('Page finished loading: $url');

            /// if we're 'done' then we can close the page and return back the result
            /// to the caller to evaluate and either continue on or show an error
            if (url.startsWith('https://done.getrydr.com')) {
              /// read some params from the querystring that we'll then pass back
              /// as a result object to the caller
              final Uri uri = Uri.parse(url);

              final String postBackId = uri.queryParameters['postbackid'];

              Navigator.of(context).pop(
                ConnectInstagramResults(
                  postBackId != null,
                  uri.queryParameters['username'],
                  postBackId,
                  uri.queryParameters['linkedasaccounttype'] != null
                      ? rydrAccountTypeFromInt(
                          int.parse(uri.queryParameters['linkedasaccounttype']))
                      : null,
                  uri.queryParameters['error'],
                  uri.queryParameters['errorReason'],
                  uri.queryParameters['errorDesc'],
                ),
              );
            }
          },
        );
      }),
    );
  }
}
