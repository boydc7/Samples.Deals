import 'dart:math';
import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:rydrworkspaces/app/theme.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:timeago/timeago.dart' as timeago;

final Map<String, Color> _requestStatusColors = {
  "requested": Color(0xFFFFCB2F),
  "requested_dark": Colors.yellow.shade300,
  "inProgress": Colors.orange,
  "inProgress_dark": Colors.orange.shade400,
  "redeemed": AppColors.blue,
  "redeemed_dark": Colors.blue.shade500,
  "completed": AppColors.successGreen,
  "completed_dark": AppColors.successGreen,
  "invited": Colors.purple.shade400,
  "invited_dark": Colors.purple.shade300,
  "cancelled": AppColors.grey400,
  "cancelled_dark": AppColors.grey400,
  "denied": AppColors.grey400,
  "denied_dark": AppColors.grey400,
  "delinquent": AppColors.errorRed,
  "delinquent_dark": AppColors.errorRed,
};

class Utils {
  static String formatAgo(DateTime dt) => timeago
      .format(dt, locale: 'en_short')
      .replaceAll(' ', '')
      .replaceAll('min', 'm');

  static const _daysInMonth = const [
    0,
    31,
    28,
    31,
    30,
    31,
    30,
    31,
    31,
    30,
    31,
    30,
    31
  ];

  static bool isLeapYear(int value) =>
      value % 400 == 0 || (value % 4 == 0 && value % 100 != 0);

  static int daysInMonth(int year, int month) {
    var result = _daysInMonth[month];
    if (month == 2 && isLeapYear(year)) result++;
    return result;
  }

  static DateTime addMonths(DateTime dt, int value) {
    var r = value % 12;
    var q = (value - r) ~/ 12;
    var newYear = dt.year + q;
    var newMonth = dt.month + r;
    if (newMonth > 12) {
      newYear++;
      newMonth -= 12;
    }
    var newDay = min(dt.day, daysInMonth(newYear, newMonth));
    if (dt.isUtc) {
      return new DateTime.utc(newYear, newMonth, newDay, dt.hour, dt.minute,
          dt.second, dt.millisecond, dt.microsecond);
    } else {
      return new DateTime(newYear, newMonth, newDay, dt.hour, dt.minute,
          dt.second, dt.millisecond, dt.microsecond);
    }
  }

  static String formatDoubleForDisplay(double val) {
    final NumberFormat n = val != null && val > 9999
        ? NumberFormat.compact()
        : NumberFormat.decimalPattern();
    return val != null ? n.format(val) : "";
  }

  static String formatDoubleForDisplayAsInt(double val) {
    final NumberFormat n = val != null && val > 9999
        ? NumberFormat.compact()
        : NumberFormat.decimalPattern();
    return val != null ? n.format(val.toInt()) : "";
  }

  static String formatDateLong(DateTime dt) {
    final DateFormat f = DateFormat.yMMMd().add_jm();
    return dt != null ? f.format(dt.toLocal()) : "";
  }

  static String formatDateFull(DateTime dt) {
    final DateFormat f = DateFormat.yMMMMd();
    return dt != null ? f.format(dt.toLocal()) : "";
  }

  static String formatDateShort(DateTime dt) {
    final DateFormat f = DateFormat.MMMd();
    return dt != null ? f.format(dt.toLocal()) : "";
  }

  static String formatDateShortWithTime(DateTime dt) {
    final DateFormat f = DateFormat("MMM d, y 'at' h:mm a");

    return dt != null ? "${f.format(dt.toLocal())}" : "";
  }

  static String formatDateShortNumbers(DateTime dt) {
    final DateFormat f = DateFormat.Md();
    return dt != null ? f.format(dt.toLocal()) : "";
  }

  static void launchUrl(
    context,
    String url, {
    String unableMessage,
    String trackingName,
  }) async {
    /// TODO: not yet implemented
  }

