import 'dart:async';
import 'dart:io' show Platform;
import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:flutter/cupertino.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

import 'package:rydr_app/app/theme.dart';

Future<T> showSharedModalBottomInfo<T>(
  context, {
  bool hideTitleOnAndroid = true,
  bool hasCustomScrollView = false,
  bool largeTitle = false,
  double initialRatio,
  String title,
  String subtitle,
  Widget child,
  Widget topWidget,
}) {
  final bool dark = Theme.of(context).brightness == Brightness.dark;
  final bool isIos = Platform.isIOS;

  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (BuildContext context) {
      return Stack(
        alignment: Alignment.center,
        children: <Widget>[
          /// This is so the blurry dark area acts as a touchzone to close the bottomSheet
          GestureDetector(
            onTap: () {
              Navigator.pop(context);
            },
            child: Container(
              width: MediaQuery.of(context).size.width,
              height: MediaQuery.of(context).size.height,
              color: Colors.transparent,
            ),
          ),

          /// This is the bottomSheet
          BackdropFilter(
            filter: ImageFilter.blur(sigmaX: 2.0, sigmaY: 2.0),
            child: DraggableScrollableSheet(
              initialChildSize: initialRatio == null ? 0.65 : initialRatio,
              maxChildSize: 0.9,
              minChildSize: 0.25,
              builder:
                  (BuildContext context, ScrollController scrollController) {
                return Stack(
                  alignment: Alignment.bottomCenter,
                  children: <Widget>[
                    /// This adds a little black to the bottom corners
                    Container(
                      color: Colors.black,
                      height: 16.0,
                    ),

                    /// Actual bottom sheet stuff here
                    ClipRRect(
                      borderRadius: BorderRadius.circular(16.0),
                      child: Container(
                        decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(16.0),
                            color: dark
                                ? Theme.of(context).appBarTheme.color
                                : Theme.of(context).scaffoldBackgroundColor),
                        child: hasCustomScrollView
                            ? child
                            : ListView(
                                controller: scrollController,
                                children: <Widget>[
                                  Column(
                                    mainAxisSize: MainAxisSize.max,
                                    children: <Widget>[
                                      Container(
                                        width: double.infinity,
                                        margin: EdgeInsets.only(
                                            left: isIos ? 16.0 : 0.0,
                                            right: isIos ? 16.0 : 0.0,
                                            bottom: isIos ? 8.0 : 0.0),
                                        child: Column(
                                          mainAxisSize: MainAxisSize.min,
                                          crossAxisAlignment:
                                              CrossAxisAlignment.center,
                                          children: <Widget>[
                                            Container(
                                              height: 4.0,
                                              width: 28.0,
                                              margin:
                                                  EdgeInsets.only(top: 12.0),
                                              decoration: BoxDecoration(
                                                  color: dark
                                                      ? Theme.of(context)
                                                          .hintColor
                                                      : Theme.of(context)
                                                          .canvasColor,
                                                  borderRadius:
                                                      BorderRadius.circular(
                                                          8.0)),
                                            ),
                                            Visibility(
                                              visible: topWidget != null,
                                              child: Padding(
                                                padding:
                                                    EdgeInsets.only(top: 24.0),
                                                child: topWidget,
                                              ),
                                            ),
                                            title != null
                                                ? isIos
                                                    ? Padding(
                                                        padding: EdgeInsets.only(
                                                            top: largeTitle
                                                                ? 20.0
                                                                : 12.0,
                                                            left: 16.0,
                                                            right: 16.0,
                                                            bottom:
                                                                subtitle != null
                                                                    ? largeTitle
                                                                        ? 8.0
                                                                        : 4.0
                                                                    : 16.0),
                                                        child: Text(title,
                                                            textAlign: isIos
                                                                ? TextAlign
                                                                    .center
                                                                : TextAlign
                                                                    .left,
                                                            style: TextStyle(
                                                                fontSize:
                                                                    largeTitle
                                                                        ? 22.0
                                                                        : 16.0,
                                                                fontWeight:
                                                                    FontWeight
                                                                        .w600)),
                                                      )
                                                    : !hideTitleOnAndroid
                                                        ? Padding(
                                                            padding: EdgeInsets.only(
                                                                top: 22.0,
                                                                left: 16.0,
                                                                right: 16.0,
                                                                bottom:
                                                                    subtitle !=
                                                                            null
                                                                        ? 4.0
                                                                        : 22.0),
                                                            child: Text(title,
                                                                textAlign: isIos
                                                                    ? TextAlign
                                                                        .center
                                                                    : TextAlign
                                                                        .left,
                                                                style: TextStyle(
                                                                    fontWeight:
                                                                        FontWeight
                                                                            .w600)),
                                                          )
                                                        : Container()
                                                : Container(),
                                            subtitle != null
                                                ? Padding(
                                                    padding: EdgeInsets.only(
                                                        left:
                                                            isIos ? 32.0 : 16.0,
                                                        right:
                                                            isIos ? 32.0 : 16.0,
                                                        bottom: 20.0),
                                                    child: Text(
                                                      subtitle,
                                                      textAlign: isIos
                                                          ? TextAlign.center
                                                          : TextAlign.left,
                                                      style: Theme.of(context)
                                                          .textTheme
                                                          .bodyText2
                                                          .merge(
                                                            TextStyle(
                                                                color: Theme.of(
                                                                        context)
                                                                    .hintColor),
                                                          ),
                                                    ),
                                                  )
                                                : Container(),
                                            title != null && !hideTitleOnAndroid
                                                ? Divider(height: 0)
                                                : Container(),
                                          ],
                                        ),
                                      ),
                                      child
                                    ],
                                  ),
                                ],
                              ),
                      ),
                    ),
                  ],
                );
              },
            ),
          ),
        ],
      );
    },
  );
}

