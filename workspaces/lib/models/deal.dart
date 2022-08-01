import 'package:intl/intl.dart';
import 'package:rydrworkspaces/models/deal_expiration_info.dart';
import 'package:rydrworkspaces/models/deal_request.dart';
import 'package:rydrworkspaces/models/deal_request_completion_stats.dart';
import 'package:rydrworkspaces/models/deal_restriction.dart';
import 'package:rydrworkspaces/models/deal_stat.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/enums/publisher_media.dart';
import 'package:rydrworkspaces/models/place.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';
import 'package:rydrworkspaces/models/publisher_media.dart';
import 'package:rydrworkspaces/models/publisher_media_line_item.dart';

class Deal {
  int id;
  DateTime publishedOn;
  PublisherAccount publisherAccount;
  DealRequest request;
  DealRequestCompletionStats requestCompletionStats;
  DealStatus status;
  String title;
  String description;
  double value;
  double distanceInMiles;

  Place place;
  DateTime expirationDate;
  int maxApprovals;
  List<DealRestriction> restrictions;
  List<DealStat> stats;
  List<PublisherMediaLineItem> receiveType;
  List<PublisherMedia> publisherMedias;
  String receiveNotes;
  String approvalNotes;
  List<PublisherAccount> invitedPublisherAccounts;
  List<PublisherAccount> pendingRecentRequesters;
  bool autoApproveRequests;
  int unreadMessages;
  int approvalsRemaining;
  bool isPrivateDeal;
  Map<int, DealRequestStatus> invitedPublisherAccountStatuses;

  DealExpirationInfo expirationInfo;

  List<String> unset;

  Deal();

  String get valueFormatted => value == null
      ? "0"
      : NumberFormat.compactCurrency(decimalDigits: 2, symbol: "\$")
          .format(this.value);

  String get distanceInMilesDisplay => distanceInMiles == null
      ? ""
      : distanceInMiles.toStringAsFixed(
              distanceInMiles.truncateToDouble() == distanceInMiles ? 0 : 1) +
          ' mi';

  int get minAge {
    final String value = getRestriction(DealRestrictionType.minAge);

    return value == null ? null : int.parse(value);
  }

  double get minEngagementRating {
    final String value =
        getRestriction(DealRestrictionType.minEngagementRating);

    return value != null && value != "null" ? double.parse(value) : null;
  }

  String get minEngagementRatingDisplay =>
      minEngagementRating == null ? "0%" : '$minEngagementRating+ %';

  int get minFollowerCount {
    final String value = getRestriction(DealRestrictionType.minFollowerCount);

    return value != null && value != "null" ? int.parse(value) : null;
  }

  String get minFollowerCountDisplay {
    final NumberFormat minFollowers = NumberFormat.decimalPattern();
    final String value = getRestriction(DealRestrictionType.minFollowerCount);

    if (value != null) {
      int count = int.tryParse(value);
      return count != null ? minFollowers.format(count) : "";
    }

    return "";
  }

  String get minFollowerCountCompactDisplay {
    final NumberFormat minFollowers = NumberFormat.compact();
    final String value = getRestriction(DealRestrictionType.minFollowerCount);

    if (value == '0' || value == '-1') {
      return '0';
    }

    if (value != null) {
      int count = int.tryParse(value);
      return count != null ? minFollowers.format(count) : "";
    }

    return "";
  }

  String get minFollowerCountCompactDisplayMin {
    final NumberFormat minFollowers = NumberFormat.compact();
    final String value = getRestriction(DealRestrictionType.minFollowerCount);

    if (value == '0' || value == '-1') {
      return 'No min';
    }

    if (value != null) {
      int count = int.tryParse(value);
      return count != null ? '${minFollowers.format(count)} min' : "";
    }

    return "";
  }

  int get maxApprovalsRemaining =>
      approvalsRemaining != null && approvalsRemaining != 0
          ? approvalsRemaining
          : maxApprovals != null && maxApprovals != 0
              ? this.maxApprovals - getStatAsInt(DealStatType.totalApproved)
              : 0;

  String get maxApprovalsDisplay => maxApprovals == null || maxApprovals == 0
      ? "Unlimited"
      : maxApprovals.toString();

  String get titleClean => title.lastIndexOf(' ') > -1
      ? title.replaceFirst(RegExp(r' '), '\u00A0', title.lastIndexOf(' '))
      : title;

  int get requestedPosts {
    if (receiveType == null) {
      return 0;
    }

    return receiveType.firstWhere(
        (PublisherMediaLineItem item) => item.type == PublisherContentType.post,
        orElse: () {
      return PublisherMediaLineItem(quantity: 0);
    }).quantity;
  }

  int get requestedStories {
    if (receiveType == null) {
      return 0;
    }

    return receiveType.firstWhere(
        (PublisherMediaLineItem item) =>
            item.type == PublisherContentType.story, orElse: () {
      return PublisherMediaLineItem(quantity: 0);
    }).quantity;
  }

