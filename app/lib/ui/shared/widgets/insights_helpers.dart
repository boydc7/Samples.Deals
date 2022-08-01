import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/models/publisher_insights_growth.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';
import 'package:shimmer/shimmer.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

insightsShowExtraInfo({
  @required BuildContext context,
  @required String title,
  @required String subtitle,
  @required Widget child,
  double initialRatio,
}) {
  showSharedModalBottomInfo(context,
      hideTitleOnAndroid: false,
      title: title,
      subtitle: subtitle,
      child: child,
      initialRatio: initialRatio);
}

Widget insightsNoGraph(BuildContext context) {
  return Container(
    height: 200,
    child: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Icon(
          AppIcons.analytics,
          color: AppColors.grey300,
        ),
        SizedBox(
          height: 16,
        ),
        Text("There's not enough data available...",
            style: Theme.of(context).textTheme.caption),
      ],
    ),
  );
}

Widget insightsGrowthMinMax(
  BuildContext context, {
  @required PublisherInsightsGrowthSummary summary,
  @required String title,
  @required Color color,
}) {
  final NumberFormat f = NumberFormat.decimalPattern();
  final DateFormat dtMonth = DateFormat('MMMMEEEEd');

  return summary.max.total > 0
      ? ListTileTheme(
          textColor: Theme.of(context).textTheme.bodyText2.color,
          child: Container(
            padding: EdgeInsets.symmetric(horizontal: 16.0),
            child: Column(
              children: <Widget>[
                ListTile(
                  contentPadding: EdgeInsets.symmetric(horizontal: 0),
                  dense: true,
                  title: Row(
                    children: <Widget>[
                      Container(
                        height: 8.0,
                        width: 8.0,
                        margin: EdgeInsets.only(right: 8.0),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(16.0),
                          color: color,
                        ),
                      ),
                      Expanded(
                        child: Text(
                          title,
                          style: TextStyle(fontSize: 14.0),
                        ),
                      )
                    ],
                  ),
                  trailing: Text(f.format(summary.total)),
                ),
                insightsStatTileDense(
                  context: context,
                  title: "High · ${dtMonth.format(summary.max.day)}",
                  valueAsString: f.format(summary.max.total),
                  formatAsInt: true,
                ),
                summary.min.total > 0
                    ? insightsStatTileDense(
                        context: context,
                        title: "Low · ${dtMonth.format(summary.min.day)}",
                        valueAsString: f.format(summary.min.total),
                        formatAsInt: true,
                      )
                    : Container(),
                SizedBox(height: 8.0),
              ],
            ),
          ),
        )
      : Container();
}

Widget insightsSectionHeader(
    {@required BuildContext context,
    @required String title,
    IconData icon,
    Function onTap,
    String subtitle,
    bool showingChart = false,
    String bottomSheetTitle,
    String bottomSheetSubtitle,
    Widget bottomSheetWidget,
    Widget subtitleWidget,
    double initialRatio}) {
  return GestureDetector(
    onTap: onTap != null
        ? onTap
        : bottomSheetTitle != null
            ? () {
                insightsShowExtraInfo(
                    context: context,
                    title: bottomSheetTitle,
                    subtitle: bottomSheetSubtitle,
                    child: bottomSheetWidget,
                    initialRatio: MediaQuery.of(context).textScaleFactor > 1
                        ? 0.75
                        : initialRatio);
              }
            : null,
    child: Container(
      color: Colors.transparent,
      padding: EdgeInsets.only(left: 16.0, top: 16.0, right: 16.0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.start,
                  children: <Widget>[
                    SizedBox(height: 4.0),
                    Text(
                      title,
                      textAlign: TextAlign.left,
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(context).textTheme.subtitle1.merge(
                            TextStyle(
                                fontWeight: FontWeight.w600,
                                color: Theme.of(context).primaryColor),
                          ),
                    ),
                    subtitle != null
                        ? Column(
                            children: <Widget>[
                              SizedBox(height: 4.0),
                              Text(
                                subtitle,
                                textAlign: TextAlign.left,
                                style:
                                    Theme.of(context).textTheme.caption.merge(
                                          TextStyle(color: AppColors.grey300),
                                        ),
                              ),
                            ],
                          )
                        : Container(
                            height: 0,
                            width: 0,
                          ),
                  ],
                ),
              ),
              bottomSheetTitle != null
                  ? Container(
                      alignment: Alignment.centerLeft,
                      height: 32.0,
                      width: 40.0,
                      padding: EdgeInsets.only(left: 16.0),
                      child: Icon(
                        AppIcons.infoCircle,
                        size: 24.0,
                        color: AppColors.grey300.withOpacity(0.5),
                      ),
                    )
                  : Container(
                      width: 40,
                    ),
            ],
          ),
          subtitleWidget != null
              ? subtitleWidget
              : Container(
                  height: 0,
                  width: 0,
                ),
        ],
      ),
    ),
  );
}

