import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/services/authenticate.dart';
import 'package:rydr_app/services/instagram.dart';
import 'package:rydr_app/ui/connect/utils.dart';

enum ConnectChooseTypeState {
  linking,
  linked,
  error,
}

class ConnectChooseTypeBloc {
  final _chooseTypeState = BehaviorSubject<ConnectChooseTypeState>();

  PublisherAccount _userToLink;
  RydrAccountType _linkAsType;
  bool _canChangeType = true;

  RydrAccountType get linkAsType => _linkAsType;

  /// if the user sent to this page was previously linked as a certain type
  /// then we don't currently allow the user to change the type on this flow
  /// so in the bloc and in the UI we'd use this to adjust the flow accordingly
  bool get canChangeType => _canChangeType;

  /// on initState we create the bloc and pass it the user we want to link
  /// this user could be an existing user in which case we'd preset the linkAsType
  ConnectChooseTypeBloc(PublisherAccount userToLink) {
    _userToLink = userToLink;

    /// if this user was linked previously then set the type they were previously
    /// and iniate a linking of the page automatically
    if (_userToLink.linkedAsAccountType == RydrAccountType.business ||
        _userToLink.linkedAsAccountType == RydrAccountType.influencer) {
      _canChangeType = false;
      setLinkAsType(_userToLink.linkedAsAccountType);

      linkUser();
    }
  }

  dispose() {
    _chooseTypeState.close();
  }

  BehaviorSubject<ConnectChooseTypeState> get chooseTypeState =>
      _chooseTypeState.stream;

  void setLinkAsType(RydrAccountType type) {
    _linkAsType = type;
  }

  void linkUser() async {
    _chooseTypeState.sink.add(ConnectChooseTypeState.linking);

    /// if we're working with a 'basic' IG profile, then we'll use a different endpoint
    /// to actually now create the publisher on the server and link it...
    /// whereas, otherwise we're looking at linking an existing facebook IG pro page
    if (_userToLink.isAccountBasic) {
      final IntIdResponse intIdResponse = await InstagramService.linkProfile(
        _userToLink.postBackId,
        _linkAsType,
      );

      /// if successfull then continue on by loading workspaces and then trying to 'switch'
      /// to the newly created profile, otherwise, update the state and do nothing else
      if (intIdResponse.hasError) {
        _chooseTypeState.sink.add(ConnectChooseTypeState.error);
      } else {
        final bool successWs =
            await AuthenticationService.instance().loadWorkspaces();

        if (!successWs) {
          _chooseTypeState.sink.add(ConnectChooseTypeState.error);
        } else {
          final bool successPub =
              await appState.switchProfile(intIdResponse.model);

          _chooseTypeState.sink.add(successPub
              ? ConnectChooseTypeState.linked
              : ConnectChooseTypeState.error);
        }
      }
    } else {
      final PublisherAccount linkedUser = await ConnectUtils.linkUser(
        _userToLink,
        PublisherType.facebook,
        _linkAsType,
      );

      _chooseTypeState.sink.add(linkedUser == null
          ? ConnectChooseTypeState.error
          : ConnectChooseTypeState.linked);
    }
  }
}
