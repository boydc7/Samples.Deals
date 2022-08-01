import 'package:rxdart/rxdart.dart';
import 'package:collection/collection.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/deal_metadata.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/deal.dart';
import 'package:rydr_app/models/deal_restriction.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/tag.dart';
import 'package:rydr_app/services/deal.dart';

enum DealEditState {
  Saved,
  ErrorSaving,
  Paused,
  ErrorPausing,
  Archived,
  ErrorArchiving,
  Deleted,
  ErrorDeleting,
  Editing,
  UpdatedImage,
  ErrorUpdatingImage,
  AddedInvites,
  ErrorAddingInvites,
}

class DealEditBloc {
  final _dealResponse = BehaviorSubject<DealResponse>();
  final _status = BehaviorSubject<DealStatus>();
  final _expirationDate = BehaviorSubject<DateTime>();
  final _value = BehaviorSubject<double>();
  final _invitesToAdd = BehaviorSubject<List<PublisherAccount>>();
  final _publisherMedias = BehaviorSubject<List<PublisherMedia>>();
  final _followerCount = BehaviorSubject<int>();
  final _engagementRating = BehaviorSubject<double>();
  final _age = BehaviorSubject<bool>();
  final _autoApprove = BehaviorSubject<bool>();
  final _approvalNotes = BehaviorSubject<String>();
  final _editing = BehaviorSubject<bool>.seeded(true);
  final _hasChanges = BehaviorSubject<bool>.seeded(false);
  final _scrolled = BehaviorSubject<bool>();
  final _pageState = BehaviorSubject<DealEditState>();
  final _focusCostOfGoods = BehaviorSubject<bool>();
  final _focusApprovalNotes = BehaviorSubject<bool>();
  final _startDate = BehaviorSubject<DateTime>();
  final _endDate = BehaviorSubject<DateTime>();
  final _mediaStartDate = BehaviorSubject<DateTime>();
  final _artwork = BehaviorSubject<List<PublisherApprovedMedia>>();
  final _tags = BehaviorSubject<List<Tag>>();
  final _hasEndDate = BehaviorSubject<bool>();

  bool _canEdit = false;

  dispose() {
    _dealResponse.close();

    _status.close();
    _expirationDate.close();
    _value.close();
    _invitesToAdd.close();
    _publisherMedias.close();
    _followerCount.close();
    _engagementRating.close();
    _age.close();
    _autoApprove.close();
    _approvalNotes.close();
    _startDate.close();
    _endDate.close();
    _hasEndDate.close();
    _mediaStartDate.close();
    _artwork.close();
    _tags.close();

    _editing.close();
    _hasChanges.close();
    _scrolled.close();
    _pageState.close();
    _focusCostOfGoods.close();
    _focusApprovalNotes.close();
  }

  BehaviorSubject<DealResponse> get dealResponse => _dealResponse.stream;
  BehaviorSubject<DealStatus> get status => _status.stream;
  BehaviorSubject<DateTime> get expirationDate => _expirationDate.stream;
  BehaviorSubject<double> get value => _value.stream;
  BehaviorSubject<List<PublisherAccount>> get invitesToAdd =>
      _invitesToAdd.stream;
  BehaviorSubject<List<PublisherMedia>> get publisherMedias =>
      _publisherMedias.stream;
  BehaviorSubject<int> get followerCount => _followerCount.stream;
  BehaviorSubject<double> get engagementRating => _engagementRating.stream;
  BehaviorSubject<bool> get age => _age.stream;
  BehaviorSubject<bool> get autoApprove => _autoApprove.stream;
  BehaviorSubject<String> get approvalNotes => _approvalNotes.stream;
  BehaviorSubject<bool> get editing => _editing.stream;
  BehaviorSubject<bool> get hasChanges => _hasChanges.stream;
  BehaviorSubject<bool> get scrolled => _scrolled.stream;
  BehaviorSubject<DealEditState> get pageState => _pageState.stream;
  BehaviorSubject<bool> get focusCostOfGoods => _focusCostOfGoods.stream;
  BehaviorSubject<bool> get focusApprovalNotes => _focusApprovalNotes.stream;
  BehaviorSubject<DateTime> get startDate => _startDate.stream;
  BehaviorSubject<DateTime> get endDate => _endDate.stream;
  BehaviorSubject<bool> get hasEndDate => _hasEndDate.stream;
  BehaviorSubject<DateTime> get mediaStartDate => _mediaStartDate.stream;
  BehaviorSubject<List<PublisherApprovedMedia>> get artwork => _artwork.stream;
  BehaviorSubject<List<Tag>> get tags => _tags.stream;

