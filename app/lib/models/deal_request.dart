import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';

import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/publisher_media_stat.dart';
import 'package:rydr_app/models/dialog_message.dart';
import 'package:rydr_app/models/deal_request_status_change.dart';

class DealRequest {
  int dealId;
  DealRequestStatus status;
  PublisherAccount publisherAccount;
  DialogMessage lastMessage;
  DateTime requestedOn;

  /// use latest status change as timestamp on 'now' request
  List<DealRequestStatusChange> statusChanges;
  List<PublisherMedia> completionMedia;

  DateTime completionMediaStatsLastSyncedOn;
  PublisherMediaStatValues completionMediaStatValues;
  PublisherMediaStatValues completionStoryMediaStatValues;
  PublisherMediaStatValues completionPostMediaStatValues;

  int daysUntilDelinquent;
  bool isDelinquent;

  /// hard-coded default value for how many days we'll add
  /// to the request completion deadline when the business chooses to extend
  final int defaultDaysToExtendCompletionDeadline = 7;

  DealRequest();

  DealRequest.fromJson(Map<String, dynamic> json) {
    this.requestedOn = DateTime.parse(json['requestedOn'].toString());

    this.status = dealRequestStatusFromJson(json['status']);
    this.publisherAccount = PublisherAccount.fromJson(json['publisherAccount']);
    this.daysUntilDelinquent = json['daysUntilDelinquent'] ?? 7;
    this.isDelinquent = json['isDelinquent'] ?? false;
    this.completionMedia = json['completionMedia'] != null
        ? jsonToCompletionMedia(json['completionMedia'])
        : [];
    this.statusChanges = json['statusChanges'] != null
        ? jsonToStatusChanges(json['statusChanges'])
        : [];
    this.lastMessage = json['lastMessage'] != null
        ? DialogMessage.fromJson(json['lastMessage'])
        : null;

    /// sum up all stats for each completion media
    if (this.completionMedia.length > 0) {
      List<PublisherStatValue> completionMediaStats = [];

      this.completionMedia.forEach((PublisherMedia media) {
        if (media.lifetimeStats != null) {
          media.lifetimeStats.stats.forEach((PublisherStatValue stat) {
            int indexOfStat = completionMediaStats
                .indexWhere((PublisherStatValue val) => val.name == stat.name);

            if (indexOfStat == -1) {
              completionMediaStats.add(stat);
            } else {
              PublisherStatValue existingStat =
                  completionMediaStats[indexOfStat];
              PublisherStatValue newStat = PublisherStatValue(
                  stat.name, stat.value + existingStat.value);

              completionMediaStats.removeAt(indexOfStat);
              completionMediaStats.add(newStat);
            }
          });

          /// take the oldest 'sync' date of the media as the one we'd consider
          /// being the most up to date sync date for representing stats as a whole
          if (this.completionMediaStatsLastSyncedOn == null ||
              this
                  .completionMediaStatsLastSyncedOn
                  .isAfter(media.lifetimeStats.lastSyncedOn)) {
            this.completionMediaStatsLastSyncedOn =
                media.lifetimeStats.lastSyncedOn;
          }
        }
      });

      completionMediaStatValues =
          PublisherMediaStatValues(completionMediaStats);
    }

    if (this.completionMedia.length > 0 && this.completedStories > 0) {
      List<PublisherStatValue> completionStoryMediaStats = [];

      this
          .completionMedia
          .where(
              (PublisherMedia m) => m.contentType == PublisherContentType.story)
          .forEach((PublisherMedia media) {
        if (media.lifetimeStats != null) {
          media.lifetimeStats.stats.forEach((PublisherStatValue stat) {
            int indexOfStat = completionStoryMediaStats
                .indexWhere((PublisherStatValue val) => val.name == stat.name);

            if (indexOfStat == -1) {
              completionStoryMediaStats.add(stat);
            } else {
              PublisherStatValue existingStat =
                  completionStoryMediaStats[indexOfStat];
              PublisherStatValue newStat = PublisherStatValue(
                  stat.name, stat.value + existingStat.value);

              completionStoryMediaStats.removeAt(indexOfStat);
              completionStoryMediaStats.add(newStat);
            }
          });

          /// take the oldest 'sync' date of the media as the one we'd consider
          /// being the most up to date sync date for representing stats as a whole
          if (this.completionMediaStatsLastSyncedOn == null ||
              this
                  .completionMediaStatsLastSyncedOn
                  .isAfter(media.lifetimeStats.lastSyncedOn)) {
            this.completionMediaStatsLastSyncedOn =
                media.lifetimeStats.lastSyncedOn;
          }
        }
      });

      completionStoryMediaStatValues =
          PublisherMediaStatValues(completionStoryMediaStats);
    }

    if (this.completionMedia.length > 0 && this.completedPosts > 0) {
      List<PublisherStatValue> completionPostMediaStats = [];

      this
          .completionMedia
          .where(
              (PublisherMedia m) => m.contentType == PublisherContentType.post)
          .forEach((PublisherMedia media) {
        if (media.lifetimeStats != null) {
          media.lifetimeStats.stats.forEach((PublisherStatValue stat) {
            int indexOfStat = completionPostMediaStats
                .indexWhere((PublisherStatValue val) => val.name == stat.name);

            if (indexOfStat == -1) {
              completionPostMediaStats.add(stat);
            } else {
              PublisherStatValue existingStat =
                  completionPostMediaStats[indexOfStat];
              PublisherStatValue newStat = PublisherStatValue(
                  stat.name, stat.value + existingStat.value);

              completionPostMediaStats.removeAt(indexOfStat);
              completionPostMediaStats.add(newStat);
            }
          });

          /// take the oldest 'sync' date of the media as the one we'd consider
          /// being the most up to date sync date for representing stats as a whole
          if (this.completionMediaStatsLastSyncedOn == null ||
              this
                  .completionMediaStatsLastSyncedOn
                  .isAfter(media.lifetimeStats.lastSyncedOn)) {
            this.completionMediaStatsLastSyncedOn =
                media.lifetimeStats.lastSyncedOn;
          }
        }
      });

      completionPostMediaStatValues =
          PublisherMediaStatValues(completionPostMediaStats);
    }
  }

