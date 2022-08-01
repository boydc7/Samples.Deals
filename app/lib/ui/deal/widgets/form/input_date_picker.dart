import 'package:flutter/material.dart';
import 'package:flutter/cupertino.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/app/utils.dart';

class DealInputDatePicker extends StatefulWidget {
  final String title;
  final DateTime currentValue;
  final bool reactivate;
  final bool supportsNoDate;
  final Function onCancel;
  final Function onContinue;

  DealInputDatePicker({
    @required this.title,
    @required this.onContinue,
    @required this.currentValue,
    this.onCancel,
    this.reactivate = false,
    this.supportsNoDate = true,
  });

  @override
  _DealInputDatePickerState createState() => _DealInputDatePickerState();
}

class _DealInputDatePickerState extends State<DealInputDatePicker> {
  /// this is set by the picker or will default to the default date
  DateTime _date;

  /// if the incoming / original value is set to null or maxdate then
  /// the deal currently is set to never end so we set the flag
  /// which we can use to style the widget accordingly
  bool _neverEnds;

  /// set the default date to tomorrow 8pm
  DateTime _defaultDate = DateTime(
      DateTime.now().year, DateTime.now().month, DateTime.now().day + 1, 20);

  @override
  void initState() {
    super.initState();

    _neverEnds = widget.currentValue == null || widget.currentValue == maxDate;
  }

  @override
  void dispose() {
    super.dispose();
  }

  void handleDate() => widget.onContinue(_date ?? _defaultDate);

  void handleNeverExpires() => widget.onContinue(maxDate);

  void _cancel() =>
      widget.onCancel != null ? widget.onCancel() : Navigator.of(context).pop();

  @override
  Widget build(BuildContext context) => Container(
        color: Theme.of(context).appBarTheme.color,
        height: MediaQuery.of(context).copyWith().size.height / 2,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                AppBarCloseButton(
                  context,
                  onPressed: widget.onCancel,
                ),
                Container(
                  padding: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  child: Text(widget.title,
                      style: Theme.of(context).textTheme.headline6),
                ),
              ],
            ),
            Divider(height: 1),
            Expanded(
              child: CupertinoTheme(
                data: CupertinoThemeData(
                  brightness: Theme.of(context).brightness,
                ),
                child: CupertinoDatePicker(
                  minimumDate: DateTime.now().subtract(Duration(days: 1)),

                  /// NOTE! leave minute interval to default 1 otherwise widget can throw errors
                  /// if we get dates that are not divisible by the 15 minute increments we had it set as
                  /// => don't use this => minuteInterval: 15,
                  backgroundColor: Theme.of(context).appBarTheme.color,
                  initialDateTime: !_neverEnds
                      ? widget.currentValue.toLocal()
                      : _defaultDate,
                  onDateTimeChanged: (DateTime newDate) =>
                      _date = newDate.toUtc(),
                  maximumYear: DateTime.now().year + 2,
                  minimumYear: DateTime.now().year,
                  mode: CupertinoDatePickerMode.dateAndTime,
                ),
              ),
            ),
            SafeArea(
              bottom: true,
              child: Padding(
                padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
                child: Row(
                  children: <Widget>[
                    /// if the original value is set to maxDate (never expires) then the first button
                    /// will cancel the user out of the bottom sheet, otherwise it'll let them set it to never expired
                    Expanded(
                      child: SecondaryButton(
                        label: _neverEnds || widget.supportsNoDate == false
                            ? "Cancel"
                            : "No Expiration",
                        primary: !_neverEnds,
                        onTap: _neverEnds || widget.supportsNoDate == false
                            ? _cancel
                            : handleNeverExpires,
                      ),
                    ),
                    SizedBox(width: 8),

                    /// depeding on whether or not the original date is set to never expires
                    /// we'll make this continue button larger using flex 1 vs. 2
                    Expanded(
                      flex: _neverEnds ? 2 : 1,
                      child: PrimaryButton(
                        label: widget.reactivate ? "Extend RYDR" : "Continue",
                        onTap: handleDate,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      );
}
