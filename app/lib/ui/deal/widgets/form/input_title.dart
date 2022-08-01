import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_field.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_media.dart';

class DealInputTitle extends StatefulWidget {
  final DealType dealType;
  final BehaviorSubject<String> valueStream;
  final Function handleUpdate;
  final Function handleUpdateFocus;
  final Stream<bool> focusStream;
  final Stream<int> charStream;
  final Stream<PublisherMedia> mediaStream;
  final Function handleUpdateMedia;

  DealInputTitle({
    this.dealType = DealType.Deal,
    this.valueStream,
    @required this.handleUpdate,
    @required this.handleUpdateFocus,
    @required this.focusStream,
    @required this.charStream,
    this.mediaStream,
    this.handleUpdateMedia,
  });

  @override
  _DealInputTitleState createState() => _DealInputTitleState();
}

class _DealInputTitleState extends State<DealInputTitle> {
  final FocusNode focusNode = FocusNode();
  TextEditingController controller;

  @override
  void initState() {
    super.initState();

    controller = TextEditingController(text: widget.valueStream.value ?? '');

    controller.addListener(() {
      widget.handleUpdate(controller.text.trim());
      if (controller.text.length == 40) {
        HapticFeedback.vibrate();
      }
    });

    focusNode.addListener(() => widget.handleUpdateFocus(focusNode.hasFocus));
  }

  @override
  void dispose() {
    controller.dispose();
    focusNode.dispose();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Column(
        children: <Widget>[
          Padding(
            padding: EdgeInsets.only(left: 16, right: 16),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                /// if we don't pass a handler for chosing/updating an image
                /// then don't show the image picker here...
                widget.handleUpdateMedia == null
                    ? Container()
                    : Row(
                        children: <Widget>[
                          StreamBuilder<PublisherMedia>(
                              stream: widget.mediaStream,
                              builder: (context, snapshot) {
                                final PublisherMedia existingMedia =
                                    snapshot.data;

                                /// if we have no media, or the streams' last value is still null
                                /// then purposefully return back the same widget, but wrap in container
                                /// to force a full re-build of a different widget if/when there is another value on the stream
                                return existingMedia == null
                                    ? Container(
                                        child: DealMedia(
                                          existingMedia: null,
                                          currentDealStatus: DealStatus.draft,
                                          onChoose: widget.handleUpdateMedia,
                                        ),
                                      )
                                    : DealMedia(
                                        existingMedia: existingMedia,
                                        currentDealStatus: DealStatus.draft,
                                        onChoose: widget.handleUpdateMedia,
                                      );
                              }),
                          SizedBox(width: 8.0),
                        ],
                      ),
                Expanded(
                  child: DealTextField(
                    controller: controller,
                    isVirtual: widget.dealType == DealType.Virtual,
                    focusNode: focusNode,
                    minLines: 1,
                    maxLines: 3,
                    maxCharacters: 40,
                    labelText: widget.dealType == DealType.Event
                        ? 'Event Title'
                        : 'Title of Your RYDR*',
                    hintText: widget.dealType == DealType.Event
                        ? 'Give it a short and distinct name...'
                        : 'e.g.- Fresh Coffee and Pastries!',
                  ),
                ),
              ],
            ),
          ),
          StreamBuilder<bool>(
            stream: widget.focusStream,
            builder: (context, snapshot) {
              return AnimatedContainer(
                duration: Duration(milliseconds: 250),
                height:
                    snapshot.data != null && snapshot.data == true ? 16.0 : 0.0,
                child: Container(
                  padding: EdgeInsets.only(top: 4.0, right: 16.0),
                  alignment: Alignment.centerRight,
                  child: ListView(
                    padding: EdgeInsets.only(top: 0),
                    physics: NeverScrollableScrollPhysics(),
                    children: <Widget>[
                      StreamBuilder<int>(
                        stream: widget.charStream,
                        builder: (context, snapshot) {
                          final count = snapshot.data ?? 0;
                          final int min = 10;
                          final int remaining = min - count;

                          if (min - count <= 0) {
                            return Align(
                              alignment: Alignment.bottomRight,
                              child: Padding(
                                padding: EdgeInsets.only(right: 2.0, top: 1.0),
                                child: Icon(
                                  AppIcons.spellCheck,
                                  size: 10.5,
                                  color: AppColors.successGreen,
                                ),
                              ),
                            );
                          } else {
                            return Text(
                              '$remaining',
                              textAlign: TextAlign.right,
                              style: Theme.of(context).textTheme.caption.merge(
                                    TextStyle(
                                      fontWeight: FontWeight.w500,
                                      color: AppColors.errorRed,
                                    ),
                                  ),
                            );
                          }
                        },
                      ),
                    ],
                  ),
                ),
              );
            },
          ),
        ],
      );
}
