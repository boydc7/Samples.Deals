import 'package:flutter/material.dart';

class ListHelper {
  DateTime today;
  DateTime yesterday;
  DateTime thisWeek;
  DateTime lastWeek;
  DateTime thisMonth;
  DateTime lastMonth;

  ListHelper() {
    final DateTime now = DateTime.now();

    today = DateTime(now.year, now.month, now.day);
    yesterday = today.subtract(Duration(days: 1));
    thisWeek = today.subtract(Duration(days: today.weekday));
    lastWeek = today
        .subtract(Duration(days: today.weekday))
        .subtract(Duration(days: 7));
    thisMonth = DateTime(now.year, now.month, 1);
    lastMonth = DateTime(now.year, now.month - 1, 1);
  }

  Widget addListHeader(BuildContext context, String label, int index) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        index > 0
            ? Column(
                children: <Widget>[SizedBox(height: 4.0), Divider(height: 1)])
            : Container(),
        Padding(
          padding: EdgeInsets.only(
            top: 16.0,
            left: 16.0,
            bottom: 8.0,
          ),
          child: Text(
            label,
            style: Theme.of(context).textTheme.bodyText1,
          ),
        ),
      ],
    );
  }

  Widget addDateHeader(
    BuildContext context,
    DateTime lastDate,
    DateTime currentDate,
    int index,
  ) {
    final String currentLabel = _labelCurrent(currentDate);

    return _labelLast(lastDate) == currentLabel
        ? Container()
        : addListHeader(context, currentLabel, index);
  }

  String _labelLast(DateTime lastDate) =>
      lastDate == null ? null : _label(lastDate);
  String _labelCurrent(DateTime currentDate) => _label(currentDate);

  String _label(DateTime dateTime) {
    final DateTime abs = DateTime(dateTime.year, dateTime.month, dateTime.day);

    if (abs == today) {
      return "Today";
    } else if (abs == yesterday) {
      return "Yesterday";
    } else if (abs.isAtSameMomentAs(thisWeek) || abs.isAfter(thisWeek)) {
      return "This Week";
    } else if (abs.isAtSameMomentAs(lastWeek) || abs.isAfter(lastWeek)) {
      return "Last Week";
    } else if (abs.isAtSameMomentAs(thisMonth) || abs.isAfter(thisMonth)) {
      return "This Month";
    } else if (abs.isAtSameMomentAs(lastMonth) || abs.isAfter(lastMonth)) {
      return "Last Month";
    } else {
      return "Older";
    }
  }
}
