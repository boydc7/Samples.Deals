import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/models/publisher_insights_growth.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';

enum chartDataColor {
  blue,
  teal,
  green,
  red,
}

class ChartData {
  final chartDataColor dataColor;
  final List<FlSpot> data;
  final int maxY;
  final int minY;
  final bool isPercent;

  Color dotColor;
  List<Color> lineColor;
  List<Color> underColor;

  ChartData({
    this.isPercent = false,
    @required this.dataColor,
    @required this.data,
    @required this.maxY,
    @required this.minY,
  }) {
    if (this.dataColor == chartDataColor.blue) {
      dotColor = AppColors.blue;

      lineColor = [
        AppColors.blue700.withOpacity(0.75),
        AppColors.blue700,
      ];

      underColor = [
        AppColors.blue700.withOpacity(0.055),
        AppColors.blue700.withOpacity(0.0)
      ];
    } else if (this.dataColor == chartDataColor.green) {
      dotColor = AppColors.successGreen;

      lineColor = [
        AppColors.successGreen.withOpacity(0.75),
        AppColors.successGreen,
      ];

      underColor = [
        AppColors.successGreen.withOpacity(0.055),
        AppColors.successGreen.withOpacity(0.0)
      ];
    } else if (this.dataColor == chartDataColor.red) {
      dotColor = AppColors.errorRed;

      lineColor = [
        AppColors.errorRed.withOpacity(0.75),
        AppColors.errorRed,
      ];

      underColor = [
        AppColors.errorRed.withOpacity(0.055),
        AppColors.errorRed.withOpacity(0.0)
      ];
    } else {
      dotColor = AppColors.teal;

      lineColor = [
        AppColors.teal.withOpacity(0.75),
        AppColors.teal,
      ];

      underColor = [
        AppColors.teal.withOpacity(0.055),
        AppColors.teal.withOpacity(0.0)
      ];
    }
  }
}

class ProfileInsightsChart extends StatelessWidget {
  final List<DateTime> dates;
  final List<ChartData> data;

  ProfileInsightsChart({
    @required this.dates,
    @required this.data,
  });

  String chartTitlesFromDates(double value) {
    /// It seems the fl_chart package re-renders the graph spots incrementally
    /// which leads to previous data sets re-buildling which does not match the
    /// count of dates we have passed to this widget
    ///
    /// in other words, the last set may have had 30 dates, but now we have 7
    /// and on a rebuild we'd get values > 7 for dates that are only 7
    ///
    /// so we have to guard against this here...
    if (value.toInt() >= dates.length) {
      return '';
    }

    bool lastFive = dates.length == 6;
    bool lastSeven = dates.length == 7;
    bool lastTen = dates.length == 11;
    bool lastTwentyFive = dates.length == 25;
    bool lastThirty = dates.length == 30;
    bool lastFifty = dates.length == 51;
    bool lastHundred = dates.length >= 100;

    /// showing/hiding dates below chart
    if (value == 0.0 && (lastTen || lastFive)) {
      return '';
    } else if (lastTen || lastFive || lastSeven) {
      return Utils.formatDateShortNumbers(dates[value.toInt()]);
    } else if (dates.length < 25 && value.toInt().isEven) {
      return Utils.formatDateShortNumbers(dates[value.toInt()]);
    } else if (lastTwentyFive && value < 2) {
      return '';
    } else if ((lastTwentyFive || lastThirty) && value.toInt().isEven) {
      return Utils.formatDateShortNumbers(dates[value.toInt()]);
    } else if (lastFifty && (value == 50 || value == 3)) {
      return Utils.formatDateShortNumbers(dates[value.toInt()]);
    } else if (lastHundred && (value == 99 || value == 5)) {
      return Utils.formatDateShortNumbers(dates[value.toInt()]);
    } else {
      return '';
    }
  }

  bool showLastDotFromDates(FlSpot spot) {
    if (dates.length >= 5) {
      return spot.x == dates.length - 1;
    } else {
      if (spot.x == 0.0 || spot.x == dates.length - 1) {
        return true;
      } else {
        return false;
      }
    }
  }

  bool showVertGridFromDates(FlSpot spot) {
    if (spot.x == 0 && dates.length >= 5) {
      return false;
    }
    return true;
  }

