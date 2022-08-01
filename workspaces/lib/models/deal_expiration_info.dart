import 'package:intl/intl.dart';

/// represents more granular information about when a deal expires
/// to give us access to various breakdowns given an expiration date
class DealExpirationInfo {
  bool neverExpires;
  bool isExpired;
  bool isExpiringSoon;
  int daysLeft;
  int hoursLeft;
  int minsLeft;
  String display;
  String simpleDisplay;
  String displayTimeLeft;

  DealExpirationInfo(DateTime expirationDateUtc) {
    neverExpires = expirationDateUtc == null;
    isExpiringSoon = false;
    isExpired = false;

    if (!neverExpires) {
      final DateTime today = DateTime.now();
      final DateTime expirationDateLocal = expirationDateUtc.toLocal();
      final DateFormat f = DateFormat('EEE, MMM d, yyyy h:mm a');
      final DateFormat simple = DateFormat('EEE, MMM d @ ha');
      final Duration diff = expirationDateLocal.difference(today);

      isExpired = today.isAfter(expirationDateLocal);
      isExpiringSoon = diff.inDays < 7;
      daysLeft = diff.inDays;
      hoursLeft = diff.inHours;
      minsLeft = diff.inMinutes;

      if (!isExpired) {
        if (daysLeft > 1) {
          displayTimeLeft = '$daysLeft days left';
        } else if (daysLeft == 1) {
          displayTimeLeft = '1 day left';
        } else if (hoursLeft > 1) {
          displayTimeLeft = '$hoursLeft hours left';
        } else if (hoursLeft == 1) {
          displayTimeLeft = '1 hour left';
        } else if (minsLeft > 0) {
          displayTimeLeft =
              '$minsLeft ${minsLeft > 1 ? " miutes left" : " minute left"}';
        } else {
          displayTimeLeft = 'expires soon';
        }
      }

      display = f.format(expirationDateLocal) +
          ' ' +
          expirationDateLocal.timeZoneName;
      simpleDisplay = simple.format(expirationDateLocal);
    } else {
      display = "Never";
      simpleDisplay = "Never";
    }
  }

  @override
  String toString() =>
      'DealExpirationInfo(neverExpires: $neverExpires, isExpired: $isExpired, isExpiringSoon: $isExpiringSoon, daysLeft: $daysLeft, hoursLeft: $hoursLeft, minsLeft: $minsLeft, display: $display, displayTimeLeft: $displayTimeLeft)';
}
