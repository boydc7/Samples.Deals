import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/deals.dart';
import 'package:rydr_app/ui/deal/blocs/deal_edit.dart';
import 'package:rydr_app/ui/deal/blocs/deal_invites.dart';
import 'package:rydr_app/ui/deal/utils.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class DealEditInvites extends StatelessWidget {
  final DealInvitesBloc invitesBloc = DealInvitesBloc();
  final DealEditBloc bloc;
  final Function onSend;
  final bool expired;

  DealEditInvites(this.bloc, this.onSend, this.expired);

  final Map<String, String> _pageContent = {
    "title_existing": "Invited Creators",
    "title_addinvites": "Invite Creators",
    "subtitle_addinvites": "Send personal invites to this RYDR",
    "title_additional_invites": "Additional Creators to Invite",
    "title_additional_invites_no_existing": "Creators to Invite",
    "status_invited": "Waiting for a response...",
    "status_denied": "Creator declined your invite",
    "status_cancelled": "RYDR was cancelled",
    "status_inProgress": "RYDR in progress",
    "status_completed": "Completed by Creator - tap to view insights",
  };

  void _openPicker(
    BuildContext context,
    Deal deal,
    List<PublisherAccount> existingInvites,
  ) =>
      showDealInvitePicker(context, true,
          dealId: deal.id, existingInvites: existingInvites,
          onClose: (List<PublisherAccount> newInvites) {
        FocusScope.of(context).requestFocus(FocusNode());

        bloc.setInvitesToAdd(newInvites);
      });

  void _sendInvites() => onSend();

  @override
  Widget build(BuildContext context) {
    final Deal deal = bloc.dealResponse.value.model;

    /// only published deals are applicable to show the invites section
    /// then from there we'll determine if they can add new ones or not (based on subscription)
    return deal.status == DealStatus.published
        ? StreamBuilder<List<PublisherAccount>>(
            stream: bloc.invitesToAdd,
            builder: (context, snapshot) {
              final List<PublisherAccount> usersInvited =
                  snapshot.data != null ? snapshot.data : [];

              return FutureBuilder<DealsResponse>(
                future: invitesBloc.loadInviteRequests(bloc.deal.id),
                builder: (context, snapshot) {
                  final bool hasExistingInvites = snapshot.data != null &&
                      !snapshot.data.hasError &&
                      snapshot.data.models != null &&
                      snapshot.data.models.isNotEmpty;

                  return snapshot.connectionState == ConnectionState.waiting
                      ? Padding(
                          padding: EdgeInsets.all(16),
                          child: Text(
                            "Loading existing invites...",
                            style: Theme.of(context).textTheme.bodyText1,
                          ),
                        )
                      : Column(
                          children: <Widget>[
                            /// show existing invites first, if we have any from when the deal was first created
                            /// or after we've added and sent more when editing a deal
                            hasExistingInvites
                                ? _buildExistingInvites(
                                    context,
                                    deal,
                                    hasExistingInvites,
                                    usersInvited,
                                    snapshot.data.models,
                                  )
                                : Container(height: 0),

                            /// if the user can add additional invites then show
                            /// either the new ones added, or section for adding new ones
                            usersInvited.isNotEmpty
                                ? _buildAdditionalInvitees(context, deal,
                                    usersInvited, hasExistingInvites)
                                : !hasExistingInvites && bloc.canUseInvites
                                    ? _buildNoInvites(context, deal,
                                        usersInvited, hasExistingInvites)
                                    : Container(height: 0),
                          ],
                        );
                },
              );
            },
          )
        : Container(height: 0);
  }

  Widget _buildNoInvites(
    BuildContext context,
    Deal deal,
    List<PublisherAccount> usersInvited,
    bool hasExistingInvites,
  ) =>

      /// if the deal is expired or the business doesn't have a valid
      /// subscription then we don't show the no invites section
      expired || !bloc.canUseInvites
          ? Container()
          : Column(
              children: <Widget>[
                ListTile(
                  onTap: () => _openPicker(context, deal, usersInvited),
                  title: Text(
                    _pageContent['title_addinvites'],
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                  subtitle: Text(
                    _pageContent['subtitle_addinvites'],
                    style: Theme.of(context).textTheme.bodyText2,
                  ),
                  trailing: Icon(
                    AppIcons.plusCircle,
                    color: Theme.of(context).primaryColor,
                  ),
                ),
                sectionDivider(context),
              ],
            );

  Widget _buildAdditionalInvitees(
    BuildContext context,
    Deal deal,
    List<PublisherAccount> usersInvited,
    bool hasExistingInvites,
  ) =>
      Column(
        children: <Widget>[
          Container(
            color: hasExistingInvites
                ? Theme.of(context).canvasColor
                : Colors.transparent,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Padding(
                  padding: EdgeInsets.only(top: 24, left: 16.0, right: 16.0),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: <Widget>[
                      Text(
                        _pageContent[hasExistingInvites
                            ? 'title_additional_invites'
                            : 'title_additional_invites_no_existing'],
                        style: Theme.of(context).textTheme.bodyText1,
                      ),
                      Text(
                        "story · post",
                        style: Theme.of(context).textTheme.caption.merge(
                              TextStyle(
                                color: Theme.of(context).hintColor,
                              ),
                            ),
                      ),
                    ],
                  ),
                ),
                _buildInvitesToAdd(
                  context,
                  deal,
                  usersInvited,
                ),

                /// add button row which can have a 'add creators' button
                /// and/or potentially a 'send invites' if we have new ones to send
                _buildActionButtons(context, deal, usersInvited),
              ],
            ),
          ),
          Visibility(
            visible: !hasExistingInvites,
            child: sectionDivider(context),
          ),
        ],
      );

  /// existing invites with a link to the request
  Widget _buildExistingInvites(
    BuildContext context,
    Deal deal,
    bool hasExisting,
    List<PublisherAccount> usersInvited,
    List<Deal> existingDealInviteRequests,
  ) =>
      Column(
        children: <Widget>[
          Padding(
            padding: EdgeInsets.only(top: 16.0, bottom: 4.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Padding(
                  padding: EdgeInsets.only(left: 16.0, right: 12.0),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: <Widget>[
                      Text(
                        _pageContent['title_existing'],
                        style: Theme.of(context).textTheme.bodyText1,
                      ),

                      /// show invite more 'link' if we don't have additional new invites
                      /// and the user is allowed to use  the invite creators feature
                      Visibility(
                        visible: usersInvited.isEmpty &&
                            bloc.canUseInvites &&
                            !expired,
                        child: TextButton(
                          isBasic: true,
                          bold: false,
                          caption: true,
                          label: "Invite More",
                          color: Theme.of(context).primaryColor,
                          onTap: () => _openPicker(context, deal, usersInvited),
                        ),
                      )
                    ],
                  ),
                ),
                Column(
                  children: existingDealInviteRequests
                      .map((Deal deal) => ListTile(
                            leading: UserAvatar(deal.request.publisherAccount,
                                requestStatus: deal.request.status),
                            title: Text(
                              deal.request.publisherAccount.userName,
                              style: Theme.of(context).textTheme.bodyText1,
                            ),
                            subtitle: Text(
                              _pageContent[
                                      'status_${dealRequestStatusToString(deal.request.status)}'] ??
                                  "",
                              style: Theme.of(context).textTheme.bodyText2,
                            ),
                            trailing: Icon(
                              AppIcons.angleRight,
                              color: AppColors.grey300,
                              size: 18.0,
                            ),
                            onTap: () => Navigator.of(context)
                                .pushNamed(AppRouting.getRequestRoute(
                              deal.id,
                              deal.request.publisherAccount.id,
                            )),
                          ))
                      .toList(),
                ),
              ],
            ),
          ),
          Visibility(
            visible: hasExisting,
            child: sectionDivider(context),
          ),
        ],
      );

  Widget _buildInvitesToAdd(
    BuildContext context,
    Deal deal,
    List<PublisherAccount> usersInvited,
  ) =>
      Column(
        children: usersInvited
            .map((PublisherAccount invite) => Dismissible(
                key: Key(invite.userName),
                direction: DismissDirection.endToStart,
                background: Container(
                  padding: EdgeInsets.only(right: 16.0),
                  alignment: Alignment.centerRight,
                  color: AppColors.errorRed,
                  child: Text(
                    'Remove Invite',
                    style: TextStyle(color: AppColors.white),
                  ),
                ),
                onDismissed: (direction) => bloc.removeInvite(invite),
                child: ListTile(
                  leading: UserAvatar(invite),
                  title: Text(
                    invite.userName,
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                  subtitle: Row(
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          "Average Reach",
                          style: Theme.of(context).textTheme.bodyText2,
                        ),
                      ),
                      Text(
                          "${invite.publisherMetrics.avgStoryReachDisplay != '' ? invite.publisherMetrics.avgStoryReachDisplay : 'n/a'} · ${invite.publisherMetrics.avgPostReachDisplay != '' ? invite.publisherMetrics.avgPostReachDisplay : 'n/a'}")
                    ],
                  ),
                )))
            .toList(),
      );

  /// row of up to two buttons (or none)
  /// one to invite creators to this deal, another to send if we have new ones
  /// deal must not be expired and business must have valid subscription
  Widget _buildActionButtons(
    BuildContext context,
    Deal deal,
    List<PublisherAccount> usersInvited,
  ) =>
      Visibility(
        visible: usersInvited.isNotEmpty && !expired && bloc.canUseInvites,
        child: Padding(
          padding:
              EdgeInsets.only(top: 8.0, left: 16.0, right: 16.0, bottom: 20.0),
          child: Row(
            children: <Widget>[
              Expanded(
                child: PrimaryButton(
                  onTap: () => _openPicker(context, deal, usersInvited),
                  label: "Invite More",
                  hasIcon: true,
                  icon: AppIcons.plusCircle,
                ),
              ),
              SizedBox(width: 8.0),
              Expanded(
                child: PrimaryButton(
                  onTap: _sendInvites,
                  label: "Send Invites",
                  hasIcon: true,
                  icon: AppIcons.envelope,
                ),
              )
            ],
          ),
        ),
      );
}
