import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';

class DealRequestStatusChange {
  int dealId;
  int publisherAccountId;
  DealRequestStatus fromStatus;
  DealRequestStatus toStatus;
  int modifiedByPublisherAccountId;
  int occurredOn;
  String reason;
  double latitude;
  double longitude;

  DealRequestStatusChange.fromJson(Map<String, dynamic> json) {
    this.dealId = json['dealId'];
    this.publisherAccountId = json['publisherAccountId'];
    this.fromStatus = dealRequestStatusFromJson(json['fromStatus']);
    this.toStatus = dealRequestStatusFromJson(json['toStatus']);
    this.modifiedByPublisherAccountId = json['modifiedByPublisherAccountId'];
    this.occurredOn = json['occurredOn'];
    this.reason = json['reason'];
    this.latitude =
        json['latitude'] != null ? json['latitude'].toDouble() : null;
    this.longitude =
        json['longitude'] != null ? json['longitude'].toDouble() : null;
  }

  String get occurredOnDisplayAgo => Utils.formatAgo(occurredOnDateTime);
  String get occurredOnDisplay =>
      Utils.formatDateShortWithTime(occurredOnDateTime);

  DateTime get occurredOnDateTime =>
      DateTime.fromMillisecondsSinceEpoch(this.occurredOn * 1000);
}
