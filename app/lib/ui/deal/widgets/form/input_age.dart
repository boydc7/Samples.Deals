import 'package:flutter/material.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_toggle.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

class DealInputAge extends StatelessWidget {
  final Stream<bool> valueStream;
  final Function handleUpdate;
  final bool isExpired;

  DealInputAge({
    @required this.valueStream,
    @required this.handleUpdate,
    this.isExpired = false,
  });

  void _edit(BuildContext context, bool value) {
    FocusScope.of(context).requestFocus(FocusNode());

    handleUpdate(value);
  }

  void _showAgeRestricitonAlert(BuildContext context, bool value) {
    if (value) {
      showSharedModalAlert(context, Text("Age Restriction"),
          content: Text(
              "This will mark this RYDR as 21+.\n\nWe cannot guarantee that all Creator's requesting this RYDR will be over 21. Always check for a valid driver's license."),
          actions: <ModalAlertAction>[
            ModalAlertAction(
                label: "Ok",
                onPressed: () {
                  Navigator.pop(context);
                  _edit(context, value);
                })
          ]);
    } else {
      FocusScope.of(context).requestFocus(FocusNode());

      handleUpdate(value);
    }
  }

  @override
  Widget build(BuildContext context) => StreamBuilder<bool>(
      stream: valueStream,
      builder: (context, snapshot) => isExpired
          ? ListTile(
              title: Text(
                'Age Restriction',
                style: Theme.of(context).textTheme.bodyText2,
              ),
              trailing: Text(snapshot.data != null && snapshot.data == true
                  ? "Yes"
                  : "None"),
            )
          : Padding(
              padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
              child: DealTextToggle(
                labelText: 'Age Restriction',
                subtitleText: '21+ Required',
                selected: snapshot.data != null && snapshot.data == true,
                onChange: (value) => _showAgeRestricitonAlert(context, value),
              )));
}