  static NetworkImage googleMapImage(
    BuildContext context, {
    double latitude,
    double longitude,
    int zoom,
    int width,
    int height,
  }) {
    bool dark = Theme.of(context).brightness == Brightness.dark;
    //if (dark) {
    //return NetworkImage(
    //    'https://maps.googleapis.com/maps/api/staticmap?key=${AppConfig.instance.googleMapsApiKey}&center=$latitude,$longitude&zoom=$zoom&format=png&maptype=roadmap&style=element:labels.icon%7Cvisibility:off&style=element:labels.text.fill%7Ccolor:0x000000%7Csaturation:36%7Clightness:40&style=element:labels.text.stroke%7Ccolor:0x000000%7Clightness:16%7Cvisibility:on&style=feature:administrative%7Chue:0x00c3ff&style=feature:administrative%7Celement:geometry.fill%7Ccolor:0x000000%7Clightness:20&style=feature:administrative%7Celement:geometry.stroke%7Ccolor:0x000000%7Clightness:17%7Cweight:1.2&style=feature:administrative%7Celement:labels.text.fill%7Ccolor:0xefefef&style=feature:administrative.land_parcel%7Celement:labels.text.fill%7Ccolor:0xfc1d1d%7Cvisibility:on&style=feature:landscape%7Celement:geometry%7Ccolor:0x0a0c0e%7Clightness:10&style=feature:landscape%7Celement:labels.text.fill%7Clightness:100&style=feature:landscape%7Celement:labels.text.stroke%7Clightness:-51%7Cvisibility:on&style=feature:poi%7Celement:geometry%7Chue:0x0080ff%7Csaturation:-34%7Cinvert_lightness:true%7Clightness:-4%7Cgamma:0.95&style=feature:poi%7Celement:labels%7Cvisibility:off&style=feature:poi%7Celement:labels.icon%7Cvisibility:off&style=feature:road.arterial%7Celement:geometry%7Ccolor:0x000000%7Clightness:20&style=feature:road.arterial%7Celement:geometry.fill%7Ccolor:0x020304%7Clightness:5&style=feature:road.arterial%7Celement:geometry.stroke%7Ccolor:0x020304%7Clightness:5&style=feature:road.arterial%7Celement:labels.text%7Ccolor:0xffffff%7Cvisibility:on&style=feature:road.arterial%7Celement:labels.text.stroke%7Cvisibility:off&style=feature:road.highway%7Celement:geometry.fill%7Ccolor:0x020304%7Clightness:5&style=feature:road.highway%7Celement:geometry.stroke%7Ccolor:0x020304%7Clightness:5&style=feature:road.highway%7Celement:labels.text.fill%7Ccolor:0xe6e6e6&style=feature:road.highway%7Celement:labels.text.stroke%7Cvisibility:off&style=feature:road.local%7Celement:geometry%7Ccolor:0x000000%7Clightness:16&style=feature:road.local%7Celement:geometry.fill%7Ccolor:0x020304%7Csaturation:0%7Clightness:5&style=feature:road.local%7Celement:geometry.stroke%7Ccolor:0x020304%7Clightness:5&style=feature:road.local%7Celement:labels%7Ccolor:0xe6e6e6%7Clightness:10&style=feature:road.local%7Celement:labels.text.stroke%7Cvisibility:off&style=feature:transit%7Celement:geometry%7Ccolor:0x000000%7Clightness:19&style=feature:water%7Celement:geometry%7Ccolor:0x020304%7Clightness:7&style=feature:water%7Celement:labels.text.fill%7Ccolor:0x464646&size=${width}x$height');
    //} else {
    //return NetworkImage(
    //    'https://maps.googleapis.com/maps/api/staticmap?key=${AppConfig.instance.googleMapsApiKey}&center=$latitude,$longitude&zoom=$zoom&format=png&maptype=roadmap&style=hue:0xe7ecf0&style=feature:administrative%7Celement:labels.text.fill%7Ccolor:0x262626&style=feature:administrative.land_parcel%7Celement:labels.text.fill%7Ccolor:0xff0000&style=feature:administrative.neighborhood%7Celement:labels.text.fill%7Ccolor:0x262626&style=feature:landscape%7Celement:geometry.fill%7Ccolor:0xf1f4f6&style=feature:landscape%7Celement:labels.text.fill%7Ccolor:0x496271&style=feature:poi%7Celement:geometry.fill%7Chue:0x00ff3f%7Csaturation:-20%7Clightness:10%7Cgamma:1%7Cvisibility:on&style=feature:poi%7Celement:geometry.stroke%7Cweight:1.59&style=feature:poi%7Celement:labels%7Cvisibility:off&style=feature:poi%7Celement:labels.icon%7Chue:0x00ff3a%7Csaturation:-100%7Clightness:-19%7Cvisibility:off%7Cweight:0.01&style=feature:road%7Csaturation:-70&style=feature:road%7Celement:geometry.fill%7Ccolor:0xffffff&style=feature:road%7Celement:geometry.stroke%7Ccolor:0xc6d3dc&style=feature:road%7Celement:labels.text.fill%7Ccolor:0x262626&style=feature:road.arterial%7Celement:labels%7Cvisibility:off&style=feature:road.highway%7Celement:labels.icon%7Cvisibility:off&style=feature:transit%7Cvisibility:off&style=feature:water%7Csaturation:-60%7Cvisibility:simplified&style=feature:water%7Celement:geometry.fill%7Ccolor:0xacddfa&size=${width}x$height');
    //}
  }

  static Color getRequestStatusColor(DealRequestStatus status, bool darkMode) =>
      _requestStatusColors[
          dealRequestStatusToString(status) + (darkMode ? "_dark" : "")];
}
