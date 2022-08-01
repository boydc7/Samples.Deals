import 'dart:async';
import 'dart:ui';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/ui/deal/blocs/request.dart';
import 'package:rydr_app/ui/deal/widgets/shared/brand.dart';
import 'package:rydr_app/ui/deal/widgets/shared/description.dart';
import 'package:rydr_app/ui/deal/widgets/shared/place.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_type.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_notes.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/ui/deal/utils.dart';
import 'package:rydr_app/ui/map/blocs/deal.dart';
import 'package:rydr_app/ui/map/blocs/map.dart';
import 'package:rydr_app/ui/map/widgets/deal_auto_approved.dart';
import 'package:rydr_app/ui/map/widgets/deal_header.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

/// eithe from the map or from a stand alone link/deep link
class InfluencerDeal extends StatefulWidget {
  final MapBloc mapBloc;
  final Deal deal;
  final Function onClose;

  InfluencerDeal({
    this.mapBloc,
    this.deal,
    @required this.onClose,
  });

  @override
  _InfluencerDealState createState() => _InfluencerDealState();
}

class _InfluencerDealState extends State<InfluencerDeal>
    with SingleTickerProviderStateMixin {
  final MapDealBloc _dealBloc = MapDealBloc();
  final RequestBloc _bloc = RequestBloc();

  StreamSubscription _subDeal;
  StreamSubscription _subMapTapped;
  BuildContext _contextDetails;
  ThemeData _theme;
  bool _darkMode;
  bool _isInvite;
  bool _isVirtual;
  Deal _deal;
  Color _actionColor;

  /// are we viewing a stand-alone deal (e.g. from a deep link for example)
  /// or are we coming from the map? depends on if we have widget.deal or not
  bool _isStandAlone;

  /// threshold for showing / hidding the bottom bar
  /// as well as the actions buttons
  final double _bottomBarThreshold = 0.49;
  final double _actionButtonsThreshold = 0.8;

  /// initial scroll factor (changes if stand alone),
  /// sheet size and min (only applicable to map view)
  double _initialScrollFactor = 0.49;
  final double _initialChildSize = 0.49;
  final double _minChildSize = 0.24;

  @override
  void initState() {
    super.initState();

    /// set deal if we have an incoming one
    /// and if we have one then we're viewing stand alone
    _deal = widget.deal;
    _isStandAlone = widget.deal != null;

    /// only wire listeners if we're coming from the map
    /// and adjust the initial child size if we are from the map
    if (!_isStandAlone) {
      _subMapTapped = widget.mapBloc.mapTapped.listen((bool tapped) {
        if (tapped == true) {
          if (_dealBloc.showBottomBar?.value == true) {
            DraggableScrollableActuator.reset(_contextDetails);

            _reset();
          }
        }
      });

      _subDeal = widget.mapBloc.selectedDeal.listen((Deal deal) {
        if (deal != null) {
          DraggableScrollableActuator.reset(_contextDetails);
        } else {
          _reset();
        }
      });
    } else {
      _initialScrollFactor = 1.0;
      _dealBloc.setShowBottomBar(true);
    }
  }

  @override
  void dispose() {
    _subDeal?.cancel();
    _subMapTapped?.cancel();
    _bloc.dispose();
    _dealBloc.dispose();

    super.dispose();
  }

  void _reset() {
    _dealBloc.setScrollFactor(_initialChildSize);
    _dealBloc.setShowBottomBar(false);
    _dealBloc.setShowActions(true);
  }

  void _onClose() => widget.onClose();

  bool _onSheetMove(DraggableScrollableNotification notification) {
    final double val = notification.extent;

    if (val > _bottomBarThreshold && _dealBloc.showBottomBar?.value != true) {
      _dealBloc.setShowBottomBar(true);
    } else if (val <= _bottomBarThreshold &&
        _dealBloc.showBottomBar?.value != false) {
      _dealBloc.setShowBottomBar(false);
    }

    if (val <= _actionButtonsThreshold &&
        _dealBloc.showActions?.value != true) {
      _dealBloc.setShowActions(true);
    } else if (val > _actionButtonsThreshold &&
        _dealBloc.showActions?.value != false) {
      _dealBloc.setShowActions(false);
    }

    if (val < 1) {
      /// keep track of the scroll factor once we're no longer full extended
      /// e.g. the draggable sheet is not fully at the top
      _dealBloc.setScrollFactor(val);
    }

    return true;
  }

  void _showAutoApproveDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 2.0, sigmaY: 2.0),
        child: FadeInBottomTop(
            5,
            Dialog(
              backgroundColor: Colors.transparent,
              elevation: 0,
              child: DealAutoApproved(_deal),
            ),
            500),
      ),
    );
  }

  void _clickRequest() => showSharedModalAlert(context,
          Text(_deal.minAge == 21 ? "Confirm 21+ Request" : "Request RYDR"),
          content: Text(_deal.minAge == 21
              ? appState.currentProfile.isAccountFull
                  ? "Sending this request confirms you are over 21 years old and will allow ${_deal.publisherAccount.userName} to view your account analytics and recent posts.\n\nWe will notify you when they approve or deny your request."
                  : "Sending this request confirms you are over 21 years old and will allow ${_deal.publisherAccount.userName} to view your recent posts.\n\nWe will notify you when they approve or deny your request."
              : appState.currentProfile.isAccountFull
                  ? "Sending this request will allow ${_deal.publisherAccount.userName} to view your account analytics and recent posts.\n\nWe will notify you when they approve or deny your request."
                  : "Sending this request will allow ${_deal.publisherAccount.userName} to view your recent posts.\n\nWe will notify you when they approve or deny your request."),
          actions: <ModalAlertAction>[
            ModalAlertAction(
                label: "Not Now",
                isDestructiveAction: true,
                onPressed: () => Navigator.of(context).pop()),
            ModalAlertAction(
                isDefaultAction: true,
                label: "Request",
                onPressed: () {
                  /// close the alert
                  Navigator.of(context).pop();

                  /// show loading overlay while we update the request
                  showSharedLoadingLogo(context);

                  /// update the deal status and react to the response accordingly
                  _bloc.sendRequest(_deal).then((BaseResponse res) {
                    /// close the "sending alert"
                    Navigator.of(context).pop();

                    if (res.error == null) {
                      /// if the deal was an auto-approve request, then show the
                      /// overlay showing their already approved, otherwise, send the creator
                      /// to the deal details page which will show their request status
                      if (_deal.autoApproveRequests == true) {
                        _showAutoApproveDialog();
                      } else {
                        /// if the request was successful then we simply reload the page
                        /// include publisher id who made the request if this is a business
                        Navigator.of(context).pushNamedAndRemoveUntil(
                            AppRouting.getRequestRoute(
                                _deal.id, appState.currentProfile.id),
                            (Route<dynamic> route) => false);
                      }
                    } else {
                      final bool hasExisting =
                          res.error.response.statusMessage == "AlreadyExists";

                      showSharedModalError(
                        context,
                        title: hasExisting
                            ? "RYDR Already Requested"
                            : "Unable to Complete",
                        subTitle: hasExisting
                            ? "You've already requested this RYDR, please view your requests list for the status of this request."
                            : "We are currently not able to submit your request. Please try again in a few moments.",
                      );
                    }
                  });
                }),
          ]);

  void _handleInviteEllipsisTap() => showSharedModalBottomActions(context,
          title: 'Invite Options',
          actions: <ModalBottomAction>[
            ModalBottomAction(
              isCurrentAction: true,
              child: Text("Accept Invite"),
              icon: AppIcons.check,
              onTap: () {
                Navigator.of(context).pop();
                _handleInviteAcceptClick();
              },
            ),
            ModalBottomAction(
              isDestructiveAction: true,
              child: Text("Not Interested"),
              icon: AppIcons.times,
              onTap: () {
                Navigator.of(context).pop();
                _handleInviteDeclineClick();
              },
            ),
          ]);

  void _handleEllipsisTap() => showSharedModalBottomActions(
        context,
        title: _deal.title,
        subtitle: "RYDR Options",
        actions: <ModalBottomAction>[
          /// only show request option if this deal can be requested
          /// by the current profile
          _deal.canBeRequested
              ? ModalBottomAction(
                  isCurrentAction: true,
                  child: Text("Request this RYDR"),
                  icon: AppIcons.megaphone,
                  onTap: () {
                    Navigator.of(context).pop();
                    _clickRequest();
                  },
                )
              : null,
          ModalBottomAction(
            child: Text("Share"),
            icon: AppIcons.share,
            onTap: () {
              Navigator.of(context).pop();
              showDealShare(context, _deal);
            },
          ),
        ].where((element) => element != null).toList(),
      );

  /// accepting an invite can be done by accepting a confirmation modal
  void _handleInviteAcceptClick() {
    final String content =
        "Accepting this invite will allow ${_deal.publisherAccount.userName} to view your account analytics and recent posts.";

    showSharedModalAlert(context, Text("Accept RYDR Invite"),
        content: Text(content),
        actions: <ModalAlertAction>[
          ModalAlertAction(
              label: "Not Now",
              isDestructiveAction: true,
              onPressed: () => Navigator.of(context).pop()),
          ModalAlertAction(
              isDefaultAction: true,
              label: "Accept",
              onPressed: () {
                /// close the alert
                Navigator.of(context).pop();

                /// show loading overlay while we update the request
                showSharedLoadingLogo(context);

                _bloc.acceptInvite(_deal).then((bool success) {
                  Navigator.of(context).pop();

                  if (success) {
                    /// if the request was successful then we simply reload the page
                    /// include publisher id who made the request if this is a business
                    Navigator.of(context).pushReplacementNamed(
                        AppRouting.getRequestRoute(
                            _deal.id,
                            appState.currentProfile.isBusiness
                                ? _deal.request.publisherAccount.id
                                : null));
                  } else {
                    showSharedModalError(context);
                  }
                });
              }),
        ]);
  }

  /// declining an invite will send the user to a page where they
  /// can optionally add a message to the business along with the decline decision
  void _handleInviteDeclineClick() => Navigator.of(context).pushNamed(
      AppRouting.getRequestDeclineRoute(
        _deal.id,
        appState.currentProfile.id,
      ),
      arguments: _deal);

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;

    return _isStandAlone ? _buildDealStandAlone() : _buildDealFromMap();
  }

  /// building a deal when viewing it on top of the map, we wrap it in a sheet
  /// that can be dragged up/down and has min / max child sizes set
  Widget _buildDealFromMap() =>
      NotificationListener<DraggableScrollableNotification>(
        onNotification: _onSheetMove,
        child: DraggableScrollableActuator(
          child: DraggableScrollableSheet(
            initialChildSize: _initialChildSize,
            maxChildSize: 1.0,
            minChildSize: _minChildSize,
            builder: (BuildContext context, ScrollController scrollController) {
              _contextDetails = context;

              return StreamBuilder<Deal>(
                  stream: widget.mapBloc.selectedDeal,
                  builder: (context, snapshot) {
                    _deal = snapshot.data;

                    return _buildDeal(scrollController, _deal);
                  });
            },
          ),
        ),
      );

  /// building a stand alone deal
  Widget _buildDealStandAlone() => _buildDeal(null, _deal);

  Widget _buildDeal(ScrollController scrollController, Deal deal) {
    _isVirtual =
        deal?.dealType != null ? deal?.dealType == DealType.Virtual : false;
    _isInvite = deal?.isInvited ?? false;
    _actionColor = _isInvite
        ? Utils.getRequestStatusColor(DealRequestStatus.invited, _darkMode)
        : _theme.primaryColor;

    return Scaffold(
      backgroundColor: Colors.transparent,
      body: Container(
        decoration: BoxDecoration(
          color: _theme.scaffoldBackgroundColor,
          borderRadius: BorderRadius.only(
            topRight: Radius.circular(16),
            topLeft: Radius.circular(16),
          ),
        ),
        child: StreamBuilder<double>(
          stream: _dealBloc.scrollFactor,
          builder: (context, snapshot) {
            final double factor = snapshot.data ?? _initialScrollFactor;

            return CustomScrollView(
              controller: scrollController,
              slivers: _deal == null
                  ? []
                  : [
                      SliverPersistentHeader(
                        delegate: DealHeaderAppBar(
                            deal: _deal, expandedHeight: factor * 280),
                      ),
                      SliverList(
                          delegate: SliverChildListDelegate([
                        _buildNotch(),
                        _buildTitle(),
                        Container(
                            decoration: BoxDecoration(
                                color: _theme.scaffoldBackgroundColor),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: <Widget>[
                                SizedBox(height: 6.0),
                                _buildDescription(),
                                _buildChips(),
                                _deal.dealType == DealType.Virtual
                                    ? Padding(
                                        padding: EdgeInsets.only(
                                            left: 16.0, bottom: 16),
                                        child: RichText(
                                          text: TextSpan(
                                            style: Theme.of(context)
                                                .textTheme
                                                .caption
                                                .merge(TextStyle(
                                                  color: Colors.deepOrange,
                                                )),
                                            children: <TextSpan>[
                                              TextSpan(
                                                text: 'Virtual RYDR: ',
                                                style: TextStyle(
                                                    fontWeight:
                                                        FontWeight.w600),
                                              ),
                                              TextSpan(
                                                  text:
                                                      'You will redeem this RYDR online.'),
                                            ],
                                          ),
                                        ),
                                      )
                                    : Container(),
                                _buildPostingRequirements(),
                                DealReceiveNotes(_deal),
                                DealBrand(_deal),
                                DealPlace(_deal, false),
                                SizedBox(height: kToolbarHeight * 1.5)
                              ],
                            )),
                      ])),
                    ],
            );
          },
        ),
      ),
      floatingActionButtonLocation: FloatingActionButtonLocation.centerDocked,
      floatingActionButton: _buildBottomActionButton(),
      extendBody: true,
      bottomNavigationBar: _buildBottomActionBar(),
    );
  }

  Widget _buildNotch() => Container(
        color: _theme.scaffoldBackgroundColor,
        height: 28.0,
        padding: EdgeInsets.only(top: 12, bottom: 12),
        child: Center(
          child: Container(
            width: 28.0,
            decoration: BoxDecoration(
                color: _darkMode ? Colors.white24 : _theme.canvasColor,
                borderRadius: BorderRadius.circular(8.0)),
          ),
        ),
      );

  Widget _buildTitle() => Container(
      color: _theme.scaffoldBackgroundColor,
      padding: EdgeInsets.symmetric(horizontal: 16.0),
      child: _isStandAlone
          ? Text(
              _deal.titleClean,
              maxLines: 9999,
              overflow: TextOverflow.ellipsis,
              textAlign: TextAlign.left,
              style: _theme.textTheme.bodyText2.merge(
                TextStyle(
                    fontWeight: FontWeight.w500,
                    fontSize: 24.0,
                    color: _theme.textTheme.bodyText2.color),
              ),
            )
          : StreamBuilder<bool>(
              stream: _dealBloc.showBottomBar,
              builder: (context, snapshot) => AnimatedDefaultTextStyle(
                style: snapshot.data == true
                    ? _theme.textTheme.bodyText2.merge(
                        TextStyle(
                            fontWeight: FontWeight.w500,
                            fontSize: 24.0,
                            color: _theme.textTheme.bodyText2.color),
                      )
                    : _theme.textTheme.bodyText2.merge(
                        TextStyle(
                            fontWeight: FontWeight.w500,
                            fontSize: 22.0,
                            color: _theme.textTheme.bodyText2.color),
                      ),
                duration: Duration(milliseconds: 250),
                child: Text(
                  _deal.titleClean,
                  maxLines: snapshot.data == true ? 9999 : 1,
                  overflow: TextOverflow.ellipsis,
                  textAlign: TextAlign.left,
                ),
              ),
            ));

  Widget _buildDescription() => _isStandAlone
      ? DealDescription(_deal, maxLines: 9999)
      : StreamBuilder<bool>(
          stream: _dealBloc.showActions,
          builder: (context, snapshot) => StreamBuilder<bool>(
              stream: _dealBloc.showBottomBar,
              builder: (context, snapshotBar) {
                /// note snapshot vs. snapshotBar using both to either show
                /// one line, two, or the entire description
                return DealDescription(
                  _deal,
                  maxLines: snapshot.data == true
                      ? snapshotBar.data == true ? 2 : 1
                      : 9999,
                );
              }));

  Widget _buildChips() => Container(
      height: 56,
      padding: EdgeInsets.only(bottom: 8),
      width: double.infinity,
      child: StreamBuilder<bool>(
          stream: _dealBloc.showBottomBar,
          builder: (context, snapshot) {
            final bool showingBottomBar = snapshot.data == true;

            return Row(
              children: <Widget>[
                showingBottomBar
                    ? Container()
                    : Row(
                        children: <Widget>[
                          GestureDetector(
                            onTap: _onClose,
                            child: Container(
                              height: 32,
                              width: 32,
                              margin: EdgeInsets.only(
                                  right: 8, top: 8, bottom: 8, left: 16),
                              decoration: BoxDecoration(
                                borderRadius: BorderRadius.circular(20),
                                color: _theme.scaffoldBackgroundColor,
                                border: Border.all(
                                  color: _theme.textTheme.bodyText2.color,
                                ),
                              ),
                              child: Center(
                                child: Icon(
                                  AppIcons.chevronDownReg,
                                  color: _theme.textTheme.bodyText2.color,
                                  size: 16,
                                ),
                              ),
                            ),
                          ),
                        ],
                      ),
                Expanded(
                  child: ListView(
                    scrollDirection: Axis.horizontal,
                    padding: EdgeInsets.only(
                        left: showingBottomBar ? 16 : 0, right: 16),
                    children: <Widget>[
                      /// if we're not currenlty showing the bottom bar which has the request/accept-invite button
                      /// then show a chip that does the same here... NOTE: do check that this deal can be requested (deal.canBeRequested)
                      Visibility(
                        visible: !showingBottomBar && _deal.canBeRequested,
                        child: _buildChip(
                          avatar: Icon(
                              _isInvite
                                  ? AppIcons.solidHeart
                                  : AppIcons.megaphoneSolid,
                              color: Colors.white,
                              size: 16),
                          isPrimary: true,
                          label: _isInvite ? "Accept Invite" : "Request",
                          onTap: _isInvite
                              ? _handleInviteAcceptClick
                              : _clickRequest,
                        ),
                      ),
                      Visibility(
                          visible: !showingBottomBar,
                          child: SizedBox(width: 8)),
                      _buildChip(
                        avatar: CachedNetworkImage(
                          imageUrl: _deal.publisherAccount.profilePicture,
                          imageBuilder: (context, imageProvider) =>
                              CircleAvatar(
                            backgroundColor: _theme.canvasColor,
                            backgroundImage: imageProvider,
                          ),
                          errorWidget: (context, url, error) => ImageError(
                            logUrl: url,
                            logParentName:
                                'map/widgets/deal.dart > CircleAvatar',
                          ),
                        ),
                        isPrimary: false,
                        label: _deal.publisherAccount.userName,
                        onTap: () => Utils.goToProfile(
                          context,
                          _deal.publisherAccount,
                          _deal,
                        ),
                      ),
                      SizedBox(width: 8),
                      _buildChip(
                        avatar: Icon(
                          AppIcons.mapMarkerAltSolid,
                          color: _isVirtual ? Colors.deepOrange : _actionColor,
                          size: 18,
                        ),
                        isPrimary: false,
                        label:
                            '${_deal.distanceInMilesDisplay} · ${_deal.place.name}',
                        onTap: () =>
                            Utils.launchMapsAction(context, _deal.place),
                      ),
                      Visibility(
                          visible: !_isInvite, child: SizedBox(width: 8)),
                      Visibility(
                        visible: !_isInvite,
                        child: _buildChip(
                            avatar: Icon(
                              AppIcons.shareSolid,
                              color:
                                  _isVirtual ? Colors.deepOrange : _actionColor,
                              size: 18,
                            ),
                            isPrimary: false,
                            label: "Share",
                            onTap: () => showDealShare(context, _deal)),
                      ),
                    ],
                  ),
                ),
              ],
            );
          }));

  Widget _buildChip({
    Function onTap,
    Widget avatar,
    bool isPrimary,
    String label,
  }) {
    return GestureDetector(
      onLongPress: isPrimary
          ? _isInvite ? _handleInviteEllipsisTap : _handleEllipsisTap
          : null,
      child: ActionChip(
        pressElevation: 1.0,
        onPressed: onTap,
        avatar: avatar,
        backgroundColor: isPrimary
            ? _isVirtual ? Colors.deepOrange : _actionColor
            : _theme.scaffoldBackgroundColor,
        shape: OutlineInputBorder(
          borderRadius: BorderRadius.circular(40),
          borderSide: BorderSide(
            width: isPrimary ? 0.0 : 1.0,
            color: isPrimary
                ? Colors.transparent
                : _isVirtual ? Colors.deepOrange : _actionColor,
          ),
        ),
        labelStyle: _theme.textTheme.bodyText1.merge(
          TextStyle(
            color: isPrimary
                ? Colors.white
                : _isVirtual ? Colors.deepOrange : _actionColor,
          ),
        ),
        label: Text(label),
      ),
    );
  }

  Widget _buildPostingRequirements() => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          DealReceiveType(_deal),
          Padding(
            padding: EdgeInsets.only(left: 16.0, right: 16.0),
            child: RichText(
              textAlign: TextAlign.left,
              text: TextSpan(
                style: _theme.textTheme.caption.merge(
                  TextStyle(
                    color: _theme.hintColor,
                  ),
                ),
                children: <TextSpan>[
                  TextSpan(
                      style: TextStyle(
                        fontWeight: FontWeight.w600,
                        color: _theme.primaryColor,
                        height: 1.3,
                      ),
                      text: _deal.minAge == 21
                          ? 'You must be 21 or older to request this RYDR.\n'
                          : ''),
                  TextSpan(
                      text: _isInvite
                          ? 'By accepting this invite for '
                          : 'By requesting '),
                  TextSpan(
                      text: '${_deal.title}',
                      style: TextStyle(fontWeight: FontWeight.w600)),
                  TextSpan(
                      text:
                          ', you are agreeing to post the quantity of media mentioned above.')
                ],
              ),
            ),
          ),
          SizedBox(height: 16.0),
          sectionDivider(context),
        ],
      );

  Widget _buildBottomActionBar() => StreamBuilder<bool>(
        stream: _dealBloc.showBottomBar,
        builder: (context, snapshot) {
          /// ensure we have a deal and then show/hide the bottom bar
          /// based on the scroll extend which sets the showBottomBar prop
          ///
          /// NOTE: adjust notch margin and shape, as well as text based on
          /// whether or not this deal can be requested by the current profile or not
          return _deal != null && snapshot.data == true
              ? FadeInBottomTop(
                  5,
                  BottomAppBar(
                    elevation: 4.0,
                    color: _darkMode
                        ? Color(0xFF232323)
                        : _theme.appBarTheme.color,
                    notchMargin: _deal.canBeRequested ? 4 : 0,
                    shape: _deal.canBeRequested
                        ? CircularNotchedRectangle()
                        : null,
                    child: Padding(
                      padding: EdgeInsets.symmetric(vertical: 4, horizontal: 8),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        crossAxisAlignment: _deal.canBeRequested
                            ? CrossAxisAlignment.end
                            : CrossAxisAlignment.center,
                        children: <Widget>[
                          IconButton(
                            icon: Icon(
                              _isStandAlone
                                  ? AppIcons.times
                                  : AppIcons.chevronDown,
                              color: _theme.textTheme.bodyText2.color,
                            ),
                            onPressed: _onClose,
                          ),
                          Padding(
                            padding: EdgeInsets.only(bottom: 4.0),
                            child: Text(
                              !_deal.canBeRequested
                                  ? "Sorry, you are not eligible :("
                                  : _isInvite
                                      ? "Accept Invite"
                                      : _deal.minAge == 21
                                          ? _isVirtual
                                              ? "Request Virtual (21+)"
                                              : "Request (21+)"
                                          : _isVirtual
                                              ? "Request Virtual"
                                              : "Request",
                              style: _deal.canBeRequested
                                  ? _theme.textTheme.caption.merge(
                                      TextStyle(
                                          fontWeight: FontWeight.w500,
                                          color: _isVirtual
                                              ? Colors.deepOrange
                                              : _actionColor),
                                    )
                                  : _theme.textTheme.bodyText1,
                            ),
                          ),
                          IconButton(
                            icon: Icon(
                              AppIcons.ellipsisV,
                              color: _theme.textTheme.bodyText2.color,
                            ),
                            onPressed: _isInvite
                                ? _handleInviteEllipsisTap
                                : _handleEllipsisTap,
                          )
                        ],
                      ),
                    ),
                  ),
                  350)
              : Container(height: 0, width: 0);
        },
      );

  Widget _buildBottomActionButton() => StreamBuilder<bool>(
      stream: _dealBloc.showBottomBar,
      builder: (context, snapshot) {
        /// ensure we have a deal that can be requested
        return _deal != null && snapshot.data == true && _deal.canBeRequested
            ? FadeInScaleUp(
                8,
                GestureDetector(
                  onLongPress:
                      _isInvite ? _handleInviteEllipsisTap : _handleEllipsisTap,
                  child: FloatingActionButton(
                      elevation: 2,
                      backgroundColor: _isInvite
                          ? _darkMode
                              ? Colors.purple.shade300
                              : Colors.purple.shade400
                          : _isVirtual
                              ? Colors.deepOrange
                              : _theme.primaryColor,
                      child: Padding(
                        padding: EdgeInsets.only(right: _isInvite ? 0.0 : 3.0),
                        child: Icon(
                          _isInvite
                              ? AppIcons.solidHeart
                              : AppIcons.megaphoneSolid,
                          color: Colors.white,
                        ),
                      ),
                      onPressed:
                          _isInvite ? _handleInviteAcceptClick : _clickRequest),
                ),
              )
            : Container();
      });
}
