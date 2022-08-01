import 'dart:async';
import 'dart:math';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/blocs/request.dart';
import 'package:rydr_app/ui/deal/widgets/request/title.dart';
import 'package:rydr_app/ui/deal/utils.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class RequestScaffold extends StatefulWidget {
  final Deal deal;
  final List<Widget> body;
  final Function refresh;

  RequestScaffold(this.deal, this.body, this.refresh);

  @override
  _RequestScaffoldState createState() => _RequestScaffoldState();
}

class _RequestScaffoldState extends State<RequestScaffold> {
  final RequestBloc _bloc = RequestBloc();
  final ScrollController _scrollController = ScrollController();
  double _expandedHeight;

  /// internal variables we use throughout widgets
  ThemeData _theme;
  bool _isBusiness;
  bool _isBusinessPro;
  bool _darkMode;
  Deal _deal;
  DealRequestStatus _status;
  Color _statusColor;
  int _publisherAccountIdForRoutes;

  /// handle floating action button tap
  void _processActionButtonTap() {
    if (_isBusiness) {
      if (_status == DealRequestStatus.requested) {
        _businessActionRequestedApprove();
      } else if (_status == DealRequestStatus.inProgress) {
        _businessActionInProgress();
      } else if (_status == DealRequestStatus.redeemed) {
        _businessActionAddTime();
      }
    } else {
      if (_status == DealRequestStatus.invited) {
        _creatorActionInvited();
      } else if (_status == DealRequestStatus.inProgress) {
        _creatorActionInProgress();
      } else if (_status == DealRequestStatus.redeemed) {
        _creatorActionRedeemed();
      }
    }
  }

  void _processActionButtonTapExpired() {
    showSharedModalAlert(context, Text("RYDR Expired"),
        content: Text(_isBusiness
            ? "This invite can no longer be used"
            : "Looks like you're just a little too late. This RYDR has expired and cannot be used."),
        actions: [
          ModalAlertAction(
              isDefaultAction: true,
              label: "Ok",
              onPressed: () {
                Navigator.of(context).pop();
                showSharedLoadingLogo(context);

                /// when either creator or business accepts that the underlying deal has since expired
                /// either from an invite or from a request that was not approved/delined in time
                /// we mark the request as cancelled along with a custom note depending on who cancelled
                _bloc.markCancelled(_deal).then((success) {
                  Navigator.of(context).pop();

                  success
                      ? Navigator.of(context).pushNamedAndRemoveUntil(
                          AppRouting.getRequestRoute(
                              _deal.id, _publisherAccountIdForRoutes),
                          (Route<dynamic> route) => false)
                      : showSharedModalError(context);
                });
              }),
        ]);
  }

  void _processActionButtonTapDelinquentConfirm() =>
      Future.delayed(Duration(milliseconds: 350), () {
        showSharedModalAlert(
          context,
          Text("Are You Sure?"),
          content: Text(
              "This will count as 1 of the Creator's delinquent RYDRs. A Creator must have less than 5 delinquent RYDRs to be active on the RYDR platform. Please confirm the Creator has not posted."),
          actions: <ModalAlertAction>[
            ModalAlertAction(
              isDestructiveAction: true,
              label: "Cancel",
              onPressed: () => Navigator.of(context).pop(),
            ),
            ModalAlertAction(
              isDefaultAction: true,
              label: "Confirm Delinquent",
              onPressed: () {
                Navigator.of(context).pop();
                showSharedLoadingLogo(context);

                /// the business has marked the request as "delinquent"
                /// if successful we'll simply reload the request, otherwise show error modal
                _bloc.markDelinquent(_deal).then(
                  (success) {
                    Navigator.of(context).pop();

                    success
                        ? Navigator.of(context).pushReplacementNamed(
                            AppRouting.getRequestRoute(
                                _deal.id, _publisherAccountIdForRoutes))
                        : showSharedModalError(context);
                  },
                );
              },
            ),
          ],
        );
      });

  void _processActionButtonTapDelinquent() =>
      showSharedModalAlert(context, Text("Creator Did Not Post"),
          content: Text(
              "This will mark the RYDR as delinquent. A Creator needs to have less than five(5) delinquent RYDRs to use the platform."),
          actions: [
            ModalAlertAction(
                isDefaultAction: true,
                isDestructiveAction: true,
                label: "Mark Delinquent",
                onPressed: () {
                  Navigator.of(context).pop();
                  _processActionButtonTapDelinquentConfirm();
                }),
            ModalAlertAction(
                label: "Give 7 More Days",
                onPressed: () {
                  Navigator.of(context).pop();
                  _businessActionAddTime();
                }),
            ModalAlertAction(
                label: "Cancel", onPressed: () => Navigator.of(context).pop()),
          ]);

  void _businessActionRequestedApprove() => Navigator.of(context).pushNamed(
      AppRouting.getRequestApproveRoute(
          _deal.id, _deal.request.publisherAccount.id),
      arguments: _deal);