Widget insightsStatTile({
  @required BuildContext context,
  @required String title,
  double value,
  double toggledValue,
  String valueAsString,
  String toggledValueAsString,
  bool formatAsInt = false,
  bool formatAsCurrency = false,
  bool formatAsPercentage = false,
  bool toggledFormatAsInt = false,
  bool toggledFormatAsCurrency = false,
  bool toggledFormatAsPercentage = false,
  bool showingChart = false,
  Function onTap,
  bool toggleLegend = true,
  bool noLegend = false,
}) {
  final NumberFormat currencyFormat = NumberFormat.compactSimpleCurrency();
  final NumberFormat currencyFormatLong =
      NumberFormat.compactSimpleCurrency(decimalDigits: 4);
  final NumberFormat numberFormat = NumberFormat.decimalPattern();

  String valueForDisplay = valueAsString ?? '';
  String toggledValueForDisplay = toggledValueAsString ?? valueForDisplay;

  if (toggledValue == null) {
    toggledValue = value;
    toggledFormatAsInt = formatAsInt;
    toggledFormatAsCurrency = formatAsCurrency;
    toggledFormatAsPercentage = formatAsPercentage;
  }

  if (value != null) {
    valueForDisplay = formatAsInt
        ? numberFormat.format(value.toInt())
        : formatAsCurrency
            ? currencyFormat.format(value) == "\$0.00" ||
                    currencyFormat.format(value) == "\$0.01"
                ? currencyFormatLong.format(value)
                : currencyFormat.format(value)
            : formatAsPercentage
                ? value.toStringAsFixed(1) + '%'
                : value.toString();
  }

  if (toggledValue != null) {
    toggledValueForDisplay = toggledFormatAsInt
        ? numberFormat.format(toggledValue)
        : toggledFormatAsCurrency
            ? currencyFormat.format(toggledValue)
            : toggledFormatAsPercentage
                ? toggledValue.toStringAsFixed(1) + '%'
                : toggledValue.toString();
  }

  Color legendColor = title == 'From Likes' ||
          title == 'Stories' ||
          title == 'Followers' ||
          title == 'Total Impressions' ||
          title == 'Impressions' ||
          title == 'Male' ||
          title == 'Engagement Rate' ||
          title == 'Story Engagement Rate' ||
          title == 'Stories: Average CPM' ||
          title == 'Neutral'
      ? Theme.of(context).primaryColor
      : title == 'From Comments' ||
              title == 'Images' ||
              title == 'Following' ||
              title == 'Unique People' ||
              title == 'Female'
          ? Colors.deepOrange
          : title == 'From Video Views' ||
                  title == 'Carousels' ||
                  title == 'People Reached' ||
                  title == 'Total Reach' ||
                  title == 'Posts: Average CPM' ||
                  title == 'Posts'
              ? AppColors.teal
              : title == 'Videos'
                  ? AppColors.blue100
                  : title == "Positive"
                      ? AppColors.successGreen
                      : title == "Negative"
                          ? AppColors.errorRed
                          : AppColors.grey800;

  return GestureDetector(
    onTap: onTap != null ? onTap : null,
    child: Container(
      color: Colors.transparent,
      padding: EdgeInsets.symmetric(vertical: 16.0),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: <Widget>[
          Expanded(
            child: Row(
              children: <Widget>[
                Visibility(
                  visible: !noLegend,
                  child: AnimatedContainer(
                    duration: Duration(milliseconds: showingChart ? 250 : 200),
                    curve:
                        showingChart ? Curves.decelerate : Curves.fastOutSlowIn,
                    height: !toggleLegend ? 8.0 : showingChart ? 8.0 : 0.0,
                    width: !toggleLegend ? 8.0 : showingChart ? 8.0 : 0.0,
                    margin: EdgeInsets.only(
                        right: !toggleLegend ? 8.0 : showingChart ? 8.0 : 0.0),
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(16.0),
                      color: legendColor,
                    ),
                  ),
                ),
                Expanded(
                  child: Text(
                    title,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
          ),
          AnimatedCrossFade(
            duration: Duration(milliseconds: showingChart ? 250 : 200),
            firstCurve: showingChart ? Curves.decelerate : Curves.fastOutSlowIn,
            secondCurve:
                showingChart ? Curves.decelerate : Curves.fastOutSlowIn,
            crossFadeState: !showingChart
                ? CrossFadeState.showFirst
                : CrossFadeState.showSecond,
            firstChild: Container(
              width: 120,
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: Text(valueForDisplay, textAlign: TextAlign.right),
                  ),
                  /*
                          doubleValue != ''
                              ? Expanded(
                                  child: Text(doubleValue,
                                      style: style, textAlign: TextAlign.right),
                                )
                              : Container(
                                  width: 0,
                                  height: 0,
                                )
                                */
                ],
              ),
            ),
            secondChild: Text(toggledValueForDisplay),
          ),
        ],
      ),
    ),
  );
}

