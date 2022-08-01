import 'dart:async';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/deal.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/deal/blocs/deal_edit.dart';
import 'package:rydr_app/ui/deal/widgets/edit/edit_fields.dart';
import 'package:rydr_app/ui/deal/widgets/edit/invites.dart';
import 'package:rydr_app/ui/deal/widgets/edit/metrics.dart';
import 'package:rydr_app/ui/deal/widgets/shared/description.dart';
import 'package:rydr_app/ui/deal/widgets/shared/place.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_notes.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_type_listItem.dart';
import 'package:rydr_app/ui/deal/widgets/shared/quantity.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/icons.dart';

import 'package:rydr_app/models/deal.dart';

import 'package:rydr_app/ui/deal/utils.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_media.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

class DealEditPage extends StatefulWidget {
  final int dealId;

  DealEditPage(this.dealId);

  @override
  _DealEditPageState createState() => _DealEditPageState();
}

class _DealEditPageState extends State<DealEditPage> {
  final DealEditBloc _bloc = DealEditBloc();

  final TextEditingController _valueController = TextEditingController();
  final ScrollController _scrollController = ScrollController();

  StreamSubscription _subSaved;

  @override
  void initState() {
    _bloc.loadDeal(widget.dealId);

    _subSaved = _bloc.pageState.listen(_onSavedChanged);

    _valueController.addListener(_valueListener);
    _scrollController.addListener(_scrollListener);

    super.initState();
  }