  void _businessActionRequestedDecline() =>
      showSharedModalAlert(context, Text("Confirm Decline"),
          content: Text(
              'Please confirm you are declining this request from ${_deal.request.publisherAccount.userName}.'),
          actions: <ModalAlertAction>[
            ModalAlertAction(
                label: "Cancel",
                isDestructiveAction: false,
                isDefaultAction: true,
                onPressed: () => Navigator.of(context).pop()),
            ModalAlertAction(
                isDestructiveAction: true,
                label: "Decline",
                onPressed: () {
                  Navigator.of(context).pop();
                  showSharedLoadingLogo(context);

                  _bloc.declineRequest(_deal).then((success) {
                    success
                        ? showSharedModalAlert(
                            context, Text("RYDR Request Declined"),
                            content: Text(
                                "We've notified ${_deal.request.publisherAccount.userName} of your decision to decline their request."),
                            actions: <ModalAlertAction>[
                                ModalAlertAction(
                                    isDefaultAction: true,
                                    label: "OK",
                                    onPressed: () {
                                      Navigator.of(context).pop();

                                      Navigator.of(context)
                                          .pushNamedAndRemoveUntil(
                                              AppRouting.getRequestsPending,
                                              (Route<dynamic> route) => false);
                                    })
                              ])
                        : showSharedModalError(context);
                  });
                }),
          ]);

  void _businessActionInProgress() => showSharedModalAlert(
        context,
        Text("Mark RYDR as Redeemed"),
        content: Text(
            "Choosing to mark this RYDR as redeemed will give the Creator ${_deal.request.daysUntilDelinquent} days to post the required posts. Only mark as redeemed if you're certain the Creator redeemed this RYDR."),
        actions: <ModalAlertAction>[
          ModalAlertAction(
            isDestructiveAction: true,
            label: "Cancel",
            onPressed: () => Navigator.of(context).pop(),
          ),
          ModalAlertAction(
            isDefaultAction: true,
            label: "Confirm",
            onPressed: () {
              Navigator.of(context).pop();
              _businessActionRedeemed();
            },
          ),
        ],
      );

  void _businessActionRedeemed() =>
      Future.delayed(Duration(milliseconds: 350), () {
        showSharedModalAlert(
          context,
          Text("Are You Sure?"),
          content: Text(
              "If this Creator does not complete this RYDR within ${_deal.request.daysUntilDelinquent} days, it will become delinquent. A Creator must have less than 5 delinquent RYDRs to be active on the RYDR platform. Please confirm this RYDR has been redeemed."),
          actions: <ModalAlertAction>[
            ModalAlertAction(
              isDestructiveAction: true,
              label: "Cancel",
              onPressed: () => Navigator.of(context).pop(),
            ),
            ModalAlertAction(
              isDefaultAction: true,
              label: "Confirm",
              onPressed: () {
                Navigator.of(context).pop();
                showSharedLoadingLogo(context);

                /// the business has marked the request as "redeemed"
                /// if successful we'll simply reload the request, otherwise show error modal
                _bloc.redeem(_deal).then(
                  (success) {
                    Navigator.of(context).pop();

                    success
                        ? Navigator.of(context).pushReplacementNamed(
                            AppRouting.getRequestRoute(
                                _deal.id, _publisherAccountIdForRoutes))
                        : showSharedModalError(context);
                  },
                );
              },
            ),
          ],
        );
      });

  void _businessActionAddTime() => showSharedModalAlert(
        context,
        Text("Give the Creator more time"),
        content: Text(
            "This will give ${_deal.request.publisherAccount.userName} an additional ${_deal.request.defaultDaysToExtendCompletionDeadline} days to complete this RYDR.\n\nThe new completion deadline will be ${Utils.formatDateShort(_deal.request.newCompletionDeadline)}"),
        actions: <ModalAlertAction>[
          ModalAlertAction(
            label: "Cancel",
            onPressed: () => Navigator.of(context).pop(),
          ),
          ModalAlertAction(
            isDefaultAction: true,
            label: "Add Time",
            onPressed: () {
              Navigator.of(context).pop();
              showSharedLoadingLogo(context);

              /// if successful we'll simply reload the request, otherwise show error modal
              _bloc.addTimeToComplete(_deal).then(
                (success) {
                  Navigator.of(context).pop();

                  success
                      ? Navigator.of(context).pushReplacementNamed(
                          AppRouting.getRequestRoute(
                              _deal.id, _publisherAccountIdForRoutes))
                      : showSharedModalError(context);
                },
              );
            },
          )
        ],
      );

  void _creatorActionInvited() =>
      showSharedModalAlert(context, Text("Accept RYDR Invite"),
          content: Text(
              "Accepting this invite will allow ${_deal.publisherAccount.userName} to view your account analytics and recent posts."),
          actions: <ModalAlertAction>[
            ModalAlertAction(
                label: "Not Now",
                isDestructiveAction: true,
                onPressed: () => Navigator.of(context).pop()),
            ModalAlertAction(
                isDefaultAction: true,
                label: "Accept",
                onPressed: () {
                  Navigator.of(context).pop();
                  showSharedLoadingLogo(context);

                  /// the creator is accepting the invite
                  /// if successful we'll simply reload the request, otherwise show error modal
                  _bloc.acceptInvite(_deal).then(
                    (success) {
                      Navigator.of(context).pop();

                      success
                          ? Navigator.of(context).pushReplacementNamed(
                              AppRouting.getRequestRoute(
                                  _deal.id, _publisherAccountIdForRoutes))
                          : showSharedModalError(context);
                    },
                  );
                }),
          ]);