  @override
  Widget build(BuildContext context) {
    if (data == null || data.isEmpty) {
      return Container();
    }

    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final NumberFormat f = NumberFormat.decimalPattern();
    final double maxY = data
        .reduce((curr, next) => curr.maxY > next.maxY ? curr : next)
        .maxY
        .toDouble();
    final double minY = data
        .reduce((curr, next) => curr.minY > next.minY ? next : curr)
        .minY
        .toDouble();

    return maxY < 1
        ? Container()
        : Container(
            alignment: Alignment.bottomCenter,
            color: Theme.of(context).scaffoldBackgroundColor,
            height: 300.0,
            width: MediaQuery.of(context).size.width,
            child: AspectRatio(
              aspectRatio: 1.5,
              child: Padding(
                padding: EdgeInsets.only(
                  top: 16.0,
                  bottom: 16.0,
                  right: 24.0,
                  left: dates.length < 5 ? 24.0 : 0.0,
                ),
                child: LineChart(
                  LineChartData(
                    lineTouchData: LineTouchData(
                      getTouchedSpotIndicator:
                          (LineChartBarData barData, List<int> spotIndexes) {
                        return spotIndexes.map((spotIndex) {
                          return TouchedSpotIndicatorData(
                            FlLine(
                              color:
                                  Theme.of(context).textTheme.bodyText1.color,
                              strokeWidth: 2.0,
                            ),
                            FlDotData(
                              dotSize: 4.0,
                              dotColor:
                                  Theme.of(context).textTheme.bodyText1.color,
                            ),
                          );
                        }).toList();
                      },
                      touchTooltipData: LineTouchTooltipData(
                        getTooltipItems: (List<LineBarSpot> lineBarsSpot) {
                          return lineBarsSpot.map((lineBarSpot) {
                            return LineTooltipItem(
                              data[0].isPercent
                                  ? "${lineBarSpot.y.toStringAsFixed(1)}%"
                                  : f.format(lineBarSpot.y),
                              TextStyle(
                                color: lineBarSpot.bar.dotData.dotColor,
                                fontWeight: FontWeight.w500,
                              ),
                            );
                          }).toList();
                        },
                        tooltipBottomMargin: 12.0,
                        tooltipRoundedRadius: 4.0,
                        tooltipPadding: EdgeInsets.only(
                            top: 8.0, left: 8.0, right: 8.0, bottom: 7.0),
                        tooltipBgColor: dark
                            ? AppColors.grey800.withOpacity(0.8)
                            : Colors.white.withOpacity(0.85),
                      ),
                    ),
                    gridData: FlGridData(show: false),
                    titlesData: FlTitlesData(
                      bottomTitles: SideTitles(
                        showTitles: true,
                        reservedSize: 16,
                        margin: 16,
                        textStyle: Theme.of(context).textTheme.caption.merge(
                            TextStyle(
                                color: Theme.of(context).hintColor,
                                fontSize: 11.0)),
                        getTitles: chartTitlesFromDates,
                      ),
                      leftTitles: SideTitles(showTitles: false),
                    ),
                    borderData: FlBorderData(show: false),
                    minX: 0,
                    maxX: dates.length.toDouble() - 1,
                    maxY: maxY + (maxY * 0.002),
                    minY: minY - (minY * 0.002),
                    lineBarsData: data
                        .map(
                          (d) => LineChartBarData(
                            curveSmoothness: 0.3,
                            spots: d.data,
                            isCurved: true,
                            colorStops: [0.0, 1.0],
                            colors: d.lineColor,
                            /*
                    colors: growthRateWeek == 0
                        ? [AppColors.grey400.withOpacity(0.55), AppColors.grey400]
                        : growthRateWeek.isNegative
                            ? [
                                Colors.deepOrange.shade600.withOpacity(0.55),
                                Colors.deepOrange.shade600
                              ]
                            : [
                                AppColors.successGreen.withOpacity(0.55),
                                AppColors.successGreen
                              ],                      
                      */

                            barWidth: 2,
                            isStrokeCapRound: false,
                            dotData: FlDotData(
                              show: true,
                              dotSize: 4.0,
                              /*
                      dotColor: growthRateWeek == 0
                          ? AppColors.grey400
                          : growthRateWeek.isNegative
                              ? Colors.deepOrange
                              : AppColors.successGreen,                        
                        */
                              dotColor: d.dotColor,
                              checkToShowDot: showLastDotFromDates,
                            ),
                            belowBarData: BarAreaData(
                              show: true,
                              colors: d.underColor,

                              /*
                      colors: growthRateMonth == 0
                          ? [
                              AppColors.white.withOpacity(0.055),
                              AppColors.white.withOpacity(0.0)
                            ]
                          : growthRateMonth.isNegative
                              ? [
                                  Colors.deepOrange.shade600.withOpacity(0.055),
                                  Colors.deepOrange.shade600.withOpacity(0.0)
                                ]
                              : [
                                  AppColors.successGreen.withOpacity(0.055),
                                  AppColors.successGreen.withOpacity(0.0)
                                ],                        
                        */

                              gradientColorStops: [0.5, 1.0],
                              gradientFrom: Offset(0, 0),
                              gradientTo: Offset(0, 1),
                              spotsLine: BarAreaSpotsLine(
                                  show: true,
                                  flLineStyle: FlLine(
                                    color: AppColors.grey300.withOpacity(0.2),
                                    strokeWidth: 1,
                                  ),
                                  checkToShowSpotLine: showVertGridFromDates),
                            ),
                          ),
                        )
                        .toList(),
                  ),

                  /// remove animation to avoid jitter when switching between intervals
                  swapAnimationDuration: Duration(microseconds: 1),
                ),
              ),
            ),
          );
  }
}

class ProfileInsightsBarChart extends StatelessWidget {
  final PublisherInsightsGrowthSummary summary;

  ProfileInsightsBarChart(this.summary);

