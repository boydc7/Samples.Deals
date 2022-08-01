import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/deal_restriction.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/publisher_media_line_item.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/publisher_account.dart';
import 'package:rydr_app/models/tag.dart';
import 'package:rydr_app/services/deal.dart';
import 'package:rydr_app/app/map_config.dart';
import 'package:rydr_app/services/deal_invites.dart';
import 'package:rydr_app/ui/deal/constants.dart';

enum DealPage {
  Add,
  Preview,
  Done,
  DraftSaved,
  SaveError,
}

enum DealPreviewError {
  ErrorTitleMinLength,
  ErrorTitleMaxLength,
  ErrorDescriptionMinLength,
  ErrorDescriptionMaxLength,
  ErrorAutoApproveUnlimited,
  ErrorExpirationDateInPast,
  ErrorCostOfGoodsMissing,
  ErrorInvalidPlace,
  ErrorMissingPlace,
  ErrorMissingReceiveType,
  ErrorInvitesMissing,
  ErrorInvitesVsQuantity,
  ErrorMissingVisibility,
  ErrorVisibleMarketPlaceButMissingThresholds,
  ErrorMissingThresholds,
}

class DealAddBloc {
  final _log = getLogger('DealAddBloc');

  final _page = BehaviorSubject<DealPage>();

  final _visibilityType = BehaviorSubject<DealVisibilityType>();
  final _thresholdType = BehaviorSubject<DealThresholdType>();

  final _focusTitle = BehaviorSubject<bool>();
  final _focusDescription = BehaviorSubject<bool>();
  final _focusCostOfGoods = BehaviorSubject<bool>();
  final _focusReceiveNotes = BehaviorSubject<bool>();
  final _focusApprovalNotes = BehaviorSubject<bool>();

  final _charTitle = BehaviorSubject<int>.seeded(0);
  final _charDescription = BehaviorSubject<int>.seeded(0);

  final _canShowExchangeSection = BehaviorSubject<bool>();
  final _canPreview = BehaviorSubject<bool>();

  final _id = BehaviorSubject<int>();
  final _dealType = BehaviorSubject<DealType>();
  final _status = BehaviorSubject<DealStatus>();
  final _title = BehaviorSubject<String>();
  final _description = BehaviorSubject<String>();
  final _value = BehaviorSubject<double>();
  final _expirationDate = BehaviorSubject<DateTime>();
  final _quantity = BehaviorSubject<int>();
  final _invites = BehaviorSubject<List<PublisherAccount>>();
  final _media = BehaviorSubject<PublisherMedia>();
  final _receiveNotes = BehaviorSubject<String>();
  final _approvalNotes = BehaviorSubject<String>();
  final _followerCount = BehaviorSubject<int>();
  final _engagementRating = BehaviorSubject<double>();
  final _autoApprove = BehaviorSubject<bool>();
  final _age = BehaviorSubject<bool>();
  final _stories = BehaviorSubject<int>();
  final _posts = BehaviorSubject<int>();
  final _place = BehaviorSubject<Place>();
  final _tags = BehaviorSubject<List<Tag>>();

  bool _checkedTitle = false;
  bool _checkedDescription = false;
  bool _checkedPlace = false;
  bool _checkedValue = false;
  bool _checkedVisibility = false;
  bool _loadedInvites;

  DealAddBloc({
    Deal dealToCopy,
    DealType dealType,
  }) {
    _loadedInvites = false;

    /// when starting the 'bloc' we will configure certain options and functionality
    /// depending on if the current user is in a personal or team workspace
    if (!canUseInvites) {
      setVisibilityType(DealVisibilityType.Marketplace);
    }

    _init(
      dealToCopy,
      dealType,
    );
  }

  dispose() {
    _page.close();

    _visibilityType.close();
    _thresholdType.close();

    _focusTitle.close();
    _focusDescription.close();
    _focusCostOfGoods.close();
    _focusReceiveNotes.close();
    _focusApprovalNotes.close();

    _charTitle.close();
    _charDescription.close();

    _canPreview.close();
    _canShowExchangeSection.close();

    _id.close();
    _dealType.close();
    _status.close();
    _title.close();
    _description.close();
    _value.close();
    _expirationDate.close();
    _quantity.close();
    _invites.close();
    _media.close();
    _receiveNotes.close();
    _approvalNotes.close();
    _followerCount.close();
    _engagementRating.close();
    _autoApprove.close();
    _age.close();
    _stories.close();
    _posts.close();
    _place.close();
    _tags.close();
  }

