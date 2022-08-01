import 'package:flutter/material.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_use_recent.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_field.dart';

import 'package:rydr_app/app/utils.dart';

class DealInputReceiveNotes extends StatefulWidget {
  final DealType dealType;
  final BehaviorSubject<String> valueStream;
  final Function handleUpdate;
  final Function handleUpdateFocus;
  final Stream<bool> focusStream;
  final String placeName;

  DealInputReceiveNotes({
    this.dealType = DealType.Deal,
    this.valueStream,
    @required this.handleUpdate,
    @required this.handleUpdateFocus,
    @required this.focusStream,
    @required this.placeName,
  });

  @override
  _DealInputReceiveNotesState createState() => _DealInputReceiveNotesState();
}

class _DealInputReceiveNotesState extends State<DealInputReceiveNotes> {
  final FocusNode focusNode = FocusNode();
  TextEditingController controller;

  @override
  void initState() {
    super.initState();

    controller = TextEditingController(text: widget.valueStream.value ?? '');

    controller.addListener(() => widget.handleUpdate(controller.text.trim()));

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

  void _insertText(String text) {
    final String currentText = controller.text;
    final int cursorPosition = controller.selection.base.offset;
    final int newCursorPosition = cursorPosition + text.length + 1;
    final String leadingSpace = cursorPosition == 0
        ? ''
        : currentText.substring(cursorPosition - 1, cursorPosition) == ' '
            ? ''
            : ' ';
    final String trailingSpace = cursorPosition >= currentText.length
        ? ' '
        : currentText.substring(cursorPosition, cursorPosition + 1) == ' '
            ? ''
            : ' ';

    final String textToInsert = '$leadingSpace$text$trailingSpace';

    controller.value = controller.value.copyWith(
        text:
            Utils.addCharAtPosition(currentText, textToInsert, cursorPosition),
        selection: TextSelection(
          baseOffset: newCursorPosition,
          extentOffset: newCursorPosition,
        ));
  }

  @override
  Widget build(BuildContext context) => Column(
        children: <Widget>[
          Padding(
            padding: EdgeInsets.only(left: 16, right: 16),
            child: DealTextField(
              isVirtual: widget.dealType == DealType.Virtual,
              controller: controller,
              focusNode: focusNode,
              minLines: 3,
              maxLines: 5,
              maxCharacters: 400,
              labelText: widget.dealType == DealType.Event
                  ? 'Suggestions for Posts from the Event'
                  : 'Post Content Guidelines',
              hintText: widget.dealType == DealType.Event
                  ? 'e.g.- Please be sure to tag ${appState.currentProfile.userName} and ${widget.placeName}...'
                  : 'Include location tags, hashtags, or mentions here.',
            ),
          ),
          StreamBuilder<bool>(
              stream: widget.focusStream,
              builder: (context, snapshot) {
                if (snapshot.data != null && snapshot.data == true) {
                  return Container(
                    padding: EdgeInsets.only(top: 8, left: 16),
                    height: 56,
                    child: Row(
                      children: <Widget>[
                        UseRecent(
                          controller: controller,
                          title: "Recent Post Suggestions",
                          fieldToUse: "receiveNotes",
                        ),
                        Expanded(
                          child: ListView(
                            scrollDirection: Axis.horizontal,
                            padding: EdgeInsets.only(left: 16, right: 16),
                            children: <Widget>[
                              _buildActionChip(
                                  '@${appState.currentProfile.userName}'),
                              SizedBox(width: 8),
                              widget.placeName != null
                                  ? _buildActionChip(widget.placeName)
                                  : Container(),
                            ],
                          ),
                        )
                      ],
                    ),
                  );
                } else {
                  return Container(height: 0, width: 0);
                }
              }),
          SizedBox(height: 16),
        ],
      );

  Widget _buildActionChip(String label) => ActionChip(
        pressElevation: 1.0,
        onPressed: () => _insertText(label),
        avatar: Icon(AppIcons.plus, size: 16),
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
        shape: OutlineInputBorder(
          borderRadius: BorderRadius.circular(40),
          borderSide: BorderSide(
            width: 1.0,
            color: Theme.of(context).iconTheme.color,
          ),
        ),
        labelStyle: Theme.of(context).textTheme.bodyText1.merge(
              TextStyle(
                color: Theme.of(context).iconTheme.color,
              ),
            ),
        label: Text(label),
      );
}