  String chartTitlesFromDates(double value, List<DateTime> dates) {
    final datesLength = dates.length;
    final chartVal = value.toInt();

    if (datesLength == 7 ||
        datesLength - 1 == chartVal ||
        chartVal == 0 ||
        chartVal == (datesLength - 1) ~/ 2) {
      /// NOTE: seems there's a bug in charting lib whereby previous dates array is passed here
      /// when toggling between data sets, so we guard against invalid index error
      return chartVal < datesLength
          ? Utils.formatDateShortNumbers(dates[chartVal])
          : "";
    } else {
      return '';
    }
  }

  @override
  Widget build(BuildContext context) => Container(
        margin: EdgeInsets.only(top: 24.0),
        child: AspectRatio(
          aspectRatio: 1.5,
          child: Padding(
            padding: EdgeInsets.only(
                left: 16.0, right: 16.0, bottom: 16.0, top: 16.0),
            child: BarChart(
              mainBarData(
                  context, summary.dates, summary.avg, summary.max.total),
            ),
          ),
        ),
      );

  BarChartGroupData makeGroupData(
    BuildContext context,
    int x,
    double y, {
    Color barColor,
    List<int> showTooltips = const [],
  }) =>
      BarChartGroupData(
        x: x,
        barRods: [
          BarChartRodData(
            y: y,
            color: Theme.of(context).primaryColor,
            width: summary.dates.length <= 7 ? 8.0 : 4.0,
          ),
        ],
      );

  List<BarChartGroupData> showingGroups(BuildContext context) {
    List<BarChartGroupData> data = [];

    for (int x = 0; x < summary.flSpots.length; x++) {
      data.add(makeGroupData(context, x, summary.flSpots[x].y));
    }

    return data;
  }

  BarChartData mainBarData(
    BuildContext context,
    List<DateTime> dates,
    double avg,
    int max,
  ) {
    NumberFormat f = NumberFormat.decimalPattern();
    bool dark = Theme.of(context).brightness == Brightness.dark;

    /// take the max number and make it easier to work with
    double maxBy100 = max / 100;

    /// round it, then add 1 so we can use it as a multiplier
    double maxMultiplier = (maxBy100.floor() + 1).toDouble();

    /// now use that multiplier to set the horizontal line intervals
    double maxFactor = max <= 15
        ? maxMultiplier
        : max < (maxMultiplier * 100) ? (maxMultiplier * 10) : maxMultiplier;

    return BarChartData(
      gridData: FlGridData(
        getDrawingHorizontalLine: (double value) {
          if (value == avg.toInt()) {
            return FlLine(color: AppColors.teal);
          } else {
            return FlLine(
                color: Theme.of(context).canvasColor, strokeWidth: 1.0);
          }
        },
        checkToShowHorizontalLine: (double value) {
          if (value == avg.toInt()) {
            return true;
          } else if (value != 0 && (value / maxFactor) % 1 == 0) {
            return true;
          } else {
            return false;
          }
        },
      ),
      barTouchData: BarTouchData(
        touchTooltipData: BarTouchTooltipData(
          getTooltipItem: (barGroupData, x1, barRodData, x2) {
            return BarTooltipItem(
              f.format(barRodData.y),
              TextStyle(
                color: AppColors.blue,
                fontWeight: FontWeight.w500,
              ),
            );
          },
          tooltipRoundedRadius: 4.0,
          tooltipBottomMargin: 8.0,
          tooltipPadding:
              EdgeInsets.only(top: 8.0, bottom: 4.0, left: 8.0, right: 8.0),
          tooltipBgColor: dark
              ? AppColors.grey800.withOpacity(0.8)
              : Colors.white.withOpacity(0.85),
        ),
      ),
      alignment: BarChartAlignment.spaceEvenly,
      titlesData: FlTitlesData(
        show: true,
        bottomTitles: SideTitles(
          showTitles: true,
          interval: 32,
          reservedSize: 16,
          margin: 16,
          textStyle: Theme.of(context)
              .textTheme
              .caption
              .merge(TextStyle(color: AppColors.grey300, fontSize: 11.0)),
          getTitles: (double value) {
            return chartTitlesFromDates(value, dates);
          },
        ),
        leftTitles: SideTitles(
          showTitles: true,
          reservedSize: 24,
          margin: 8,
          textStyle: Theme.of(context).textTheme.caption.merge(
                TextStyle(
                    color: AppColors.teal,
                    fontSize: 11.0,
                    fontWeight: FontWeight.bold),
              ),
          getTitles: (double value) {
            NumberFormat f = NumberFormat.compact();
            if (value == avg.toInt()) {
              return f.format(value);
            } else {
              return "";
            }
          },
        ),
        rightTitles: SideTitles(
          showTitles: true,
          reservedSize: 16,
          margin: 16,
          textStyle: Theme.of(context)
              .textTheme
              .caption
              .merge(TextStyle(color: AppColors.grey300, fontSize: 11.0)),
          getTitles: (double value) {
            if (value != 0 && (value / maxFactor) % 1 == 0) {
              return "${value.toInt()}";
            } else {
              return "";
            }
          },
        ),
      ),
      borderData: FlBorderData(
        show: false,
      ),
      barGroups: showingGroups(context),
    );
  }
}
