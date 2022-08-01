import 'package:rydrworkspaces/app/utils.dart';
import 'package:rydrworkspaces/models/enums/publisher_account.dart';
import 'package:rydrworkspaces/models/publisher_media.dart';
import 'package:rydrworkspaces/models/publisher_metric.dart';

class PublisherAccount {
  int id;
  PublisherType type;
  PublisherAccountType accountType;
  RydrAccountType rydrPublisherType;
  SubscriptionType subscriptionType;
  String accountId;
  String pageId;
  String userName;
  String fullName;
  String email;
  String link;
  String description;
  bool isSyncDisabled;
  bool isVerified;
  bool optInToAi;
  String profilePicture;
  DateTime lastSyncedOn;
  DateTime createdOn;
  int unreadNotifications;
  List<PublisherMedia> recentMedia = [];
  List<PublisherMetric> metrics;
  int maxDelinquent;
  PublisherMetrics publisherMetrics;
  RydrAccountType linkedAsAccountType;

  PublisherAccount.fromJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.type = publisherTypeFromString(json['type']);
    this.accountType = publisherAccountTypeFromString(json['accountType']);
    this.rydrPublisherType = rydrAccountTypeFromInt(json['rydrAccountType']);
    this.subscriptionType =
        subscriptionTypeFromString(json['subscriptionType']);
    this.accountId = json['accountId'];
    this.userName = json['userName'];
    this.profilePicture = json['profilePicture'];
    this.fullName = json['fullName'];
    this.email = json['email'];
    this.isSyncDisabled = json['isSyncDisabled'];
    this.optInToAi = json['optInToAi'];
    this.unreadNotifications = json['unreadNotifications'] ?? 0;
    this.metrics =
        json['metrics'] != null ? jsonToMetrics(json['metrics']) : null;
    this.link = json['website'];
    this.description = json['description'];
    this.lastSyncedOn = json['lastSyncedOn'] != null
        ? DateTime.parse(json['lastSyncedOn'].toString())
        : null;
    this.createdOn = json['createdOn'] != null
        ? DateTime.parse(json['createdOn'].toString())
        : null;
    this.maxDelinquent = json['maxDelinquent'] ?? 5;

