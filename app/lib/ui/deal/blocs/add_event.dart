import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/deal_metadata.dart';
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

enum EventPage {
  Details,
  PromoteWithPosts,
  PromoteStartDate,
  EventMedia,
  PostRequirements,
  InvitePicker,
  Preview,
  Done,
}

class AddEventBloc {
  final _log = getLogger('AddEventBloc');
  final _page = BehaviorSubject<EventPage>();
  final _focusTitle = BehaviorSubject<bool>();
  final _focusDescription = BehaviorSubject<bool>();
  final _focusReceiveNotes = BehaviorSubject<bool>();
  final _charTitle = BehaviorSubject<int>.seeded(0);
  final _charDescription = BehaviorSubject<int>.seeded(0);
  final _id = BehaviorSubject<int>();
  final _visibilityType = BehaviorSubject<DealVisibilityType>();
  final _status = BehaviorSubject<DealStatus>();
  final _title = BehaviorSubject<String>();
  final _description = BehaviorSubject<String>();
  final _startDate = BehaviorSubject<DateTime>();
  final _endDate = BehaviorSubject<DateTime>();
  final _mediaStartDate = BehaviorSubject<DateTime>();
  final _media = BehaviorSubject<PublisherMedia>();
  final _receiveNotes = BehaviorSubject<String>();
  final _followerCount = BehaviorSubject<int>();
  final _engagementRating = BehaviorSubject<double>();
  final _autoApprove = BehaviorSubject<bool>.seeded(true);
  final _age = BehaviorSubject<bool>();
  final _stories = BehaviorSubject<int>();
  final _posts = BehaviorSubject<int>();
  final _place = BehaviorSubject<Place>();
  final _hasEndDate = BehaviorSubject<bool>();
  final _artwork = BehaviorSubject<List<PublisherApprovedMedia>>();
  final _tags = BehaviorSubject<List<Tag>>();

  final _invites = BehaviorSubject<List<PublisherAccount>>();

  StreamSubscription _subPage;
  bool _loadedInvites;

  AddEventBloc(Deal dealToCopy) {
    _loadedInvites = false;
    _init(dealToCopy);

    /// listen to changes of the page which can help us make some
    /// changes to data as previous pages were changed by the user
    _subPage = _page.listen((page) {
      /// if we're going to the preview page and we don't have any invites
      /// for this event then we have to ensure that the deal is visible / public
      if (page == EventPage.Preview) {
        if (_invites.value == null || _invites.value.isEmpty) {
          _visibilityType.sink.add(DealVisibilityType.Marketplace);
        }
      } else if (page == EventPage.PostRequirements) {
        loadInvites();
      }
    });
  }

  dispose() {
    _page.close();

    _focusTitle.close();
    _focusDescription.close();
    _focusReceiveNotes.close();

    _charTitle.close();
    _charDescription.close();

    _id.close();
    _visibilityType.close();
    _status.close();
    _title.close();
    _description.close();
    _invites.close();
    _media.close();
    _receiveNotes.close();
    _followerCount.close();
    _engagementRating.close();
    _autoApprove.close();
    _age.close();
    _stories.close();
    _posts.close();
    _place.close();
    _hasEndDate.close();
    _startDate.close();
    _endDate.close();
    _mediaStartDate.close();
    _artwork.close();
    _tags.close();

    _subPage.cancel();
  }

  BehaviorSubject<EventPage> get page => _page.stream;

  BehaviorSubject<bool> get focusTitle => _focusTitle.stream;
  BehaviorSubject<bool> get focusDescription => _focusDescription.stream;
  BehaviorSubject<bool> get focusReceiveNotes => _focusReceiveNotes.stream;

  BehaviorSubject<int> get charTitle => _charTitle.stream;
  BehaviorSubject<int> get charDescription => _charDescription.stream;

  BehaviorSubject<int> get id => _id.stream;
  BehaviorSubject<DealVisibilityType> get visibilityType =>
      _visibilityType.stream;
  BehaviorSubject<DealStatus> get status => _status.stream;
  BehaviorSubject<String> get title => _title.stream;
  BehaviorSubject<String> get description => _description.stream;
  BehaviorSubject<DateTime> get startDate => _startDate.stream;
  BehaviorSubject<DateTime> get endDate => _endDate.stream;
  BehaviorSubject<List<PublisherAccount>> get invites => _invites.stream;
  BehaviorSubject<PublisherMedia> get media => _media.stream;
  BehaviorSubject<String> get receiveNotes => _receiveNotes.stream;
  BehaviorSubject<int> get followerCount => _followerCount.stream;
  BehaviorSubject<double> get engagementRating => _engagementRating.stream;
  BehaviorSubject<bool> get autoApprove => _autoApprove.stream;
  BehaviorSubject<bool> get age => _age.stream;
  BehaviorSubject<int> get stories => _stories.stream;
  BehaviorSubject<int> get posts => _posts.stream;
  BehaviorSubject<Place> get place => _place.stream;
  BehaviorSubject<bool> get hasEndDate => _hasEndDate.stream;
  BehaviorSubject<List<PublisherApprovedMedia>> get artwork => _artwork.stream;
  BehaviorSubject<List<Tag>> get tags => _tags.stream;