  void _creatorActionInProgress() => Navigator.of(context).pushNamed(
      AppRouting.getRequestRedeemRoute(_deal.id, _publisherAccountIdForRoutes),
      arguments: _deal);

  void _creatorActionRedeemed() => Navigator.of(context).pushNamed(
      AppRouting.getRequestCompleteRoute(
        _deal.id,
        _publisherAccountIdForRoutes,
      ),
      arguments: _deal);

  /// handle ellipsis 'options' tap
  void _processOptionsButtonTap() {
    if (_isBusiness) {
      if (_status == DealRequestStatus.requested) {
        _businessOptionsRequested();
      } else if (_status == DealRequestStatus.invited) {
        _businessOptionsInvited();
      } else if (_status == DealRequestStatus.inProgress) {
        _businessOptionsInProgress();
      } else if (_status == DealRequestStatus.redeemed) {
        _businessOptionsRedeemed();
      } else if (_status == DealRequestStatus.completed) {
        _businessOptionsCompleted();
      }
    } else {
      if (_status == DealRequestStatus.requested) {
        _creatorOptionsRequested();
      } else if (_status == DealRequestStatus.invited) {
        _creatorOptionsInvited();
      } else if (_status == DealRequestStatus.inProgress) {
        _creatorOptionsInProgress();
      } else if (_status == DealRequestStatus.redeemed) {
        _creatorOptionsRedeemed();
      }
    }
  }

  void _businessOptionsRequested() => showSharedModalBottomActions(
        context,
        title: 'Available Request Options',
        subtitle: _deal.title,
        actions: [
          ModalBottomAction(
            child: Text("Approve"),
            isCurrentAction: true,
            icon: AppIcons.check,
            onTap: () {
              Navigator.pop(context);
              Navigator.of(context).pushNamed(
                  AppRouting.getRequestApproveRoute(
                      _deal.id, _deal.request.publisherAccount.id),
                  arguments: _deal);
            },
          ),
          ModalBottomAction(
            child: Text("Decline"),
            icon: AppIcons.times,
            onTap: () {
              Navigator.pop(context);
              _businessActionRequestedDecline();
            },
          ),
        ],
      );

  void _businessOptionsInvited() => showSharedModalBottomActions(context,
          title: 'Available Invite Options',
          subtitle: _deal.title,
          actions: <ModalBottomAction>[
            ModalBottomAction(
              isCurrentAction: true,
              isDestructiveAction: true,
              child: Text("Cancel Invite"),
              icon: AppIcons.check,
              onTap: () {
                Navigator.of(context).pop();
                Navigator.of(context).pushNamed(
                    AppRouting.getRequestCancelRoute(
                      _deal.id,
                      _publisherAccountIdForRoutes,
                    ),
                    arguments: _deal);
              },
            ),
          ]);

  void _businessOptionsInProgress() => showSharedModalBottomActions(
        context,
        title: 'Available RYDR Options',
        subtitle: _deal.title,
        actions: [
          ModalBottomAction(
            child: Text("Mark as Redeemed"),
            icon: AppIcons.ticketAlt,
            onTap: () {
              Navigator.pop(context);
              _businessActionInProgress();
            },
          ),
          ModalBottomAction(
            child: Text("Message ${_deal.request.publisherAccount.userName}"),
            icon: AppIcons.commentAltLines,
            onTap: () {
              Navigator.of(context).pop();
              _goToDialog();
            },
          ),
          ModalBottomAction(
            child: Text("Cancel RYDR"),
            isDestructiveAction: true,
            icon: AppIcons.timesCircle,
            onTap: () {
              Navigator.pop(context);

              Navigator.of(context).pushNamed(
                  AppRouting.getRequestCancelRoute(
                      _deal.id, _publisherAccountIdForRoutes),
                  arguments: _deal);
            },
          ),
        ],
      );

  void _businessOptionsRedeemed() => showSharedModalBottomActions(
        context,
        title: 'Available RYDR Options',
        subtitle: _deal.title,
        actions: [
          ModalBottomAction(
            child: Text("Message ${_deal.request.publisherAccount.userName}"),
            icon: AppIcons.commentAltLines,
            onTap: () {
              Navigator.of(context).pop();
              _goToDialog();
            },
          ),
          ModalBottomAction(
            isCurrentAction: true,
            child: Text("Add time for completion"),
            icon: AppIcons.clock,
            onTap: () {
              Navigator.of(context).pop();
              _businessActionAddTime();
            },
          ),

          /// if delinquent, then allow them to mark delinquent
          _deal.request.isDelinquent
              ? ModalBottomAction(
                  child: Text("Mark as Delinquent"),
                  isDestructiveAction: true,
                  icon: AppIcons.timesCircle,
                  onTap: () {
                    Navigator.of(context).pop();
                    _processActionButtonTapDelinquent();
                  },
                )
              : null,
          ModalBottomAction(
            child: Text("Cancel RYDR"),
            isDestructiveAction: true,
            icon: AppIcons.timesCircle,
            onTap: () {
              Navigator.pop(context);

              Navigator.of(context).pushNamed(
                  AppRouting.getRequestCancelRoute(
                      _deal.id, _publisherAccountIdForRoutes),
                  arguments: _deal);
            },
          ),
        ].where((option) => option != null).toList(),
      );

