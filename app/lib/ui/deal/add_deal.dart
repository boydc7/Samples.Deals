import 'dart:async';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/blocs/add_deal.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_details.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_done.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_preview.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

class DealAddDeal extends StatefulWidget {
  final Deal deal;
  final DealType dealType;

  DealAddDeal({
    this.deal,
    this.dealType = DealType.Deal,
  });

  @override
  _DealAddDealState createState() => _DealAddDealState();
}

class _DealAddDealState extends State<DealAddDeal>
    with SingleTickerProviderStateMixin {
  DealAddBloc _bloc;
  StreamSubscription _subPage;
  TabController _tabController;

  final Map<String, String> _pageContent = {
    /// Important! These error map keys match the DealPreviewError enum
    /// and need to be kept insync so the lookup to an error will succeed
    /// also the min length for title and description is a const in the bloc
    "ErrorTitleMinLength":
        "The title needs to be at least 10 characters in length.",
    "ErrorDescriptionMinLength":
        "The description needs to be at least 25 characters in length.",
    "ErrorDescriptionMaxLength":
        "The description can have a maximum of 140 characters.",
    "ErrorAutoApproveUnlimited":
        "We do not allow an unlimited quantity to be auto-approved.",
    "ErrorExpirationDateInPast": "The expiration date cannot be in the past.",
    "ErrorCostOfGoodsMissing": "Enter valid cost of goods to continue.",
    "ErrorMissingPlace": "You must choose a location for this RYDR.",
    "ErrorInvalidPlace": "RYDR is not yet available for this location.",
    "ErrorMissingReceiveType":
        "You must choose a quantity and type of post. Select at least one story or one post.",
    "ErrorInvitesMissing":
        "Based on your quantity, you must invite at least as many Creators as you have quantity available.",
    "ErrorInvitesVsQuantity":
        "The quantity of RYDRs available exceeds the amount of invited creators. Either modify the quantity or invite more Creators.",
    "ErrorMissingVisibility":
        "You must choose how your RYDR will be seen. Choose either 'Marketplace' or 'Invitation Only'.",
    "ErrorVisibleMarketPlaceButMissingThresholds":
        "You must choose who will be able to see your RYDR. Choose either 'Followers & Engagement' or 'RYDR Insights'.",
    "ErrorMissingThresholds":
        "You must set a threshold for who will be able to see your RYDR. Set a threshold of 'Minimum Follower Count' and/or 'Minimum Engagement Rate'.",
  };

  @override
  void initState() {
    _tabController = TabController(
      vsync: this,
      length: 3,
      initialIndex: 0,
    );

    _bloc = DealAddBloc(
      dealToCopy: widget.deal,
      dealType: widget.dealType,
    );

    _subPage = _bloc.page.listen(_onPageChanged);
    super.initState();
  }

  @override
  void dispose() {
    _tabController.dispose();
    _subPage?.cancel();
    _bloc.dispose();

    super.dispose();
  }

  void _onPageChanged(DealPage page) {
    if (page == DealPage.Add) {
      /// remove focus so we hide the keyboard if it was showing
      /// for the redeem notes on the preview page
      FocusScope.of(context).requestFocus(FocusNode());

      _tabController.animateTo(0);
    } else if (page == DealPage.Preview) {
      _tabController.animateTo(1);
    } else if (page == DealPage.Done) {
      Navigator.of(context).pop();

      _tabController.animateTo(2);
    } else if (page == DealPage.SaveError) {
      Navigator.of(context).pop();

      showSharedModalError(
        context,
        title: 'Unable to save RYDR',
        subTitle:
            'We were unable to save your RYDR, please try again in a few moments.',
      );
    } else if (page == DealPage.DraftSaved) {
      Navigator.of(context).pop();

      showSharedModalAlert(context, Text("Draft Saved"),
          actions: <ModalAlertAction>[
            ModalAlertAction(
                label: "OK",
                onPressed: () {
                  Navigator.of(context).pushNamedAndRemoveUntil(
                      AppRouting.getHome, (Route<dynamic> route) => false);
                })
          ]);
    }
  }

  /// if the user "x"'s out on the app bar we run this to see
  /// if there's enough information entered where we might want to prompt the user
  /// to save their current input/changes as a draft, or just let them close out
  void _checkSave(BuildContext context) {
    if (_bloc.canSaveDraft) {
      showSharedModalBottomActions(context,
          title: 'If you go back now, your progress will\nbe discarded.',
          actions: <ModalBottomAction>[
            ModalBottomAction(
                child: Text("Discard"),
                icon: AppIcons.trashAlt,
                isDestructiveAction: true,
                onTap: () {
                  Navigator.of(context).pop();

                  _goBack(context);
                }),
            ModalBottomAction(
                child: Text("Save Draft"),
                icon: AppIcons.save,
                isDestructiveAction: false,
                onTap: () {
                  Navigator.of(context).pop();

                  showSharedLoadingLogo(
                    context,
                    content: "Saving Draft",
                  );

                  _bloc.save(false);
                }),
          ]);
    } else {
      _goBack(context);
    }
  }

  void _goBack(BuildContext context) => Navigator.of(context).canPop()
      ? Navigator.of(context).pop()
      : Navigator.of(context).pushNamed(AppRouting.getHome);

  /// do actual validation of min lengths and other settings here
  /// if the user is looking to jump to the preview page and show them
  /// errors for individual fields here when they have something wrong
  void _tryPreview() {
    final List<DealPreviewError> previewErrors = _bloc.checkCanPreview();

    /// remove any focus
    FocusScope.of(context).requestFocus(FocusNode());

    if (previewErrors == null || previewErrors.isEmpty) {
      _bloc.setPage(DealPage.Preview);
    } else {
      final List<String> errors = previewErrors
          .map((error) =>
              '\n\n${_pageContent[error.toString().replaceAll('DealPreviewError.', '')]}')
          .toList();

      showSharedModalError(
        context,
        title: 'This RYDR needs a tweak...',
        subTitle: errors.join(''),
      );
    }
  }

  /// saves the deal
  void _save(bool publish) async {
    showSharedLoadingLogo(
      context,
      content: publish ? "Saving RYDR" : "Saving Draft",
    );

    _bloc.save(publish);
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: _buildAppBar(),
        body: TabBarView(
          physics: NeverScrollableScrollPhysics(),
          controller: _tabController,
          children: <Widget>[
            DealAddDetails(
              bloc: _bloc,
              dealToCopy: widget.deal,
              handleContinue: _tryPreview,
            ),
            DealAddPreview(
              bloc: _bloc,
              save: _save,
            ),
            DealAddDone(
              _bloc,
            ),
          ],
        ),
      );

  Widget _buildAppBar() => PreferredSize(
        child: StreamBuilder<DealType>(
            stream: _bloc.dealType,
            builder: (context, dealType) {
              return StreamBuilder<DealPage>(
                  stream: _bloc.page,
                  builder: (context, snapshot) {
                    final DealPage page = snapshot.data ?? DealPage.Add;

                    return page == DealPage.Add
                        ? _buildAppBarAdd(dealType.data)
                        : page == DealPage.Preview
                            ? _buildAppBarPreview(dealType.data)
                            : _buildAppBarDone();
                  });
            }),
        preferredSize: Size.fromHeight(kToolbarHeight),
      );

  Widget _buildAppBarAdd(DealType dealType) => AppBar(
        leading: AppBarCloseButton(
          context,
          onPressed: () => _checkSave(context),
        ),
        centerTitle: true,
        title: Text("Create a RYDR"),
        actions: <Widget>[
          StreamBuilder<bool>(
              stream: _bloc.canPreview,
              builder: (context, snapshot) {
                return TextButton(
                  label: 'Next',
                  color: dealType == DealType.Virtual
                      ? Colors.deepOrange
                      : Theme.of(context).primaryColor,
                  onTap: snapshot.data != null && snapshot.data == true
                      ? _tryPreview
                      : null,
                );
              }),
        ],
      );

  Widget _buildAppBarPreview(DealType dealType) => AppBar(
        leading: AppBarBackButton(
          context,
          onPressed: () => _bloc.setPage(DealPage.Add),
        ),
        title: Text("How to Redeem"),
        actions: <Widget>[
          TextButton(
            label: 'Publish',
            color: dealType == DealType.Virtual
                ? Colors.deepOrange
                : Theme.of(context).primaryColor,
            onTap: () => _save(true),
          )
        ],
      );

  Widget _buildAppBarDone() => AppBar(
        elevation: 0,
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
        leading: AppBarCloseButton(
          context,
          onPressed: () => Navigator.of(context).pushNamedAndRemoveUntil(
              AppRouting.getDealsActive, (Route<dynamic> route) => false),
        ),
      );
}
