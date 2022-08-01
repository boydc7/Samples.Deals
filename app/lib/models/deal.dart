import 'package:intl/intl.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal_metadata.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/tag.dart';
import 'publisher_media_line_item.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/deal_stat.dart';
import 'package:rydr_app/models/deal_restriction.dart';
import 'package:rydr_app/models/deal_request.dart';
import 'package:rydr_app/models/deal_expiration_info.dart';
import 'package:rydr_app/models/deal_request_completion_stats.dart';
import 'package:rydr_app/models/publisher_media.dart';

class Deal {
  int id;
  DealType dealType;
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
  List<Tag> tags;
  String receiveNotes;
  String approvalNotes;
  List<PublisherAccount> pendingRecentRequesters;
  bool autoApproveRequests;
  int unreadMessages;
  int approvalsRemaining;
  bool isPrivateDeal;
  List<PublisherAccount> invitesToAdd;
  List<int> publisherApprovedMediaIds;
  List<DealMetaData> metaData;
  bool isInvited;
  bool canBeRequested;

  Deal();

  DealExpirationInfo get expirationInfo =>
      DealExpirationInfo(this.expirationDate);

  String get distanceInMilesDisplay => distanceInMiles == null
      ? ""
      : distanceInMiles.toStringAsFixed(
              distanceInMiles.truncateToDouble() == distanceInMiles ? 0 : 1) +
          ' mi';

  double get minEngagementRating =>
      _getRestrictionAsDouble(DealRestrictionType.minEngagementRating);
  int get minFollowerCount =>
      _getRestrictionAsInt(DealRestrictionType.minFollowerCount);
  int get minAge => _getRestrictionAsInt(DealRestrictionType.minAge);

  String get minFollowerCountCompactDisplayMin {
    final NumberFormat minFollowers = NumberFormat.compact();
    final String value = _getRestriction(DealRestrictionType.minFollowerCount);

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

  String get titleClean => title.lastIndexOf(' ') > -1
      ? title.replaceFirst(RegExp(r' '), '\u00A0', title.lastIndexOf(' '))
      : title;

  int get requestedPosts => _getReceiveType(PublisherContentType.post);
  int get requestedStories => _getReceiveType(PublisherContentType.story);

  DateTime get startDate => _getMetaDate(DealMetaType.StartDate);
  DateTime get endDate => _getMetaDate(DealMetaType.EndDate);
  DateTime get mediaStartDate => _getMetaDate(DealMetaType.MediaStartDate);

  _processDealJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.dealType = dealTypeFromJson(json['dealType']);
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
        ? List<PublisherMediaLineItem>.from(json['receiveType']
            .map((r) => PublisherMediaLineItem.fromJson(r))
            .toList())
        : null;
    this.tags = json['tags'] != null
        ? List<Tag>.from(json['tags'].map((t) => Tag.fromJson(t)).toList())
        : null;
    this.receiveNotes = json['receiveNotes'];
    this.publisherMedias = json['publisherMedias'] != null
        ? List<PublisherMedia>.from(json['publisherMedias']
            .map((media) => PublisherMedia.fromJson(media))
            .toList())
        : null;
    this.restrictions = json['restrictions'] != null
        ? List<DealRestriction>.from(json['restrictions']
            .map((r) => DealRestriction.fromJson(r))
            .toList())
        : null;
    this.isPrivateDeal = json['isPrivateDeal'];

    this.metaData = json['metaData'] != null
        ? json == null
            ? null
            : List<DealMetaData>.from(json['metaData']
                .map((data) => DealMetaData.fromJson(data))
                .toList())
        : null;
    this.publisherApprovedMediaIds = json['publisherApprovedMediaIds'] != null
        ? List.from(List.from(json['publisherApprovedMediaIds'])..sort())
        : null;
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
        ? List<PublisherAccount>.from(json['pendingRecentRequesters']
            .map((stat) => PublisherAccount.fromJson(stat))
            .toList())
        : null;

    this.stats = json['stats'] != null ? jsonToStats(json['stats']) : null;

    this.unreadMessages = json['unreadMessages'];
    this.approvalsRemaining = json['approvalsRemaining'];

    /// indicates to a creator that they have an invite pending for this deal
    /// when we call published deals endpoint - default to false to avoid nulls
    this.isInvited = json['isInvited'] ?? false;

    /// NOTE: currently this flag is only returned when calling the getbyLink endpoint
    /// for retrieving deal data, we'll set it to true otherwise
    this.canBeRequested = json['canBeRequested'] ?? true;

    this.request = json['dealRequest'] != null
        ? DealRequest.fromJson(json['dealRequest'])
        : null;

    this.requestCompletionStats = this.request != null &&
            this.request.status == DealRequestStatus.completed &&
            this.request.completionMediaStatValues != null
        ? DealRequestCompletionStats(this.value, this.request)
        : null;
  }

