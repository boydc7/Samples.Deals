import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_toggle.dart';

class DealInputAutoApprove extends StatelessWidget {
  final Stream<bool> valueStream;
  final Stream<int> quantityStream;
  final Function handleUpdate;
  final bool canApproveUnlimited;
  final DealType dealType;

  DealInputAutoApprove({
    @required this.valueStream,
    @required this.quantityStream,
    @required this.handleUpdate,
    this.dealType = DealType.Deal,
    this.canApproveUnlimited = false,
  });

  void _edit(BuildContext context, bool value) {
    FocusScope.of(context).requestFocus(FocusNode());

    handleUpdate(value);
  }

  @override
  Widget build(BuildContext context) => StreamBuilder<bool>(
        stream: valueStream,
        builder: (context, snapshot) {
          final bool autoApprove = snapshot.data == true;

          return StreamBuilder<int>(
            stream: quantityStream,
            builder: (context, snapshot) {
              final int maxApprovals = snapshot.data ?? 0;
              final String labelSingular =
                  dealType == DealType.Event ? "RSVP" : "Request";
              final String labelPlural =
                  dealType == DealType.Event ? "RSVPs" : "Requests";

              return Padding(
                padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
                child: DealTextToggle(
                  labelText: maxApprovals == 0 && !canApproveUnlimited
                      ? "Disabled: Auto-Approve"
                      : maxApprovals == 0
                          ? "Auto-approve all $labelPlural"
                          : maxApprovals == 1
                              ? "Auto-approve the Only RYDR $labelSingular"
                              : "Auto-approve all $maxApprovals $labelPlural",
                  subtitleText: maxApprovals == 0 && !canApproveUnlimited
                      ? "You cannot auto-approve unlimited $labelPlural"
                      : maxApprovals == 1
                          ? "First $labelSingular will be approved, then RYDR will expire."
                          : "Approve Creators that meet your minimums",
                  selected: autoApprove,
                  onChange: maxApprovals == 0 && !canApproveUnlimited
                      ? null
                      : (value) => _edit(context, value),
                ),
              );
            },
          );
        },
      );
}