  BehaviorSubject<DealPage> get page => _page.stream;

  BehaviorSubject<DealVisibilityType> get visibilityType =>
      _visibilityType.stream;
  BehaviorSubject<DealThresholdType> get thresholdType => _thresholdType.stream;

  BehaviorSubject<bool> get focusTitle => _focusTitle.stream;
  BehaviorSubject<bool> get focusDescription => _focusDescription.stream;
  BehaviorSubject<bool> get focusCostOfGoods => _focusCostOfGoods.stream;
  BehaviorSubject<bool> get focusReceiveNotes => _focusReceiveNotes.stream;
  BehaviorSubject<bool> get focusApprovalNotes => _focusApprovalNotes.stream;

  BehaviorSubject<int> get charTitle => _charTitle.stream;
  BehaviorSubject<int> get charDescription => _charDescription.stream;

  BehaviorSubject<int> get id => _id.stream;
  BehaviorSubject<DealType> get dealType => _dealType.stream;
  BehaviorSubject<DealStatus> get status => _status.stream;
  BehaviorSubject<String> get title => _title.stream;
  BehaviorSubject<String> get description => _description.stream;
  BehaviorSubject<double> get value => _value.stream;
  BehaviorSubject<DateTime> get expirationDate => _expirationDate.stream;
  BehaviorSubject<int> get quantity => _quantity.stream;
  BehaviorSubject<List<PublisherAccount>> get invites => _invites.stream;
  BehaviorSubject<PublisherMedia> get media => _media.stream;
  BehaviorSubject<String> get receiveNotes => _receiveNotes.stream;
  BehaviorSubject<String> get approvalNotes => _approvalNotes.stream;
  BehaviorSubject<int> get followerCount => _followerCount.stream;
  BehaviorSubject<double> get engagementRating => _engagementRating.stream;
  BehaviorSubject<bool> get autoApprove => _autoApprove.stream;
  BehaviorSubject<bool> get age => _age.stream;
  BehaviorSubject<int> get stories => _stories.stream;
  BehaviorSubject<int> get posts => _posts.stream;
  BehaviorSubject<Place> get place => _place.stream;
  BehaviorSubject<List<Tag>> get tags => _tags.stream;
  BehaviorSubject<bool> get canPreview => _canPreview.stream;
  BehaviorSubject<bool> get canShowExchangeSection =>
      _canShowExchangeSection.stream;

  /// flags for showing / hiding certain functionality
  bool get canUseInvites => appState.isBusinessPro;

  /// until we have this figured out we turn it off
  bool get canUseInsights => false;

  void setPage(DealPage page) => _page.sink.add(page);

  void setVisibilityType(DealVisibilityType type) {
    if (visibilityType.value == null || visibilityType.value != type) {
      _visibilityType.sink.add(type);

      if (type == DealVisibilityType.InviteOnly) {
        _thresholdType.sink.add(null);
        _followerCount.sink.add(null);
        _engagementRating.sink.add(null);

        /// if the quantity is set to 'unlimited' (null)
        /// then instead set it to either one, or if we already have invites
        /// then set it to match the invites already selected
        if (_quantity.value == 0) {
          setQuantity(_invites.value != null ? _invites.value.length : 1);
        }
      } else {
        /// until we get critical mass, for now we will disable the "rydr insights"
        /// functionality and button and pre-select the followers
        setThresholdType(DealThresholdType.Restrictions);

        /// if we don't have follower counts and/or engagement rating then set those to defaults
        if (followerCount.value == null) {
          setFollowerCount(defaultFollowerCount);
        }

        if (engagementRating.value == null) {
          setEngagementRating(defaultEngagementRating);
        }
      }

      if (!_checkedVisibility) {
        _checkedVisibility = true;
        _checkEnablePreview();
      }
    }
  }

  void setThresholdType(DealThresholdType type) {
    if (thresholdType.value == null || thresholdType.value != type) {
      _thresholdType.sink.add(type);
      _enableShowExchangeSection();
    }
  }

  void setFocusTitle(bool val) => _focusTitle.sink.add(val);
  void setFocusDescription(bool val) => _focusDescription.sink.add(val);
  void setFocusCostOfGoods(bool val) => _focusCostOfGoods.sink.add(val);
  void setFocusReceiveNotes(bool val) => _focusReceiveNotes.sink.add(val);
  void setFocusApprovalNotes(bool val) => _focusApprovalNotes.sink.add(val);

