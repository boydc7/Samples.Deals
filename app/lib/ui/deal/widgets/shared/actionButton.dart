import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/blocs/request.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

class DealActionButton extends StatelessWidget {
  final Deal deal;

  DealActionButton(this.deal);

  @override
  Widget build(BuildContext context) {
    final RequestBloc _bloc = RequestBloc();
    final double scaleFactor = MediaQuery.of(context).textScaleFactor;
    var phonePattern =
        r"(\+?( |-|\.)?\d{1,2}( |-|\.)?)?(\(?\d{3}\)?|\d{3})( |-|\.)?(\d{3}( |-|\.)?\d{4})";
    var urlPattern = r'(?:(?:https?|ftp):\/\/)?[\w/\-?=%.]+\.[\w/\-?=%.]+';
    final String notes = deal?.approvalNotes?.trim() ?? "";
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
    DealRequestStatus _status = deal.request.status;
    int daysRemaining = deal.request.daysRemainingToComplete;
    bool inProgress = _status == DealRequestStatus.inProgress;
    bool redeemed = _status == DealRequestStatus.redeemed;
    bool invited = _status == DealRequestStatus.invited;
    bool event = deal.dealType == DealType.Event;
    String buttonLabel = redeemed
        ? "Select Your Posts"
        : inProgress
            ? "Ready to Redeem"
            : invited ? event ? "Confirm RSVP" : "Accept Invite" : "";
    String caption = inProgress
        ? "Tap to unlock steps to redeem"
        : daysRemaining <= 0
            ? "Time to Post Expired"
            : "$daysRemaining ${daysRemaining == 1 ? "day" : "days"} remaining to post and submit";
    Function _buttonOnTap = redeemed
        ? () {
            Navigator.of(context).pushNamed(
                AppRouting.getRequestCompleteRoute(
                  deal.id,
                  appState.currentProfile.id,
                ),
                arguments: deal);
          }
        : inProgress
            ? () {
                Navigator.of(context).pushNamed(
                    AppRouting.getRequestRedeemRoute(
                        deal.id, appState.currentProfile.id),
                    arguments: deal);
              }
            : invited
                ? () {
                    showSharedLoadingLogo(context);

                    /// the creator is accepting the invite
                    /// if successful we'll simply reload the request, otherwise show error modal
                    _bloc.acceptInvite(deal).then(
                      (success) {
                        Navigator.of(context).pop();

                        success
                            ? Navigator.of(context).pushReplacementNamed(
                                AppRouting.getRequestRoute(
                                    deal.id, appState.currentProfile.id))
                            : showSharedModalError(context);
                      },
                    );
                  }
                : () {};

    if (appState.currentProfile.isCreator &&
        _status != DealRequestStatus.requested) {
      return Padding(
        padding: EdgeInsets.only(
          bottom: 16,
        ),
        child: Column(
          children: <Widget>[
            redeemed && deal.approvalNotes != null
                ? Column(
                    children: <Widget>[
                      sectionDivider(context),
                      Padding(
                        padding: EdgeInsets.only(bottom: 16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            Padding(
                              padding: EdgeInsets.symmetric(horizontal: 16),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: <Widget>[
                                  Padding(
                                    padding: EdgeInsets.only(top: 16),
                                    child: Text("Steps to Redeem",
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodyText1),
                                  ),
                                  SizedBox(height: 4),
                                  Text(
                                    notes,
                                    style:
                                        Theme.of(context).textTheme.bodyText2,
                                    strutStyle: StrutStyle(
                                      height: scaleFactor == 1
                                          ? Theme.of(context)
                                              .textTheme
                                              .bodyText2
                                              .height
                                          : 1.5 * scaleFactor,
                                      forceStrutHeight: true,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            hasUrl || hasPhone
                                ? Container(
                                    height: 56,
                                    padding: EdgeInsets.only(top: 16),
                                    child: ListView(
                                      scrollDirection: Axis.horizontal,
                                      padding:
                                          EdgeInsets.only(left: 16, right: 8),
                                      children: <Widget>[
                                        hasUrl
                                            ? ActionChip(
                                                backgroundColor: Theme.of(
                                                        context)
                                                    .scaffoldBackgroundColor,
                                                shape: OutlineInputBorder(
                                                  borderRadius:
                                                      BorderRadius.circular(40),
                                                  borderSide: BorderSide(
                                                    width: 1.0,
                                                    color: Theme.of(context)
                                                        .primaryColor,
                                                  ),
                                                ),
                                                pressElevation: 1.0,
                                                avatar: Icon(
                                                  AppIcons.mousePointer,
                                                  size: 16,
                                                  color: Theme.of(context)
                                                      .primaryColor,
                                                ),
                                                label: Text(
                                                  url.first,
                                                  overflow:
                                                      TextOverflow.ellipsis,
                                                  style: Theme.of(context)
                                                      .textTheme
                                                      .caption
                                                      .merge(TextStyle(
                                                          color: Theme.of(
                                                                  context)
                                                              .primaryColor)),
                                                ),
                                                onPressed: () =>
                                                    Utils.launchUrl(
                                                        context, usableURL),
                                              )
                                            : Container(),
                                        hasUrl && hasPhone
                                            ? SizedBox(width: 8)
                                            : Container(),
                                        hasPhone
                                            ? Row(
                                                children: phoneNumbers
                                                    .map(
                                                      (String number) =>
                                                          Padding(
                                                        padding:
                                                            EdgeInsets.only(
                                                                right: 8.0),
                                                        child: ActionChip(
                                                          backgroundColor: Theme
                                                                  .of(context)
                                                              .scaffoldBackgroundColor,
                                                          shape:
                                                              OutlineInputBorder(
                                                            borderRadius:
                                                                BorderRadius
                                                                    .circular(
                                                                        40),
                                                            borderSide:
                                                                BorderSide(
                                                              width: 1.0,
                                                              color: Theme.of(
                                                                      context)
                                                                  .primaryColor,
                                                            ),
                                                          ),
                                                          pressElevation: 1.0,
                                                          avatar: Icon(
                                                              AppIcons.phone,
                                                              size: 16,
                                                              color: Theme.of(
                                                                      context)
                                                                  .primaryColor),
                                                          label: Text(
                                                            number,
                                                            overflow:
                                                                TextOverflow
                                                                    .ellipsis,
                                                            style: Theme.of(
                                                                    context)
                                                                .textTheme
                                                                .caption
                                                                .merge(TextStyle(
                                                                    color: Theme.of(
                                                                            context)
                                                                        .primaryColor)),
                                                          ),
                                                          onPressed: () =>
                                                              Utils.launchUrl(
                                                                  context,
                                                                  "tel://$number"),
                                                        ),
                                                      ),
                                                    )
                                                    .toList(),
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
                : Container(),
            Padding(
              padding: EdgeInsets.symmetric(horizontal: 16),
              child: Column(
                children: <Widget>[
                  PrimaryButton(
                    label: buttonLabel,
                    buttonColor: Utils.getRequestStatusColor(_status,
                        Theme.of(context).brightness == Brightness.dark),
                    labelColor: Colors.white,
                    hasShadow: true,
                    icon: redeemed
                        ? AppIcons.solidImages
                        : inProgress
                            ? AppIcons.ticketAltSolid
                            : AppIcons.solidHeart,
                    rotateIcon: inProgress ? true : false,
                    hasIcon: true,
                    round: true,
                    onTap: _buttonOnTap,
                  ),
                  invited ? SizedBox(height: 8) : Container(),
                  invited
                      ? PrimaryButton(
                          label: "Not Interested",
                          buttonColor:
                              Theme.of(context).scaffoldBackgroundColor,
                          labelColor: Theme.of(context).hintColor,
                          round: true,
                          onTap: () => Navigator.of(context).pushNamed(
                              AppRouting.getRequestDeclineRoute(
                                deal.id,
                                appState.currentProfile.id,
                              ),
                              arguments: deal),
                        )
                      : Container(),
                ],
              ),
            ),
            Visibility(
              visible: inProgress || redeemed,
              child: Padding(
                padding: EdgeInsets.only(top: 8.0),
                child: Text(
                  caption,
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(
                          color: inProgress
                              ? Theme.of(context).hintColor
                              : Theme.of(context).primaryColor,
                        ),
                      ),
                ),
              ),
            ),
          ],
        ),
      );
    } else {
      return Container();
    }
  }
}
