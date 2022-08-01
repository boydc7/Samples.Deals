import 'dart:async';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/links.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/enums/workspace.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/ui/connect/connect_instagram.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

import 'package:rydr_app/ui/connect/blocs/connect_profile.dart';

class ConnectProfilePage extends StatefulWidget {
  /// if we just want to switch to another facebook token account...
  final bool switchFacebookAcount;

  /// if we just want to add another instagram basic account then load the page
  /// without a Ui, wait for it to build, then pop open the instagram login flow
  final bool addInstagramBasic;

  /// if either of the above is false (the default), then this page will show
  /// the user the option to choose to connect a facebook token or a basic instagram
  /// or, alternatively get 'help' and figure out what type of instagram account they have

  ConnectProfilePage({
    this.switchFacebookAcount = false,
    this.addInstagramBasic = false,
  });

  @override
  _ConnectProfilePageState createState() => _ConnectProfilePageState();
}

class _ConnectProfilePageState extends State<ConnectProfilePage> {
  final ConnectProfileBloc _bloc = ConnectProfileBloc();

  @override
  void initState() {
    super.initState();

    /// if we are opening this page and are looking to either switch
    /// or connect an account explicitly, then set the state to connecting already,
    /// start a delay, and then trigger opening the auth flow for the requested platform
    if (widget.switchFacebookAcount) {
      _bloc.setState(ConnectProfileState.connectingFacebook);

      Future.delayed(const Duration(seconds: 1), () => _loginWithFacebook());
    } else if (widget.addInstagramBasic) {
      _bloc.setState(ConnectProfileState.connectingInstagram);

      Future.delayed(const Duration(seconds: 1), () => _loginWithInstagram());
    }
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  /// options bottom action sheet showing options the user has when they initially
  /// connect with rydr and don't have anything 'connected' yet...
  /// here they can log back out of their main authentication provider and we should
  void _showOptions() {
    showSharedModalBottomActions(
      context,
      title: 'Account Options',
      actions: [
        ModalBottomAction(
            child: Text("Do I have an Instagram Business Profile?"),
            onTap: () => Utils.launchUrl(
                  context,
                  AppLinks.supportUrlNoInstagramProfiles,
                  trackingName: 'support',
                )),
      ].where((i) => i != null).toList(),
    );
  }

  /// will start the facebook auth flow
  /// NOTE!: guard against calling this when the user is in a team workspace
  /// should only be allowed from personal or when there's no workspace yet
  void _loginWithFacebook() async {
    if (appState.currentWorkspace != null &&
        appState.currentWorkspace.type != WorkspaceType.Personal) {
      showSharedModalError(context,
          title: "Not Supported",
          subTitle:
              "Facebook Connect is only supported in your personal workspace.");

      /// update the connect profile state and return out
      _bloc.setState(ConnectProfileState.idle);
      return;
    }

    final bool fbSuccess = await _bloc.loginWithFacebook();

    /// if successful then continue the user onto the next step, otherwise do nothing and the conenct profile state stream
    /// is updated and any listeners will update the UI accordingly
    if (fbSuccess) {
      Navigator.of(context).pushNamedAndRemoveUntil(
          AppRouting.getConnectPages, (Route<dynamic> route) => false);
    }
  }

  void _loginWithInstagram() {
    _bloc.getInstagramAuthUrl().then((url) {
      if (url != null) {
        Navigator.of(context)
            .push(MaterialPageRoute(
          builder: (context) => ConnectInstagramPage(url),
          fullscreenDialog: true,
        ))
            .then((result) {
          /// process the result, which could indicate the user simply closed out (cancelled),
          /// succeded with the linking, or we had an error
          /// NOTE: sucess == null inidcates the user cancelled, so nothing to do
          _bloc.processInstagramResult(result).then((success) {
            if (success == true) {
              /// Create a basic publisher type from the result returned, which may also include
              /// a 'linkedAsAccountType' indicating to us this is a profile we already know about
              /// then send the user off to the page where they are informed or can make a choice for
              /// what type of account to link this profile as....
              PublisherAccount userToLink =
                  PublisherAccount.fromInstaBusinessAccount({
                "instagramBusinessAccount": {
                  /// the postback id is one that we get back as a result
                  /// from our end, we pass that along to the link type page
                  /// where we then will send it back to our server upon completion of the link
                  "postBackId": result.postBackId,

                  /// indicate previous linked as type if we have one
                  /// and set the link type to 'basic' so we know on the 'choose type' page
                  /// that we're coming from an IG account onboarding...
                  "linkedAsAccountType":
                      rydrAccountTypeToInt(result.linkedAsAccountType),
                  "linkType": publisherLinkTypeToString(PublisherLinkType.Basic)
                }
              });

              Navigator.of(context).pushNamedAndRemoveUntil(
                  AppRouting.getConnectChooseType,
                  (Route<dynamic> route) => false,
                  arguments: userToLink);
            } else if (success == false) {
              showSharedModalError(
                context,
                title: "Instagram Connect Failed",
                subTitle:
                    "Unable to connect your Instagram account at this time, please try again in a few moments",
              );
            }
          });
        });
      } else {
        showSharedModalError(
          context,
          title: "Instagram Load Failed",
          subTitle:
              "Unable to open Instagram login at this time, please try again in a few moments",
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final bool isInitial = widget.switchFacebookAcount == false &&
        widget.addInstagramBasic == false;

    return Scaffold(
      /// the app bar will either have a 'close' button for when we came here to add or
      /// switch accounts, or an options ellipsis for when we have no profiles and this is
      /// the initial 'connect' something page we show to a user after they registered with rydr
      appBar: AppBar(
        elevation: 0,
        leading: isInitial ? null : AppBarCloseButton(context),
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
        title: Text("Link an Instagram Profile"),
        actions: <Widget>[
          isInitial
              ? IconButton(
                  icon: Icon(AppIcons.ellipsisV),
                  onPressed: _showOptions,
                )
              : Container()
        ],
      ),
      body: SafeArea(
        bottom: true,
        child: StreamBuilder<ConnectProfileState>(
          stream: _bloc.connectState,
          builder: (context, snapshot) {
            final ConnectProfileState state = snapshot.data == null
                ? ConnectProfileState.idle
                : snapshot.data;
            final bool connected =
                state == ConnectProfileState.connectedFacebook ||
                    state == ConnectProfileState.connectedInstagram;

            return Stack(
              alignment: Alignment.center,
              children: <Widget>[
                Container(
                  width: double.infinity,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.center,
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      Visibility(
                        visible: isInitial || widget.addInstagramBasic,
                        child: Expanded(
                          child: Center(
                            child: SizedBox(
                              height: 190.0,
                              width: MediaQuery.of(context).size.width - 64,
                              child: InkWell(
                                borderRadius: BorderRadius.circular(16),
                                onTap: state ==
                                        ConnectProfileState.connectingInstagram
                                    ? null
                                    : _loginWithInstagram,
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.center,
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: <Widget>[
                                    Container(
                                      height: 40,
                                      width: 40,
                                      decoration: BoxDecoration(
                                        image: DecorationImage(
                                          fit: BoxFit.cover,
                                          image: AssetImage(
                                            'assets/icons/instagram-logo-gradient.png',
                                          ),
                                        ),
                                      ),
                                    ),
                                    SizedBox(
                                      height: 8.0,
                                    ),
                                    Text('Instagram Basic',
                                        style: Theme.of(context)
                                            .textTheme
                                            .headline6),
                                    SizedBox(
                                      height: 4.0,
                                    ),
                                    Text('Must screenshot stories',
                                        textAlign: TextAlign.center,
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodyText2),
                                    Row(
                                      mainAxisAlignment:
                                          MainAxisAlignment.center,
                                      mainAxisSize: MainAxisSize.min,
                                      children: <Widget>[
                                        Container(
                                          margin: EdgeInsets.only(
                                              top: 16, bottom: 8),
                                          decoration: BoxDecoration(
                                            borderRadius:
                                                BorderRadius.circular(4.0),
                                            boxShadow: AppShadows.elevation[0],
                                            color: Theme.of(context)
                                                .scaffoldBackgroundColor,
                                          ),
                                          padding: EdgeInsets.symmetric(
                                              horizontal: 16.0, vertical: 8.5),
                                          child: state ==
                                                  ConnectProfileState
                                                      .connectingInstagram
                                              ? Text(
                                                  'Connecting...',
                                                  textAlign: TextAlign.center,
                                                  style: TextStyle(
                                                      fontWeight:
                                                          FontWeight.w600,
                                                      fontSize: 16.0,
                                                      color: Theme.of(context)
                                                          .textTheme
                                                          .bodyText2
                                                          .color),
                                                )
                                              : Row(
                                                  mainAxisAlignment:
                                                      MainAxisAlignment.center,
                                                  children: <Widget>[
                                                    Container(
                                                      height: 16,
                                                      width: 16,
                                                      decoration: BoxDecoration(
                                                        image: DecorationImage(
                                                          fit: BoxFit.cover,
                                                          image: AssetImage(
                                                            'assets/icons/instagram-logo-gradient.png',
                                                          ),
                                                        ),
                                                      ),
                                                    ),
                                                    SizedBox(
                                                      width: 10.0,
                                                    ),
                                                    Text(
                                                      'Continue with Instagram',
                                                      textAlign:
                                                          TextAlign.center,
                                                      style: TextStyle(
                                                          fontWeight:
                                                              FontWeight.w600,
                                                          fontSize: 16.0,
                                                          color:
                                                              Theme.of(context)
                                                                  .textTheme
                                                                  .bodyText2
                                                                  .color),
                                                    ),
                                                  ],
                                                ),
                                        ),
                                      ],
                                    ),
                                    Text(
                                      'Private or Personal Profiles',
                                      textAlign: TextAlign.center,
                                      style: Theme.of(context)
                                          .textTheme
                                          .caption
                                          .merge(
                                            TextStyle(
                                              color:
                                                  Theme.of(context).hintColor,
                                            ),
                                          ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),

                      _buildConnectError(state),

                      /// show divider if we are showing both options
                      Visibility(
                        visible: isInitial,
                        child: Divider(height: 1),
                      ),

                      Visibility(
                        visible: isInitial || widget.switchFacebookAcount,
                        child: Expanded(
                          child: Center(
                            child: SizedBox(
                              height: 190.0,
                              width: MediaQuery.of(context).size.width - 64,
                              child: InkWell(
                                borderRadius: BorderRadius.circular(16),
                                onTap: state ==
                                        ConnectProfileState.connectingFacebook
                                    ? null
                                    : _loginWithFacebook,
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.center,
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: <Widget>[
                                    Icon(
                                      AppIcons.facebook,
                                      size: 40,
                                      color: Color(0xff4267B2),
                                    ),
                                    SizedBox(
                                      height: 8.0,
                                    ),
                                    Text('Instagram Professional',
                                        style: Theme.of(context)
                                            .textTheme
                                            .headline6),
                                    SizedBox(
                                      height: 4.0,
                                    ),
                                    Text('Story selection fully integrated',
                                        textAlign: TextAlign.center,
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodyText2),
                                    Row(
                                      mainAxisAlignment:
                                          MainAxisAlignment.center,
                                      mainAxisSize: MainAxisSize.min,
                                      children: <Widget>[
                                        Container(
                                          margin: EdgeInsets.only(
                                              top: 16, bottom: 8),
                                          decoration: BoxDecoration(
                                            borderRadius:
                                                BorderRadius.circular(4.0),
                                            boxShadow: AppShadows.elevation[0],
                                            color: Color(0xff4267B2),
                                          ),
                                          padding: EdgeInsets.symmetric(
                                              horizontal: 16.0, vertical: 8.5),
                                          child: state ==
                                                  ConnectProfileState
                                                      .connectingFacebook
                                              ? Text(
                                                  'Connecting...',
                                                  textAlign: TextAlign.center,
                                                  style: TextStyle(
                                                      fontWeight:
                                                          FontWeight.w600,
                                                      fontSize: 16.0,
                                                      color: Colors.white),
                                                )
                                              : Row(
                                                  mainAxisAlignment:
                                                      MainAxisAlignment.center,
                                                  children: <Widget>[
                                                    Icon(
                                                      AppIcons.facebook,
                                                      size: 16.0,
                                                      color: Colors.white,
                                                    ),
                                                    SizedBox(
                                                      width: 6.0,
                                                    ),
                                                    Text(
                                                      'Continue with Facebook',
                                                      textAlign:
                                                          TextAlign.center,
                                                      style: TextStyle(
                                                          fontWeight:
                                                              FontWeight.w600,
                                                          fontSize: 16.0,
                                                          color: Colors.white),
                                                    ),
                                                  ],
                                                ),
                                        ),
                                      ],
                                    ),
                                    Text(
                                      'Business or Creator Professional Profiles',
                                      textAlign: TextAlign.center,
                                      style: Theme.of(context)
                                          .textTheme
                                          .caption
                                          .merge(
                                            TextStyle(
                                              color:
                                                  Theme.of(context).hintColor,
                                            ),
                                          ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                Visibility(
                  visible: connected,
                  child: FadeInOpacityOnly(
                    40,
                    Container(
                      height: MediaQuery.of(context).size.height,
                      width: MediaQuery.of(context).size.width,
                      color: Theme.of(context).scaffoldBackgroundColor,
                    ),
                    duration: 2000,
                  ),
                )
              ],
            );
          },
        ),
      ),
      bottomNavigationBar: InkWell(
        onTap: () => Utils.launchUrl(
          context,
          AppLinks.supportUrlNoInstagramProfiles,
          trackingName: 'support',
        ),
        child: Container(
            margin:
                EdgeInsets.only(bottom: MediaQuery.of(context).padding.bottom),
            height: kMinInteractiveDimension,
            width: double.infinity,
            color: Colors.transparent,
            child: Center(
                child: Text(
              "Do I have an Instagram professional profile?",
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(
                      color: Theme.of(context).hintColor,
                    ),
                  ),
            ))),
      ),
    );
  }

  Widget _buildConnectError(ConnectProfileState state) {
    /// adjust the error message based on the state
    String message = "We were unable to connect your account";

    if (state == ConnectProfileState.cancelledbyUser) {
      message = widget.addInstagramBasic
          ? "Log in with your Instagram account to continue"
          : widget.switchFacebookAcount
              ? "Log in with your Facebook account to continue"
              : "Log in with Facebook or Instagram to continue";
    } else if (state == ConnectProfileState.permissionsDenied) {
      /// TODO: Brian, do we here want to show or send them to the page
      /// that explains what and why we need the facebook permissions
      message = "All permissions must be accepted to continue";
    } else if (state == ConnectProfileState.connectedInstagram) {
      /// TODO: Brian, if we get an error from instagram, should we have a message
      /// specific to instagram and maybe even include link(s) as to why it may have failed
      /// and how to fix it?
      message =
          "There was an error with Instagram.com, please try again in a few moments";
    } else if (state == ConnectProfileState.connectingFacebook) {
      /// TODO: Brian, same as instagram error above, do we want some specific help/links for the user?
      message =
          "There was an error with Facebook.com, please try again in a few moments";
    }

    return state == ConnectProfileState.cancelledbyUser ||
            state == ConnectProfileState.permissionsDenied ||
            state == ConnectProfileState.errorInstagram ||
            state == ConnectProfileState.errorFacebook
        ? FadeInOpacityOnly(
            10,
            Padding(
              padding: EdgeInsets.all(16),
              child: Text(
                message,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(
                        color: AppColors.errorRed,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
              ),
            ),
          )
        : Container();
  }
}