  static List<DealStat> jsonToStats(List<dynamic> json) {
    List<DealStat> stats = [];
    json.forEach((stat) {
      stats.add(DealStat.fromJson(stat));
    });

    return stats;
  }

  Map<String, Map<String, dynamic>> toPayload() {
    Map<String, Map<String, dynamic>> payload = {
      "model": {
        "id": id == null ? 0 : id,
      }
    };

    if (id == null) {
      payload['model']['publisherAccountId'] = appState.currentProfile.id;
    }

    /// ingore dealType on updates
    if (dealType != null && id == null) {
      payload['model']['dealType'] = dealTypeToString(dealType);
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

    if (tags != null && tags.isNotEmpty) {
      payload['model']['tags'] = tags.map((Tag tag) => tag.toJson()).toList();
    }

    if (receiveNotes != null && receiveNotes.isNotEmpty) {
      payload['model']['receiveNotes'] = receiveNotes;
    }

    if (invitesToAdd != null && invitesToAdd.isNotEmpty) {
      payload['model']['invitedPublisherAccounts'] = invitesToAdd
          .map((PublisherAccount user) => user.toInviteJson())
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

    if (metaData != null && metaData.isNotEmpty) {
      payload['model']['metaData'] =
          metaData.map((DealMetaData metaData) => metaData.toJson()).toList();
    }

    if (publisherApprovedMediaIds != null &&
        publisherApprovedMediaIds.isNotEmpty) {
      payload['model']['publisherApprovedMediaIds'] = publisherApprovedMediaIds;
    }

    if (autoApproveRequests != null) {
      payload['model']['autoApproveRequests'] = autoApproveRequests;
    }

    if (expirationDate != null) {
      payload['model']['expirationDate'] = expirationDate.toIso8601String();
    }

    /// NOTE: leaving maxApprovals as null will be considered "unlimited" approvals
    /// we'll also take anything like 0 or -1 as being "unlimited"
    if (maxApprovals != null && maxApprovals > 0) {
      payload['model']['maxApprovals'] = maxApprovals;
    }

    if (approvalNotes != null && approvalNotes.isNotEmpty) {
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

  removeReceiveType(PublisherMediaLineItem mediaLineItem) =>
      this.receiveType.remove(mediaLineItem);

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

  DateTime _getMetaDate(DealMetaType type) => this.metaData != null &&
          this.metaData.isNotEmpty &&
          this.metaData.where((data) => data.type == type).length > 0
      ? DateTime.parse(
          this.metaData.firstWhere((data) => data.type == type).value)
      : null;

  int _getReceiveType(PublisherContentType type) => receiveType == null
      ? 0
      : receiveType.firstWhere(
          (PublisherMediaLineItem item) => item.type == type, orElse: () {
          return PublisherMediaLineItem(quantity: 0);
        }).quantity;

  String _getRestriction(DealRestrictionType type) {
    if (this.restrictions == null) {
      return null;
    }

    DealRestriction restriction = this.restrictions.firstWhere(
          (restriction) => restriction.type == type,
          orElse: () => null,
        );

    return restriction != null ? restriction.value : null;
  }

  int _getRestrictionAsInt(DealRestrictionType type) {
    final String value = _getRestriction(type);

    return value != null && value != "null" ? int.parse(value) : null;
  }

  double _getRestrictionAsDouble(DealRestrictionType type) {
    final String value = _getRestriction(type);

    return value != null && value != "null" ? double.parse(value) : null;
  }

  @override
  String toString() =>
      """$runtimeType (id: $id, publishedOn: $publishedOn, publisherAccount: $publisherAccount, request: $request, status: $status, title: $title, description: $description, value: $value, place: ${place.toString()}, expirationDate: $expirationDate, maxApprovals: $maxApprovals, restrictions: $restrictions, receiveType: $receiveType, publisherMedias: $publisherMedias, receiveNotes: $receiveNotes, approvalNotes: $approvalNotes, invitesToAdd: $invitesToAdd, autoApproveRequests: $autoApproveRequests, unreadMessages: $unreadMessages, isPrivateDeal: $isPrivateDeal)""";
}
