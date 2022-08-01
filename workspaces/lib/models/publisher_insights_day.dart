import 'package:rydrworkspaces/app/utils.dart';

class PublisherInsightsDay {
  final int total;
  final DateTime day;
  final String url;

  PublisherInsightsDay(
    this.total,
    this.day,
    this.url,
  );

  String get dayTimeDisplay => Utils.formatDateShortWithTime(this.day);
}
