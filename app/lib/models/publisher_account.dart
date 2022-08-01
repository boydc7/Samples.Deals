import 'package:firebase_auth/firebase_auth.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/publisher_metric.dart';
import 'package:rydr_app/models/tag.dart';

class PublisherAccount {
  int id;
  AuthType authType;
  PublisherType type;
  PublisherAccountType accountType;
  RydrAccountType rydrPublisherType;
  PublisherLinkType linkType;
  SubscriptionType subscriptionType;
  String accountId;
  String pageId;
  String userName;
  String fullName;
  String email;
  String website;
  String description;
  bool isSyncDisabled;
  bool isVerified;
  bool isPrivate;
  bool optInToAi;
  String profilePicture;
  DateTime lastSyncedOn;
  DateTime createdOn;
  int unreadNotifications;
  List<PublisherMedia> recentMedia = [];
  List<PublisherMetric> metrics;
  List<Tag> tags;
  int maxDelinquent;
  PublisherMetrics publisherMetrics;
  RydrAccountType linkedAsAccountType;

  /// only applicable for instabusinessaccount models
  /// we get this back from a basic IG auth flow and will transfer it
  /// with the pubmodel as part of the instagramBusinessAccount prop
  String postBackId;

  /// getter/setter for knowing if this account is on rydr already
  /// or a result from an instagram search
  bool isFromInstagram = false;

  PublisherAccount.fromFirebase(FirebaseUser firebaseUser,
      [String displayName]) {
    this.accountId = firebaseUser.uid;
    this.userName = displayName ?? firebaseUser.displayName;
    this.profilePicture = firebaseUser.photoUrl;
    this.email = firebaseUser.email;
    this.fullName = displayName ?? firebaseUser.displayName;
    this.authType =
        authTypeFromString(firebaseUser.providerData.first.providerId);
  }

  _processPublisherAccountJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.type = publisherTypeFromString(json['type']);
    this.accountType = publisherAccountTypeFromString(json['accountType']);
    this.rydrPublisherType = rydrAccountTypeFromInt(json['rydrAccountType']);
    this.linkType = publisherLinkTypeFromString(json['linkType']);
    this.accountId = json['accountId'];
    this.userName = json['userName'];
    this.profilePicture = json['profilePicture'];
    this.fullName = json['fullName'];
    this.email = json['email'];
    this.website = json['website'];
    this.isSyncDisabled = json['isSyncDisabled'];
    this.optInToAi = json['optInToAi'];
    this.isPrivate = json['isPrivate'] ?? false;
    this.maxDelinquent = json['maxDelinquent'] ?? 5;
    this.metrics = json['metrics'] != null
        ? List<PublisherMetric>.from((json['metrics']
            .entries
            .map((e) => PublisherMetric(e.key, e.value.toDouble()))
            .toList()))
        : null;

    this.tags = json['tags'] != null
        ? List<Tag>.from(json['tags'].map((t) => Tag.fromJson(t)).toList())
        : null;

    this.description = json['description'];
    this.lastSyncedOn = json['lastSyncedOn'] != null
        ? DateTime.parse(json['lastSyncedOn'].toString())
        : null;
    this.createdOn = json['createdOn'] != null
        ? DateTime.parse(json['createdOn'].toString())
        : null;