  void _businessOptionsCompleted() => showSharedModalBottomActions(
        context,
        title: 'Completed RYDR Options',
        subtitle: _deal.title,
        actions: [
          ModalBottomAction(
            child: Text("Share Completion Report"),
            icon: AppIcons.chartArea,
            onTap: () {
              Navigator.of(context).pop();
              showSharedLoadingLogo(context);

              _bloc.getExternalReportUrl(_deal).then((shareUrl) {
                Navigator.of(context).pop();

                if (shareUrl != null) {
                  showRequestCompletedShare(
                    context,
                    _deal,
                    shareUrl,
                  );
                } else {
                  showSharedModalError(context);
                }
              });
            },
          ),
          ModalBottomAction(
            child: Text("View Completion Report in Browser"),
            icon: AppIcons.chartArea,
            onTap: () {
              Navigator.of(context).pop();
              showSharedLoadingLogo(context);

              _bloc.getExternalReportUrl(_deal).then((shareUrl) {
                Navigator.of(context).pop();

                if (shareUrl != null) {
                  Utils.launchUrl(context, shareUrl,
                      trackingName: 'Completion report');
                } else {
                  showSharedModalError(context);
                }
              });
            },
          ),
        ],
      );

  void _creatorOptionsRequested() => showSharedModalBottomActions(
        context,
        title: 'Available RYDR Options',
        subtitle: _deal.title,
        actions: [
          ModalBottomAction(
              child: Text("Cancel Request"),
              isDestructiveAction: true,
              icon: AppIcons.timesCircle,
              onTap: () {
                Navigator.pop(context);

                Navigator.of(context).pushNamed(
                    AppRouting.getRequestCancelRoute(
                      _deal.id,
                      _publisherAccountIdForRoutes,
                    ),
                    arguments: _deal);
              }),
        ],
      );

  void _creatorOptionsInvited() => showSharedModalBottomActions(context,
          title: 'Available Invite Options',
          subtitle: _deal.title,
          actions: <ModalBottomAction>[
            ModalBottomAction(
              isCurrentAction: true,
              child: Text("Accept Invite"),
              icon: AppIcons.check,
              onTap: () {
                Navigator.of(context).pop();

                _creatorActionInvited();
              },
            ),
            ModalBottomAction(
              isDestructiveAction: true,
              child: Text("Not Interested"),
              icon: AppIcons.times,
              onTap: () {
                Navigator.of(context).pop();
                Navigator.of(context).pushNamed(
                    AppRouting.getRequestDeclineRoute(
                      _deal.id,
                      _publisherAccountIdForRoutes,
                    ),
                    arguments: _deal);
              },
            ),
          ]);

  void _creatorOptionsInProgress() => showSharedModalBottomActions(
        context,
        title: 'Available RYDR Options',
        subtitle: _deal.title,
        actions: [
          ModalBottomAction(
            child: Text("Show Redeem Ticket"),
            icon: AppIcons.ticketAlt,
            onTap: () {
              Navigator.pop(context);
              Navigator.of(context).pushNamed(
                  AppRouting.getRequestRedeemRoute(
                      _deal.id, _publisherAccountIdForRoutes),
                  arguments: _deal);
            },
          ),
          ModalBottomAction(
            child: Text("Complete and Choose Posts"),
            icon: AppIcons.check,
            onTap: () {
              Navigator.pop(context);
              Navigator.of(context).pushNamed(
                  AppRouting.getRequestCompleteRoute(
                    _deal.id,
                    _publisherAccountIdForRoutes,
                  ),
                  arguments: _deal);
            },
          ),
          ModalBottomAction(
            child: Text("Cancel RYDR"),
            isDestructiveAction: true,
            icon: AppIcons.timesCircle,
            onTap: () {
              Navigator.pop(context);

              Navigator.of(context).pushNamed(
                  AppRouting.getRequestCancelRoute(
                    _deal.id,
                    _publisherAccountIdForRoutes,
                  ),
                  arguments: _deal);
            },
          ),
        ],
      );

  void _creatorOptionsRedeemed() => showSharedModalBottomActions(
        context,
        title: 'Available RYDR Options',
        subtitle: _deal.title,
        actions: [
          ModalBottomAction(
            child: Text("Show Redeem Ticket"),
            icon: AppIcons.ticketAlt,
            onTap: () {
              Navigator.pop(context);
              Navigator.of(context).pushNamed(
                  AppRouting.getRequestRedeemRoute(
                      _deal.id, _publisherAccountIdForRoutes),
                  arguments: _deal);
            },
          ),
          ModalBottomAction(
            child: Text("Complete and Choose Posts"),
            icon: AppIcons.check,
            onTap: () {
              Navigator.pop(context);
              Navigator.of(context).pushNamed(
                  AppRouting.getRequestCompleteRoute(
                    _deal.id,
                    _publisherAccountIdForRoutes,
                  ),
                  arguments: _deal);
            },
          ),
          ModalBottomAction(
            child: Text("Message ${_deal.publisherAccount.userName}"),
            icon: AppIcons.commentAltLines,
            onTap: () {
              Navigator.of(context).pop();

              _goToDialog();
            },
          ),
          ModalBottomAction(
            child: Text("Cancel RYDR"),
            isDestructiveAction: true,
            icon: AppIcons.timesCircle,
            onTap: () {
              Navigator.pop(context);

              Navigator.of(context).pushNamed(
                  AppRouting.getRequestCancelRoute(
                    _deal.id,
                    _publisherAccountIdForRoutes,
                  ),
                  arguments: _deal);
            },
          ),
        ],
      );