  int get preEventDays =>
      _startDate.value != null && _mediaStartDate.value != null
          ? _mediaStartDate.value.difference(_startDate.value).inDays.abs()
          : 0;

  void setPage(EventPage page) => _page.sink.add(page);
  void setFocusTitle(bool val) => _focusTitle.sink.add(val);
  void setFocusDescription(bool val) => _focusDescription.sink.add(val);
  void setFocusReceiveNotes(bool val) => _focusReceiveNotes.sink.add(val);
  void setId(int value) => _id.sink.add(value);
  void setStatus(DealStatus value) => _status.sink.add(value);
  void setEndDate(DateTime value) => _endDate.sink.add(value);
  void setMediaStartDate(DateTime value) => _mediaStartDate.sink.add(value);
  void setMedia(PublisherMedia value) => _media.sink.add(value);
  void setReceiveNotes(String value) => _receiveNotes.sink.add(value);
  void setFollowerCount(int value) => _followerCount.sink.add(value);
  void setEngagementRating(double value) => _engagementRating.sink.add(value);
  void setAutoApprove(bool value) => _autoApprove.sink.add(value);
  void setAge(bool value) => _age.sink.add(value);
  void setStories(int value) => _stories.sink.add(value);
  void setPosts(int value) => _posts.sink.add(value);
  void setInvites(List<PublisherAccount> value) => _invites.sink.add(value);
  void setTags(List<Tag> value) => _tags.sink.add(value);

  void setTitle(String value) {
    _title.sink.add(value);
    _charTitle.sink.add(value.length);
  }

  void setDescription(String value) {
    _description.sink.add(value);
    _charDescription.sink.add(value.length);
  }

  void setPlace(Place place) {
    if (place == null) {
      return;
    }

    _place.sink.add(place);
  }

  void setHasEndDate(bool value) {
    _hasEndDate.sink.add(value);

    if (value == false) {
      setEndDate(null);
    }
  }

  void setVisibilityType(DealVisibilityType type) {
    if (visibilityType.value == null || visibilityType.value != type) {
      _visibilityType.sink.add(type);

      if (type == DealVisibilityType.InviteOnly) {
        setFollowerCount(null);
        setEngagementRating(null);
      } else {
        /// if we don't have follower counts and/or engagement rating then set those to defaults
        if (followerCount.value == null) {
          setFollowerCount(defaultFollowerCount);
        }

        if (engagementRating.value == null) {
          setEngagementRating(defaultEngagementRating);
        }
      }
    }
  }

  void setStartDate(DateTime value) {
    /// adjust media start date if the start date has changed
    /// or, default the mediaStartDate if we've just set the start date
    /// for the first time...
    if (_startDate.value == null) {
      _startDate.sink.add(value);
      _mediaStartDate.sink.add(value.add(Duration(days: 7)));
    } else {
      if (_startDate.value != value) {
        final DateTime valueCopy = DateTime.parse(value.toString());

        _mediaStartDate.sink.add(valueCopy.subtract(
            Duration(days: _mediaStartDate.value == null ? 7 : preEventDays)));
      }

      _startDate.sink.add(value);
    }
  }

  void setArtwork(List<PublisherApprovedMedia> media) =>
      _artwork.sink.add(media);

  void loadInvites() async {
    /// if we haven't loaded invites then load them
    if (_loadedInvites == false) {
      /// if we are continuing a draft, then load up any existing invites saved with the draft
      /// deal and add them to the invites stream - this should only be done once, additional
      /// invites are then added by the invite picker
      if (deal.id != null && deal.id > 0) {
        final PublisherAccountsResponse invitesResponse =
            await DealInvitesService.getDealInvites(deal.id);

        if (!invitesResponse.hasError &&
            invitesResponse.models != null &&
            invitesResponse.models.isNotEmpty) {
          setInvites(invitesResponse.models);
        }
      }

      _loadedInvites = true;
    }
  }

  Future<bool> save(bool publish) async {
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

        /// set page to done
        setPage(EventPage.Done);
      } else {
        /// log as screen
        AppAnalytics.instance.logScreen('deal/add/saveddraft');
      }

