import 'package:flutter/material.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/ui/connect/blocs/connect_help.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

class ConnectHelpPage extends StatefulWidget {
  @override
  _ConnectHelpPageState createState() => _ConnectHelpPageState();
}

class _ConnectHelpPageState extends State<ConnectHelpPage>
    with SingleTickerProviderStateMixin {
  final ConnectHelpBloc _bloc = ConnectHelpBloc();
  PageController _pageController;

  @override
  void initState() {
    super.initState();
    _pageController = PageController(initialPage: 0);
  }

  @override
  void dispose() {
    _pageController.dispose();

    super.dispose();
  }

  void _lookupUsername(String username) {
    /// remove focus, hide the keyboard
    FocusScope.of(context).requestFocus(FocusNode());

    showSharedLoadingLogo(context);

    _bloc.lookupHandle(username).then((success) {
      Navigator.of(context).pop();

      if (success) {
        /// move the user along to the next page
        _goToPage(2);
      } else {
        showSharedModalError(context,
            title: "Unable to find user",
            subTitle:
                "We were unable to find information on the username you entered...");
      }
    });
  }

  void _goToPage(int page) {
    _bloc.setPage(page);
    _pageController.jumpToPage(page);
  }

  void _goBack() => Navigator.of(context).pop();

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          backgroundColor: Theme.of(context).scaffoldBackgroundColor,
          leading: StreamBuilder<int>(
              stream: _bloc.page,
              builder: (context, snapshot) {
                final int page = snapshot.data ?? 0;

                return page == 0
                    ? AppBarCloseButton(context)
                    : page == 2
                        ? Container()
                        : AppBarBackButton(
                            context,
                            onPressed: () => _goToPage(0),
                          );
              }),
          elevation: 0,
        ),
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
        body: PageView(
          controller: _pageController,
          physics: NeverScrollableScrollPhysics(),
          children: <Widget>[
            _initialQuestions(),
            _noAccountListed(),
            _instagramDetails(),
            _whyFacebook(),
            _noLikePermissions()
          ],
        ),
      );

  Widget _initialQuestions() => Container(
        width: double.infinity,
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: 32),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              Padding(
                padding: EdgeInsets.only(bottom: 4.0),
                child: Text(
                  'Can we help?',
                  style: Theme.of(context).textTheme.headline5,
                ),
              ),
              Padding(
                padding: EdgeInsets.only(bottom: 32.0),
                child: Text(
                  'The connect process can be a bit confusing.',
                  textAlign: TextAlign.center,
                  style: Theme.of(context)
                      .textTheme
                      .caption
                      .merge(TextStyle(color: Theme.of(context).hintColor)),
                ),
              ),
              SecondaryButton(
                  fullWidth: true,
                  context: context,
                  label: 'I didn\'t see my Instagram account listed.',
                  onTap: () => _goToPage(1)),
              SizedBox(height: 16),
              SecondaryButton(
                  fullWidth: true,
                  context: context,
                  label: 'Why do I have to connect with Facebook?',
                  onTap: () => _goToPage(3)),
              SizedBox(height: 16),
              SecondaryButton(
                  fullWidth: true,
                  context: context,
                  label: 'I didn\'t want to approve all the permissions.',
                  onTap: () => _goToPage(4)),
            ],
          ),
        ),
      );

  Widget _noAccountListed() => Container(
        width: double.infinity,
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.center,
          children: <Widget>[
            Padding(
              padding: EdgeInsets.only(bottom: 4.0),
              child: Text(
                'Can\'t See Account',
                style: Theme.of(context).textTheme.headline5,
              ),
            ),
            Padding(
              padding: EdgeInsets.only(bottom: 32.0),
              child: Text(
                'What account were you looking for?',
                textAlign: TextAlign.center,
                style: Theme.of(context)
                    .textTheme
                    .caption
                    .merge(TextStyle(color: Theme.of(context).hintColor)),
              ),
            ),
            Padding(
              padding: EdgeInsets.symmetric(horizontal: 32),
              child: TextFormField(
                onFieldSubmitted: _lookupUsername,
                textInputAction: TextInputAction.next,
                style: Theme.of(context).textTheme.bodyText2.merge(
                      TextStyle(
                        fontSize: 16.0,
                      ),
                    ),
                decoration: InputDecoration(
                  labelText: "Instagram Username",
                  labelStyle: TextStyle(color: Theme.of(context).hintColor),
                  prefixIcon: Icon(AppIcons.instagram,
                      size: 18, color: Theme.of(context).hintColor),
                  focusedBorder: OutlineInputBorder(
                    borderSide: BorderSide(
                        color: Theme.of(context).primaryColor, width: 2.0),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderSide: BorderSide(color: Theme.of(context).hintColor),
                  ),
                  border: OutlineInputBorder(
                    borderSide: BorderSide(color: Theme.of(context).hintColor),
                  ),
                ),
              ),
            ),
          ],
        ),
      );

  Widget _instagramDetails() => ListView(
        padding: EdgeInsets.symmetric(horizontal: 32),
        children: <Widget>[
          Padding(
            padding: EdgeInsets.only(bottom: 4.0),
            child: Text(
              _bloc.isPrivate ? 'Private Account' : 'Good News!',
              style: Theme.of(context).textTheme.headline5,
              textAlign: TextAlign.center,
            ),
          ),
          Text(
            _bloc.isPrivate
                ? 'Unfortunately RYDR doesn\'t support private Instagram accounts, but we\'re working on it.'
                : 'You\'re only one step away from connecting your account.',
            textAlign: TextAlign.center,
            style: Theme.of(context)
                .textTheme
                .caption
                .merge(TextStyle(color: Theme.of(context).hintColor)),
          ),
          Padding(
            padding: EdgeInsets.symmetric(vertical: 32),
            child: Stack(
              alignment: Alignment.center,
              overflow: Overflow.visible,
              children: <Widget>[
                GestureDetector(
                  onTap: () => Utils.launchUrl(
                    context,
                    "https://instagram.com/${_bloc.username}",
                    trackingName: 'profile',
                  ),
                  child: Container(
                    width: 250,
                    height: 445,
                    decoration: BoxDecoration(
                      boxShadow: AppShadows.elevation[0],
                      border: Border.all(
                          color: Theme.of(context).dividerColor.withOpacity(
                              Theme.of(context).brightness == Brightness.dark
                                  ? 0.15
                                  : 0.75)),
                      image: DecorationImage(
                        fit: BoxFit.contain,
                        image: AssetImage(
                          Theme.of(context).brightness == Brightness.dark
                              ? _bloc.isPrivate ||
                                      (!_bloc.isPrivate && !_bloc.isBusiness)
                                  ? 'assets/switch-prof_dark.png'
                                  : 'assets/connect-profile_dark.png'
                              : _bloc.isPrivate ||
                                      (!_bloc.isPrivate && !_bloc.isBusiness)
                                  ? 'assets/switch-prof_light.png'
                                  : 'assets/connect-profile_light.png',
                        ),
                      ),
                    ),
                  ),
                ),
                _bloc.isBusiness
                    ? Positioned(
                        bottom: 80,
                        child: Container(
                          height: 56,
                          width: 256,
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(4),
                            border: Border.all(
                                width: 4,
                                color: Theme.of(context).primaryColor),
                          ),
                          alignment: Alignment.bottomCenter,
                          child: SizedBox(
                            width: 248,
                            height: 24,
                            child: Container(
                              color: Theme.of(context).primaryColor,
                              child: Center(
                                child: Padding(
                                  padding: EdgeInsets.only(top: 2.0),
                                  child: Text(
                                    "You must have a Facebook Page connected",
                                    textAlign: TextAlign.center,
                                    style: Theme.of(context)
                                        .textTheme
                                        .caption
                                        .merge(
                                          TextStyle(color: Colors.white),
                                        ),
                                  ),
                                ),
                              ),
                            ),
                          ),
                        ),
                      )
                    : Positioned(
                        bottom: 112,
                        child: Container(
                          height: 56,
                          width: 256,
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(4),
                            border: Border.all(
                                width: 4,
                                color: Theme.of(context).primaryColor),
                          ),
                          alignment: Alignment.bottomCenter,
                          child: SizedBox(
                            width: 248,
                            height: 24,
                            child: Container(
                              color: Theme.of(context).primaryColor,
                              child: Center(
                                child: Padding(
                                  padding: EdgeInsets.only(top: 2.0),
                                  child: Text(
                                    "You must have a Professional Account",
                                    textAlign: TextAlign.center,
                                    style: Theme.of(context)
                                        .textTheme
                                        .caption
                                        .merge(
                                          TextStyle(color: Colors.white),
                                        ),
                                  ),
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
              ],
            ),
          ),
          _bloc.isBusiness
              ? RichText(
                  textAlign: TextAlign.center,
                  text: TextSpan(
                    children: [
                      TextSpan(text: "From your "),
                      TextSpan(
                        text: "Instagram profile",
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                      TextSpan(text: ", tap "),
                      TextSpan(
                        text: "Edit Profile",
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                      TextSpan(text: ", then "),
                      TextSpan(
                        text: "Connect or Create",
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                      TextSpan(
                          text:
                              " under Profile Information to connect a Facebook Page to your account."),
                    ],
                    style: Theme.of(context).textTheme.bodyText2,
                  ),
                )
              : RichText(
                  textAlign: TextAlign.center,
                  text: TextSpan(
                    children: [
                      TextSpan(text: "From your "),
                      TextSpan(
                        text: "Instagram profile",
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                      TextSpan(text: ", tap "),
                      TextSpan(
                        text: "Switch to Professional Account",
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                      TextSpan(text: ", choose "),
                      TextSpan(
                        text: "Creator or Business",
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                      TextSpan(
                          text:
                              ", then also be sure to connect a Facebook Page to your new profile."),
                    ],
                    style: Theme.of(context).textTheme.bodyText2,
                  ),
                ),
          Padding(
            padding: EdgeInsets.only(top: 32.0, bottom: 16),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: PrimaryButton(
                    buttonColor: Theme.of(context).canvasColor,
                    labelColor: Theme.of(context).textTheme.bodyText2.color,
                    label: "Back to Connect",
                    onTap: _goBack,
                  ),
                ),
                SizedBox(width: 8),
                Expanded(
                  child: PrimaryButton(
                    label: "Open Profile",
                    icon: AppIcons.instagram,
                    hasIcon: true,
                    onTap: () => Utils.launchUrl(
                      context,
                      "https://instagram.com/${_bloc.username}",
                      trackingName: 'profile',
                    ),
                  ),
                ),
              ],
            ),
          ),
          Divider(),
          GestureDetector(
            child: Container(
              padding:
                  EdgeInsets.only(left: 16, right: 16, top: 16, bottom: 16),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.center,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                      "Instagram Professional Profiles and Why They Are Required for RYDR.",
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(color: Theme.of(context).primaryColor))),
                  Padding(
                    padding: EdgeInsets.only(top: 2.0),
                    child: Text("help.instagram.com",
                        style: Theme.of(context).textTheme.caption),
                  ),
                ],
              ),
            ),
            onTap: () => Utils.launchUrl(context,
                "https://getrydr.com/support/instagram-professional-profiles-and-why-they-are-required-for-rydr/"),
          ),
          SizedBox(height: MediaQuery.of(context).padding.bottom),
        ],
      );

  Widget _whyFacebook() => Padding(
        padding: EdgeInsets.only(
          left: 16,
          right: 16,
          top: 16,
          bottom: MediaQuery.of(context).padding.bottom + 16,
        ),
        child: Column(
          children: <Widget>[
            Text(
              'Why Facebook?',
              style: Theme.of(context).textTheme.headline5,
            ),
            SizedBox(height: 32),
            Expanded(
              child: Text(
                  "If you didn't know already, Instagram was purchased by Facebook in 2012. They've slowly been moving all of the Instagram data over into Facebook. This now includes all your post, story, and follower insights like impressions, reach, follower locations and more.\n\nIn order for RYDR to give the businesses the data they need to make good decisions, we need to get some of that data from our users.\n\nBy connecting with Facebook, we'll be able to access your professional Instagram profile and insights from the connected Facebook Page."),
            ),
            PrimaryButton(label: "Back to Connect", onTap: _goBack),
          ],
        ),
      );

  Widget _noLikePermissions() => Padding(
        padding: EdgeInsets.only(
          left: 16,
          right: 16,
          top: 16,
          bottom: MediaQuery.of(context).padding.bottom + 16,
        ),
        child: Column(
          children: <Widget>[
            Text(
              'Facebook Permissions',
              style: Theme.of(context).textTheme.headline5,
            ),
            SizedBox(height: 32),
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.start,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    "We only ask for permissions to features that will be used to bring you the best experience on RYDR. Here is a detailed list of every permission we ask for and why we require it.\n\n",
                  ),
                  Text(
                    "Email Address",
                    textAlign: TextAlign.left,
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                  SizedBox(height: 6),
                  Text(
                    "If available, we use it to send you account related emails.\n",
                  ),
                  Text(
                    "Access profile and posts from the Instagram account connected to your Page",
                    textAlign: TextAlign.left,
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                  SizedBox(height: 6),
                  Text(
                    "This allows RYDR to show you your most recent posts, and for you to see other RYDR user's recent posts. We use these posts for creating and completing RYDRS.\n",
                  ),
                  Text(
                    "Access insights for the Instagram account connected to your Page",
                    textAlign: TextAlign.left,
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                  SizedBox(height: 6),
                  Text(
                    "This allows RYDR to store and display your Instagram insights over time, as well as compile the insights for the business to view on a completed RYDR.\n",
                  ),
                  Text(
                    "Manage and show a list of your Pages",
                    textAlign: TextAlign.left,
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                  SizedBox(height: 6),
                  Text(
                    "This allows RYDR to obtain access tokens to the available Facebook Pages connected to Instagram profiles.\n\n",
                  ),
                ],
              ),
            ),
            PrimaryButton(label: "Back to Connect", onTap: _goBack),
          ],
        ),
      );
}
