import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_dropdown.dart';
import 'package:rydr_app/ui/deal/utils.dart';

class DealInputFollowerCount extends StatelessWidget {
  final Stream<int> valueStream;
  final Function handleUpdate;
  final bool isExpired;

  DealInputFollowerCount({
    @required this.valueStream,
    @required this.handleUpdate,
    this.isExpired = false,
  });

  void _edit(BuildContext context, int snapshot) => showDealMinFollowersChoice(
      context: context,
      currentValue: snapshot,
      onContinue: (newValue) {
        Navigator.of(context).pop();
        FocusScope.of(context).requestFocus(FocusNode());

        handleUpdate(newValue);
      },
      onCancel: () => Navigator.of(context).pop());

  @override
  Widget build(BuildContext context) {
    final NumberFormat followers = NumberFormat.decimalPattern();

    return StreamBuilder<int>(
      stream: valueStream,
      builder: (context, snapshot) => isExpired
          ? ListTile(
              title: Text(
                'Minimum Follower Count',
                style: Theme.of(context).textTheme.bodyText2,
              ),
              trailing: Text(
                snapshot.data == null ? "" : followers.format(snapshot.data),
              ),
            )
          : Padding(
              padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
              child: DealTextDropdown(
                onTap: () => _edit(context, snapshot.data),
                labelText: 'Minimum Follower Count',
                value: snapshot.data == null
                    ? ""
                    : followers.format(snapshot.data),
                valueDisplay: snapshot.data == null
                    ? ""
                    : snapshot.data == 0
                        ? "Any"
                        : snapshot.data == 500000
                            ? "500,000+ followers"
                            : followers.format(snapshot.data) + " followers",
              ),
            ),
    );
  }
}