      return true;
    } else {
      return false;
    }
  }

  /// populates a deal from our existing stream values
  /// which we'll then use to submit to the server
  Deal get deal => Deal()
    ..dealType = DealType.Event
    ..autoApproveRequests = autoApprove.value
    ..description = description.value
    ..id = id.value
    ..invitesToAdd = invites.value
    ..isPrivateDeal = _visibilityType.value == DealVisibilityType.InviteOnly
    ..place = place.value
    ..publisherMedias = media.value != null ? [media.value] : null
    ..receiveNotes = receiveNotes.value
    ..receiveType = receiveType
    ..restrictions = restrictions
    ..status = status.value
    ..title = title.value
    ..tags = tags.value
    ..publisherApprovedMediaIds =
        _artwork.value != null && _artwork.value.isNotEmpty
            ? _artwork.value.map((m) => m.id).toList()
            : []
    ..metaData = [
      _startDate.value != null
          ? DealMetaData(
              DealMetaType.StartDate,
              _startDate.value.toIso8601String(),
            )
          : null,
      _hasEndDate.value == true
          ? DealMetaData(
              DealMetaType.EndDate,
              _endDate.value.toIso8601String(),
            )
          : null,
      _mediaStartDate.value != null
          ? DealMetaData(
              DealMetaType.MediaStartDate,
              _mediaStartDate.value.toIso8601String(),
            )
          : null
    ].where((el) => el != null).toList();

  List<PublisherMediaLineItem> get receiveType => [
        stories.value != null && stories.value > 0
            ? PublisherMediaLineItem(
                type: PublisherContentType.story,
                quantity: stories.value,
              )
            : null,
        posts.value != null && posts.value > 0
            ? PublisherMediaLineItem(
                type: PublisherContentType.post,
                quantity: posts.value,
              )
            : null
      ]..removeWhere((value) => value == null);

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

  void _init(Deal dealToCopy) {
    _log.d('_initDeal | $dealToCopy');

    if (dealToCopy != null) {
      /// if we're passing an existing deal, then copy over its values
      /// this could be from duplicating a template or continuing on a draft deal
      setId(dealToCopy.status == DealStatus.draft ? dealToCopy.id : null);

      /// NOTE: seems isprivate deal on editing draft is not sticking?a
      /// or probably if we save it the first time as a draft with isprivate = false
      /// then it can no longer be updated, will have to ask Chad
      setVisibilityType(deal.isPrivateDeal
          ? DealVisibilityType.InviteOnly
          : DealVisibilityType.Marketplace);
      setTitle(dealToCopy.title);
      setDescription(dealToCopy.description);
      setReceiveNotes(dealToCopy.receiveNotes);
      setAutoApprove(dealToCopy.autoApproveRequests ?? false);
      setMedia(dealToCopy.publisherMedias != null &&
              dealToCopy.publisherMedias.length > 0
          ? dealToCopy.publisherMedias[0]
          : null);

      setStories(dealToCopy.requestedStories);
      setPosts(dealToCopy.requestedPosts);
      setFollowerCount(dealToCopy.minFollowerCount);
      setEngagementRating(dealToCopy.minEngagementRating);
      setAge(dealToCopy.minAge == 21);
      setStartDate(dealToCopy.startDate);
      setEndDate(dealToCopy.endDate);
      setHasEndDate(dealToCopy.endDate != null);
      setTags(dealToCopy.tags);
    } else {
      /// if we're staring a deal from scratch then set some defaults
      setVisibilityType(DealVisibilityType.InviteOnly);
      setStories(defaultStories);
      setPosts(defaultPosts);
      setEngagementRating(defaultEngagementRating);
      setFollowerCount(defaultFollowerCount);
      setAutoApprove(false);
    }
  }

  /// Validate individual properties of a deal
  bool get validTitle =>
      _title.value != null &&
      _title.value.length >= minTitleLength &&
      _title.value.length <= maxTitleLength;
  bool get validDescription =>
      _description.value != null &&
      _description.value.length >= minDescriptionLength &&
      _description.value.length <= maxDescriptionLength;
  bool get validPlace => isPlaceIsInValidRegion(_place.value);
  bool get validStartDate =>
      _startDate.value != null && _startDate.value.isAfter(DateTime.now());
  bool get validEndDate =>
      (_hasEndDate.value == null || _hasEndDate.value == false) ||
      (validStartDate &&
          _hasEndDate.value == true &&
          _endDate.value != null &&
          _endDate.value.isAfter(_startDate.value));

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
}

class EventArtwork {}