  @override
  void dispose() {
    _subSaved?.cancel();
    _bloc.dispose();

    _valueController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  void _scrollListener() {
    if (_scrollController.offset > 50.0 &&
        (_bloc.scrolled.value == null || _bloc.scrolled.value == false)) {
      _bloc.setScrolled(true);
    } else if (_scrollController.offset <= 50.0 &&
        _bloc.scrolled.value == true) {
      _bloc.setScrolled(false);
    }
  }

  void _onSavedChanged(DealEditState state) {
    if (state == DealEditState.ErrorSaving ||
        state == DealEditState.ErrorPausing ||
        state == DealEditState.ErrorArchiving ||
        state == DealEditState.ErrorDeleting ||
        state == DealEditState.ErrorUpdatingImage ||
        state == DealEditState.ErrorAddingInvites) {
      Navigator.of(context).pop();

      showSharedModalError(
        context,
        title: 'Unable to update RYDR',
        subTitle:
            'We were unable to update your RYDR, please try again in a few moments.',
      );
    } else {
      /// if there was no error then we replace/reload the active marketplace list
      /// so that any changes would be reflected in the list...
      Navigator.of(context).pop();
      Navigator.of(context).pushReplacementNamed(AppRouting.getDealsActive);
    }
  }

  void _save(BuildContext context) {
    showSharedLoadingLogo(context, content: "Updating RYDR");

    _bloc.save();
  }

  void _valueListener() {
    _bloc.setValue(_valueController.text);
  }

  void _showDealActions(bool isExpired) {
    final bool isPaused = _bloc.status.value == DealStatus.paused;
    final bool isPrivate = _bloc.dealResponse.value.model.isPrivateDeal;
    final bool noCompleted = _bloc.dealResponse.value.model
                .getStat(DealStatType.totalCompleted) ==
            null ||
        _bloc.dealResponse.value.model.getStat(DealStatType.totalCompleted) ==
            0;

    showSharedModalBottomActions(context,
        title: isPaused
            ? 'RYDR Options: Paused'
            : isExpired ? 'RYDR Options: Expired' : 'RYDR Options',
        actions: <ModalBottomAction>[
          /// can't share if this deal is paused, expired, or private
          /// otherwise show the sharing option in the bottom sheet
          isPaused || isExpired || isPrivate
              ? null
              : ModalBottomAction(
                  child: Text("Share"),
                  icon: AppIcons.share,
                  onTap: () {
                    Navigator.of(context).pop();
                    _share();
                  }),

          /// if this deal has no completed RYDRs then no need to show
          /// an option to navigate to the insights
          noCompleted
              ? null
              : ModalBottomAction(
                  child: Text("View Completed Insights"),
                  icon: AppIcons.analytics,
                  onTap: () {
                    Navigator.of(context).pop();
                    Navigator.of(context).pushNamed(
                        AppRouting.getDealInsightsRoute(widget.dealId),
                        arguments: _bloc.dealResponse.value.model);
                  }),

          isExpired && !isPaused
              ? ModalBottomAction(
                  child: Text("Extend RYDR"),
                  icon: AppIcons.play,
                  onTap: () {
                    Navigator.of(context).pop();
                    _showExpirationPicker();
                  },
                )
              : ModalBottomAction(
                  child: Text(isPaused ? "Reactivate" : "Pause"),
                  icon: isPaused ? AppIcons.play : AppIcons.pause,
                  onTap: () {
                    Navigator.of(context).pop();
                    _showPauseToggleConfirmation(isPaused ? false : true);
                  },
                ),

          ModalBottomAction(
            child: Text(isExpired ? "Recreate" : "Duplicate"),
            icon: AppIcons.copy,
            onTap: () {
              Navigator.of(context).pop();
              _goToRecreate(isExpired);
            },
          ),

          ModalBottomAction(
            child: Text("Archive"),
            icon: AppIcons.archive,
            onTap: () {
              Navigator.of(context).pop();
              _showArchiveConfirmation(isExpired);
            },
          ),
          ModalBottomAction(
            child: Text("Delete"),
            icon: AppIcons.archive,
            isDestructiveAction: true,
            onTap: () {
              Navigator.of(context).pop();
              _showDeleteConfirmation();
            },
          ),
        ].where((element) => element != null).toList());
  }

  void _showExpirationPicker() => showDealDatePicker(
      context: context,
      reactivate: true,
      onCancel: () => Navigator.of(context).pop(),
      onContinue: (DateTime newValue) {
        Navigator.of(context).pop();
        showSharedLoadingLogo(context);

        _bloc.setExpirationDate(newValue);
        _bloc.save();
      });

  void _showPauseToggleConfirmation(bool pauseIt) {
    showSharedModalAlert(
      context,
      Text(pauseIt ? "Pause RYDR" : "Reactivate RYDR"),
      content: Text(pauseIt
          ? "This will temporarily remove your RYDR from the public marketplace. Any existing requests will remain active."
          : "This will make your RYDR visible in the public marketplace."),
      actions: <ModalAlertAction>[
        ModalAlertAction(
          label: "Not Now",
          onPressed: () {
            Navigator.of(context).pop();
          },
        ),
        ModalAlertAction(
          label: pauseIt ? "Pause" : "Reactivate",
          isDefaultAction: true,
          isDestructiveAction: !pauseIt ? false : true,
          onPressed: () {
            Navigator.of(context).pop();

            showSharedLoadingLogo(
              context,
              content: pauseIt ? "Pausing RYDR" : "Reactivating RYDR",
            );

            _bloc.pause(pauseIt);
          },
        ),
      ],
    );
  }

  void _showDeleteConfirmation() {
    showSharedModalAlert(
      context,
      Text("Delete RYDR"),
      content: Text(
          "This will permanently remove your RYDR from the public marketplace and cancel any existing pending requests."),
      actions: <ModalAlertAction>[
        ModalAlertAction(
            label: "Cancel", onPressed: () => Navigator.of(context).pop()),
        ModalAlertAction(
          isDestructiveAction: true,
          label: "Delete",
          onPressed: () {
            Navigator.of(context).pop();

            showSharedLoadingLogo(
              context,
              content: "Deleting RYDR",
            );

            _bloc.delete();
          },
        ),
      ],
    );
  }

  void _showArchiveConfirmation(bool isExpired) {
    showSharedModalAlert(
      context,
      Text("Archive RYDR"),
      content: Text(isExpired
          ? "This will remove this expired RYDR from your list and move it into Profile > Options > Account > Archived RYDRs. \n\nYou can also reactivate or recreate this RYDR to put it back into the Marketplace."
          : "This will permanently remove your RYDR from the public marketplace. Any existing requests will remain active."),
      actions: <ModalAlertAction>[
        isExpired
            ? ModalAlertAction(
                isDefaultAction: true,
                label: "Reactivate",
                onPressed: () {
                  Navigator.of(context).pop();
                  _showExpirationPicker();
                })
            : null,
        isExpired
            ? ModalAlertAction(
                label: "Recreate",
                onPressed: () {
                  /// when re-creating a deal we're using an expired deal, so before sending it to the add-deal page
                  /// we'll extends the expiration date set on it to a date in the future
                  Navigator.of(context).pop();

                  /// NOTE! once we want to support events we'll want to send the user
                  /// to interstitial page for choosing deal type here...
                  Navigator.of(context).pushNamed(AppRouting.getDealAddDeal,
                      arguments: _bloc.dealResponse.value.model
                        ..expirationDate =
                            DateTime.now().add(Duration(days: 14)));
                })
            : null,
        ModalAlertAction(
          isDestructiveAction: true,
          label: "Archive",
          onPressed: () {
            Navigator.of(context).pop();

            showSharedLoadingLogo(
              context,
              content: "Archiving RYDR",
            );

            _bloc.archive();
          },
        ),
        ModalAlertAction(
          label: "Cancel",
          onPressed: () => Navigator.of(context).pop(),
        ),
      ].where((element) => element != null).toList(),
    );
  }

  void _share() async {
    showDealShare(context, _bloc.dealResponse.value.model);
  }

  void _updateImage(PublisherMedia media) {
    /// show overlay while we save
    showSharedLoadingLogo(
      context,
      content: "Updating RYDR",
    );

    _bloc.saveMedia(media);
  }

  void _sendInvites() {
    showSharedModalAlert(context, Text("Invite Creators?"),
        content: Text(
            "Tap 'Send Invites' to invite the selected creators to this RYDR"),
        actions: [
          ModalAlertAction(
              label: "Cancel", onPressed: () => Navigator.of(context).pop()),
          ModalAlertAction(
              label: "Send Invites",
              isDefaultAction: true,
              onPressed: () {
                /// close this modal before opening a loading overlay
                /// which will show while we process the invites to send
                Navigator.of(context).pop();

                showSharedLoadingLogo(context);

                _bloc.sendInvites();
              }),
        ]);
  }

  void _goToInsights(Deal deal) => Navigator.of(context)
      .pushNamed(AppRouting.getDealInsightsRoute(deal.id), arguments: deal);

  /// when re-creating and extending a deal we're using an expired deal, so before sending it to the add-deal page
  /// we'll extends the expiration date set on it to a date in the future
  ///
  /// NOTE! once we want to support events we'll want to send the user
  /// to interstitial page for choosing deal type here...
  void _goToRecreate(bool extend) => extend
      ? Navigator.of(context).pushNamed(AppRouting.getDealAddDeal,
          arguments: _bloc.dealResponse.value.model
            ..expirationDate = DateTime.now().add(Duration(days: 14)))
      : Navigator.of(context).pushNamed(AppRouting.getDealAddDeal,
          arguments: _bloc.dealResponse.value.model);

  void _checkSave(BuildContext context, bool hasChanges) {
    if (hasChanges) {
      showSharedModalBottomActions(context,
          title: "Discard Changes",
          subtitle: 'If you go back now, the edits to this RYDR will be lost.',
          actions: <ModalBottomAction>[
            ModalBottomAction(
                child: Text("Discard Changes"),
                icon: AppIcons.trashAlt,
                isDestructiveAction: true,
                onTap: () {
                  Navigator.of(context).pop();
                  Navigator.of(context).pop();
                }),
            ModalBottomAction(
                child: Text("Save Changes"),
                icon: AppIcons.save,
                isDestructiveAction: false,
                onTap: () {
                  Navigator.of(context).pop();

                  showSharedLoadingLogo(
                    context,
                    content: "Updating RYDR",
                  );

                  _bloc.save();
                }),
          ]);
    } else {
      if (Navigator.of(context).canPop()) {
        Navigator.of(context).pop();
      } else {
        Navigator.of(context).pushNamed(AppRouting.getHome);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return StreamBuilder<DealResponse>(
      stream: _bloc.dealResponse,
      builder: (context, snapshot) {
        return snapshot.connectionState == ConnectionState.waiting
            ? _buildLoadingBody(dark)
            : snapshot.error != null || snapshot.data.error != null
                ? _buildErrorBody(dark, snapshot.data)
                : _buildSuccessBody(dark, snapshot.data);
      },
    );
  }

  Widget _buildLoadingBody(bool dark) => Scaffold(
        body: ListView(
          children: <Widget>[
            LoadingDetailsShimmer(),
          ],
        ),
      );

  Widget _buildErrorBody(bool dark, DealResponse dealResponse) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
        ),
        body: RetryError(
          onRetry: () => _bloc.loadDeal(
            widget.dealId,
          ),
          error: dealResponse.error,
        ),
      );

  Widget _buildSuccessBody(bool dark, DealResponse dealResponse) {
    final Deal deal = dealResponse.model;
    final bool isPaused = deal.status == DealStatus.paused;
    final bool isExpired = deal.expirationInfo.isExpired;
    final bool isArchived = deal.status == DealStatus.completed;
    final bool isDeleted = deal.status == DealStatus.deleted;
    final bool isVirtual = deal.dealType == DealType.Virtual;
    final bool noCompleted = deal.getStat(DealStatType.currentCompleted) == 0;

    final String appBarTitle = isDeleted
        ? "RYDR Deleted"
        : isArchived
            ? "RYDR Archived"
            : isExpired
                ? "RYDR Expired"
                : isPaused ? "RYDR Paused" : deal.title;

    final String dealTitle = isDeleted
        ? "Deleted: ${deal.title}"
        : isArchived
            ? "Archived: ${deal.title}"
            : isExpired
                ? "Expired: ${deal.title}"
                : isPaused ? "Paused: ${deal.title}" : deal.title;

    final appBarActions = _bloc.canEdit
        ? [
            IconButton(
              highlightColor: Colors.transparent,
              splashColor: Colors.transparent,
              icon: Icon(AppIcons.ellipsisV),
              onPressed: () => _showDealActions(isExpired),
            )
          ]
        : [Container()];

    return Scaffold(
      appBar: PreferredSize(
        child: StreamBuilder<bool>(
          stream: _bloc.scrolled,
          builder: (context, snapshot) {
            final bool scrolled =
                snapshot.data != null && snapshot.data == true;

            return AppBar(
              title: snapshot.data == null
                  ? Text('')
                  : !scrolled
                      ? FadeOutOpacityOnly(0, Text(appBarTitle))
                      : FadeInOpacityOnly(0, Text(appBarTitle)),
              backgroundColor: scrolled
                  ? Theme.of(context).appBarTheme.color
                  : Theme.of(context).scaffoldBackgroundColor,
              elevation: scrolled ? 1.0 : 0.0,
              leading: StreamBuilder<bool>(
                stream: _bloc.hasChanges,
                builder: (context, snapshot) {
                  final bool hasChanges =
                      snapshot.data != null && snapshot.data == true;

                  return AppBarBackButton(context,
                      onPressed: () => _checkSave(context, hasChanges));
                },
              ),
              actions: appBarActions,
            );
          },
        ),
        preferredSize: Size.fromHeight(kToolbarHeight),
      ),
      body: ListView(
        controller: _scrollController,
        children: <Widget>[
          Column(
            children: <Widget>[
              Container(
                height: 240.0,
                width: double.infinity,
                child: Align(
                  alignment: Alignment.topCenter,
                  child: Container(
                    height: 200.0,
                    width: 200.0,
                    child: StreamBuilder<List<PublisherMedia>>(
                      stream: _bloc.publisherMedias,
                      builder: (context, snapshot) {
                        return snapshot.data == null
                            ? Container()
                            : DealMedia(
                                existingMedia: snapshot.data.isNotEmpty
                                    ? snapshot.data[0]
                                    : null,
                                currentDealStatus: deal.status,
                                onChoose: _updateImage,
                                big: true,
                                expired: isExpired,
                              );
                      },
                    ),
                  ),
                ),
              ),
              Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.center,
                children: <Widget>[
                  Padding(
                    padding: EdgeInsets.symmetric(horizontal: 16.0),
                    child: Text(
                      dealTitle,
                      style: Theme.of(context).textTheme.bodyText2.merge(
                            TextStyle(
                                fontWeight: FontWeight.w500,
                                fontSize: 26.0,
                                color: Theme.of(context)
                                    .textTheme
                                    .bodyText2
                                    .color),
                          ),
                      textAlign: TextAlign.center,
                    ),
                  ),
                  isVirtual
                      ? Padding(
                          padding: EdgeInsets.only(top: 4.0),
                          child: Text(
                            "Virtual",
                            style: TextStyle(
                              fontWeight: FontWeight.bold,
                              color: Colors.deepOrange,
                            ),
                          ),
                        )
                      : Container(
                          height: 0,
                          width: 0,
                        ),
                  SizedBox(height: 16.0),
                  isExpired || isArchived || isDeleted
                      ? _buildActionButtons(isExpired, isArchived, isDeleted)
                      : isPaused
                          ? _buildPausedButton()
                          : Container(
                              height: 0,
                              width: 0,
                            ),
                ],
              ),
              DealMetrics(deal),
              Visibility(
                visible: !noCompleted,
                child: Column(
                  children: <Widget>[
                    Divider(height: 1),
                    Padding(
                      padding: EdgeInsets.symmetric(vertical: 8.0),
                      child: TextButton(
                        label: 'View Completed Insights',
                        color: Theme.of(context).primaryColor,
                        onTap: () => _goToInsights(deal),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          sectionDivider(context),
          DealEditFields(_bloc, !isExpired && !isArchived && !isDeleted),
          sectionDivider(context),
          DealEditInvites(_bloc, _sendInvites, isExpired),
          DealDescription(deal),
          DealReceiveTypeListItem(deal),
          DealReceiveNotes(deal),
          DealQuantity(deal),
          DealPlace(deal, false),
        ],
      ),
      bottomNavigationBar: StreamBuilder<bool>(
        stream: _bloc.hasChanges,
        builder: (context, snapshot) {
          final bool hasChanges =
              snapshot.data != null && snapshot.data == true;

          if (hasChanges) {
            return BottomAppBar(
              child: Container(
                padding: EdgeInsets.all(16),
                child: PrimaryButton(
                  label: "Update",
                  context: context,
                  onTap: () => _save(context),
                  buttonColor: deal.dealType == DealType.Virtual
                      ? Colors.deepOrange
                      : Theme.of(context).primaryColor,
                ),
              ),
            );
          } else {
            return Container(height: 0, width: 0);
          }
        },
      ),
    );
  }

  Widget _buildActionButtons(bool isExpired, bool isArchived, bool isDeleted) =>
      Padding(
        padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
        child: Column(
          children: <Widget>[
            Visibility(
              visible: isExpired && !isArchived && !isDeleted,
              child: Padding(
                padding: EdgeInsets.only(bottom: 16.0),
                child: Text(
                  "This RYDR has expired.",
                  style: TextStyle(
                    color: Theme.of(context).hintColor,
                  ),
                ),
              ),
            ),
            Row(
              children: <Widget>[
                Visibility(
                  visible: !isArchived && !isDeleted,
                  child: Expanded(
                    child: SecondaryButton(
                      label: "Extend RYDR",
                      onTap: _showExpirationPicker,
                      primary: true,
                      context: context,
                    ),
                  ),
                ),
                Visibility(
                  visible: !isArchived && !isDeleted,
                  child: SizedBox(width: 8.0),
                ),
                Expanded(
                  child: SecondaryButton(
                    label: "Recreate",
                    onTap: () => _goToRecreate(true),
                    context: context,
                  ),
                ),
              ],
            ),
          ],
        ),
      );

  Widget _buildPausedButton() => GestureDetector(
        onTap: () => _showPauseToggleConfirmation(false),
        child: Container(
          height: 32.0,
          margin: EdgeInsets.only(left: 64.0, right: 64.0, bottom: 8.0),
          padding: EdgeInsets.symmetric(horizontal: 16.0),
          decoration: BoxDecoration(
              border: Border.all(color: Colors.grey.shade300, width: 1.0),
              borderRadius: BorderRadius.circular(16.0),
              color: Theme.of(context).scaffoldBackgroundColor),
          child: Center(
              child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Text(
                'PAUSED',
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(
                          color: AppColors.grey300,
                          fontWeight: FontWeight.w600),
                    ),
              ),
              Text(
                ' · TAP TO REACTIVATE',
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(color: AppColors.grey300),
                    ),
              ),
            ],
          )),
        ),
      );
}
