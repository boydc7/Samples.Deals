import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/enums/workspace.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/fbig_users.dart';
import 'package:rydr_app/ui/connect/blocs/pages.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class ConnectPagesUnLinked extends StatefulWidget {
  ConnectPagesUnLinked();

  @override
  _ConnectPagesUnLinkedState createState() => _ConnectPagesUnLinkedState();
}

class _ConnectPagesUnLinkedState extends State<ConnectPagesUnLinked> {
  final _bloc = ConnectPagesBloc.instance;

  @override
  void initState() {
    super.initState();

    _bloc.loadPages();
  }

  /// if this user was previously linked, then show the modal alert prompting the user
  /// to either relink with the given / previous type, or cancel out of the dialog
  /// otherwise
  /// send them to the onboard page where they have to choose the type of account
  void _onUnlinkedUserTap(BuildContext context, PublisherAccount userToLink) {
    /// If we're linking a profile to a team workspace, then we can only link them as a business
    /// so show an alert to the user that this will be the case
    if (appState.currentWorkspace.type == WorkspaceType.Team) {
      showSharedModalAlert(
        context,
        Text('Connect as Business'),
        content:
            Text('${userToLink.userName} will be connected as a Business.'),
        actions: [
          ModalAlertAction(
              isDefaultAction: true,
              label: 'Connect as a Business',
              onPressed: () {
                Navigator.of(context).pop();
                _linkProfile(context, userToLink, RydrAccountType.business);
              }),
          ModalAlertAction(
              isDestructiveAction: true,
              label: 'Do not Connect',
              onPressed: () {
                Navigator.of(context).pop();
              }),
        ],
      );
    } else if (userToLink.linkedAsAccountType != RydrAccountType.unknown) {
      final bool isInfluencer =
          userToLink.linkedAsAccountType == RydrAccountType.influencer;

      showSharedModalAlert(
        context,
        Text('Reconnect'),
        content: isInfluencer
            ? Text(
                '${userToLink.userName} was previously connected as a Creator.')
            : Text(
                '${userToLink.userName} was previously connected as a Business.'),
        actions: [
          ModalAlertAction(
              isDefaultAction: true,
              label: isInfluencer
                  ? 'Connect as a Creator'
                  : 'Connect as a Business',
              onPressed: () {
                Navigator.of(context).pop();
                _linkProfile(
                    context, userToLink, userToLink.linkedAsAccountType);
              }),
          ModalAlertAction(
              isDestructiveAction: true,
              label: 'Do not Connect',
              onPressed: () {
                Navigator.of(context).pop();
              }),
        ],
      );
    } else {
      Navigator.of(context).pushNamedAndRemoveUntil(
          AppRouting.getConnectChooseType, (Route<dynamic> route) => false,
          arguments: userToLink);
    }
  }