Future<T> showSharedModalBottomActions<T>(
  context, {
  bool hideCancel = false,
  bool hideTitleOnAndroid = true,
  bool hasActions = true,
  String title,
  String subtitle,
  Widget child,
  List<ModalBottomAction> actions,
}) {
  List<Widget> _actionTiles = [];
  int counter = 0;
  TextStyle style;
  bool dark = Theme.of(context).brightness == Brightness.dark;
  bool isIos = Platform.isIOS;

  if (hasActions) {
    actions.forEach((ModalBottomAction action) {
      if (counter > 0) {
        _actionTiles.add(Divider(height: 0));
      }

      if (action.isDefaultAction) {
        style = TextStyle(
            color: dark ? Colors.white : AppColors.grey800,
            fontWeight: FontWeight.w600);
      } else if (action.isDestructiveAction) {
        style =
            TextStyle(color: AppColors.errorRed, fontWeight: FontWeight.w600);
      } else if (action.isCurrentAction) {
        style = TextStyle(
            color: Theme.of(context).primaryColor, fontWeight: FontWeight.w600);
      } else if (action.isInactiveAction) {
        style = TextStyle(
            color: Theme.of(context).hintColor, fontWeight: FontWeight.w400);
      } else {
        style = TextStyle(
            color: dark ? Colors.white : AppColors.grey800,
            fontWeight: FontWeight.w400);
      }

      if (isIos) {
        _actionTiles.add(
          GestureDetector(
            onTap: action.onTap,
            child: Container(
              width: double.infinity,
              color: Colors.transparent,
              child: Center(
                child: Padding(
                  padding:
                      EdgeInsets.symmetric(vertical: 16.0, horizontal: 10.0),
                  child: DefaultTextStyle(
                    style: style,
                    child: action.child,
                  ),
                ),
              ),
            ),
          ),
        );
      } else {
        _actionTiles.add(ListTile(
          leading: action.icon != null
              ? Icon(
                  action.icon,
                  color: action.isCurrentAction
                      ? Theme.of(context).primaryColor
                      : action.isInactiveAction
                          ? Theme.of(context).hintColor
                          : action.isDestructiveAction
                              ? AppColors.errorRed
                              : Theme.of(context).iconTheme.color,
                )
              : null,
          onTap: action.onTap,
          title: DefaultTextStyle(
            style: style,
            child: action.child,
          ),
        ));
      }

      counter++;
    });
  }

  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (BuildContext context) {
      return BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 2.0, sigmaY: 2.0),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Container(
              width: double.infinity,
              decoration: BoxDecoration(
                  color: Theme.of(context).appBarTheme.color,
                  borderRadius: BorderRadius.only(
                      topLeft: Radius.circular(8.0),
                      topRight: Radius.circular(8.0),
                      bottomLeft: Radius.circular(
                          counter > 0 ? isIos ? 8.0 : 0.0 : 0.0),
                      bottomRight: Radius.circular(
                          counter > 0 ? isIos ? 8.0 : 0.0 : 0.0)),
                  boxShadow: AppShadows.elevation[3]),
              margin: EdgeInsets.only(
                  left: isIos ? 16.0 : 0.0,
                  right: isIos ? 16.0 : 0.0,
                  bottom: isIos ? 8.0 : 0.0),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: isIos
                    ? CrossAxisAlignment.center
                    : CrossAxisAlignment.start,
                children: <Widget>[
                  title != null
                      ? isIos
                          ? Padding(
                              padding: EdgeInsets.only(
                                  top: 16.0,
                                  left: 16.0,
                                  right: 16.0,
                                  bottom: subtitle != null ? 4.0 : 16.0),
                              child: Text(title,
                                  textAlign:
                                      isIos ? TextAlign.center : TextAlign.left,
                                  style:
                                      TextStyle(fontWeight: FontWeight.w600)),
                            )
                          : !hideTitleOnAndroid
                              ? Padding(
                                  padding: EdgeInsets.only(
                                      top: 22.0,
                                      left: 16.0,
                                      right: 16.0,
                                      bottom: subtitle != null ? 4.0 : 22.0),
                                  child: Text(title,
                                      textAlign: isIos
                                          ? TextAlign.center
                                          : TextAlign.left,
                                      style: TextStyle(
                                          fontWeight: FontWeight.w600)),
                                )
                              : Container()
                      : Container(),
                  subtitle != null
                      ? isIos
                          ? Padding(
                              padding: EdgeInsets.only(
                                  left: 32.0, right: 32.0, bottom: 16.0),
                              child: Text(subtitle,
                                  textAlign: TextAlign.center,
                                  style: Theme.of(context).textTheme.caption),
                            )
                          : !hideTitleOnAndroid
                              ? Padding(
                                  padding: EdgeInsets.only(
                                      left: 16.0, right: 16.0, bottom: 16.0),
                                  child: Text(subtitle,
                                      textAlign: TextAlign.left,
                                      style:
                                          Theme.of(context).textTheme.caption),
                                )
                              : Container()
                      : Container(),
                  title != null && hideTitleOnAndroid
                      ? Divider(height: 0)
                      : Container(),
                  SafeArea(
                    bottom: !hasActions,
                    child: hasActions
                        ? Column(
                            mainAxisSize: MainAxisSize.min,
                            crossAxisAlignment: isIos
                                ? CrossAxisAlignment.center
                                : CrossAxisAlignment.start,
                            children: _actionTiles,
                          )
                        : Padding(padding: EdgeInsets.all(16.0), child: child),
                  ),
                ],
              ),
            ),
            Visibility(
              visible: isIos ? !hideCancel : false,
              child: GestureDetector(
                onTap: () {
                  Navigator.of(context).pop();
                },
                child: Container(
                  width: double.infinity,
                  height: 56.0,
                  decoration: BoxDecoration(
                    color: Theme.of(context).appBarTheme.color,
                    borderRadius: BorderRadius.circular(8.0),
                  ),
                  margin:
                      EdgeInsets.only(left: 16.0, right: 16.0, bottom: 16.0),
                  child: Center(
                    child: Text('Cancel'),
                  ),
                ),
              ),
            ),
          ],
        ),
      );
    },
  );
}

