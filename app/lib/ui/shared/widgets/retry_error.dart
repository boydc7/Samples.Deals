import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/services/authenticate.dart';
import 'package:rydr_app/app/routing.dart';
import 'buttons.dart';

class RetryError extends StatelessWidget {
  final dynamic error;
  final Function onRetry;
  final bool fullSize;
  final bool addSignout;

  RetryError({
    this.error,
    this.onRetry,
    this.fullSize = true,
    this.addSignout = false,
  });

  void _signOut(BuildContext context) {
    AuthenticationService.instance().signOut(true).then((_) {
      Navigator.of(context).pushNamed(AppRouting.getAuthenticate);
    });
  }

  String _handleError(dynamic error) {
    String errorDescription = "";

    if (error is DioError) {
      switch (error.type) {
        case DioErrorType.CANCEL:
          errorDescription = "Request to API server was cancelled";
          break;
        case DioErrorType.CONNECT_TIMEOUT:
          errorDescription =
              "Please make sure you have an active\nconnection and try again.";
          break;
        case DioErrorType.SEND_TIMEOUT:
          errorDescription =
              "Please make sure you have an active\nconnection and try again.";
          break;
        case DioErrorType.DEFAULT:
          errorDescription =
              "There was an issue completing this request.\nPlease try again in a few moments.";
          break;
        case DioErrorType.RECEIVE_TIMEOUT:
          errorDescription =
              "Please make sure you have an active\nconnection and try again.";
          break;
        case DioErrorType.RESPONSE:
          errorDescription =
              "There was an issue completing this request.\nPlease try again in a few moments.";
          break;
      }
    } else {
      errorDescription =
          "We're not sure what happened.\nPlease make sure you have an active\nconnection and try again.";
    }
    return errorDescription;
  }

  @override
  Widget build(BuildContext context) => Container(
        height: fullSize ? MediaQuery.of(context).size.height - 200 : 250,
        alignment: Alignment.center,
        margin: EdgeInsets.symmetric(horizontal: 32),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.center,
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(AppIcons.wifiSlash, size: 28),
            SizedBox(height: 8),
            Text("Unable to Connect",
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.headline6),
            SizedBox(height: 4),
            Text(
              _handleError(error),
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyText2.merge(
                    TextStyle(color: Theme.of(context).hintColor),
                  ),
            ),
            SizedBox(height: 24),
            Row(
              children: <Widget>[
                Expanded(
                  child: SecondaryButton(
                    onTap: onRetry,
                    label: "Retry",
                  ),
                ),
                Visibility(
                  visible: addSignout,
                  child: SizedBox(
                    width: 8,
                  ),
                ),
                Visibility(
                  visible: addSignout,
                  child: Expanded(
                    child: SecondaryButton(
                      onTap: () => _signOut(context),
                      label: "Sign Out",
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      );
}
