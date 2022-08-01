import 'package:flutter/material.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_use_recent.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_field.dart';

class DealInputApprovalNotes extends StatefulWidget {
  final BehaviorSubject<String> valueStream;
  final Function handleUpdate;
  final Function handleUpdateFocus;
  final Stream<bool> focusStream;
  final bool isExpired;
  final bool isVirtual;

  DealInputApprovalNotes({
    this.valueStream,
    @required this.handleUpdate,
    @required this.handleUpdateFocus,
    @required this.focusStream,
    this.isExpired = false,
    this.isVirtual = false,
  });

  @override
  _DealInputApprovalNotesState createState() => _DealInputApprovalNotesState();
}

class _DealInputApprovalNotesState extends State<DealInputApprovalNotes> {
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

  @override
  Widget build(BuildContext context) => widget.isExpired
      ? Container()
      : Padding(
          padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              DealTextField(
                isVirtual: widget.isVirtual,
                controller: controller,
                focusNode: focusNode,
                minLines: 5,
                maxLines: 8,
                maxCharacters: 400,
                labelText: 'Steps to Redeem',
                hintText:
                    'e.g.- Please arrive 15 minutes early and see John at the Host Desk!',
              ),
              SizedBox(height: 8),
              Text(
                "This message is shown to approved Creators only.",
                textAlign: TextAlign.left,
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(
                        color: Theme.of(context).hintColor,
                      ),
                    ),
              ),
              StreamBuilder<bool>(
                  stream: widget.focusStream,
                  builder: (context, snapshot) => snapshot.data == true
                      ? Padding(
                          padding: EdgeInsets.symmetric(vertical: 16),
                          child: UseRecent(
                            controller: controller,
                            title: "Most Recent Approval Notes",
                            fieldToUse: "approvalNotes",
                          ),
                        )
                      : Container()),
            ],
          ),
        );
}