Widget insightsStatTileDense({
  @required BuildContext context,
  @required String title,
  double value,
  double toggledValue,
  String valueAsString,
  String toggledValueAsString,
  bool formatAsInt = false,
  bool formatAsCurrency = false,
  bool formatAsPercentage = false,
  bool toggledFormatAsInt = false,
  bool toggledFormatAsCurrency = false,
  bool toggledFormatAsPercentage = false,
  Function onTap,
  bool showingChart = false,
  bool indent = true,
  bool showArrow = false,
}) {
  final NumberFormat currencyFormat = NumberFormat.compactSimpleCurrency();
  final NumberFormat numberFormat = NumberFormat.decimalPattern();

  String valueForDisplay = valueAsString ?? '';
  String toggledValueForDisplay = toggledValueAsString ?? valueForDisplay;

  if (toggledValue == null) {
    toggledValue = value;
    toggledFormatAsInt = formatAsInt;
    toggledFormatAsCurrency = formatAsCurrency;
    toggledFormatAsPercentage = formatAsPercentage;
  }

  if (value != null) {
    valueForDisplay = formatAsInt
        ? numberFormat.format(value)
        : formatAsCurrency
            ? currencyFormat.format(value)
            : formatAsPercentage
                ? value.toStringAsFixed(1) + '%'
                : value.toString();
  }

  if (toggledValue != null) {
    toggledValueForDisplay = toggledFormatAsInt
        ? numberFormat.format(value)
        : toggledFormatAsCurrency
            ? currencyFormat.format(value)
            : toggledFormatAsPercentage
                ? value.toStringAsFixed(1) + '%'
                : value.toString();
  }

  TextStyle styleDense = Theme.of(context)
      .textTheme
      .bodyText2
      .merge(TextStyle(fontSize: 13.0, color: AppColors.grey300));

  return GestureDetector(
    onTap: onTap != null ? onTap : null,
    child: Container(
      color: Colors.transparent,
      padding: EdgeInsets.only(bottom: 16.0, top: 0),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          Visibility(
            visible: indent,
            child: SizedBox(
              width: 16.0,
            ),
          ),
          Expanded(
            child: Text(title, style: styleDense),
          ),
          showingChart == false
              ? showArrow
                  ? Container(
                      width: 80.0,
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.end,
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: <Widget>[
                          Text(valueForDisplay, style: styleDense),
                          Container(
                            width: 10.0,
                            margin: EdgeInsets.only(left: 8.0),
                            child: Icon(
                              AppIcons.angleRight,
                              color: AppColors.grey300,
                              size: 18.0,
                            ),
                          )
                        ],
                      ),
                    )
                  : Text(valueForDisplay, style: styleDense)
              : AnimatedCrossFade(
                  duration: Duration(milliseconds: showingChart ? 250 : 200),
                  firstCurve:
                      showingChart ? Curves.decelerate : Curves.fastOutSlowIn,
                  secondCurve:
                      showingChart ? Curves.decelerate : Curves.fastOutSlowIn,
                  crossFadeState: !showingChart
                      ? CrossFadeState.showFirst
                      : CrossFadeState.showSecond,
                  firstChild: Text(toggledValueForDisplay, style: styleDense),
                  secondChild: Text(valueForDisplay, style: styleDense),
                ),
        ],
      ),
    ),
  );
}

