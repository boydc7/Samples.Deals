import 'package:intl/intl.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/record_type.dart';

class MessageDialog {
  int id;
  String name;
  List<PublisherAccount> members;
  List<RecordType> forRecords;
  String lastMessage;
  DateTime lastMessageSentOn;
  int lastMessageSentByPublisherAccountId;
  int unreadMessages;

  MessageDialog({
    this.id,
    this.members,
    this.lastMessage,
    this.lastMessageSentOn,
    this.lastMessageSentByPublisherAccountId,
    this.unreadMessages,
  });

  MessageDialog.fromJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.name = json['name'];
    this.lastMessage = json['lastMessage'];
    this.lastMessageSentOn = DateTime.parse(json['lastMessageSentOn']);
    this.lastMessageSentByPublisherAccountId =
        json['lastMessageSentByPublisherAccountId'];
    this.unreadMessages = json['unreadMessages'];
    this.forRecords = jsonToRecordTypes(json['forRecords']);
    this.members = jsonToMembers(json['members']);
  }

  List<RecordType> jsonToRecordTypes(List<dynamic> json) {
    List<RecordType> recordTypes = [];
    json.forEach((recordType) {
      recordTypes.add(RecordType.fromJson(recordType));
    });

    return recordTypes;
  }

  List<PublisherAccount> jsonToMembers(List<dynamic> json) {
    List<PublisherAccount> users = [];
    json.forEach((user) {
      users.add(PublisherAccount.fromJson(user['publisherAccount']));
    });

    return users;
  }

  /// should always have members, if for some reason we don't then return
  /// the user "me" themselves;
  getFrom(PublisherAccount me) =>
      this.members.firstWhere((m) => m.id != me.id, orElse: () {
        return me;
      });

  getDealId() {
    RecordType rec = this
        .forRecords
        .firstWhere((r) => r.type == RecordOfType.Deal, orElse: () {
      return null;
    });

    if (rec != null) {
      return rec.id;
    }

    return null;
  }

  /// human-readable time-only component in users local time
  /// formatted to show time only for today, day of week if within a week
  /// and otherwise have shortened full-date for last message sent on
  String get lastMessageSentOnDisplay {
    final DateTime now = DateTime.now();
    final DateTime lastMessage = this.lastMessageSentOn.toLocal();

    /// remove time component from now and last message
    /// to get an accurate difference in days between now and last message
    final DateTime today = DateTime(now.year, now.month, now.day);
    final DateTime last =
        DateTime(lastMessage.year, lastMessage.month, lastMessage.day);

    /// difference between today (minus time) and last message (minus time)
    final int differenceInDays = today.difference(last).inDays;

    if (differenceInDays == 0) {
      return DateFormat.jm().format(lastMessage);
    } else if (differenceInDays == 1) {
      return "Yesterday";
    } else if (differenceInDays < 8) {
      return DateFormat('EEEE').format(lastMessage);
    } else {
      return DateFormat('M/d/yy').format(lastMessage);
    }
  }
}
