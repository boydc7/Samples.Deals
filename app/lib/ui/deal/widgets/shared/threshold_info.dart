import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/models/enums/deal.dart';

class DealThresholdInfo extends StatelessWidget {
  final DealType dealType;
  final Stream<double> engagementRatingStream;
  final Stream<int> followerCountStream;

  DealThresholdInfo({
    this.dealType = DealType.Deal,
    @required this.engagementRatingStream,
    @required this.followerCountStream,
  });

  @override
  Widget build(BuildContext context) => StreamBuilder<double>(
      stream: engagementRatingStream,
      builder: (context, snapshot) {
        final double currentValue =
            snapshot.data == null || snapshot.data < 2 ? 2 : snapshot.data;

        final String engRatingFormatted = currentValue == 2
            ? "a low"
            : currentValue == 4.5
                ? "a medium"
                : currentValue == 7.0 ? "a high" : "an amazing";
        final NumberFormat f = NumberFormat.decimalPattern();

        return StreamBuilder<int>(
          stream: followerCountStream,
          builder: (context, snapshot) {
            final int followerCount = snapshot.data ?? -1;

            return Container(
              padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
              alignment: Alignment.center,
              child: Text(
                followerCount == null && currentValue == null
                    ? dealType == DealType.Event
                        ? "Anyone can RSVP to this event."
                        : "This RYDR can be requested by anyone."
                    : followerCount == 0 && currentValue == 0
                        ? dealType == DealType.Event
                            ? "Anyone can RSVP to this event."
                            : "This RYDR can be requested by anyone."
                        : (currentValue == null || currentValue == 0) &&
                                followerCount == 0
                            ? dealType == DealType.Event
                                ? "Anyone can RSVP to this event."
                                : "This RYDR can be requested by anyone."
                            : (currentValue == null || currentValue == 0) &&
                                    followerCount >= 0
                                ? dealType == DealType.Event
                                    ? "Creators with more than ${f.format(followerCount)} followers can RSVP to this event."
                                    : 'Creators with more than ${f.format(followerCount)} followers can request this RYDR.'
                                : currentValue > 0 && followerCount == 0
                                    ? dealType == DealType.Event
                                        ? "Creators with $engRatingFormatted engagement rating can RSVP to this event."
                                        : 'Creators with $engRatingFormatted engagement rating can request this RYDR.'
                                    : currentValue > 0 && followerCount > 0
                                        ? dealType == DealType.Event
                                            ? "Creators with more than ${f.format(followerCount)} followers and $engRatingFormatted engagement rating ($currentValue% +) can RSVP to this event."
                                            : 'Creators with more than ${f.format(followerCount)} followers and $engRatingFormatted engagement rating ($currentValue% +) can request this RYDR.'
                                        : dealType == DealType.Event
                                            ? "Anyone can RSVP to this event."
                                            : 'This RYDR can be requested by anyone.',
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(color: Theme.of(context).primaryColor),
                    ),
              ),
            );
          },
        );
      });
}