  /// Inputs that update deal properties
  void setId(int value) => _id.sink.add(value);
  void setDealType(DealType type) => _dealType.sink.add(type);
  void setTitle(String value) {
    _title.sink.add(value);
    _charTitle.sink.add(value != null ? value.length : 0);

    if (value != null && value.length >= minTitleLength && !_checkedTitle) {
      _checkedTitle = true;
      _checkEnablePreview();
    }

    if (value != null && value.length <= maxTitleLength && !_checkedTitle) {
      _checkedTitle = true;
      _checkEnablePreview();
    }
  }

  void setDescription(String value) {
    _description.sink.add(value);
    _charDescription.sink.add(value != null ? value.length : 0);

    if (value != null &&
        value.length >= minDescriptionLength &&
        !_checkedDescription) {
      _checkedDescription = true;
      _checkEnablePreview();
    }
  }

  void setValue(double value) {
    _value.sink.add(value);

    if (value != null && !_checkedValue) {
      _checkedValue = true;
      _checkEnablePreview();
    }
  }

  void setPlace(Place place) {
    if (place == null) {
      return;
    }

    _place.sink.add(place);

    if (!_checkedPlace) {
      _checkedPlace = true;
      _checkEnablePreview();
    }
  }

  void setQuantity(int value) {
    _quantity.sink.add(value);

    /// disable auto-approve if we have unlimited quantity
    if (value == 0) {
      _autoApprove.sink.add(false);
    }
  }

  void setStatus(DealStatus value) => _status.sink.add(value);
  void setExpirationDate(DateTime value) => _expirationDate.sink.add(value);
  void setMedia(PublisherMedia value) => _media.sink.add(value);
  void setReceiveNotes(String value) => _receiveNotes.sink.add(value);
  void setApprovalNotes(String value) => _approvalNotes.sink.add(value);
  void setFollowerCount(int value) => _followerCount.sink.add(value);
  void setEngagementRating(double value) => _engagementRating.sink.add(value);
  void setAutoApprove(bool value) => _autoApprove.sink.add(value);
  void setAge(bool value) => _age.sink.add(value);
  void setStories(int value) => _stories.sink.add(value);
  void setPosts(int value) => _posts.sink.add(value);
  void setTags(List<Tag> value) => _tags.sink.add(value);

  void loadInvites() async {
    /// if we haven't loaded invites then load them
    if (_loadedInvites == false) {
      /// if we are continuing a draft, then load up any existing invites saved with the draft
      /// deal and add them to the invites stream - this should only be done once, additional
      /// invites are then added by the invite picker
      if (deal.id != null && deal.id > 0) {
        final PublisherAccountsResponse invitesResponse =
            await DealInvitesService.getDealInvites(
          deal.id,

          /// force refresh on each load in case we've removed some, re-saved the draft
          /// then immediately come back to it...
          forceRefresh: true,
        );

        if (!invitesResponse.hasError &&
            invitesResponse.models != null &&
            invitesResponse.models.isNotEmpty) {
          setInvites(invitesResponse.models);
        }
      }

      _loadedInvites = true;
    }
  }

  void setInvites(List<PublisherAccount> value) {
    _invites.sink.add(value);
    _enableShowExchangeSection();
  }

  void removeInvite(PublisherAccount value) {
    List<PublisherAccount> existing = List.from(invites.value);

    /// NOTE! we'll compare on username vs. id or account id given that newly added
    /// instagram users don't have an id BUT an account id, but once saved we won't have
    /// an accountid but an id
    existing.removeWhere((PublisherAccount u) => u.userName == value.userName);

    setInvites(existing);
  }

  void save(bool publish) async {
    /// set status to published or draft
    setStatus(publish ? DealStatus.published : DealStatus.draft);

    final IntIdResponse res = await DealService.saveDeal(deal);

    if (res.error == null) {
      /// update the id on the deal
      setId(res.id);

      /// if the user just published a new deal for the first time then log this with analytics
      if (publish) {
        /// log as screen
        AppAnalytics.instance.logScreen('deal/add/saved');

        /// publish the 'done' page type
        setPage(DealPage.Done);
      } else {
        /// log as screen
        AppAnalytics.instance.logScreen('deal/add/saveddraft');

        /// publish the 'draft saved' page type
        setPage(DealPage.DraftSaved);
      }
    } else {
      setPage(DealPage.SaveError);
    }
  }

