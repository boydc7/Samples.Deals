import 'package:flutter/material.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_dropdown.dart';
import 'package:rydr_app/ui/deal/utils.dart';

class DealInputPosts extends StatelessWidget {
  final Stream<int> valueStream;
  final Function handleUpdate;

  DealInputPosts({
    this.valueStream,
    this.handleUpdate,
  });

  void _edit(BuildContext context, int currentValue) {
    showDealCounterChoice(
        context: context,
        counterType: 'posts',
        currentValue: currentValue,
        onContinue: (int newValue) {
          Navigator.of(context).pop();

          FocusScope.of(context).requestFocus(FocusNode());

          handleUpdate(newValue);
        },
        onCancel: () => Navigator.of(context).pop());
  }

  @override
  Widget build(BuildContext context) => StreamBuilder<int>(
        stream: valueStream,
        builder: (context, snapshot) => Padding(
          padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
          child: DealTextDropdown(
            onTap: () => _edit(context, snapshot.data),
            labelText: 'Instagram Posts',
            value: snapshot.data == null ? "" : snapshot.data.toString(),
            valueDisplay: snapshot.data == null
                ? ""
                : snapshot.data == 0 ? "None" : snapshot.data.toString(),
          ),
        ),
      );
}
