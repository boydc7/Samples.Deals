import 'package:flutter/material.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/state.dart';

class ImageError extends StatefulWidget {
  final String logUrl;
  final String logParentName;
  final int logPublisherAccountId;
  final Widget errorWidget;

  ImageError({
    @required this.logUrl,
    @required this.logParentName,
    this.logPublisherAccountId,
    this.errorWidget,
  });

  @override
  _ImageErrorState createState() => _ImageErrorState();
}

class _ImageErrorState extends State<ImageError> {
  @override
  void initState() {
    super.initState();

    final int publisherAccountId = widget.logPublisherAccountId != null
        ? widget.logPublisherAccountId
        : appState.currentProfile != null ? appState.currentProfile.id : 0;

    /// log error
    AppErrorLogger.instance.reportError(
      'Other',
      {
        "url": widget.logUrl,
        "parent": widget.logParentName,
        "publisherAccountId": publisherAccountId
      },
      StackTrace.current,
    );
  }

  @override
  Widget build(BuildContext context) {
    return widget.errorWidget ?? Container();
  }
}