    this.publisherMetrics = PublisherMetrics(this.metrics);
  }

  /// process all base model props for a publisher account
  PublisherAccount.fromJson(Map<String, dynamic> json) {
    _processPublisherAccountJson(json);
  }

  /// Maps to a workspace user, basically a 'profile light'
  /// use for app-state profiles mostly, includes some additonal data outside of profile model
  PublisherAccount.fromProfileJson(Map<String, dynamic> json) {
    _processPublisherAccountJson(json['publisherAccountProfile']);

    this.subscriptionType =
        subscriptionTypeFromString(json['subscriptionType']);
    this.unreadNotifications = json['unreadNotifications'] ?? 0;

    this.metrics = [
      json['followerCount'] != null
          ? PublisherMetric(
              PublisherMetricName.FollowedBy, json['followerCount'].toDouble())
          : null,
    ].where((el) => el != null).toList();

    this.publisherMetrics = PublisherMetrics(this.metrics);
  }

  /// Maps to fbIgusers returned by the server when looking at list of FB pages
  /// available to 'link' to the current master user/workspace
  PublisherAccount.fromInstaBusinessAccount(Map<String, dynamic> json) {
    this.id = json['publisherAccountId'];
    this.pageId = json['id'];
    this.fullName = json['name'];
    this.accountId = json['instagramBusinessAccount']['id'];
    this.userName = json['instagramBusinessAccount']['userName'];
    this.website = json['instagramBusinessAccount']['website'];
    this.description = json['instagramBusinessAccount']['description'];
    this.profilePicture = json['instagramBusinessAccount']['profilePictureUrl'];

    this.linkedAsAccountType = rydrAccountTypeFromInt(
        json['instagramBusinessAccount']['linkedAsAccountType']);

    /// if we have a linkType then map that as well, otherwise ignore it
    /// we set this when we create a temp profile after initially completing
    /// the auth flow for a basic IG account
    if (json['instagramBusinessAccount']['linkType'] != null) {
      this.linkType = publisherLinkTypeFromString(
          json['instagramBusinessAccount']['linkType']);
    }

    /// if we have a postbackId then map that as well
    if (json['instagramBusinessAccount']['postBackId'] != null) {
      this.postBackId = json['instagramBusinessAccount']['postBackId'];
    }

    this.metrics = [
      json['instagramBusinessAccount']['followersCount'] != null
          ? PublisherMetric(PublisherMetricName.FollowedBy,
              json['instagramBusinessAccount']['followersCount'].toDouble())
          : null,
      json['instagramBusinessAccount']['followsCount'] != null
          ? PublisherMetric(PublisherMetricName.Follows,
              json['instagramBusinessAccount']['followsCount'].toDouble())
          : null,
      json['instagramBusinessAccount']['mediaCount'] != null
          ? PublisherMetric(PublisherMetricName.Media,
              json['instagramBusinessAccount']['mediaCount'].toDouble())
          : null,
    ].where((el) => el != null).toList();

    this.publisherMetrics = PublisherMetrics(this.metrics);
  }

  /// Maps to results returned by the instagram web search on things like
  /// the invite picker and the business finder page
  PublisherAccount.fromInstaJson(Map<String, dynamic> json) {
    this.accountId = json['user']['pk'];
    this.userName = json['user']['username'];
    this.fullName = json['user']['full_name'];
    this.profilePicture = json['user']['profile_pic_url'];
    this.isVerified = json['user']['is_verified'];

    /// NOTE: at this time the only account types we can create
    /// by using instagram accounts directly (for deal invites) are creator accounts
    this.type = PublisherType.instagram;
    this.accountType = PublisherAccountType.fbIgUser;
    this.rydrPublisherType = RydrAccountType.influencer;

    /// dummy metrics to avoid nulls
    this.publisherMetrics = PublisherMetrics([]);

    /// instagram search result
    this.isFromInstagram = true;
  }

  Map<String, dynamic> toJson() => {
        "id": this.id,
        "type": publisherTypeToString(this.type),
        "accountType": publisherAccountTypeToString(this.accountType),
        "rydrAccountType": rydrAccountTypeToInt(this.rydrPublisherType),
        "linkType": publisherLinkTypeToString(this.linkType),
        "accountId": this.accountId,
        "userName": this.userName,
        "fullName": this.fullName,
        "profilePicture": this.profilePicture,
        "email": this.email,
        "isSyncDisabled": this.isSyncDisabled,
        "unreadNotifications": this.unreadNotifications,
        "website": this.website,
        "description": this.description,
        "lastSyncedOn":
            this.lastSyncedOn != null ? lastSyncedOn.toString() : null,
        "createdOn": this.createdOn != null ? createdOn.toString() : null,
        "metrics": this.metrics != null
            ? Map.fromIterable(this.metrics,
                key: (e) => e.name, value: (e) => e.value)
            : null,
        "tags": this.tags != null
            ? this.tags.map((Tag t) => t.toJson()).toList()
            : null,
        "linkedAsAccountType": rydrAccountTypeToInt(this.linkedAsAccountType),
        'pageId': this.pageId != null ? pageId : null,
      };

  Map<String, dynamic> toInviteJson() => {
        "id": this.id,
        "type": publisherTypeToString(this.type),
        "accountType": publisherAccountTypeToString(this.accountType),
        "rydrAccountType": rydrAccountTypeToInt(this.rydrPublisherType),
        "linkType": publisherLinkTypeToString(this.linkType),
        "accountId": this.accountId,
        "userName": this.userName,
        "fullName": this.fullName,
        "profilePicture": this.profilePicture,
      };

  Map<String, dynamic> toProfileJson() => {
        "publisherAccountProfile": {
          "id": this.id,
          "profilePicture": this.profilePicture,
          "userName": this.userName,
          "rydrAccountType": rydrAccountTypeToInt(this.rydrPublisherType),
          "linkType": publisherLinkTypeToString(this.linkType),
          "optInToAi": this.optInToAi,
          "createdOn": this.createdOn != null ? createdOn.toString() : null,
          "maxDelinquent": this.maxDelinquent,
        },
        "subscriptionType": subscriptionTypeToString(this.subscriptionType),
        "unreadNotifications": this.unreadNotifications,
      };

  String get nameDisplay => this.fullName != null && this.fullName.isNotEmpty
      ? Utils.replaceEncoding(this.fullName)
      : this.userName != null && this.userName.isNotEmpty
          ? this.userName
          : this.email != null && this.email.isNotEmpty
              ? this.email.split('@')[0]
              : '${rydrAccountTypeToString(this.rydrPublisherType)}-${this.id}';

  String get descriptionDisplay =>
      this.description != null && this.description.isNotEmpty
          ? Utils.replaceEncoding(this.description)
          : "";

  bool get isBusiness =>
      this.rydrPublisherType == RydrAccountType.business ||
      this.rydrPublisherType == RydrAccountType.businessAndInfluencer;

  bool get isCreator =>
      this.rydrPublisherType == RydrAccountType.influencer ||
      this.rydrPublisherType == RydrAccountType.businessAndInfluencer;

  /// full = we have a facebook-type instagram page with a real access token
  /// basic = we have an instagram basic display account & token
  /// soft = we linked an instagram account ourselves without any token
  bool get isAccountFull => this.linkType == PublisherLinkType.Full;
  bool get isAccountBasic => this.linkType == PublisherLinkType.Basic;
  bool get isAccountSoft => this.linkType == PublisherLinkType.None;

  String get lastSyncedOnDisplay =>
      this.lastSyncedOn == null || this.lastSyncedOn.year < 1980
          ? null
          : Utils.formatDateLong(this.lastSyncedOn);

  String get websiteLink {
    final String link = this.website != null ? this.website : '';
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

  /// NOTE! this can be changed/extended to filter out internal tags
  /// or change it to be just categories (e.g. key: category)
  String getTagsAsString() {
    return this.tags != null && this.tags.isNotEmpty
        ? tags.map((Tag t) => t.value).toList().join(',')
        : null;
  }

  void updateUnreadNotificationsCount([bool decrement = false]) {
    if (decrement) {
      this.unreadNotifications -= this.unreadNotifications;
    } else {
      this.unreadNotifications += this.unreadNotifications;
    }
  }
}
