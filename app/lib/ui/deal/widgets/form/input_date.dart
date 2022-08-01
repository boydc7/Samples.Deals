import 'package:flutter/material.dart';
import 'package:rydr_app/models/deal_expiration_info.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_date_picker.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_dropdown.dart';
import 'package:rydr_app/app/utils.dart';

class DealInputDate extends StatelessWidget {
  final String labelText;
  final String emtpyText;
  final Stream<DateTime> value;
  final Function handleUpdate;
  final bool supportsNoDate;
  final bool readOnly;

  DealInputDate({
    @required this.labelText,
    @required this.emtpyText,
    @required this.value,
    @required this.handleUpdate,
    this.supportsNoDate = true,
    this.readOnly = false,
  });

  void _edit(BuildContext context, DateTime currentValue) {
    showModalBottomSheet(
        context: context,
        builder: (BuildContext builder) => DealInputDatePicker(
              title: labelText,
              currentValue: currentValue,
              supportsNoDate: supportsNoDate,
              onContinue: (DateTime newValue) {
                Navigator.of(context).pop();

                FocusScope.of(context).requestFocus(FocusNode());

                handleUpdate(newValue);
              },
            ));
  }

  @override
  Widget build(BuildContext context) => StreamBuilder<DateTime>(
      stream: value,
      builder: (context, snapshot) {
        final currentVal = snapshot.data == null || snapshot.data == maxDate
            ? ""
            : snapshot.data.toString();

        return readOnly
            ? _buildReadOnly(context, snapshot.data)
            : Padding(
                padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
                child: DealTextDropdown(
                  onTap: () => _edit(context, snapshot.data),
                  labelText: labelText,
                  value: currentVal,
                  valueDisplay: currentVal == ""
                      ? emtpyText
                      : DealExpirationInfo(snapshot.data).display,
                ),
              );
      });

  Widget _buildReadOnly(BuildContext context, DateTime date) {
    if (date == null) {
      return Container();
    }

    final DealExpirationInfo dateInfo = DealExpirationInfo(date.toUtc());

    return ListTile(
      title: Text(
        labelText,
        style: Theme.of(context).textTheme.bodyText2,
      ),
      trailing: !dateInfo.neverExpires && dateInfo.daysLeft < 5
          ? Container(
              width: 120,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: <Widget>[
                  Container(
                    height: 28.0,
                    alignment: Alignment.center,
                    margin: EdgeInsets.only(left: 2.0),
                    padding:
                        EdgeInsets.symmetric(horizontal: 10.0, vertical: 4.0),
                    decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(32.0),
                        color: dateInfo.isExpired
                            ? Theme.of(context).hintColor
                            : dateInfo.daysLeft == 1
                                ? Colors.deepOrange.shade100
                                : Colors.amber.shade100),
                    child: Text(
                      dateInfo.displayTimeLeft,
                      style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(
                              fontWeight: FontWeight.w500,
                              height: 1.0,
                              color: dateInfo.isExpired
                                  ? Theme.of(context).appBarTheme.color
                                  : dateInfo.daysLeft == 1
                                      ? Colors.deepOrange.shade700
                                      : Colors.orange.shade700,
                            ),
                          ),
                    ),
                  ),
                ],
              ),
            )
          : Text(dateInfo.simpleDisplay),
    );
  }
}
