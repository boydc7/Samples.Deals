import 'package:flutter/material.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class DealInputEngagementRating extends StatelessWidget {
  final Stream<double> valueStream;
  final Function handleUpdate;
  final bool isExpired;

  DealInputEngagementRating({
    @required this.valueStream,
    @required this.handleUpdate,
    this.isExpired = false,
  });

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return StreamBuilder<double>(
      stream: valueStream,
      builder: (context, snapshot) {
        final double currentValue = snapshot.data ?? 2;

        final String engRating = currentValue == 2
            ? "Low"
            : currentValue == 4.5
                ? "Medium"
                : currentValue == 7.0 ? "High" : "Amazing";

        return isExpired
            ? ListTile(
                title: Text(
                  "Minimum Engagement Level",
                  style: Theme.of(context).textTheme.bodyText2,
                ),
                trailing: Text("$engRating"),
              )
            : Padding(
                padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
                child: Column(
                  children: <Widget>[
                    Container(
                      margin: EdgeInsets.only(top: 8),
                      padding: EdgeInsets.symmetric(vertical: 4),
                      decoration: BoxDecoration(
                          border:
                              Border.all(color: Theme.of(context).hintColor),
                          borderRadius: BorderRadius.circular(4)),
                      child: Stack(
                        overflow: Overflow.visible,
                        children: <Widget>[
                          Positioned(
                            top: -11.0,
                            left: 8.0,
                            child: FadeInBottomTop(
                                0,
                                Container(
                                  padding:
                                      EdgeInsets.symmetric(horizontal: 4.0),
                                  color:
                                      Theme.of(context).scaffoldBackgroundColor,
                                  child: Text(
                                    "Minimum Engagement Rating",
                                    style: Theme.of(context)
                                        .textTheme
                                        .caption
                                        .merge(
                                          TextStyle(
                                              color:
                                                  Theme.of(context).hintColor),
                                        ),
                                  ),
                                ),
                                250),
                          ),
                          SliderTheme(
                            data: SliderThemeData(
                              minThumbSeparation: 8,
                              trackHeight: 4,
                              trackShape: RoundedRectSliderTrackShape(),
                              valueIndicatorColor:
                                  Theme.of(context).textTheme.bodyText2.color,
                              valueIndicatorTextStyle: TextStyle(
                                color: Theme.of(context).appBarTheme.color,
                                fontWeight: FontWeight.w600,
                              ),
                              overlayColor: Theme.of(context)
                                  .textTheme
                                  .bodyText2
                                  .color
                                  .withOpacity(0.07),
                              thumbColor:
                                  Theme.of(context).textTheme.bodyText2.color,
                              activeTrackColor:
                                  Theme.of(context).textTheme.bodyText2.color,
                              activeTickMarkColor:
                                  Theme.of(context).textTheme.bodyText2.color,
                              inactiveTrackColor: Theme.of(context).canvasColor,
                              inactiveTickMarkColor: Theme.of(context)
                                  .hintColor
                                  .withOpacity(dark ? 0.1 : 0.3),
                            ),
                            child: Slider(
                              label: "$engRating Engagement",
                              onChanged: handleUpdate,
                              value: currentValue,
                              min: 2,
                              max: 9.5,
                              divisions: 3,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              );
      },
    );
  }
}