  bool get canEdit => _canEdit;

  /// flags for showing / hiding certain functionality
  bool get canUseInvites => appState.isBusinessPro;

  void setStatus(DealStatus value) => _status.sink.add(value);
  void setExpirationDate(DateTime value) {
    _expirationDate.sink.add(value);
    _checkHasChanges();
  }

  void setValue(String cost) {
    if (cost.trim().length > 0 && double.tryParse(cost.trim()) != null) {
      _value.sink.add(double.parse(cost.trim()));
    } else {
      _value.sink.add(null);
    }

    _checkHasChanges();
  }

  void setFollowerCount(int value) {
    _followerCount.sink.add(value);
    _checkHasChanges();
  }

  void setEngagementRating(double value) {
    _engagementRating.sink.add(value);
    _checkHasChanges();
  }

  void setAge(bool value) {
    _age.sink.add(value);
    _checkHasChanges();
  }

  void setAutoApprove(bool value) {
    _autoApprove.sink.add(value);
    _checkHasChanges();
  }

  void setApprovalNotes(String value) {
    _approvalNotes.sink.add(value);
    _checkHasChanges();
  }

  void setStartDate(DateTime value) {
    _startDate.sink.add(value);
    _checkHasChanges();
  }

  void setEndDate(DateTime value) {
    _endDate.sink.add(value);
    _checkHasChanges();
  }

  void setHasEndDate(bool value) {
    _hasEndDate.sink.add(value);

    if (value == false) {
      setEndDate(null);
    }

    _checkHasChanges();
  }

  void setMediaStartDate(DateTime value) {
    _mediaStartDate.sink.add(value);
    _checkHasChanges();
  }

  void setTags(List<Tag> value) {
    _tags.sink.add(value);
    _checkHasChanges();
  }

  void setScrolled(bool val) => _scrolled.sink.add(val);
  void setFocusCostOfGoods(bool val) => _focusCostOfGoods.sink.add(val);
  void setFocusApprovalNotes(bool val) => _focusApprovalNotes.sink.add(val);

  void setInvitesToAdd(List<PublisherAccount> value) =>
      _invitesToAdd.sink.add(value);
  void setPublisherMedias(List<PublisherMedia> value) =>
      _publisherMedias.sink.add(value);
  void removeInvite(PublisherAccount value) {
    List<PublisherAccount> existing = List.from(invitesToAdd.value);
    existing
        .removeWhere((PublisherAccount u) => u.accountId == value.accountId);

    setInvitesToAdd(existing);
  }

  void setArtwork(List<PublisherApprovedMedia> media) {
    _artwork.sink.add(media);
    _checkHasChanges();
  }

  void loadDeal(int dealId) async {
    final DealResponse res = await DealService.getDeal(dealId);

    if (res.error == null) {
      _canEdit = res.model.status != DealStatus.completed &&
          res.model.status != DealStatus.deleted;

      setStatus(res.model.status);
      setExpirationDate(res.model.expirationDate);
      setValue(res.model.value.toString());
      setPublisherMedias(res.model.publisherMedias);
      setFollowerCount(res.model.minFollowerCount);
      setEngagementRating(res.model.minEngagementRating);
      setAge(res.model.minAge == 21);
      setApprovalNotes(res.model.approvalNotes);
      setStartDate(res.model.startDate);
      setEndDate(res.model.endDate);
      setHasEndDate(res.model.endDate != null);
      setMediaStartDate(res.model.mediaStartDate);
      setTags(res.model.tags);
    }

    _dealResponse.sink.add(res);
  }