Future<T> showSharedModalAlert<T>(
  context,
  Widget title, {
  Widget content,
  List<ModalAlertAction> actions,
}) {
  if (Platform.isIOS) {
    List<CupertinoDialogAction> _actions = [];

    if (actions != null) {
      actions.forEach((ModalAlertAction action) {
        _actions.add(CupertinoDialogAction(
          child: Text(action.label),
          onPressed: action.onPressed,
          isDefaultAction: action?.isDefaultAction ?? false,
          isDestructiveAction: action?.isDestructiveAction ?? false,
        ));
      });
    }

    return showCupertinoDialog<T>(
        context: context,
        builder: (BuildContext context) {
          return BackdropFilter(
            filter: ImageFilter.blur(sigmaX: 2.0, sigmaY: 2.0),
            child: CupertinoAlertDialog(
              title: title,
              content: content,
              actions: _actions,
            ),
          );
        });
  } else {
    List<FlatButton> _actions = [];

    if (actions != null) {
      actions.forEach((ModalAlertAction action) {
        _actions.add(FlatButton(
          child: Text(action.label.toUpperCase()),
          onPressed: action.onPressed,
        ));
      });
    }

    return showDialog<T>(
        context: context,
        builder: (BuildContext context) {
          return BackdropFilter(
            filter: ImageFilter.blur(sigmaX: 2.0, sigmaY: 2.0),
            child: AlertDialog(
              backgroundColor: Theme.of(context).appBarTheme.color,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(4.0),
              ),
              elevation: 10.0,
              titleTextStyle: Theme.of(context).textTheme.bodyText1.merge(
                  TextStyle(
                      fontSize: 18.0,
                      color: Theme.of(context).textTheme.bodyText2.color)),
              title: title,
              content: content,
              actions: _actions,
            ),
          );
        });
  }
}