  void _goToDialog() => Navigator.of(context).pushNamed(
      AppRouting.getRequestDialogRoute(_deal.id, _publisherAccountIdForRoutes));

  void _onScroll() =>
      _bloc.setScrollOffset(_expandedHeight - _scrollController.offset);

  @override
  void initState() {
    super.initState();

    _expandedHeight = appState.currentProfile.isBusiness ? 120.0 : 200.0;
    _bloc.setScrollOffset(_expandedHeight);

    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController.dispose();
    _bloc.dispose();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;
    _isBusiness = appState.currentProfile.isBusiness;
    _isBusinessPro = appState.isBusinessPro;
    _deal = widget.deal;
    _status = _deal.request.status;
    _statusColor = Utils.getRequestStatusColor(_status, _darkMode);

    _publisherAccountIdForRoutes = _isBusiness
        ? _deal.request.publisherAccount.id
        : appState.currentProfile.id;

    /// only applicable to when a request is in the 'redeemed' state
    final int daysRemainingToComplete = _deal.request.daysRemainingToComplete;
    final Color remainingColor = daysRemainingToComplete >= 5
        ? _theme.primaryColor
        : daysRemainingToComplete >= 3
            ? Utils.getRequestStatusColor(
                DealRequestStatus.inProgress, _darkMode)
            : AppColors.errorRed;

    final lastChangeDate = _deal.request.lastStatusChange != null
        ? _deal.request.lastStatusChange.occurredOnDisplayAgo == "now"
            ? " Just Now"
            : _deal.request.lastStatusChange.occurredOnDisplayAgo + " ago"
        : '';

    final Map<String, String> subTitleText = _isBusiness
        ? {
            "requested":
                "Pending request from ${_deal.request.publisherAccount.userName}",
            "redeemed": "Redeemed $lastChangeDate · ${_deal.place.name}",
            "inProgress": "In-Progress · approved $lastChangeDate",
            "completed":
                "Completed by ${_deal.request.publisherAccount.userName} $lastChangeDate",
            "cancelled": "Request cancelled $lastChangeDate",
            "invited": "Invite sent $lastChangeDate",
            "denied": "Declined $lastChangeDate",
            "delinquent": "Delinquent $lastChangeDate",
          }
        : {
            "requested": "Pending · ${_deal.place.name}",
            "redeemed": "Redeemed · ${_deal.place.name}",
            "inProgress":
                "Active · ${_deal.request.wasInvited ? 'invite accepted' : 'approved'} $lastChangeDate",
            "completed": "Completed · $lastChangeDate",
            "cancelled": "Cancelled · $lastChangeDate",
            "invited":
                "${_deal.publisherAccount.userName} invited you · $lastChangeDate",
            "denied": "Declined · $lastChangeDate",
            "delinquent": "Delinquent · $lastChangeDate",
          };

    final String subtitle = widget.deal.request.wasInvited
        ? "Private Invite · " + subTitleText[dealRequestStatusToString(_status)]
        : subTitleText[dealRequestStatusToString(_status)];

    return Scaffold(
      extendBody: true,
      body: Stack(
        overflow: Overflow.visible,
        children: [
          NestedScrollView(
            controller: _scrollController,
            physics: AlwaysScrollableScrollPhysics(),
            headerSliverBuilder: (context, value) => [
              SliverAppBar(
                automaticallyImplyLeading: false,
                expandedHeight: _expandedHeight,
                primary: false,
                backgroundColor: _theme.scaffoldBackgroundColor,
                flexibleSpace: FlexibleSpaceBar(
                  background: Stack(
                    fit: StackFit.expand,
                    children: [
                      _deal.publisherMedias != null &&
                              _deal.publisherMedias.isNotEmpty
                          ? CachedNetworkImage(
                              imageUrl: _deal.publisherMedias[0].previewUrl,
                              fit: BoxFit.cover,
                              colorBlendMode:
                                  _status == DealRequestStatus.denied ||
                                          _status == DealRequestStatus.cancelled
                                      ? BlendMode.saturation
                                      : null,
                              color: _status == DealRequestStatus.denied ||
                                      _status == DealRequestStatus.cancelled
                                  ? Colors.white
                                  : null,
                              errorWidget: (context, url, error) => ImageError(
                                logUrl: url,
                                logParentName:
                                    'deal/widgets/request/scaffold.dart > deal.publisherMedias',
                              ),
                            )
                          : Container(),
                      DecoratedBox(
                        decoration: BoxDecoration(
                          gradient: LinearGradient(
                            begin: Alignment.bottomCenter,
                            end: Alignment.center,
                            colors: <Color>[
                              Colors.black38,
                              Colors.black.withOpacity(0),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
            body: RefreshIndicator(
              displacement: 0.0,
              backgroundColor: _theme.appBarTheme.color,
              color: _theme.textTheme.bodyText2.color,
              onRefresh: widget.refresh,
              child: ListView(
                children: <Widget>[
                  FadeInTopBottom(
                    0,
                    Padding(
                      padding: EdgeInsets.only(
                          top: 16, bottom: 32, left: 32, right: 32),
                      child: RequestTitle(_deal),
                    ),
                    350,
                    begin: -20.0,
                  ),
                  Column(children: widget.body),
                  SizedBox(height: kToolbarHeight * 1.5),
                ],
                physics: NeverScrollableScrollPhysics(),
              ),
            ),
          ),

          StreamBuilder<double>(
              stream: _bloc.scrollOffset,
              builder: (context, snapshot) => Positioned(
                    top: snapshot.data == null
                        ? _expandedHeight
                        : snapshot.data - 16,
                    width: MediaQuery.of(context).size.width,
                    child: Container(
                      height: 32,
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.only(
                          topRight: Radius.circular(16),
                          topLeft: Radius.circular(16),
                        ),
                        color: _theme.scaffoldBackgroundColor,
                      ),
                      child: Align(
                        alignment: Alignment.bottomCenter,
                        child: StreamBuilder<bool>(
                          stream: _bloc.fadeOutStatus,
                          builder: (context, snapshot) => snapshot.data != null
                              ? AnimatedOpacity(
                                  duration: Duration(milliseconds: 250),
                                  opacity: snapshot.data ? 0.0 : 1.0,
                                  child: Text(
                                    subtitle,
                                    textAlign: TextAlign.center,
                                    overflow: TextOverflow.ellipsis,
                                    style: _theme.textTheme.caption.merge(
                                      TextStyle(color: _theme.hintColor),
                                    ),
                                  ),
                                )
                              : Text(
                                  subtitle,
                                  textAlign: TextAlign.center,
                                  overflow: TextOverflow.ellipsis,
                                  style: _theme.textTheme.caption.merge(
                                    TextStyle(color: _theme.hintColor),
                                  ),
                                ),
                        ),
                      ),
                    ),
                  )),

          /// status-related color circle
          StreamBuilder<double>(
              stream: _bloc.scrollOffset,
              builder: (context, snapshot) => Positioned(
                    top: snapshot.data == null
                        ? _expandedHeight
                        : snapshot.data - 32,
                    left: (MediaQuery.of(context).size.width / 2) - 24,
                    width: 48,
                    child: Center(
                      child: SizedBox(
                        height: 32,
                        width: widget.deal.request.wasInvited ? 52 : 32,
                        child: Container(
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(16),
                            color: _theme.scaffoldBackgroundColor,
                          ),
                          child: Center(
                            child: widget.deal.request.wasInvited
                                ? Stack(
                                    children: <Widget>[
                                      Positioned(
                                        right: 8,
                                        top: 8,
                                        child: Container(
                                          height: 20,
                                          width: 20,
                                          decoration: BoxDecoration(
                                            borderRadius:
                                                BorderRadius.circular(16),
                                            color: _statusColor,
                                          ),
                                        ),
                                      ),
                                      Positioned(
                                        left: 8,
                                        top: 8,
                                        child: Container(
                                          height: 20,
                                          width: 20,
                                          decoration: BoxDecoration(
                                            borderRadius:
                                                BorderRadius.circular(16),
                                            color: Utils.getRequestStatusColor(
                                                DealRequestStatus.invited,
                                                _darkMode),
                                          ),
                                          child: Center(
                                            child: Padding(
                                              padding:
                                                  EdgeInsets.only(bottom: 1),
                                              child: Icon(AppIcons.starsSolid,
                                                  size: 11.0,
                                                  color: Colors.white),
                                            ),
                                          ),
                                        ),
                                      ),
                                    ],
                                  )
                                : Container(
                                    height: 20,
                                    width: 20,
                                    decoration: BoxDecoration(
                                      borderRadius: BorderRadius.circular(16),
                                      color: _statusColor,
                                    ),
                                  ),
                          ),
                        ),
                      ),
                    ),
                  )),
        ],
      ),
      floatingActionButtonLocation: FloatingActionButtonLocation.centerDocked,
      floatingActionButton:
          _buildFloatingActionButton(remainingColor, daysRemainingToComplete),
      bottomNavigationBar:
          _buildBottomNavigationBar(remainingColor, daysRemainingToComplete),
    );
  }

  Widget _buildFloatingActionButton(Color remainingColor, int daysRemaining) {
    /// builds a basic floating action button that's good enough for most use cases
    /// for specific cases we'll build the button custom in the logic below
    Widget _actionButton(Widget icon, [Function onPressed]) => GestureDetector(
          onLongPress: onPressed == null ? _processOptionsButtonTap : null,
          child: FloatingActionButton(
            elevation: 2,
            hoverElevation: 2,
            focusElevation: 2,
            highlightElevation: 2,
            splashColor: _statusColor.withOpacity(0.4),
            backgroundColor: _theme.appBarTheme.color,
            child: icon,
            onPressed: onPressed ?? _processActionButtonTap,
          ),
        );

    /// button for when a request is in the 'redeemed' status, shows for both business and creator
    /// differs in actions processed when clicked and shows different icon / color - separate here cause we use it mutiple times
    final Widget redeemedButton = Padding(
      padding: EdgeInsets.only(top: 2.0),
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          SizedBox(
            height: 56,
            width: 56,
            child: CircularProgressIndicator(
              backgroundColor: _theme.appBarTheme.color,
              value: (1 / _deal.request.daysUntilDelinquent) * daysRemaining,
              strokeWidth: 2.5,
              valueColor: AlwaysStoppedAnimation<Color>(remainingColor),
            ),
          ),
          _isBusiness
              ? Icon(AppIcons.plus, color: remainingColor)
              : Icon(
                  AppIcons.arrowRightReg,
                  color: remainingColor,
                ),
        ],
      ),
    );

    final Widget invited = _deal.expirationInfo.isExpired
        ? _actionButton(
            Icon(AppIcons.solidHeart, color: Theme.of(context).canvasColor),
            _processActionButtonTapExpired)
        : _actionButton(Icon(AppIcons.solidHeart, color: _statusColor));

    final Widget requested = _deal.expirationInfo.isExpired
        ? _actionButton(
            Icon(AppIcons.timesReg,
                color: Theme.of(context).textTheme.bodyText2.color),
            _processActionButtonTapExpired)
        : Row(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              FloatingActionButton(
                heroTag: "accept",
                elevation: 2,
                hoverElevation: 2,
                focusElevation: 2,
                highlightElevation: 2,
                splashColor: _theme.primaryColor.withOpacity(0.4),
                backgroundColor: _theme.appBarTheme.color,
                child: Icon(
                  AppIcons.solidHeart,
                  color: _theme.primaryColor,
                ),
                onPressed: _processActionButtonTap,
              ),
              SizedBox(width: 8),
              FloatingActionButton(
                heroTag: "decline",
                elevation: 2,
                hoverElevation: 2,
                focusElevation: 2,
                highlightElevation: 2,
                splashColor: _theme.primaryColor.withOpacity(0.4),
                backgroundColor: _theme.appBarTheme.color,
                child: Icon(
                  AppIcons.timesReg,
                  color: AppColors.errorRed,
                ),
                onPressed: _businessActionRequestedDecline,
              ),
            ],
          );

    /// button for business to 'mark redeemed' or for the creator to view the redeem ticket
    final Widget inProgress = _actionButton(_isBusiness
        ? Icon(AppIcons.checkReg, color: _statusColor)
        : Padding(
            padding: EdgeInsets.only(right: 2.0, bottom: 2),
            child: Transform.rotate(
                angle: 180 / pi,
                child: Icon(AppIcons.ticketAltSolid, color: _statusColor)),
          ));

    /// when "redeemed" the creator can tap to go to the 'complete' screen to choose posts
    /// the business has the option to add more time, or if the request has become delinquent can mark it as such
    final Widget redeemed = _isBusiness
        ? _deal.request.isDelinquent
            ? Row(
                mainAxisSize: MainAxisSize.min,
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  FloatingActionButton(
                    heroTag: "delinquent",
                    elevation: 2,
                    hoverElevation: 2,
                    focusElevation: 2,
                    highlightElevation: 2,
                    splashColor: AppColors.errorRed.withOpacity(0.4),
                    backgroundColor: _theme.appBarTheme.color,
                    child: Icon(
                      AppIcons.banReg,
                      color: AppColors.errorRed,
                    ),
                    onPressed: _processActionButtonTapDelinquent,
                  ),
                  SizedBox(width: 8),
                  FloatingActionButton(
                    heroTag: "extend",
                    elevation: 2,
                    hoverElevation: 2,
                    focusElevation: 2,
                    highlightElevation: 2,
                    splashColor: _theme.primaryColor.withOpacity(0.4),
                    backgroundColor: _theme.appBarTheme.color,
                    child: redeemedButton,
                    onPressed: _processActionButtonTap,
                  ),
                ],
              )
            : _actionButton(redeemedButton)
        : _actionButton(redeemedButton);

    return _status == DealRequestStatus.inProgress
        ? inProgress
        : _status == DealRequestStatus.redeemed
            ? redeemed
            : (_status == DealRequestStatus.invited && !_isBusiness) ||
                    (_status == DealRequestStatus.invited &&
                        _deal.expirationInfo.isExpired)
                ? invited
                : _status == DealRequestStatus.requested && _isBusiness
                    ? requested
                    : null;
  }

  Widget _buildBottomNavigationBar(
    Color remainingColor,
    int daysRemaining,
  ) {
    /// the text to display in the bottom bar, either below the action button
    /// or by itself if there is no actionable things to be done on the request
    String label = "";

    /// show different messages based on creator vs. business
    if (_isBusiness) {
      label = _status == DealRequestStatus.requested &&
              !_deal.expirationInfo.isExpired
          ? "Approve or Decline"
          : _status == DealRequestStatus.requested &&
                  _deal.expirationInfo.isExpired
              ? "RYDR expired. Tap to Cancel."
              : _status == DealRequestStatus.inProgress
                  ? "Mark as Redeemed"
                  : _status == DealRequestStatus.invited &&
                          !_deal.expirationInfo.isExpired
                      ? "Waiting on ${_deal.request.publisherAccount.userName}"
                      : _status == DealRequestStatus.invited &&
                              _deal.expirationInfo.isExpired
                          ? "RYDR expired. Tap to Cancel."
                          : _status == DealRequestStatus.redeemed &&
                                  _deal.request.isDelinquent
                              ? "Mark Delinquent or Add Time"
                              : _status == DealRequestStatus.redeemed
                                  ? "Add Time (${_deal.request.daysRemainingToComplete} Day${_deal.request.daysRemainingToComplete > 1 ? 's' : ''} Remaining)"
                                  : "";
    } else {
      label = _status == DealRequestStatus.requested
          ? "Waiting for a response..."
          : _status == DealRequestStatus.inProgress
              ? "Redeem${_deal.dealType == DealType.Virtual ? " Virtual" : ""}"
              : _status == DealRequestStatus.invited &&
                      !_deal.expirationInfo.isExpired
                  ? _deal.dealType == DealType.Event
                      ? "Confirm RSVP"
                      : "Accept Invite"
                  : _status == DealRequestStatus.invited &&
                          _deal.expirationInfo.isExpired
                      ? "RYDR expired. Tap to Cancel."
                      : _status == DealRequestStatus.redeemed
                          ? daysRemaining >= _deal.request.daysUntilDelinquent
                              ? "Select Posts"
                              : daysRemaining <= 0
                                  ? "Time to Post Expired"
                                  : "$daysRemaining ${daysRemaining == 1 ? "Day" : "Days"} Remaining to Complete"
                          : "";
    }

    /// completed, cancelled, and denied status messages are the same for business & creator
    label = _status == DealRequestStatus.completed
        ? "This RYDR has been completed."
        : _status == DealRequestStatus.cancelled
            ? "This RYDR has been cancelled."
            : _status == DealRequestStatus.denied
                ? "This request has been declined."
                : _status == DealRequestStatus.delinquent
                    ? "Delinquent Request\nFive(5) delinquent requests allowed"
                    : label;

    /// only show the ellipsis button for more options for request status of either
    /// invited (with active deal), requested, or in progress, all others we offer no options
    /// if its a completed request and we have a business then show options
    final optionsButton = !_deal.expirationInfo.isExpired &&
            _status != DealRequestStatus.cancelled &&
            _status != DealRequestStatus.denied &&
            _status != DealRequestStatus.delinquent &&
            (_status != DealRequestStatus.completed ||
                (_status == DealRequestStatus.completed && _isBusinessPro))
        ? IconButton(
            icon: Icon(
              AppIcons.ellipsisV,
              color: _theme.textTheme.bodyText2.color,
            ),
            onPressed: _processOptionsButtonTap,
          )
        : Container(height: 1, width: kMinInteractiveDimension);

    /// determines if we're going to show a floating actions button or not which affects
    /// whether or not we'll add a notch in the bottom bar or not
    final bool noActionButton = _status == DealRequestStatus.denied ||
        _status == DealRequestStatus.cancelled ||
        _status == DealRequestStatus.delinquent ||
        _status == DealRequestStatus.completed ||
        (!_isBusiness && _status == DealRequestStatus.requested) ||
        (_isBusiness &&
            _status == DealRequestStatus.invited &&
            !_deal.expirationInfo.isExpired);

    return BottomAppBar(
      elevation: 2.0,
      color: _darkMode ? Color(0xFF232323) : _theme.appBarTheme.color,
      notchMargin: 4,
      shape: AutomaticNotchedShape(
          RoundedRectangleBorder(), StadiumBorder(side: BorderSide())),
      child: Padding(
        padding: EdgeInsets.symmetric(vertical: 4, horizontal: 8),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          crossAxisAlignment: noActionButton
              ? CrossAxisAlignment.center
              : CrossAxisAlignment.end,
          children: <Widget>[
            AppBarBackButton(
              context,
              color: _theme.textTheme.bodyText2.color,
            ),
            Padding(
              padding: EdgeInsets.only(bottom: noActionButton ? 0.0 : 4.0),
              child: Text(
                label,
                textAlign: TextAlign.center,
                style: noActionButton
                    ? _theme.textTheme.bodyText1
                        .merge(TextStyle(color: _statusColor))
                    : _theme.textTheme.caption.merge(
                        TextStyle(
                          fontWeight: FontWeight.w500,
                          color: (_status == DealRequestStatus.requested &&
                                  _isBusiness)
                              ? _theme.textTheme.bodyText1.color
                              : _status == DealRequestStatus.redeemed
                                  ? remainingColor
                                  : _statusColor,
                        ),
                      ),
              ),
            ),
            optionsButton,
          ],
        ),
      ),
    );
  }
}