  void pause(bool pause) async {
    final res = await DealService.updateStatus(
      dealResponse.value.model.id,
      pause ? DealStatus.paused : DealStatus.published,
    );

    if (res.error == null) {
      AppAnalytics.instance
          .logScreen(pause ? 'deal/edit/paused' : 'deal/edit/unpaused');
    }

    _pageState.sink.add(
        res.error == null ? DealEditState.Paused : DealEditState.ErrorPausing);
  }

  void archive() async {
    final res = await DealService.updateStatus(
      dealResponse.value.model.id,
      DealStatus.completed,
    );

    if (res.error == null) {
      AppAnalytics.instance.logScreen('deal/edit/archived');
    }

    _pageState.sink.add(res.error == null
        ? DealEditState.Archived
        : DealEditState.ErrorArchiving);
  }

  void delete() async {
    final res = await DealService.updateStatus(
      dealResponse.value.model.id,
      DealStatus.deleted,
    );

    if (res.error == null) {
      AppAnalytics.instance.logScreen('deal/edit/deleted');
    }

    _pageState.sink.add(res.error == null
        ? DealEditState.Deleted
        : DealEditState.ErrorDeleting);
  }

  void save() async {
    final Deal dealOriginal = dealResponse.value.model;
    final Deal dealWithChanges = deal;

    BasicVoidResponse resDeal;
    BasicVoidResponse resExpirationDate;

    /// determines if we're updating any deal properties and/or
    /// clearing the expiration date (vs. changing it)
    bool updateDeal = false;
    bool clearExpirationDate = false;

    Deal dealToUpdate = Deal();
    dealToUpdate.id = deal.id;

    if (dealOriginal.value != dealWithChanges.value &&
        dealWithChanges.value != null) {
      updateDeal = true;
      dealToUpdate.value = dealWithChanges.value;
    }

    if (dealOriginal.minFollowerCount != dealWithChanges.minFollowerCount ||
        dealOriginal.minEngagementRating !=
            dealWithChanges.minEngagementRating ||
        dealOriginal.minAge != dealWithChanges.minAge) {
      updateDeal = true;
      dealToUpdate.restrictions = dealWithChanges.restrictions;
    }

    if (dealOriginal.expirationDate != dealWithChanges.expirationDate) {
      if (dealWithChanges.expirationDate == null) {
        clearExpirationDate = true;
      } else {
        updateDeal = true;
        dealToUpdate.expirationDate = dealWithChanges.expirationDate;
      }
    }

    if (dealOriginal.approvalNotes != dealWithChanges.approvalNotes &&
        dealWithChanges.approvalNotes != null) {
      updateDeal = true;
      dealToUpdate.approvalNotes = dealWithChanges.approvalNotes;
    }

    if (dealOriginal.startDate != dealWithChanges.startDate ||
        dealOriginal.endDate != dealWithChanges.endDate ||
        dealOriginal.mediaStartDate != dealWithChanges.mediaStartDate) {
      updateDeal = true;
      dealToUpdate.metaData = dealWithChanges.metaData;
    }

    if (dealOriginal.publisherApprovedMediaIds !=
        dealWithChanges.publisherApprovedMediaIds) {
      /// TODO: should we use equality check instead?
      updateDeal = true;
      dealToUpdate.publisherApprovedMediaIds =
          dealWithChanges.publisherApprovedMediaIds;
    }

    if (!ListEquality().equals(dealOriginal.tags, dealWithChanges.tags)) {
      updateDeal = true;
      dealToUpdate.tags = dealWithChanges.tags;
    }

    if (updateDeal) {
      resDeal = await DealService.updateDeal(dealToUpdate);
    }

    if (clearExpirationDate) {
      resExpirationDate =
          await DealService.updateExpirationDate(dealToUpdate.id, null);
    }

    if (resDeal?.error == null && resExpirationDate?.error == null) {
      AppAnalytics.instance.logScreen('deal/edit/updated');
    }

    _hasChanges.sink.add(false);

    _pageState.sink.add(
        resDeal?.error == null && resExpirationDate?.error == null
            ? DealEditState.Saved
            : DealEditState.ErrorSaving);
  }

