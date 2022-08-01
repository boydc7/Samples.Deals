import 'dart:async';
import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/deal.dart';

class DealAddVisibilitySection extends StatelessWidget {
  final Stream<DealVisibilityType> valueStream;
  final Function handleUpdate;
  final bool canUseInvites;

  DealAddVisibilitySection({
    @required this.valueStream,
    @required this.handleUpdate,
    @required this.canUseInvites,
  });

  final Map<String, String> _pageContent = {
    "title": "RYDR Visibility",
    "subtitle": "How and where should this RYDR be viewed?",
    "marketplace_title": "Marketplace",
    "marketplace_subtitle": "with optional invites",
    "invite_title": "Invitation Only",
    "invite_subtitle": "chosen by you",
  };

  Future<void> _handleTap(BuildContext context, DealVisibilityType type) async {
    FocusScope.of(context).requestFocus(FocusNode());

    handleUpdate(type);

    /// artificial delay to wait for re-paint of new section based on visibility type change
    /// then scroll visibility section into view
    await Future.delayed(
        Duration(milliseconds: 50),
        () => Scrollable.ensureVisible(
              context,
              curve: Curves.fastOutSlowIn,
              duration: const Duration(milliseconds: 550),
            ));
  }

  /// Invite-only functionality is only available on "team" workspaces
  /// if this is not a team then the "bloc" will have it pre-selected to marketplace
  /// and we wont' show anything here for the user to select / change
  @override
  Widget build(BuildContext context) => !canUseInvites
      ? Container(height: 0)
      : StreamBuilder<DealVisibilityType>(
          stream: valueStream,
          builder: (context, snapshot) {
            final DealVisibilityType currentType = snapshot.data;

            return Padding(
              padding:
                  EdgeInsets.only(left: 16, right: 16, top: 32, bottom: 16),
              child: Column(
                children: <Widget>[
                  Text(
                    _pageContent['title'],
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                  SizedBox(height: 2.0),
                  Text(
                    _pageContent['subtitle'],
                    style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(color: Theme.of(context).hintColor),
                        ),
                  ),
                  SizedBox(height: 12.0),
                  Row(
                    children: <Widget>[
                      _buildBox(
                        context,
                        DealVisibilityType.Marketplace,
                        currentType,
                        _pageContent['marketplace_title'],
                        _pageContent['marketplace_subtitle'],
                        _handleTap,
                      ),
                      SizedBox(width: 8.0),
                      _buildBox(
                        context,
                        DealVisibilityType.InviteOnly,
                        currentType,
                        _pageContent['invite_title'],
                        _pageContent['invite_subtitle'],
                        _handleTap,
                      ),
                    ],
                  ),
                ],
              ),
            );
          },
        );

  Widget _buildBox(
    BuildContext context,
    DealVisibilityType type,
    DealVisibilityType currentType,
    String title,
    String subTitle,
    Function handleTap,
  ) =>
      Expanded(
        child: GestureDetector(
          onTap: () => handleTap(context, type),
          child: AnimatedContainer(
            height: 100.0,
            duration: Duration(milliseconds: 250),
            decoration: BoxDecoration(
                color: currentType == null
                    ? Theme.of(context).scaffoldBackgroundColor
                    : currentType == type
                        ? Theme.of(context).appBarTheme.color
                        : Theme.of(context).scaffoldBackgroundColor,
                border: Border.all(
                    width: currentType == null
                        ? 1.0
                        : currentType == type ? 2.0 : 1.0,
                    color: currentType == null
                        ? Theme.of(context).hintColor
                        : currentType == type
                            ? Theme.of(context).primaryColor
                            : Theme.of(context).hintColor),
                borderRadius: BorderRadius.circular(4.0)),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.center,
              children: <Widget>[
                Text(title,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                        color: currentType == null
                            ? Theme.of(context).textTheme.bodyText2.color
                            : currentType != type
                                ? Theme.of(context).textTheme.bodyText2.color
                                : Theme.of(context).primaryColor)),
                Text(
                  subTitle,
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(
                            color: currentType == null
                                ? Theme.of(context).hintColor
                                : currentType != type
                                    ? Theme.of(context).hintColor
                                    : Theme.of(context).primaryColor),
                      ),
                ),
              ],
            ),
          ),
        ),
      );
}
