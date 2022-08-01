import 'package:flutter/material.dart';
import 'dart:ui';
import 'package:flutter/cupertino.dart';
import 'package:intl/intl.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class DealInputCounterPicker extends StatefulWidget {
  final String counterType;
  final int currentValue;
  final String continueLabel;
  final Function onCancel;
  final Function onContinue;
  final bool enableContinue;

  DealInputCounterPicker({
    @required this.counterType,
    @required this.onContinue,
    @required this.onCancel,
    @required this.currentValue,
    this.continueLabel,
    this.enableContinue = false,
  });

  @override
  _DealInputCounterPickerState createState() => _DealInputCounterPickerState();
}

class _DealInputCounterPickerState extends State<DealInputCounterPicker> {
  final NumberFormat f = NumberFormat.decimalPattern();
  DealSharedCounterPickerBloc _bloc;
  FixedExtentScrollController _controller;

  String title = '';
  List<int> counts = [];

  @override
  void initState() {
    super.initState();

    if (widget.counterType == 'followerCount') {
      title = 'Follower Thresholds';

      counts = [
        0,
        500,
        1000,
        2500,
        5000,
        12000,
        25000,
        50000,
        100000,
        250000,
        500000,
      ];
    } else if (widget.counterType == 'quantity') {
      title = 'Total RYDRs Available';

      counts = List.generate(100, (int index) => index);
    } else if (widget.counterType == 'stories') {
      title = 'Instagram Stories';

      counts = List.generate(20, (int index) => index);
    } else if (widget.counterType == 'posts') {
      title = 'Instagram Posts';

      counts = List.generate(20, (int index) => index);
    } else if (widget.counterType == 'preEventDay') {
      title = 'Pre-event Posting';

      counts = List.generate(30, (int index) => index);
    }

    if (widget.currentValue != null) {
      _controller = FixedExtentScrollController(
          initialItem: counts.indexOf(widget.currentValue));
    }

    _bloc = DealSharedCounterPickerBloc(
      widget.currentValue,
      widget.enableContinue,
    );
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _onContinue() => widget.onContinue(_bloc.followerCount.value);

  @override
  Widget build(BuildContext context) {
    List<Widget> choices = [];

    if (widget.counterType == 'followerCount') {
      choices = counts
          .map((count) => Padding(
              padding: EdgeInsets.only(top: 12, bottom: 0),
              child: Text(
                count == 500000
                    ? '${f.format(count)}+ followers'
                    : '${f.format(count)} followers',
                style: TextStyle(
                    fontSize: 18,
                    color: Theme.of(context).textTheme.bodyText2.color),
              )))
          .toList();
    } else if (widget.counterType == 'quantity') {
      choices = counts
          .map((count) => Padding(
              padding: EdgeInsets.only(top: 12, bottom: 0),
              child: Text(
                count == 0 ? 'Unlimited' : count.toString(),
                style: TextStyle(
                    fontSize: 18,
                    color: Theme.of(context).textTheme.bodyText2.color),
              )))
          .toList();
    } else if (widget.counterType == 'preEventDay') {
      choices = counts
          .map((count) => Padding(
              padding: EdgeInsets.only(top: 12, bottom: 0),
              child: Text(
                count == 0
                    ? "No posts before event"
                    : count == 1
                        ? count.toString() + " day"
                        : count.toString() + " days",
                style: TextStyle(
                    fontSize: 18,
                    color: Theme.of(context).textTheme.bodyText2.color),
              )))
          .toList();
    } else {
      choices = counts
          .map((count) => Padding(
              padding: EdgeInsets.only(top: 12, bottom: 0),
              child: Text(
                count.toString(),
                style: TextStyle(
                    fontSize: 18,
                    color: Theme.of(context).textTheme.bodyText2.color),
              )))
          .toList();
    }

    return Container(
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
                child:
                    Text(title, style: Theme.of(context).textTheme.headline6),
              ),
            ],
          ),
          Divider(height: 1),
          Expanded(
            child: CupertinoTheme(
              data: CupertinoThemeData(
                brightness: Theme.of(context).brightness,
              ),
              child: CupertinoPicker(
                scrollController: _controller,
                useMagnifier: true,
                onSelectedItemChanged: (value) =>
                    _bloc.setFollowerCount(counts[value]),
                backgroundColor: Theme.of(context).appBarTheme.color,
                itemExtent: 48,
                children: choices,
              ),
            ),
          ),
          SafeArea(
            bottom: true,
            child: Padding(
              padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
              child: Row(
                children: <Widget>[
                  Expanded(
                      child: SecondaryButton(
                    label: "Cancel",
                    onTap: widget.onCancel,
                  )),
                  SizedBox(width: 8),
                  Expanded(
                    child: StreamBuilder<bool>(
                        stream: _bloc.canContinue,
                        builder: (context, snapshot) => PrimaryButton(
                              label: widget.continueLabel,
                              onTap: snapshot.data == true ? _onContinue : null,
                            )),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class DealSharedCounterPickerBloc {
  final _followerCount = BehaviorSubject<int>();
  final _canContinue = BehaviorSubject<bool>();

  int _originalValue;
  int get defaultFollowerCount => 1000;

  DealSharedCounterPickerBloc(int originalValue,
      [bool enableContinue = false]) {
    _originalValue = originalValue;

    /// after setting orignal values
    /// check if we want to enable the continue by default
    _canContinue.sink.add(enableContinue);
  }

  dispose() {
    _followerCount.close();
    _canContinue.close();
  }

  BehaviorSubject<int> get followerCount => _followerCount.stream;
  BehaviorSubject<bool> get canContinue => _canContinue.stream;

  void setFollowerCount(int val) {
    _followerCount.sink.add(val);
    _canContinue.sink.add(val != _originalValue);
  }
}