  void saveMedia(PublisherMedia media) async {
    if ([media] != dealResponse.value.model.publisherMedias) {
      Deal dealToUpdate = Deal();
      dealToUpdate.id = deal.id;
      dealToUpdate.publisherMedias = [media];

      final BasicVoidResponse res = await DealService.updateDeal(dealToUpdate);

      if (res.error == null) {
        AppAnalytics.instance.logScreen('deal/edit/updatedmedia');
      }

      _publisherMedias.sink.add([media]);

      _pageState.sink.add(res.error == null
          ? DealEditState.UpdatedImage
          : DealEditState.ErrorUpdatingImage);
    }
  }

  void sendInvites() async {
    if (_invitesToAdd.value != null && _invitesToAdd.value.isNotEmpty) {
      final BasicVoidResponse res = await DealService.addInvites(
        deal.id,
        _invitesToAdd.value,
      );

      if (res.error == null) {
        AppAnalytics.instance.logScreen('deal/edit/invited');
      }

      _pageState.sink.add(res.error == null
          ? DealEditState.AddedInvites
          : DealEditState.ErrorAddingInvites);
    }
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

  /// populates a deal from our existing stream values
  /// which we'll then use to submit to the server
  Deal get deal => Deal()
    ..id = dealResponse.value.model.id
    ..dealType = dealResponse.value.model.dealType
    ..isPrivateDeal = dealResponse.value.model.isPrivateDeal
    ..restrictions = restrictions
    ..status = status.value
    ..value = value.value
    ..expirationDate = expirationDate.value
    ..tags = tags.value
    ..publisherApprovedMediaIds =
        _artwork.value != null && _artwork.value.isNotEmpty
            ? List.from(_artwork.value.map((m) => m.id).toList()..sort())
            : []
    ..approvalNotes = approvalNotes.value != null && approvalNotes.value != ""
        ? approvalNotes.value
        : null
    ..metaData = [
      _startDate.value != null
          ? DealMetaData(
              DealMetaType.StartDate,
              _startDate.value.toIso8601String(),
            )
          : null,
      _endDate.value != null
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

  void _checkHasChanges() {
    if (dealResponse.value != null) {
      final Deal _dealToSave = deal;
      final Deal _dealOriginal = dealResponse.value.model;

      _hasChanges.sink.add(_dealToSave.value != _dealOriginal.value ||
          _dealToSave.minFollowerCount != _dealOriginal.minFollowerCount ||
          _dealToSave.minEngagementRating !=
              _dealOriginal.minEngagementRating ||
          _dealToSave.minAge != _dealOriginal.minAge ||
          _dealToSave.expirationDate != _dealOriginal.expirationDate ||
          _dealToSave.approvalNotes != _dealOriginal.approvalNotes ||
          _dealToSave.startDate != _dealOriginal.startDate ||
          _dealToSave.endDate != _dealOriginal.endDate ||
          _dealToSave.mediaStartDate != _dealOriginal.mediaStartDate ||
          !ListEquality().equals(_dealToSave.publisherApprovedMediaIds,
              _dealOriginal.publisherApprovedMediaIds) ||
          !ListEquality().equals(_dealToSave.tags, _dealOriginal.tags));
    }
  }
}
