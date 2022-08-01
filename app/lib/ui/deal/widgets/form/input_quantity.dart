import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_dropdown.dart';
import 'package:rydr_app/ui/deal/utils.dart';

class DealInputQuantity extends StatelessWidget {
  final Stream<int> valueStream;
  final Function handleUpdate;

  DealInputQuantity({
    @required this.valueStream,
    @required this.handleUpdate,
  });

  void _edit(BuildContext context, int currentValue) {
    showDealCounterChoice(
        context: context,
        counterType: 'quantity',
        currentValue: currentValue,
        onContinue: (int newValue) {
          Navigator.of(context).pop();
          FocusScope.of(context).requestFocus(FocusNode());

          handleUpdate(newValue);
        },
        onCancel: () => Navigator.of(context).pop());
  }

  @override
  Widget build(BuildContext context) {
    final NumberFormat quantity = NumberFormat.decimalPattern();

    return StreamBuilder<int>(
      stream: valueStream,
      builder: (context, snapshot) => Padding(
        padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
        child: DealTextDropdown(
          onTap: () => _edit(context, snapshot.data),
          labelText: 'Quantity Available*',
          value: snapshot.data == null ? "" : quantity.format(snapshot.data),
          valueDisplay: snapshot.data == null
              ? ""
              : snapshot.data == 0
                  ? "Unlimited"
                  : quantity.format(snapshot.data),
        ),
      ),
    );
  }
}
