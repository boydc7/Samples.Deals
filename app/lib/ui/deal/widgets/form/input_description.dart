import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_use_recent.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_field.dart';

class DealInputDescription extends StatefulWidget {
  final DealType dealType;
  final BehaviorSubject<String> valueStream;
  final Function handleUpdate;
  final Function handleUpdateFocus;
  final Stream<bool> focusStream;
  final Stream<int> charStream;

  DealInputDescription({
    this.dealType = DealType.Deal,
    this.valueStream,
    @required this.handleUpdate,
    @required this.handleUpdateFocus,
    @required this.focusStream,
    @required this.charStream,
  });

  @override
  _DealInputDescriptionState createState() => _DealInputDescriptionState();
}

class _DealInputDescriptionState extends State<DealInputDescription> {
  final FocusNode focusNode = FocusNode();
  TextEditingController controller;

  @override
  void initState() {
    super.initState();

    controller = TextEditingController(text: widget.valueStream.value ?? '');

    controller.addListener(() {
      widget.handleUpdate(controller.text.trim());
      if (controller.text.length == 300) {
        HapticFeedback.vibrate();
      }
    });

    focusNode.addListener(() => widget.handleUpdateFocus(focusNode.hasFocus));
  }

  @override
  void dispose() {
    /// remove focus on the bloc before we dispose
    widget.handleUpdateFocus(false);

    controller.dispose();
    focusNode.dispose();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Padding(
        padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
        child: Column(
          children: <Widget>[
            DealTextField(
              isVirtual: widget.dealType == DealType.Virtual,
              controller: controller,
              focusNode: focusNode,
              minLines: 3,
              maxLines: 5,
              maxCharacters: 300,
              labelText: widget.dealType == DealType.Event
                  ? 'Event Description*'
                  : 'Description of Your RYDR*',
              hintText: widget.dealType == DealType.Event
                  ? 'e.g.- Come join us for an amazing night!'
                  : 'e.g.- Come in on any Monday and choose any of our fresh coffees and pastries.',
            ),
            StreamBuilder<bool>(
              stream: widget.focusStream,
              builder: (context, snapshot) {
                if (snapshot.data != null && snapshot.data == true) {
                  return Container(
                    padding: EdgeInsets.only(top: 8),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Expanded(
                          child: UseRecent(
                            controller: controller,
                            title: "Recent Descriptions",
                            fieldToUse: "description",
                          ),
                        ),
                        StreamBuilder<int>(
                          stream: widget.charStream,
                          builder: (context, snapshot) {
                            final count = snapshot.data ?? 0;
                            final int max = 25;
                            final int remaining = max - count;

                            if (max - count <= 0) {
                              return Align(
                                alignment: Alignment.bottomRight,
                                child: Padding(
                                  padding:
                                      EdgeInsets.only(right: 2.0, top: 1.0),
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
                                style:
                                    Theme.of(context).textTheme.caption.merge(
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
                  );
                } else {
                  return Container(height: 0, width: 0);
                }
              },
            ),
          ],
        ),
      );
}
