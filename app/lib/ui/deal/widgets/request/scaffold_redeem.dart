import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter/physics.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/ui/deal/blocs/request.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class RequestRedeemPage extends StatefulWidget {
  final Deal deal;

  RequestRedeemPage(this.deal);

  @override
  _RequestRedeemPageState createState() => _RequestRedeemPageState();
}

enum CardFlipStatus { normal, flipped, animating }

class _RequestRedeemPageState extends State<RequestRedeemPage>
    with TickerProviderStateMixin {
  final dashWidth = 4.0;
  final dashHeight = 1.5;

  RequestBloc _bloc;
  Deal _deal;
  ThemeData _theme;
  AnimationController _controller;
  CardFlipStatus _cardFlipStatus;

  Animation<double> angle;
  Animation<double> scaleDown;
  Animation<double> scaleUp;

  @override
  void initState() {
    super.initState();

    /// if already redeemed then don't animate / rotate the ticket
    /// instead leave it in the upright position and removme the controls to flip
    final bool _isRedeemed =
        widget.deal.request.status == DealRequestStatus.redeemed;

    final bool _virtual = widget.deal.dealType == DealType.Virtual;

    _bloc = RequestBloc();

    /// if the request has already been redeemed then set the flip status to normal
    /// along with the controller angle and initial state
    _cardFlipStatus = _isRedeemed || _virtual
        ? CardFlipStatus.normal
        : CardFlipStatus.flipped;

    _controller = AnimationController(
      duration: Duration(milliseconds: 450),
      vsync: this,
    )
      ..addListener(() {
        setState(() {});
      })
      ..addStatusListener(
        (AnimationStatus status) {
          if (status == AnimationStatus.completed) {
            /// When the animation is at the end, the card is upside down
            _cardFlipStatus = CardFlipStatus.normal;
          } else if (status == AnimationStatus.dismissed) {
            /// When the animation is at the beginning, the card is rightside up
            _cardFlipStatus = CardFlipStatus.flipped;
          } else {
            /// Otherwise the animation is running
            _cardFlipStatus = CardFlipStatus.animating;
          }
        },
      );

    angle = Tween(
            begin: _isRedeemed || _virtual ? 0.0 : pi,
            end: _isRedeemed || _virtual ? pi : 0.0)
        .animate(CurvedAnimation(
      parent: _controller,
      curve: Curves.easeOutCirc,
      reverseCurve: Curves.easeInCirc,
    ));

    scaleDown = Tween(begin: 1.1, end: 0.9).animate(
      CurvedAnimation(
        parent: _controller,
        curve: Interval(
          0.0,
          0.5,
          curve: Curves.ease,
        ),
      ),
    );

    scaleUp = Tween(begin: 0.9, end: 1.1).animate(
      CurvedAnimation(
        parent: _controller,
        curve: Interval(
          0.5,
          1.0,
          curve: Curves.ease,
        ),
      ),
    );
  }

  @override
  void dispose() {
    _bloc.dispose();
    _controller.dispose();
    super.dispose();
  }

  /// once they continue we clear the whole stack to prevent stack issues going back
  /// and to have the list reload when they go back to it
  void _continueAfterRedeemed() =>
      Navigator.of(context).pushNamedAndRemoveUntil(
        AppRouting.getRequestRoute(
          _deal.id,
          _deal.request.publisherAccount.id,
        ),
        (Route<dynamic> route) => false,
      );

  void _backToRequest() => Navigator.of(context).pop();

  void _handleRedeemed() {
    _bloc.redeem(widget.deal).then((_) {
      if (_cardFlipStatus == CardFlipStatus.flipped) {
        Future.delayed(
            Duration(milliseconds: 2000), () => _controller.forward().orCancel);
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);
    _deal = widget.deal;

    return Scaffold(
      backgroundColor: Colors.black,
      body: SafeArea(
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: 16),
          child: _buildPage(),
        ),
      ),
    );
  }

  Widget _buildPage() => Column(
        children: <Widget>[
          Expanded(
            child: FadeInTopBottom(
              5,
              StreamBuilder<BaseResponse>(
                  stream: _bloc.dealRequestResponse,
                  builder: (context, snapshot) {
                    final BaseResponse redeemedResponse = snapshot.data;

                    return Column(
                      children: <Widget>[
                        _buildContent(),
                        redeemedResponse != null &&
                                redeemedResponse.error == null
                            ? Container()
                            : _buildDashedLine(),

                        /// if this request has already been redeemed then show that status
                        /// along with date/time, etc. instead
                        _deal.request.status == DealRequestStatus.redeemed
                            // ? _buildAlreadyRedeemed()
                            ? Container()
                            : SwipeButton(
                                ageRestricted: _deal.minAge == 21,
                                virtual: _deal.dealType == DealType.Virtual,
                                onSwipeComplete: _handleRedeemed,
                                redeemedResponse: redeemedResponse,
                              ),
                      ],
                    );
                  }),
              // AnimatedBuilder(
              //   builder: (BuildContext context, Widget child) =>
              //       Transform.rotate(
              //     angle: angle.value,
              //     child: Transform.scale(
              //       scale: scaleUp.value,
              //       child: Transform.scale(
              //         scale: scaleDown.value,
              //         child: Column(
              //           children: <Widget>[
              //             _buildContent(),
              //             _buildDashedLine(),

              //             /// if this request has already been redeemed then show that status
              //             /// along with date/time, etc. instead
              //             _deal.request.status == DealRequestStatus.redeemed
              //                 ? _buildAlreadyRedeemed()
              //                 : StreamBuilder<BaseResponse>(
              //                     stream: _bloc.dealRequestResponse,
              //                     builder: (context, snapshot) {
              //                       /// when not redeemed the response will be null
              //                       /// then either be redeemed or had an error depending on .error == null
              //                       final BaseResponse redeemedResponse =
              //                           snapshot.data;

              //                       return StreamBuilder<bool>(
              //                         stream: _bloc.ticketRightSideUp,
              //                         builder: (context, snapshot) =>
              //                             SwipeButton(
              //                           rightSideUp: snapshot.data == true,
              //                           ageRestricted: _deal.minAge == 21,
              //                           virtual:
              //                               _deal.dealType == DealType.Virtual,
              //                           onSwipeComplete: _handleRedeemed,
              //                           redeemedResponse: redeemedResponse,
              //                         ),
              //                       );
              //                     },
              //                   ),
              //           ],
              //         ),
              //       ),
              //     ),
              //   ),
              //   animation: _controller,
              // ),
              500,
              begin: -(MediaQuery.of(context).size.height / 2),
            ),
          ),
          _buildControls(),
        ],
      );

  Widget _buildDashedLine() => Padding(
        padding: EdgeInsets.symmetric(horizontal: 12),
        child: Stack(
          overflow: Overflow.visible,
          children: <Widget>[
            Container(
              width: MediaQuery.of(context).size.width,
              height: dashHeight,
              color: Colors.white,
            ),
            LayoutBuilder(
              builder: (BuildContext context, BoxConstraints constraints) {
                final boxWidth = constraints.constrainWidth();
                final dashCount = (boxWidth / (2 * dashWidth)).floor();
                return Flex(
                  children: List.generate(dashCount, (_) {
                    return SizedBox(
                      width: dashWidth,
                      height: dashHeight,
                      child: DecoratedBox(
                        decoration: BoxDecoration(
                            color: Colors.black,
                            borderRadius: BorderRadius.circular(dashHeight)),
                      ),
                    );
                  }),
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  direction: Axis.horizontal,
                );
              },
            ),
          ],
        ),
      );

  Widget _buildHeaderLogo() => Padding(
        padding: EdgeInsets.only(bottom: 8.0),
        child: Stack(
          children: <Widget>[
            Transform.translate(
              offset: Offset(-27, 0),
              child: SizedBox(
                height: _deal.request.status == DealRequestStatus.redeemed
                    ? 72
                    : MediaQuery.of(context).size.width / 4,
                width: _deal.request.status == DealRequestStatus.redeemed
                    ? 72
                    : MediaQuery.of(context).size.width / 4,
                child: SvgPicture.asset(
                  'assets/icons/rydr-circle.svg',
                  color: AppColors.grey800,
                ),
              ),
            ),
            Transform.translate(
              offset: Offset(27, 0),
              child: SizedBox(
                height: _deal.request.status == DealRequestStatus.redeemed
                    ? 72
                    : MediaQuery.of(context).size.width / 4,
                width: _deal.request.status == DealRequestStatus.redeemed
                    ? 72
                    : MediaQuery.of(context).size.width / 4,
                child: UserAvatar(
                  _deal.publisherAccount,
                  width: _deal.request.status == DealRequestStatus.redeemed
                      ? 72
                      : MediaQuery.of(context).size.width / 4,
                ),
              ),
            ),
          ],
        ),
      );

  Widget _buildControls() => Container(
        height: 60,
        padding: EdgeInsets.symmetric(vertical: 8),
        child: _deal.request.status == DealRequestStatus.redeemed
            ? FadeInBottomTop(
                15,
                Row(
                  children: <Widget>[
                    Expanded(
                      child: PrimaryButton(
                        label: "Cancel",
                        onTap: _backToRequest,
                        buttonColor: Colors.black,
                        labelColor: Theme.of(context).hintColor,
                      ),
                    ),
                    SizedBox(width: 8),
                    Expanded(
                      child: PrimaryButton(
                        round: true,
                        label: "Select Posts",
                        onTap: () {
                          Navigator.of(context).pushNamed(
                              AppRouting.getRequestCompleteRoute(
                                _deal.id,
                                appState.currentProfile.id,
                              ),
                              arguments: _deal);
                        },
                      ),
                    ),
                  ],
                ),
                350)
            : StreamBuilder<BaseResponse>(
                stream: _bloc.dealRequestResponse,
                builder: (context, snapshot) {
                  /// when not redeemed the response will be null
                  /// then either be redeemed or had an error depending on .error == null
                  final BaseResponse redeemedResponse = snapshot.data;

                  if (redeemedResponse == null) {
                    return PrimaryButton(
                      labelColor: Theme.of(context).hintColor,
                      label: "Cancel",
                      buttonColor: Colors.black,
                      onTap: () => Navigator.pop(context),
                    );
                  } else {
                    if (redeemedResponse.error == null) {
                      return FadeInBottomTop(
                          30,
                          PrimaryButton(
                            label: "Close",
                            onTap: _continueAfterRedeemed,
                            labelColor: Theme.of(context).hintColor,
                            buttonColor: Colors.black,
                          ),
                          350);
                    } else {
                      return Stack(
                        children: <Widget>[
                          Align(
                            alignment: Alignment.bottomLeft,
                            child: AppBarCloseButton(
                              context,
                              iconColor: AppColors.grey300,
                            ),
                          ),
                        ],
                      );
                    }
                  }
                },
              ),
      );

  Widget _buildRedeemInfo() {
    final double scaleFactor = MediaQuery.of(context).textScaleFactor;
    var phonePattern =
        r"(\+?( |-|\.)?\d{1,2}( |-|\.)?)?(\(?\d{3}\)?|\d{3})( |-|\.)?(\d{3}( |-|\.)?\d{4})";
    var urlPattern = r'(?:(?:https?|ftp):\/\/)?[\w/\-?=%.]+\.[\w/\-?=%.]+';
    final String notes = _deal?.approvalNotes?.trim() ?? "";
    final RegExp urlExp =
        RegExp(urlPattern, caseSensitive: false, multiLine: true);
    final RegExp phoneExp =
        RegExp(phonePattern, caseSensitive: false, multiLine: true);
    final List<String> url =
        urlExp.allMatches(notes).map((url) => url.group(0)).toSet().toList();
    final List<String> phoneNumbers = phoneExp
        .allMatches(notes)
        .map((phone) => phone.group(0))
        .toSet()
        .toList();
    bool hasUrl = url.isNotEmpty;
    bool hasPhone = phoneNumbers.isNotEmpty;
    String usableURL = hasUrl
        ? url.first.startsWith("http", 0) ? url.first : "http://${url.first}"
        : "";

    return notes != ""
        ? Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Padding(
                padding: EdgeInsets.only(bottom: 16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: <Widget>[
                    Padding(
                      padding: EdgeInsets.only(top: 16),
                      child: Text("Steps to Redeem",
                          style: Theme.of(context).textTheme.bodyText1),
                    ),
                    SizedBox(height: 4),
                    Text(
                      notes,
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.bodyText2,
                      strutStyle: StrutStyle(
                        height: scaleFactor == 1
                            ? Theme.of(context).textTheme.bodyText2.height
                            : 1.5 * scaleFactor,
                        forceStrutHeight: true,
                      ),
                    ),
                    hasUrl || hasPhone
                        ? Padding(
                            padding:
                                EdgeInsets.only(left: 16, right: 16, top: 16),
                            child: Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: <Widget>[
                                hasUrl
                                    ? InkWell(
                                        onTap: () =>
                                            Utils.launchUrl(context, usableURL),
                                        child: Container(
                                          color: Colors.transparent,
                                          width: 88,
                                          child: Column(
                                            mainAxisSize: MainAxisSize.min,
                                            crossAxisAlignment:
                                                CrossAxisAlignment.center,
                                            children: <Widget>[
                                              Container(
                                                width: 72,
                                                height: 72,
                                                decoration: BoxDecoration(
                                                  border: Border.all(
                                                      color: Theme.of(context)
                                                          .primaryColor,
                                                      width: 1.5),
                                                  borderRadius:
                                                      BorderRadius.circular(36),
                                                ),
                                                child: Icon(
                                                  AppIcons.mousePointer,
                                                  size: 24,
                                                  color: Theme.of(context)
                                                      .primaryColor,
                                                ),
                                              ),
                                              SizedBox(height: 8),
                                              Text(
                                                url.first,
                                                overflow: TextOverflow.ellipsis,
                                                style: Theme.of(context)
                                                    .textTheme
                                                    .caption
                                                    .merge(TextStyle(
                                                        color: Theme.of(context)
                                                            .primaryColor)),
                                              ),
                                            ],
                                          ),
                                        ),
                                      )
                                    : Container(),
                                hasUrl && hasPhone
                                    ? SizedBox(width: 16)
                                    : Container(),
                                hasPhone
                                    ? InkWell(
                                        onTap: () => Utils.launchUrl(context,
                                            "tel://${phoneNumbers.first}"),
                                        child: Container(
                                          color: Colors.transparent,
                                          width: 88,
                                          child: Column(
                                            mainAxisSize: MainAxisSize.min,
                                            crossAxisAlignment:
                                                CrossAxisAlignment.center,
                                            children: <Widget>[
                                              Container(
                                                width: 72,
                                                height: 72,
                                                decoration: BoxDecoration(
                                                  border: Border.all(
                                                      color: Theme.of(context)
                                                          .primaryColor,
                                                      width: 1.5),
                                                  borderRadius:
                                                      BorderRadius.circular(36),
                                                ),
                                                child: Icon(
                                                  AppIcons.phone,
                                                  size: 24,
                                                  color: Theme.of(context)
                                                      .primaryColor,
                                                ),
                                              ),
                                              SizedBox(height: 8),
                                              Text(
                                                phoneNumbers.first,
                                                overflow: TextOverflow.ellipsis,
                                                style: Theme.of(context)
                                                    .textTheme
                                                    .caption
                                                    .merge(TextStyle(
                                                        color: Theme.of(context)
                                                            .primaryColor)),
                                              ),
                                            ],
                                          ),
                                        ),
                                      )
                                    : Container(),
                              ],
                            ),
                          )
                        : Container()
                  ],
                ),
              ),
            ],
          )
        : Container();
  }

  Widget _buildContent() => Expanded(
        child: Column(
          children: <Widget>[
            Expanded(
              child: Container(
                decoration: BoxDecoration(
                    border: Border.all(color: Colors.white, width: 1),
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(16)),
                padding: EdgeInsets.symmetric(horizontal: 24.0),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: <Widget>[
                    SizedBox(
                        height:
                            _deal.request.status == DealRequestStatus.redeemed
                                ? 32
                                : 64),
                    _buildHeaderLogo(),
                    Visibility(
                      visible:
                          _deal.request.status == DealRequestStatus.redeemed,
                      child: Column(
                        children: <Widget>[
                          Text(
                            _deal.title,
                            textAlign: TextAlign.center,
                            style: _theme.textTheme.bodyText1.merge(
                              TextStyle(
                                color: AppColors.grey800,
                                fontSize: 28,
                              ),
                            ),
                          ),
                          SizedBox(height: 8),
                          Text(
                            _deal.place.name,
                            textAlign: TextAlign.center,
                            style: _theme.textTheme.bodyText2.merge(
                              TextStyle(
                                color: AppColors.grey400,
                              ),
                            ),
                          ),
                          SizedBox(height: 16),
                          Divider(height: 1),
                        ],
                      ),
                    ),
                    Expanded(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        crossAxisAlignment: CrossAxisAlignment.center,
                        children: <Widget>[
                          Expanded(
                            child: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              crossAxisAlignment: CrossAxisAlignment.center,
                              children: <Widget>[
                                _deal.request.status !=
                                        DealRequestStatus.redeemed
                                    ? Column(
                                        children: <Widget>[
                                          Text(
                                            _deal.title,
                                            textAlign: TextAlign.center,
                                            style: _theme.textTheme.bodyText1
                                                .merge(
                                              TextStyle(
                                                color: AppColors.grey800,
                                                fontSize: 28,
                                              ),
                                            ),
                                          ),
                                          SizedBox(height: 8),
                                          Text(
                                            _deal.place.name,
                                            textAlign: TextAlign.center,
                                            style: _theme.textTheme.bodyText2
                                                .merge(
                                              TextStyle(
                                                color: AppColors.grey400,
                                              ),
                                            ),
                                          ),
                                        ],
                                      )
                                    : _buildRedeemInfo(),
                                widget.deal.dealType == DealType.Virtual
                                    ? Column(
                                        children: <Widget>[
                                          SizedBox(height: 16),
                                          Divider(
                                            height: 1,
                                            color: AppColors.grey300,
                                          ),
                                          SizedBox(height: 16),
                                          SelectableText(
                                            _deal.approvalNotes,
                                            cursorColor:
                                                Theme.of(context).primaryColor,
                                            toolbarOptions: ToolbarOptions(
                                              copy: true,
                                              selectAll: true,
                                            ),
                                            textAlign: TextAlign.center,
                                            style: _theme.textTheme.bodyText2
                                                .merge(
                                              TextStyle(
                                                color: AppColors.grey800,
                                              ),
                                            ),
                                          ),
                                        ],
                                      )
                                    : Container()
                              ],
                            ),
                          ),
                          SizedBox(height: 32),
                          Stack(
                            alignment: Alignment.center,
                            children: <Widget>[
                              Container(
                                height: 54,
                                width: 54,
                                decoration: BoxDecoration(
                                    borderRadius: BorderRadius.circular(40),
                                    border: Border.all(
                                        color: widget.deal.dealType ==
                                                DealType.Virtual
                                            ? Colors.deepOrange
                                            : AppColors.blue,
                                        width: 2),
                                    color: Colors.white),
                              ),
                              UserAvatar(
                                widget.deal.dealType == DealType.Virtual
                                    ? _deal.publisherAccount
                                    : _deal.request.publisherAccount,
                                width: 48,
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                    SizedBox(height: 32),
                  ],
                ),
              ),
            ),
          ],
        ),
      );
}

enum SwipePosition {
  SwipeLeft,
  SwipeRight,
}

class SwipeButton extends StatefulWidget {
  const SwipeButton({
    Key key,
    this.thumb,
    this.content,
    BorderRadius borderRadius,
    this.initialPosition = SwipePosition.SwipeRight,
    this.ageRestricted,
    this.virtual,
    this.onSwipeComplete,
    this.redeemedResponse,
  })  : assert(initialPosition != null),
        this.borderRadius = borderRadius ?? BorderRadius.zero,
        super(key: key);

  final Widget thumb;
  final Widget content;
  final bool virtual;
  final bool ageRestricted;
  final BorderRadius borderRadius;
  final SwipePosition initialPosition;
  final Function onSwipeComplete;
  final BaseResponse redeemedResponse;

  @override
  SwipeButtonState createState() => SwipeButtonState();
}

class SwipeButtonState extends State<SwipeButton>
    with SingleTickerProviderStateMixin {
  final GlobalKey _containerKey = GlobalKey();
  final GlobalKey _positionedKey = GlobalKey();

  AnimationController _controller;
  Offset _start = Offset.zero;
  bool complete;

  RenderBox get _positioned => _positionedKey.currentContext.findRenderObject();
  RenderBox get _container => _containerKey.currentContext.findRenderObject();

  @override
  void initState() {
    super.initState();
    complete = false;
    _controller = AnimationController.unbounded(vsync: this);
    if (widget.initialPosition == SwipePosition.SwipeRight) {
      _controller.value = 1.0;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 96,
      child: Stack(
        key: _containerKey,
        children: <Widget>[
          AnimatedContainer(
            duration: Duration(milliseconds: 350),
            width: double.infinity,
            height: 99,
            decoration: BoxDecoration(
              color: complete ? Colors.black : Colors.white,
              borderRadius: BorderRadius.circular(16),
            ),
            child: Center(
              child: AnimatedSwitcher(
                duration: Duration(milliseconds: 350),
                child: complete
                    ? Text(
                        widget.redeemedResponse == null
                            ? "UNLOCKING..."
                            : widget.redeemedResponse.error != null
                                ? "UNABLE TO VIEW"
                                : "UNLOCKED!",
                        style: Theme.of(context).textTheme.bodyText1.merge(
                              TextStyle(
                                  color: AppColors.white,
                                  fontSize: 20,
                                  fontWeight: FontWeight.w700,
                                  letterSpacing: 1.5),
                            ),
                      )
                    : Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        crossAxisAlignment: CrossAxisAlignment.center,
                        children: <Widget>[
                          Text(
                            "SLIDE TO UNLOCK",
                            style: Theme.of(context).textTheme.bodyText1.merge(
                                  TextStyle(
                                      color: AppColors.grey800,
                                      fontSize: 20,
                                      fontWeight: FontWeight.w700,
                                      letterSpacing: 1.5),
                                ),
                          ),
                          SizedBox(height: 4),
                          Text(
                            widget.ageRestricted
                                ? "Must be 21 or older to redeem.\nAlways check for a valid driver's license."
                                : "7 days to post once redeemed",
                            textAlign: TextAlign.center,
                            style: Theme.of(context).textTheme.caption.merge(
                                  TextStyle(
                                    color: widget.ageRestricted
                                        ? Theme.of(context).errorColor
                                        : AppColors.grey300,
                                  ),
                                ),
                          ),
                        ],
                      ),
              ),
            ),
          ),
          SizedBox(
            width: double.infinity,
            height: 99.0,
            child: AnimatedBuilder(
              animation: _controller,
              builder: (BuildContext context, Widget child) => Align(
                alignment: Alignment((_controller.value * 2.0) - 1.0, 0.0),
                child: child,
              ),
              child: Visibility(
                visible: !complete,
                child: GestureDetector(
                  onHorizontalDragStart: _onDragStart,
                  onHorizontalDragUpdate: _onDragUpdate,
                  onHorizontalDragEnd: _onDragEnd,
                  child: Container(
                    key: _positionedKey,
                    width: 72.0,
                    height: 99.0,
                    child: Container(
                      width: 72,
                      margin: EdgeInsets.all(8),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(100),
                      ),
                      child: Padding(
                        padding: EdgeInsets.only(right: 2.0),
                        child: Icon(AppIcons.chevronLeftReg,
                            color: AppColors.grey800),
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _onDragStart(DragStartDetails details) {
    final pos = _positioned.globalToLocal(details.globalPosition);
    _start = Offset(pos.dx, 0.0);
    _controller.stop(canceled: true);
  }

  void _onDragUpdate(DragUpdateDetails details) {
    final pos = _container.globalToLocal(details.globalPosition) - _start;
    final extent = _container.size.width - _positioned.size.width;
    _controller.value = (pos.dx.clamp(0.0, extent) / extent);
  }

  void _onDragEnd(DragEndDetails details) {
    final extent = _container.size.width - _positioned.size.width;
    var fractionalVelocity = (details.primaryVelocity / extent).abs();
    if (fractionalVelocity < 0.25) {
      fractionalVelocity = 0.8;
    }
    SwipePosition result;
    double acceleration, velocity;
    if (_controller.value > 0.25) {
      acceleration = 0.8;
      velocity = fractionalVelocity;
      result = SwipePosition.SwipeRight;
    } else {
      acceleration = -0.8;
      velocity = -fractionalVelocity;
      result = SwipePosition.SwipeLeft;
    }
    final simulation = _SwipeSimulation(
      acceleration,
      _controller.value,
      1.0,
      velocity,
    );
    _controller.animateWith(simulation).then((_) {
      if (result == SwipePosition.SwipeLeft) {
        widget.onSwipeComplete();

        setState(() {
          complete = true;
        });
      } else {
        setState(() {
          complete = false;
        });
      }
    });
  }
}

class _SwipeSimulation extends GravitySimulation {
  _SwipeSimulation(
      double acceleration, double distance, double endDistance, double velocity)
      : super(acceleration, distance, endDistance, velocity);

  @override
  double x(double time) => super.x(time).clamp(0.0, 1.0);

  @override
  bool isDone(double time) {
    final _x = x(time).abs();
    return _x <= 0.0 || _x >= 1.0;
  }
}