  /// list of status changes sorted by date
  List<DealRequestStatusChange> jsonToStatusChanges(List<dynamic> json) =>
      List<DealRequestStatusChange>.from(json
          .map((change) => DealRequestStatusChange.fromJson(change))
          .toList())
        ..sort((a, b) => a.occurredOn.compareTo(b.occurredOn));

  /// list of completed media sorted by date
  List<PublisherMedia> jsonToCompletionMedia(List<dynamic> json) =>
      List<PublisherMedia>.from(
          json.map((media) => PublisherMedia.fromJson(media)).toList())
        ..sort((a, b) => b.createdAt.compareTo(a.createdAt));

  Map<String, Map<String, dynamic>> toPayload(int dealId) => {
        "model": {
          "dealId": dealId,
          "publisherAccountId": publisherAccount.id,
          "status": dealRequestStatusToString(status),
        }
      };

  DealRequestStatusChange get lastStatusChange =>
      statusChanges.isNotEmpty ? statusChanges.last : null;

  String get completionMediaStatsLastSyncedOnDisplayAgo =>
      this.completionMediaStatsLastSyncedOn == null
          ? 'recently'
          : Utils.formatAgo(completionMediaStatsLastSyncedOn);

  int get completedPosts => completionMedia
      .where((PublisherMedia m) => m.contentType == PublisherContentType.post)
      .length;

  int get completedStories => completionMedia
      .where((PublisherMedia m) => m.contentType == PublisherContentType.story)
      .length;

  int get daysRemainingToComplete => lastStatusChange != null &&
          lastStatusChange.toStatus == DealRequestStatus.redeemed
      ? daysUntilDelinquent -
          lastStatusChange.occurredOnDateTime
              .difference(DateTime.now())
              .inDays
              .abs()
      : daysUntilDelinquent;

  /// are we allowed to show the request PUBLISHER's profile?
  /// this depends on their soft-link flag and the status of this request
  bool get canViewRequestPublishersProfile =>
      status != DealRequestStatus.cancelled &&
      status != DealRequestStatus.completed &&
      status != DealRequestStatus.delinquent &&
      status != DealRequestStatus.denied &&
      status != DealRequestStatus.invited &&
      publisherAccount.isAccountSoft;

  // are we allowed to show the REQUESTER's profile?
  bool get canViewRequestersProfile =>
      status != DealRequestStatus.cancelled &&
      status != DealRequestStatus.completed &&
      status != DealRequestStatus.delinquent &&
      status != DealRequestStatus.denied &&
      status != DealRequestStatus.invited;

  /// this will tell us if this request was originally an invite for the creator
  bool get wasInvited =>
      statusChanges
          .where((DealRequestStatusChange change) =>
              change.fromStatus == DealRequestStatus.invited)
          .length >
      0;

  DateTime get newCompletionDeadline => DateTime.now()
      .add(Duration(
          days: daysRemainingToComplete > 0 ? daysRemainingToComplete : 0))
      .add(Duration(days: defaultDaysToExtendCompletionDeadline));

  String get timeRemainingToCompleteDisplay => daysRemainingToComplete > 1
      ? "$daysRemainingToComplete" + "d"
      : daysRemainingToComplete == 1
          ? "~24hrs"
          : daysRemainingToComplete == 0
              ? "${DateTime.now().difference(lastStatusChange.occurredOnDateTime).inHours}h"
              : "No time";

  /// for completed requests we allow messaging each other for another day
  /// after the completion before we no longer enable sending messages through this request
  bool get canSendMessages =>
      lastStatusChange != null && status == DealRequestStatus.inProgress ||
      status == DealRequestStatus.redeemed ||
      (status == DealRequestStatus.completed &&
          DateTime.now()
                  .difference(lastStatusChange.occurredOnDateTime)
                  .inHours <
              24);
}

class DealRequestChange {
  final DealRequestStatus fromStatus;
  final DealRequestStatus toStatus;
  final Deal deal;

  DealRequestChange(
    this.fromStatus,
    this.toStatus,
    this.deal,
  );
}