    this.publisherMetrics = PublisherMetrics(this.metrics);
  }

  List<PublisherMetric> jsonToMetrics(Map<String, dynamic> json) {
    List<PublisherMetric> metrics = [];

    json.forEach((key, value) {
      metrics.add(PublisherMetric(key, value.toDouble()));
    });

    return metrics;
  }

  PublisherAccount.fromProfileJson(Map<String, dynamic> json) {
    this.id = json['publisherAccountProfile']['id'];
    this.profilePicture = json['publisherAccountProfile']['profilePicture'];
    this.userName = json['publisherAccountProfile']['userName'];
    this.rydrPublisherType = rydrAccountTypeFromInt(
        json['publisherAccountProfile']['rydrAccountType']);

    this.optInToAi = json['publisherAccountProfile']['optInToAi'];
    this.createdOn =
        DateTime.parse(json['publisherAccountProfile']['createdOn']);
    this.subscriptionType =
        subscriptionTypeFromString(json['subscriptionType']);
    this.unreadNotifications = json['unreadNotifications'] ?? 0;
    this.maxDelinquent = json['maxDelinquent'] ?? 5;

    this.metrics = [
      PublisherMetric(
          PublisherMetricName.FollowedBy, json['followerCount'].toDouble()),
    ];

    this.publisherMetrics = PublisherMetrics(this.metrics);
  }

  PublisherAccount.fromInstaBusinessAccount(Map<String, dynamic> json) {
    this.id = json['publisherAccountId'];
    this.pageId = json['id'];
    this.fullName = json['name'];
    this.accountId = json['instagramBusinessAccount']['id'];
    this.userName = json['instagramBusinessAccount']['userName'];
    this.link = json['instagramBusinessAccount']['website'];
    this.description = json['instagramBusinessAccount']['description'];
    this.profilePicture = json['instagramBusinessAccount']['profilePictureUrl'];

    this.linkedAsAccountType = rydrAccountTypeFromInt(
        json['instagramBusinessAccount']['linkedAsAccountType']);

    this.metrics = [
      PublisherMetric(PublisherMetricName.FollowedBy,
          json['instagramBusinessAccount']['followersCount'].toDouble()),
      PublisherMetric(PublisherMetricName.Follows,
          json['instagramBusinessAccount']['followsCount'].toDouble()),
      PublisherMetric(PublisherMetricName.Media,
          json['instagramBusinessAccount']['mediaCount'].toDouble()),
    ];

    this.publisherMetrics = PublisherMetrics(this.metrics);
  }

  PublisherAccount.fromInstaJson(Map<String, dynamic> json) {
    this.accountId = json['user']['pk'];
    this.userName = json['user']['username'];
    this.fullName = json['user']['full_name'];
    this.profilePicture = json['user']['profile_pic_url'];
    this.isVerified = json['user']['is_verified'];
  }

  Map<String, dynamic> toJson() => {
        "id": this.id,
        "type": publisherTypeToString(this.type),
        "accountType": publisherAccountTypeToString(this.accountType),
        "rydrAccountType": rydrAccountTypeToInt(this.rydrPublisherType),
        "accountId": this.accountId,
        "userName": this.userName,
        "fullName": this.fullName,
        "profilePicture": this.profilePicture,
        "email": this.email,
        "isSyncDisabled": this.isSyncDisabled,
        "unreadNotifications": this.unreadNotifications,
        "website": this.link,
        "description": this.description,
        "lastSyncedOn":
            this.lastSyncedOn != null ? lastSyncedOn.toString() : null,
        "createdOn": this.createdOn != null ? createdOn.toString() : null,
        "metrics": this.metrics != null ? metricsToJson() : null,
        "linkedAsAccountType": rydrAccountTypeToInt(this.linkedAsAccountType),
        'pageId': this.pageId != null ? pageId : null,
      };

  Map<String, dynamic> metricsToJson() {
    Map<String, dynamic> metrics = {};

    this.metrics.forEach((PublisherMetric metric) {
      metrics[metric.name] = metric.value;
    });

    return metrics;
  }

  String get nameDisplay => this.fullName != null && this.fullName.isNotEmpty
      ? this.fullName
      : this.userName != null && this.userName.isNotEmpty
          ? this.userName
          : this.email != null && this.email.isNotEmpty
              ? this.email.split('@')[0]
              : '${rydrAccountTypeToString(this.rydrPublisherType)}-${this.id}';

  bool get isBusiness =>
      this.rydrPublisherType == RydrAccountType.business ||
      this.rydrPublisherType == RydrAccountType.businessAndInfluencer;

  bool get isCreator =>
      this.rydrPublisherType == RydrAccountType.influencer ||
      this.rydrPublisherType == RydrAccountType.businessAndInfluencer;

  String get lastSyncedOnDisplay =>
      this.lastSyncedOn != null && this.lastSyncedOn.year == 1969
          ? null
          : Utils.formatDateLong(this.lastSyncedOn);

  String get websiteLink {
    final String link = this.link != null ? this.link : '';
    final bool isHttp = link.startsWith('http://');
    final bool isHttps = link.startsWith('https://');
    final String linkNoPrefix = isHttp
        ? link.replaceAll('http://', '')
        : isHttps ? link.replaceAll('https://', '') : '';

    return linkNoPrefix.endsWith('/')
        ? linkNoPrefix.replaceRange(
            linkNoPrefix.length - 1, linkNoPrefix.length, '')
        : linkNoPrefix;
  }

  void updateUnreadNotificationsCount([bool decrement = false]) {
    if (decrement) {
      this.unreadNotifications -= this.unreadNotifications;
    } else {
      this.unreadNotifications += this.unreadNotifications;
    }
  }
}