  void _linkProfile(BuildContext context, PublisherAccount profile,
      RydrAccountType linkAsType) {
    showSharedLoadingLogo(
      context,
      content: 'Linking Profile',
    );

    _bloc.linkUser(profile, linkAsType).then((success) {
      /// close the loading modal
      Navigator.of(context).pop();

      if (success) {
        /// Depending on whether or not the user of this device has already
        /// onboarded this type of profile we either continue them onto the next page
        Navigator.of(context).pushNamedAndRemoveUntil(
            appState.getInitialRoute(), (Route<dynamic> route) => false);
      } else {
        showSharedModalError(
          context,
          title: 'Unable to link profile',
          subTitle:
              'We had an issue linking this profile to RYDR. Please try again in a few moments...',
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) => SafeArea(
        bottom: true,
        child: Column(
          children: <Widget>[
            /// add two tiles for connecting new IG basic and facebook token
            /// always at the top before any list of new pages they can connect if they have a fb token
            _buildConnectProfiles(),

            StreamBuilder<bool>(
              stream: _bloc.loadingFb,
              builder: (context, snapshot) => snapshot.data == true
                  ? LinearProgressIndicator(
                      backgroundColor: Theme.of(context).primaryColor,
                      valueColor: AlwaysStoppedAnimation<Color>(
                        Theme.of(context).scaffoldBackgroundColor,
                      ),
                    )
                  : Container(),
            ),

            Expanded(
              child: StreamBuilder<FbIgUsersResponse>(
                stream: _bloc.unlinkedProfilesResponse,
                builder: (context, AsyncSnapshot<FbIgUsersResponse> snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) {
                    return _buildLoading();
                  } else if (snapshot.data != null && snapshot.data.hasError) {
                    return _buildError(snapshot.error);
                  } else {
                    return _buildSuccess(context, snapshot.data);
                  }
                },
              ),
            ),
          ],
        ),
      );

  Widget _buildError(FbIgUsersResponse unlinkedProfilesResponse) {
    /// check the users response from the server to see if
    /// we had a timeout, socket exception (no internet) or some other error
    /// and adjust the error message accordingly
    String message;

    if (unlinkedProfilesResponse.error != null) {
      if (unlinkedProfilesResponse.error.type == DioErrorType.RECEIVE_TIMEOUT) {
        message =
            "It took longer than expected to load your Facebook Pages. This is usually a temporary issue and may be due to a slow Internet connection.\n\nChoose 'Refresh Facebook Pages' from the options menu below to try again.";
      } else if (unlinkedProfilesResponse.error.type ==
          DioErrorType.CONNECT_TIMEOUT) {
        message =
            "We're unable to connect to the Internet. Please check to ensure you're connected to a WI-FI network or have sufficient cellular data access.\n\nChoose 'Refresh Facebook Pages' from the options menu below to try again.";
      }
    }

    if (message == null) {
      message =
          "We're unable to load your Facebook Pages.\n\nChoose 'Log out of Facebook' from the options below to re-authenticate your Profile.";
    }

    return Container(
      child: Column(
        children: <Widget>[
          Expanded(
            child: Container(
              margin: EdgeInsets.all(32),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(message),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLoading() => ListView(
        children: <Widget>[LoadingListShimmer(short: true)],
        padding: EdgeInsets.all(16),
      );

  Widget _buildConnectProfiles() => Column(
        children: <Widget>[
          ListTile(
            leading: SizedBox(
              height: 40,
              width: 40,
              child: Center(
                child: Container(
                  height: 32,
                  width: 32,
                  decoration: BoxDecoration(
                    image: DecorationImage(
                      fit: BoxFit.cover,
                      image: AssetImage(
                        'assets/icons/instagram-logo-gradient.png',
                      ),
                    ),
                  ),
                ),
              ),
            ),
            title: Text(
              "Add Instagram Basic",
              style: TextStyle(fontWeight: FontWeight.w600),
            ),
            subtitle: Text(
              "Private or non-professional profile",
              style: Theme.of(context).textTheme.bodyText2.merge(
                    TextStyle(
                      color: Theme.of(context).hintColor,
                    ),
                  ),
            ),
            onTap: () => Navigator.of(context)
                .pushNamed(AppRouting.getConnectAddInstagram),
            trailing: Container(
              width: 32.0,
              height: 32.0,
              child: Icon(
                AppIcons.plus,
                color: Theme.of(context).hintColor,
                size: 18.0,
              ),
            ),
          ),
          Divider(height: 1),

          /// this is only available when in a personal workspace
          appState.currentWorkspace.type == WorkspaceType.Personal
              ? Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    ListTile(
                      leading: SizedBox(
                        height: 40,
                        width: 40,
                        child: Center(
                          child: Icon(
                            AppIcons.facebook,
                            size: 32,
                            color: Color(0xff4267B2),
                          ),
                        ),
                      ),
                      title: Text(
                        "Add Instagram Professional",
                        style: TextStyle(fontWeight: FontWeight.w600),
                      ),
                      subtitle: Text(
                        "Business or Creator professional profile",
                        style: Theme.of(context).textTheme.bodyText2.merge(
                              TextStyle(
                                color: Theme.of(context).hintColor,
                              ),
                            ),
                      ),

                      /// if we have an existing facebook token, then send them to the 'switch' flow
                      /// if they don't then send them straight to the facebook connect flow
                      onTap: () => Navigator.of(context).pushNamed(
                          appState.currentWorkspace.hasFacebookToken
                              ? AppRouting.getConnectFacebookModify
                              : AppRouting.getConnectSwitchFacebook),

                      trailing: Container(
                        width: 32.0,
                        height: 32.0,
                        child: Icon(
                          AppIcons.plus,
                          color: Theme.of(context).hintColor,
                          size: 18.0,
                        ),
                      ),
                    ),
                    sectionDivider(context),
                  ],
                )
              : Container(),
        ],
      );

  Widget _buildSuccess(BuildContext context, FbIgUsersResponse res) {
    if (res == null || res.models == null || res.models.isEmpty) {
      /// We don't need to show any other information if the user doesn't have any extra FB
      /// pages to add. We take care of this at the top by always having the IG basic and pro
      /// links available.
      return Container();
    } else {
      return ListView.separated(
        separatorBuilder: (context, index) => Divider(height: 1),
        itemCount: res.models.length,
        itemBuilder: (BuildContext context, int index) {
          final PublisherAccount userToLink = res.models[index];

          return ListTileTheme(
            child: ListTile(
              leading: UserAvatar(
                userToLink,
              ),
              title: Text(
                userToLink.userName,
                style: TextStyle(fontWeight: FontWeight.w600),
              ),
              subtitle: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                      userToLink.publisherMetrics == null
                          ? ""
                          : userToLink.linkedAsAccountType != null &&
                                  userToLink.linkedAsAccountType !=
                                      RydrAccountType.unknown
                              ? "${rydrAccountTypeToString(userToLink.linkedAsAccountType)} · ${userToLink.publisherMetrics.followedByDisplay} followers"
                              : "${userToLink.publisherMetrics.followedByDisplay} followers",
                      style: Theme.of(context).textTheme.bodyText2.merge(
                          TextStyle(color: Theme.of(context).hintColor))),
                ],
              ),
              trailing: Container(
                width: 32.0,
                height: 32.0,
                child: Icon(
                  AppIcons.plus,
                  color: Theme.of(context).hintColor,
                  size: 18.0,
                ),
              ),
              onTap: () => _onUnlinkedUserTap(context, userToLink),
            ),
          );
        },
      );
    }
  }
}