  /// get a stats' value based on the type, returns zero int string instead of null
  int getStat(DealStatType type) {
    if (this.stats == null) {
      return 0;
    }

    DealStat stat = this.stats.firstWhere(
          (stat) => stat.type == type,
          orElse: () => null,
        );

    if (stat != null) {
      return int.parse(stat.value.toString());
    }

    return 0;
  }

  int getStatAsInt(DealStatType type) {
    if (this.stats == null) {
      return 0;
    }

    DealStat stat = this.stats.firstWhere(
          (stat) => stat.type == type,
          orElse: () => null,
        );

    if (stat != null) {
      return int.tryParse(stat.value) == null ? 0 : int.tryParse(stat.value);
    }

    return 0;
  }

  /// returns restriction value based on a given type, returns null
  /// if not found so we can easily check/compare on null vs. emptry strings
  String getRestriction(DealRestrictionType type) {
    if (this.restrictions == null) {
      return null;
    }

    DealRestriction restriction = this.restrictions.firstWhere(
          (restriction) => restriction.type == type,
          orElse: () => null,
        );

    return restriction != null ? restriction.value : null;
  }

  _processDealJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.status = dealStatusFromJson(json['status']);
    this.title = json['title'] != null ? json['title'] : "";
    this.description = json['description'];
    this.value = json['value'] != null ? json['value'].toDouble() : 0;
    this.place = json['place'] != null ? Place.fromJson(json['place']) : null;
    this.publishedOn = json['publishedOn'] != null
        ? DateTime.parse(json['publishedOn'].toString())
        : null;
    this.autoApproveRequests = json['autoApproveRequests'];
    this.approvalNotes = json['approvalNotes'];
    this.maxApprovals = json['maxApprovals'];
    this.expirationDate = json['expirationDate'] != null
        ? DateTime.parse(json['expirationDate'])
        : null;
    this.receiveType = json['receiveType'] != null
        ? jsonToReceiveType(json['receiveType'])
        : null;
    this.receiveNotes = json['receiveNotes'];
    this.publisherMedias = json['publisherMedias'] != null
        ? jsonToMedia(json['publisherMedias'])
        : null;
    this.invitedPublisherAccounts = json['invitedPublisherAccounts'] != null
        ? jsonToUsers(json['invitedPublisherAccounts'])
        : null;
    this.restrictions = json['restrictions'] != null
        ? jsonToRestrictions(json['restrictions'])
        : null;
    this.isPrivateDeal = json['isPrivateDeal'];
    this.expirationInfo = DealExpirationInfo(this.expirationDate);
  }

  Deal.fromResponseJson(Map<String, dynamic> json) {
    _processDealJson(json['deal']);

    this.publisherAccount = json['publisherAccount'] != null
        ? PublisherAccount.fromJson(json['publisherAccount'])
        : null;

    this.distanceInMiles = json['distanceInMiles'] != null
        ? json['distanceInMiles'].toDouble()
        : null;

    this.pendingRecentRequesters = json['pendingRecentRequesters'] != null
        ? jsonToUsers(json['pendingRecentRequesters'])
        : null;

    this.stats = json['stats'] != null ? jsonToStats(json['stats']) : null;

    this.unreadMessages = json['unreadMessages'];
    this.approvalsRemaining = json['approvalsRemaining'];

    this.request = json['dealRequest'] != null
        ? DealRequest.fromJson(json['dealRequest'])
        : null;

    this.requestCompletionStats = this.request != null &&
            this.request.status == DealRequestStatus.completed &&
            this.request.completionMediaStatValues != null
        ? DealRequestCompletionStats(this.value, this.request)
        : null;

    this.invitedPublisherAccountStatuses =
        json['invitedPublisherAccountStatuses']
            ?.map<int, DealRequestStatus>((k, v) {
      return new MapEntry<int, DealRequestStatus>(
          int.parse(k), dealRequestStatusFromJson(v));
    });
  }

  List<PublisherAccount> jsonToUsers(List<dynamic> json) =>
      List<PublisherAccount>.from(
          json.map((stat) => PublisherAccount.fromJson(stat)).toList());

  static List<DealStat> jsonToStats(List<dynamic> json) {
    List<DealStat> stats = [];
    json.forEach((stat) {
      stats.add(DealStat.fromJson(stat));
    });

    return stats;
  }

  List<PublisherMedia> jsonToMedia(List<dynamic> json) {
    if (json == null) {
      return [];
    }

    List<PublisherMedia> medias = [];
    json.forEach((media) {
      medias.add(PublisherMedia.fromJson(media));
    });

    return medias;
  }

  List<DealRestriction> jsonToRestrictions(List<dynamic> json) {
    List<DealRestriction> restrictions = [];
    json.forEach((restriction) {
      restrictions.add(DealRestriction.fromJson(restriction));
    });

    return restrictions;
  }

  List<PublisherMediaLineItem> jsonToReceiveType(List<dynamic> json) {
    List<PublisherMediaLineItem> receiveType = [];
    json.forEach((mediaLineItem) {
      receiveType.add(PublisherMediaLineItem.fromJson(mediaLineItem));
    });

    return receiveType;
  }

  Map<String, dynamic> toPayloadUnset() => {
        "model": {"id": id},
        "unset": unset
      };

  Map<String, Map<String, dynamic>> toPayload() {
    Map<String, Map<String, dynamic>> payload = {
      "model": {
        "id": id == null ? 0 : id,
      }
    };

    if (id == null) {
      /// TODO: need app state
      ///payload['model']['publisherAccountId'] = appState.currentProfile.id;
    }

    if (status != null) {
      payload['model']['status'] = dealStatusToString(status);
    }

    if (title != null && title.isNotEmpty) {
      payload['model']['title'] = title;
    }

    if (description != null && description.isNotEmpty) {
      payload['model']['description'] = description;
    }

    if (value != null) {
      payload['model']['value'] = value;
    }

    if (place != null) {
      /// if this is an existing place then only send the id
      /// otherwise send the model  but remove the isPrimary flag
      Map<String, dynamic> jsonPlace = place.toJson();
      jsonPlace.remove('isPrimary');

      if (place.id != null && place.id != 0) {
        jsonPlace = {
          "id": place.id.toString(),
        };
      }

      payload['model']['place'] = jsonPlace;
    }

    if (receiveType != null && receiveType.isNotEmpty) {
      payload['model']['receiveType'] = receiveType
          .map((PublisherMediaLineItem mediaLineItem) => mediaLineItem.toJson())
          .toList();
    }

    if (receiveNotes != null && receiveNotes.isNotEmpty) {
      payload['model']['receiveNotes'] = receiveNotes;
    }

    if (invitedPublisherAccounts != null &&
        invitedPublisherAccounts.isNotEmpty) {
      payload['model']['invitedPublisherAccounts'] = invitedPublisherAccounts
          .map((PublisherAccount user) => user.toJson())
          .toList();
    }

    if (publisherMedias != null && publisherMedias.isNotEmpty) {
      payload['model']['publisherMedias'] = publisherMedias
          .map((PublisherMedia media) => {"id": media.id.toString()})
          .toList();
    }

    if (restrictions != null && restrictions.isNotEmpty) {
      payload['model']['restrictions'] = restrictions
          .map((DealRestriction restriction) => restriction.toJson())
          .toList();
    }

    if (autoApproveRequests != null) {
      payload['model']['autoApproveRequests'] = autoApproveRequests;
    }

    /// NOTE: leaving expirationDate as null will be considered "never" expires
    if (expirationDate != null) {
      payload['model']['expirationDate'] = expirationDate.toIso8601String();
    }

    /// NOTE: leaving maxApprovals as null will be considered "unlimited" approvals
    /// we'll also take anything like 0 or -1 as being "unlimited"
    if (maxApprovals != null && maxApprovals > 0) {
      payload['model']['maxApprovals'] = maxApprovals;
    }

    if (approvalNotes != null) {
      payload['model']['approvalNotes'] = approvalNotes;
    }

    if (isPrivateDeal != null) {
      payload['model']['isPrivateDeal'] = isPrivateDeal.toString();
    }

    return payload;
  }

  upsertRestriction(DealRestriction restriction) {
    /// if we have no restrictions at all then add this one
    if (this.restrictions == null) {
      this.restrictions = [restriction];
    } else {
      /// find this restriction and then either add if if not exists
      /// or replace it with new restriction value
      int index =
          this.restrictions.indexWhere((r) => r.type == restriction.type);

      if (index == -1) {
        this.restrictions.add(restriction);
      } else {
        this.restrictions[index] = restriction;
      }
    }
  }

  removeRestriction(DealRestrictionType type) {
    this
        .restrictions
        .removeWhere((DealRestriction restriction) => restriction.type == type);
  }

  upsertReceiveType(PublisherMediaLineItem mediaLineItem) {
    /// if we have no receiveTypes at all then add this one
    if (this.receiveType == null) {
      this.receiveType = [mediaLineItem];
    } else {
      /// find this mediaLineItem and then either add if if not exists
      /// or replace it with new mediaLineItem value
      int index =
          this.receiveType.indexWhere((r) => r.type == mediaLineItem.type);

      if (index == -1) {
        this.receiveType.add(mediaLineItem);
      } else {
        this.receiveType[index] = mediaLineItem;
      }
    }
  }

  removeReceiveType(PublisherMediaLineItem mediaLineItem) {
    this.receiveType.remove(mediaLineItem);
  }

  @override
  String toString() =>
      """$runtimeType (id: $id, publishedOn: $publishedOn, publisherAccount: $publisherAccount, request: $request, status: $status, title: $title, description: $description, value: $value, place: ${place.toString()}, expirationDate: $expirationDate, maxApprovals: $maxApprovals, restrictions: $restrictions, receiveType: $receiveType, publisherMedias: $publisherMedias, receiveNotes: $receiveNotes, approvalNotes: $approvalNotes, invitedPublisherAccounts: $invitedPublisherAccounts, autoApproveRequests: $autoApproveRequests, unreadMessages: $unreadMessages, isPrivateDeal: $isPrivateDeal)""";
}
