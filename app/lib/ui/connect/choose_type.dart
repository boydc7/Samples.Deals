import 'dart:async';

import 'package:flutter/material.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/publisher_account.dart';

import 'package:rydr_app/ui/connect/blocs/choose.type.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class ConnectChooseTypePage extends StatefulWidget {
  final PublisherAccount userToLink;

  ConnectChooseTypePage(this.userToLink);

  @override
  State<StatefulWidget> createState() => _ConnectChooseTypePageState();
}

class _ConnectChooseTypePageState extends State<ConnectChooseTypePage>
    with TickerProviderStateMixin {
  ConnectChooseTypeBloc _bloc;

  final List<IconData> iconsCreator = [
    AppIcons.biking,
    AppIcons.skiing,
    AppIcons.angel,
    AppIcons.userGraduate,
    AppIcons.swimmer,
    AppIcons.userAstronaut
  ];

  StreamSubscription _subChooseTypeState;

  int currentCreatorIcon = 0;

  Timer _timerCreatorIcon;

  @override
  void initState() {
    super.initState();

    Future.delayed(const Duration(seconds: 1), () {
      _timerCreatorIcon = Timer.periodic(const Duration(seconds: 3), (timer) {
        setState(() {
          currentCreatorIcon = currentCreatorIcon == iconsCreator.length - 1
              ? 0
              : currentCreatorIcon + 1;
        });
      });
    });

    /// pass the user we want to link to the bloc
    /// we'll store it there as a property to then use once linking is initiated
    _bloc = ConnectChooseTypeBloc(widget.userToLink);

    /// listen to state changes on the bloc that will dictate what we do
    _subChooseTypeState =
        _bloc.chooseTypeState.listen(_onChooseTypeStateChanged);
  }

  @override
  void dispose() {
    _subChooseTypeState?.cancel();
    _bloc.dispose();

    _timerCreatorIcon.cancel();

    super.dispose();
  }

  void _onChooseTypeStateChanged(ConnectChooseTypeState state) async {
    if (state == ConnectChooseTypeState.linked) {
      /// once linked, we can send the user to the initial route
      /// which will identify if we need further onboarding or not
      Navigator.of(context).pushNamedAndRemoveUntil(
          appState.getInitialRoute(), (Route<dynamic> route) => false);
    } else if (state == ConnectChooseTypeState.error) {
      showSharedModalAlert(
        context,
        Text('Unable to link profile'),
        content: Text(
            'We had an issue linking this profile to RYDR. Please try again in a few moments...'),
        actions: <ModalAlertAction>[
          ModalAlertAction(
            onPressed: () {
              Navigator.of(context).pop();

              Navigator.of(context).pushNamedAndRemoveUntil(
                  appState.getInitialRoute(), (Route<dynamic> route) => false);
            },
            label: "Ok",
          )
        ],
      );
    }
  }

  void _onLink(RydrAccountType type) {
    _bloc.setLinkAsType(type);
    _bloc.linkUser();
  }

  @override
  Widget build(BuildContext context) {
    /// is this a profile we've previously linked? if so we don't show any
    /// information other than that we're connecting them...
    final bool previouslyLinked =
        widget.userToLink.linkedAsAccountType == RydrAccountType.business ||
            widget.userToLink.linkedAsAccountType == RydrAccountType.influencer;

    return Scaffold(
      body: SafeArea(
        bottom: true,
        child: StreamBuilder<ConnectChooseTypeState>(
          stream: _bloc.chooseTypeState,
          builder: (context, snapshot) {
            final bool linkingUser = snapshot.data != null &&
                snapshot.data == ConnectChooseTypeState.linking;

            return Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                _buildCreatorIcon(),
                SizedBox(height: 8.0),

                /// if we have a user that previously linked, then we only show 'creating' account
                previouslyLinked
                    ? Container()
                    : Column(
                        children: <Widget>[
                          Text("Are you a creator?",
                              style: Theme.of(context).textTheme.headline6),
                          SizedBox(height: 4.0),
                          Text('Get deals for posting on Instagram.',
                              textAlign: TextAlign.center,
                              style: Theme.of(context).textTheme.bodyText2),
                        ],
                      ),
                Container(
                  padding: EdgeInsets.only(left: 64, right: 64, top: 16),
                  child: PrimaryButton(
                    context: context,
                    label:
                        linkingUser ? "Creating account..." : "Yep, let's go!",
                    onTap: () => _onLink(RydrAccountType.influencer),
                  ),
                ),
              ],
            );
          },
        ),
      ),
      bottomNavigationBar: InkWell(
        onTap: () => _onLink(RydrAccountType.business),
        child: Container(
            margin:
                EdgeInsets.only(bottom: MediaQuery.of(context).padding.bottom),
            height: kMinInteractiveDimension,
            width: double.infinity,
            color: Colors.transparent,

            /// show nothing if previously linked
            child: previouslyLinked
                ? Container()
                : Center(
                    child: Text(
                    "No, I'm a business",
                    style: Theme.of(context).textTheme.bodyText1.merge(
                          TextStyle(
                            color: Theme.of(context).hintColor,
                          ),
                        ),
                  ))),
      ),
    );
  }

  Widget _buildCreatorIcon() => SizedBox(
        height: 40.0,
        width: 40.0,
        child: Container(
          margin: EdgeInsets.only(right: 8.0),
          child: AnimatedSwitcher(
            duration: Duration(milliseconds: 300),
            child: FadeInRightLeft(
              0,
              Hero(
                tag: 'creator',
                child: Icon(
                  iconsCreator[currentCreatorIcon],
                  color: Theme.of(context).textTheme.bodyText2.color,
                  size: 32.0,
                ),
              ),
              250,
              begin: 10.0,
            ),
          ),
        ),
      );
}