void showSharedModalError(
  BuildContext context, {
  String title,
  String subTitle,
}) {
  showSharedModalAlert(context, Text(title ?? "Update Error"),
      content: Text(
        subTitle ??
            "We are unable to complete this action. Please try again in a few moments.",
      ),
      actions: <ModalAlertAction>[
        ModalAlertAction(
            label: "OK",
            onPressed: () {
              Navigator.of(context).pop();
            })
      ]);
}

Future<T> showSharedLoadingLogo<T>(BuildContext context,
        {String content = ""}) =>
    showDialog<T>(
      context: context,
      barrierDismissible: false,
      builder: (BuildContext context) => BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 2.0, sigmaY: 2.0),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            LoadingLogo(
              radius: 72.0,
              color: Theme.of(context).textTheme.bodyText2.color,
              background: true,
            ),
            Visibility(
              visible: content != "",
              child: Material(
                color: Colors.transparent,
                child: Padding(
                  padding: EdgeInsets.only(top: 12.0),
                  child: Text(
                    content,
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                ),
              ),
            )
          ],
        ),
      ),
    );

class ModalAlertAction {
  @required
  String label;
  @required
  Function onPressed;
  bool isDestructiveAction;
  bool isDefaultAction;

  ModalAlertAction({
    this.label,
    this.onPressed,
    this.isDestructiveAction,
    this.isDefaultAction,
  });
}

class ModalBottomAction {
  Widget child;
  Function onTap;
  bool isDestructiveAction;
  bool isDefaultAction;
  bool isCurrentAction;
  bool isInactiveAction;
  IconData icon;

  ModalBottomAction(
      {this.child,
      this.onTap,
      this.isDefaultAction = false,
      this.isCurrentAction = false,
      this.isDestructiveAction = false,
      this.isInactiveAction = false,
      this.icon});
}