  /// check if the place for the deal is valid given our current set of locations
  /// where we are supporting deals in, will return true / false to indicate availability
  bool isPlaceIsInValidRegion(Place place) {
    if (place == null ||
        place.address == null ||
        place.address.latitude == null ||
        place.address.longitude == null) {
      return false;
    } else {
      PlaceLatLng latLng = PlaceLatLng(
        place.address.latitude,
        place.address.longitude,
      );

      for (AvailableLocation loc in mapConfig.values.availableLocations) {
        if (loc.bounds.contains(latLng)) {
          return true;
        }
      }

      return false;
    }
  }

  List<DealPreviewError> checkCanPreview() {
    Deal _deal = deal;
    List<DealPreviewError> errors = [];

    _log.d('checkCanPreview | $deal');

    /// deal title & description must have at least a minimum length
    errors.add(_deal.title.trim().length < minTitleLength
        ? DealPreviewError.ErrorTitleMinLength
        : null);

    errors.add(_deal.title.trim().length > maxTitleLength
        ? DealPreviewError.ErrorTitleMaxLength
        : null);

    errors.add(_deal.description.trim().length < minDescriptionLength
        ? DealPreviewError.ErrorDescriptionMinLength
        : null);

    errors.add(_deal.description.trim().length > maxDescriptionLength
        ? DealPreviewError.ErrorDescriptionMaxLength
        : null);

    /// if auto-approve is on, then we can't have an unlimited quantity
    errors.add(_deal.autoApproveRequests && _deal.maxApprovals < 1
        ? DealPreviewError.ErrorAutoApproveUnlimited
        : null);

    /// if this deal has an expiration date then it can't be in the past
    errors.add(_deal.expirationDate != null &&
            _deal.expirationDate.isBefore(DateTime.now())
        ? DealPreviewError.ErrorExpirationDateInPast
        : null);

    /// deal cost of goos is always required
    errors.add(
        _deal.value == null ? DealPreviewError.ErrorCostOfGoodsMissing : null);

    /// ensure we have a place
    errors.add(_deal.place == null ? DealPreviewError.ErrorMissingPlace : null);

    /// validate that the chosen deal place is in a supported region
    errors.add(isPlaceIsInValidRegion(_deal.place) == false
        ? DealPreviewError.ErrorInvalidPlace
        : null);

    /// ensure we have a visibility type set
    errors.add(visibilityType.value == null
        ? DealPreviewError.ErrorMissingVisibility
        : null);

    /// validate that we have at least a post or story
    /// if we're using restrictions vs. rydr insights
    errors.add(thresholdType.value == DealThresholdType.Restrictions &&
            (_deal.requestedStories + _deal.requestedPosts) < 1
        ? DealPreviewError.ErrorMissingReceiveType
        : null);

    /// if we're doing an invite-only deal then validate a few things
    if (visibilityType.value == DealVisibilityType.InviteOnly) {
      /// must have at least one invite
      errors.add(_deal.invitesToAdd == null
          ? DealPreviewError.ErrorInvitesMissing
          : null);

      /// can't have unlimited quantity when using invite-only
      /// we must have at least as many or more invites than we have the quantity set as
      errors.add(_deal.invitesToAdd != null &&
              (_deal.maxApprovals == 0 ||
                  _deal.maxApprovals > _deal.invitesToAdd.length)
          ? DealPreviewError.ErrorInvitesVsQuantity
          : null);
    }

    /// if the visibility is set for marketplace
    if (visibilityType.value == DealVisibilityType.Marketplace) {
      /// then we need to ensure we have a threshold type set
      errors.add(thresholdType.value == null
          ? DealPreviewError.ErrorVisibleMarketPlaceButMissingThresholds
          : null);

      /// ensure we have restrictions if threshold is based on restrictions
      errors.add(thresholdType.value == DealThresholdType.Restrictions &&
              followerCount.value == null &&
              engagementRating.value == null
          ? DealPreviewError.ErrorMissingThresholds
          : null);
    }

    return errors.where((error) => error != null).toList();
  }

  bool get canSaveDraft =>
      title.value != null &&
      (description.value != null ||
          place.value != null ||
          stories.value != null ||
          posts.value != null ||
          followerCount.value != null ||
          engagementRating.value != null);

