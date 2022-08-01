import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/ui/deal/blocs/add_deal.dart';
import 'package:rydr_app/ui/deal/widgets/shared/constants.dart';
import 'package:rydr_app/ui/deal/utils.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class DealAddInvitesSection extends StatelessWidget {
  final DealAddBloc bloc;

  DealAddInvitesSection(this.bloc);

  final Map<String, String> _pageContent = {
    "choosing_creators_title": "Choosing Creators",
    "choosing_additional_creators_title": "Optional Invitations",
  };

  void _openPicker(
          BuildContext context, List<PublisherAccount> existingInvites) =>
      showDealInvitePicker(context, false,
          dealId: bloc.deal.id,
          existingInvites: existingInvites,
          previousInvites: [], onClose: (
        List<PublisherAccount> newInvites,
      ) {
        FocusScope.of(context).requestFocus(FocusNode());

        bloc.setInvites(newInvites);
      });

  void _handleRemove(PublisherAccount u) => bloc.removeInvite(u);

  @override
  Widget build(BuildContext context) {
    /// Invite-only functionality is only available on "team" workspaces
    /// if this is not a team then the "bloc" will have it pre-selected to marketplace
    /// and we wont' show anything here for the user to select / change

    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return !bloc.canUseInvites
        ? sectionDivider(context)
        : StreamBuilder<DealVisibilityType>(
            stream: bloc.visibilityType,
            builder: (context, snapshot) {
              final DealVisibilityType visibilityType = snapshot.data;

              return Column(
                children: <Widget>[
                  Divider(),
                  SizedBox(height: 24.0),

                  /// respond to changes in invited users
                  StreamBuilder<List<PublisherAccount>>(
                    stream: bloc.invites,
                    builder: (context, snapshot) {
                      final List<PublisherAccount> usersInvited =
                          snapshot.data != null ? snapshot.data : [];

                      return Column(
                        children: <Widget>[
                          Padding(
                            padding: EdgeInsets.symmetric(horizontal: 16.0),
                            child: Column(
                              children: <Widget>[
                                _buildHeader(
                                    context, visibilityType, usersInvited),
                                SizedBox(height: 12.0),
                                _buildInvites(context, dark, usersInvited),
                                _buildAddButton(context, usersInvited),
                                SizedBox(height: 32.0),
                              ],
                            ),
                          ),
                          sectionDivider(context),
                        ],
                      );
                    },
                  ),
                ],
              );
            },
          );
  }

  Widget _buildHeader(
    BuildContext context,
    DealVisibilityType visibilityType,
    List<PublisherAccount> usersInvited,
  ) {
    final int inviteCount = usersInvited.length;

    return Column(
      children: <Widget>[
        Text(
          visibilityType == DealVisibilityType.InviteOnly
              ? _pageContent['choosing_creators_title']
              : _pageContent['choosing_additional_creators_title'],
          style: Theme.of(context).textTheme.bodyText1,
        ),
        SizedBox(height: 2.0),
        StreamBuilder<int>(
          stream: bloc.quantity,
          builder: (context, snapshot) {
            final int maxApprovals = snapshot.data ?? 0;

            return StreamBuilder<bool>(
              stream: bloc.autoApprove,
              builder: (context, snapshot) {
                final bool autoApprove =
                    snapshot.data != null && snapshot.data == true;

                /// messages will differ for invite-only vs. marketplace deals
                if (visibilityType == DealVisibilityType.InviteOnly) {
                  final int diff = maxApprovals - inviteCount;

                  /// show different messages based on the following options
                  /// 1. Must have a quantity other than "unlimited" for invite-only deals
                  /// 2. Must have at least as many invites as quantity
                  /// 3. Quantity matches number of invites
                  /// 4. Invites exceed the number of RYDRs available
                  return Text(
                    maxApprovals == 0
                        ? "Every Creator invited will have a chance to accept this request."
                        : maxApprovals > 0 && inviteCount == 0
                            ? "Invite $diff or more Creators for this RYDR."
                            : maxApprovals > inviteCount
                                ? "Invite at least $diff more ${diff > 1 ? 'Creators' : 'Creator'} or change quantity to match the number of invites."
                                : maxApprovals == inviteCount
                                    ? "If a Creator doesn't accept your invite it will go unused. Consider inviting additional Creators to make the RYDR more desirable."
                                    : maxApprovals < inviteCount
                                        ? "$maxApprovals RYDR for $inviteCount Creators - ${maxApprovals == 1 ? 'The first to accept your invite will win this RYDR' : 'The first $maxApprovals to accept will win this RYDR'}"
                                        : "",
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(color: Theme.of(context).hintColor),
                        ),
                  );
                } else {
                  /// show different messages based on the following options
                  /// 1. One RYDR available but no invites yet...
                  /// 2. One RYDR and one invite added
                  /// 3. There are more than 1 RYDR available but no invites yet...
                  /// 4. Invite count matches number of available RYDRs with auto-approve on
                  /// 5. Invite count matches number of available RYDRs with auto-approve off
                  /// 6. Default message

                  return Text(
                    inviteCount == 0
                        ? "You are able to invite Creators already on RYDR in addition to\nposting in the Marketplace."
                        : maxApprovals == 1 && inviteCount == 0
                            ? "Inviting Creators will increase the demand for this RYDR, but only one request can be accepted. Creator invites are always auto-approved, unless the quantity is exhausted before they accept."
                            : maxApprovals == 1 && maxApprovals == inviteCount
                                ? "Only $maxApprovals total request can be accepted. Creator invites are always auto-approved, unless the quantity is exhausted by Marketplace approvals before they accept."
                                : maxApprovals > 1 && inviteCount == 0
                                    ? "Inviting Creators will increase the demand for this RYDR, but only $maxApprovals total requests can be accepted. Creator invites are always auto-approved, unless the quantity is exhausted before they accept."
                                    : autoApprove
                                        ? "You are inviting $inviteCount Creators and have $maxApprovals RYDRs available. Requests from the Marketplace and invites are auto-approved. The first $maxApprovals Creators to request or accept this RYDR will win."
                                        : "You are inviting $inviteCount Creators and have $maxApprovals RYDRs available. Requests from the Marketplace will still need to be approved, however the first Creators to accept their invite will win the RYDR, unless the quantity is exhausted before they accept.",
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(color: Theme.of(context).hintColor),
                        ),
                  );
                }
              },
            );
          },
        )
      ],
    );
  }

  Widget _buildInvites(
    BuildContext context,
    bool dark,
    List<PublisherAccount> usersInvited,
  ) =>
      Visibility(
        visible: usersInvited.length > 0,
        child: Column(
          children: <Widget>[
            Wrap(
              alignment: WrapAlignment.center,
              spacing: 8.0,
              children: usersInvited
                  .where((PublisherAccount u) => !u.isFromInstagram)
                  .map((PublisherAccount u) => _buildChip(context, dark, u))
                  .toList(),
            ),
            Wrap(
              alignment: WrapAlignment.center,
              spacing: 8.0,
              children: usersInvited
                  .where((PublisherAccount u) => u.isFromInstagram)
                  .map((PublisherAccount u) => _buildChip(context, dark, u))
                  .toList(),
            ),
            SizedBox(height: 12.0),
          ],
        ),
      );

  Widget _buildChip(
    BuildContext context,
    bool dark,
    PublisherAccount u,
  ) =>
      Chip(
        avatar: UserAvatar(u, hideBorder: true),
        shape: OutlineInputBorder(
          borderRadius: BorderRadius.circular(32),
          borderSide: BorderSide(
            color: u.isFromInstagram
                ? Theme.of(context).primaryColor
                : Utils.getRequestStatusColor(DealRequestStatus.invited, dark),
          ),
        ),
        backgroundColor: u.isFromInstagram
            ? Theme.of(context).primaryColor.withOpacity(0.1)
            : Utils.getRequestStatusColor(DealRequestStatus.invited, dark)
                .withOpacity(0.1),
        label: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Flexible(
              fit: FlexFit.loose,
              child: Text(
                u.userName,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodyText1,
              ),
            ),
            Visibility(
              visible: u.isVerified != null ? u.isVerified : false,
              child: Padding(
                padding: EdgeInsets.only(left: 4.0),
                child: Icon(
                  AppIcons.badgeCheck,
                  color: Theme.of(context).primaryColor,
                  size: 12,
                ),
              ),
            ),
          ],
        ),
        padding: EdgeInsets.all(0),
        deleteIconColor: u.isFromInstagram
            ? Theme.of(context).primaryColor
            : Utils.getRequestStatusColor(DealRequestStatus.invited, dark),
        onDeleted: () => _handleRemove(u),
      );

  Widget _buildAddButton(
    BuildContext context,
    List<PublisherAccount> usersInvited,
  ) =>
      usersInvited.length < dealMaxInvites
          ? GestureDetector(
              onTap: () => _openPicker(context, usersInvited),
              child: Container(
                height: 48.0,
                width: 48.0,
                decoration: BoxDecoration(
                    color: Theme.of(context).appBarTheme.color,
                    border: Border.all(
                        color: Theme.of(context).textTheme.bodyText2.color),
                    borderRadius: BorderRadius.circular(24.0)),
                child: Center(
                  child: Icon(
                    AppIcons.plus,
                    color: Theme.of(context).textTheme.bodyText2.color,
                  ),
                ),
              ),
            )
          : Container();
}
