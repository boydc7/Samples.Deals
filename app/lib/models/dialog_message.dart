import 'package:intl/intl.dart';
import 'package:rydr_app/models/publisher_account.dart';

class DialogMessage {
  int id;
  int dialogId;
  DateTime sentOn;
  String message;
  bool isDelivered = true;
  bool isDelivering = false;
  bool isRead;
  PublisherAccount from;

  DialogMessage({
    this.id,
    this.dialogId,
    this.sentOn,
    this.message,
    this.isDelivering,
    this.isDelivered,
    this.from,
  });

  DialogMessage.fromJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.dialogId = json['dialogId'];
    this.message = json['message'];
    this.isRead = json['isRead'];
    this.isDelivered = true;
    this.isDelivering = false;

    /// we don't get these back after posting a new message
    this.sentOn =
        json['sentOn'] != null ? DateTime.parse(json['sentOn']) : null;
    this.from = json['publisherAccount'] != null
        ? PublisherAccount.fromJson(json['publisherAccount'])
        : null;
  }

  /// human-readable time-only component in users local time
  String get time => DateFormat.jm().format(this.sentOn.toLocal());

  /// human-readable time-only component in users local time
  /// formatted to show time only for today, day of week if within a week
  /// and otherwise have shortened full-date for last message sent on
  String get sentOnDateDisplay {
    final DateTime now = DateTime.now();
    final DateTime sent = this.sentOn.toLocal();

    /// remove time component from now and last message
    /// to get an accurate difference in days between now and last message
    final DateTime today = DateTime(now.year, now.month, now.day);
    final DateTime last = DateTime(sent.year, sent.month, sent.day);

    /// difference between today (minus time) and last message (minus time)
    final int differenceInDays = today.difference(last).inDays;

    if (differenceInDays == 0) {
      return "Today";
    } else if (differenceInDays == 1) {
      return "Yesterday";
    } else if (differenceInDays < 8) {
      return DateFormat('EEEE').format(sent);
    } else {
      return DateFormat('M/d/yy').format(sent);
    }
  }
}