Widget insightsBigStat({
  @required BuildContext context,
  @required String label,
  String subtitle = '',
  Widget subtitleWidget,
  double value,
  String valueAsString = '',
  bool formatAsInt = false,
  bool formatAsCurrency = false,
  bool formatAsPercentage = false,
  bool addShimmerEffect = false,
  bool labelNormal = false,
  bool centered = false,
  Function onTap,
  Color countColor = AppColors.grey800,
  Color labelColor = AppColors.grey300,
  Color subtitleColor = AppColors.grey800,
}) {
  final NumberFormat currencyFormat = NumberFormat.compactSimpleCurrency();
  final NumberFormat numberFormat = NumberFormat.compact();
  final bool valueWhole =
      value != null ? value.toStringAsFixed(1).endsWith("0") : false;

  String displayValue = valueAsString;

  if (value != null) {
    displayValue = formatAsInt
        ? numberFormat.format(value)
        : formatAsCurrency
            ? currencyFormat.format(value)
            : formatAsPercentage
                ? value < 0.1 && value != 0.0
                    ? value.toStringAsFixed(2) + "%"
                    : valueWhole
                        ? value.toStringAsFixed(0) + '%'
                        : value.toStringAsFixed(1) + '%'
                : value.toString();
  }

  return GestureDetector(
    onTap: onTap == null ? null : onTap,
    child: Column(
      mainAxisAlignment: MainAxisAlignment.start,
      crossAxisAlignment:
          centered ? CrossAxisAlignment.center : CrossAxisAlignment.start,
      children: <Widget>[
        Padding(
          padding: EdgeInsets.only(right: centered ? 0 : 16.0),
          child: Text(
            label,
            textAlign: centered ? TextAlign.center : TextAlign.left,
            style: Theme.of(context).textTheme.bodyText2.merge(
                  TextStyle(
                    color: labelColor == AppColors.grey300
                        ? Theme.of(context).textTheme.bodyText2.color
                        : labelColor,
                  ),
                ),
          ),
        ),
        SizedBox(height: 4.0),
        Text(
          displayValue,
          style: Theme.of(context).textTheme.headline4.merge(
                TextStyle(
                    fontWeight: FontWeight.w600,
                    color: countColor == AppColors.grey800
                        ? Theme.of(context).textTheme.bodyText2.color
                        : countColor),
              ),
        ),
        Visibility(
          visible: subtitle != '',
          child: Padding(
            padding: EdgeInsets.only(top: 4.0),
            child: addShimmerEffect
                ? Shimmer.fromColors(
                    baseColor: Colors.red,
                    highlightColor: Colors.yellow,
                    child: Text(
                      subtitle,
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).primaryColor),
                          ),
                    ),
                  )
                : Text(
                    subtitle,
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(
                            color: subtitleColor == AppColors.grey800
                                ? Theme.of(context).hintColor
                                : subtitleColor,
                          ),
                        ),
                  ),
          ),
        ),
        Visibility(
          visible: subtitleWidget != null,
          child: Padding(
            padding: EdgeInsets.only(top: 8),
            child: addShimmerEffect
                ? Shimmer.fromColors(
                    baseColor: Colors.red,
                    highlightColor: Colors.yellow,
                    child: subtitleWidget,
                  )
                : subtitleWidget,
          ),
        ),
      ],
    ),
  );
}

Widget insightsToggleButtons(
  BuildContext context,
  List<String> labels,
  int currentIndex,
  Function onTap,
) {
  List<bool> selected = [currentIndex == 0];

  if (labels.length > 1) {
    selected.add(currentIndex == 1);
  }

  if (labels.length > 2) {
    selected.add(currentIndex == 2);
  }

  if (labels.length > 1) {
    return Container(
      alignment: Alignment.center,
      padding: EdgeInsets.only(bottom: 8.0, top: 4.0),
      child: ToggleButtons(
        isSelected: selected,
        renderBorder: false,
        onPressed: (int index) => onTap(index),
        children: labels.map((l) => Text(l)).toList(),
        constraints: BoxConstraints(
            minWidth: MediaQuery.of(context).size.width / 4, minHeight: 34.0),
        fillColor: Colors.transparent,
        borderWidth: 3.0,
        selectedColor: Theme.of(context).primaryColor,
        textStyle: Theme.of(context).textTheme.bodyText1,
        color: Theme.of(context).hintColor,
        splashColor: AppColors.grey300.withOpacity(0.25),
        borderRadius: BorderRadius.circular(40.0),
      ),
    );
  } else {
    return Container(height: 0, width: 0);
  }
}

Widget insightsLoadingBody() => LoadingStatsShimmer();

Widget insightsNoResults(BuildContext context, String message, IconData icon) =>
    Container(
      width: double.infinity,
      padding: EdgeInsets.all(32),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          icon == null
              ? Container()
              : Icon(
                  icon,
                  size: 28,
                ),
          SizedBox(height: 16),
          Text(message, textAlign: TextAlign.center),
        ],
      ),
    );

Widget insightsBottomSheet(
  BuildContext context,
  List<InsightsBottomSheetTile> tiles,
) =>
    Column(
      mainAxisSize: MainAxisSize.max,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: tiles
          .map(
            (tile) => Container(
              padding: EdgeInsets.symmetric(horizontal: 16.0, vertical: 16.0),
              width: double.infinity,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    tile.title,
                    textAlign: TextAlign.left,
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                  SizedBox(height: 4.0),
                  Text(
                    tile.subTitle,
                    style: Theme.of(context).textTheme.bodyText2.merge(
                          TextStyle(color: Theme.of(context).hintColor),
                        ),
                  ),
                ],
              ),
            ),
          )
          .toList(),
    );

Widget insightsErrorBody(DioError error, Function onRetry) => RetryError(
      error: error,
      fullSize: false,
      onRetry: onRetry,
    );

class InsightsBottomSheetTile {
  final String title;
  final String subTitle;

  InsightsBottomSheetTile(
    this.title,
    this.subTitle,
  );
}