  /// populates a deal from our existing stream values
  /// which we'll then use to submit to the server
  Deal get deal => Deal()
    ..dealType = dealType.value
    ..approvalNotes = approvalNotes.value
    ..autoApproveRequests = autoApprove.value
    ..description = description.value
    ..expirationDate = expirationDate.value
    ..id = id.value
    ..invitesToAdd = invites.value
    ..isPrivateDeal = visibilityType.value == DealVisibilityType.InviteOnly
    ..maxApprovals = quantity.value
    ..place = place.value
    ..publisherMedias = media.value != null ? [media.value] : null
    ..receiveNotes = receiveNotes.value
    ..receiveType = receiveType
    ..restrictions = restrictions
    ..status = status.value
    ..title = title.value
    ..value = value.value
    ..tags = tags.value;

  List<PublisherMediaLineItem> get receiveType {
    List<PublisherMediaLineItem> items = [];

    items.add(stories.value != null && stories.value > 0
        ? PublisherMediaLineItem(
            type: PublisherContentType.story,
            quantity: stories.value,
          )
        : null);

    items.add(posts.value != null && posts.value > 0
        ? PublisherMediaLineItem(
            type: PublisherContentType.post,
            quantity: posts.value,
          )
        : null);

    items.removeWhere((value) => value == null);

    return items;
  }

  List<DealRestriction> get restrictions {
    List<DealRestriction> items = [
      followerCount.value != null
          ? DealRestriction(
              DealRestrictionType.minFollowerCount,
              followerCount.value.toString(),
            )
          : null,
      engagementRating.value != null
          ? DealRestriction(
              DealRestrictionType.minEngagementRating,
              engagementRating.value.toString(),
            )
          : null,
      age.value != null
          ? DealRestriction(
              DealRestrictionType.minAge,
              age.value == true ? "21" : null,
            )
          : null,
    ];

    /// remove nulls both on the restriction object and any value
    items.removeWhere((restriction) => restriction == null);
    items.removeWhere((restriction) => restriction.value == null);

    return items;
  }

  void _enableShowExchangeSection() {
    if (canShowExchangeSection.value == null) {
      _canShowExchangeSection.sink.add(true);
    }
  }

  void _checkEnablePreview() {
    if (_checkedTitle &&
        _checkedDescription &&
        _checkedPlace &&
        _checkedValue &&
        _checkedVisibility) {
      _canPreview.sink.add(true);
    }
  }

  void _init(Deal dealToCopy, DealType dealType) {
    _log.d('_initDeal | $dealToCopy');

    if (dealToCopy != null) {
      /// if we're passing an existing deal, then copy over its values
      /// this could be from duplicating a template or continuing on a draft deal
      setId(dealToCopy.status == DealStatus.draft ? dealToCopy.id : null);
      setDealType(dealToCopy.dealType);
      setTitle(dealToCopy.title);
      setDescription(dealToCopy.description);
      setReceiveNotes(dealToCopy.receiveNotes);
      setApprovalNotes(dealToCopy.approvalNotes);
      setExpirationDate(dealToCopy.expirationDate);
      setAutoApprove(dealToCopy.autoApproveRequests ?? false);
      setQuantity(dealToCopy.maxApprovals);
      setValue(dealToCopy.value);
      setMedia(dealToCopy.publisherMedias != null &&
              dealToCopy.publisherMedias.length > 0
          ? dealToCopy.publisherMedias[0]
          : null);

      setStories(dealToCopy.requestedStories);
      setPosts(dealToCopy.requestedPosts);
      setFollowerCount(dealToCopy.minFollowerCount);
      setEngagementRating(dealToCopy.minEngagementRating);
      setAge(dealToCopy.minAge == 21);
      setPlace(dealToCopy.place);
      setTags(dealToCopy.tags);

      /// "Marketplace" vs. "Invite only"
      setVisibilityType(!dealToCopy.isPrivateDeal
          ? DealVisibilityType.Marketplace
          : DealVisibilityType.InviteOnly);

      /// if its a marketplace deal with either followers or engagement
      /// then set the threshold type vs insights type
      if (!dealToCopy.isPrivateDeal) {
        setThresholdType(dealToCopy.minFollowerCount != null ||
                dealToCopy.minEngagementRating != null
            ? DealThresholdType.Restrictions
            : DealThresholdType.Insights);
      }

      /// load potential pending invites
      loadInvites();
    } else {
      /// if we're staring a deal from scratch then set some defaults
      setDealType(dealType);
      setQuantity(defaultQuantity);
      setStories(defaultStories);
      setPosts(defaultPosts);
      setEngagementRating(defaultEngagementRating);
      setFollowerCount(defaultFollowerCount);
      setAutoApprove(false);
    }
  }
}
