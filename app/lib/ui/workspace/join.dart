import 'package:flutter/material.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/workspace/blocs/join.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class WorkspaceJoinPage extends StatefulWidget {
  @override
  _WorkspaceJoinPageState createState() => _WorkspaceJoinPageState();
}

class _WorkspaceJoinPageState extends State<WorkspaceJoinPage> {
  final _bloc = WorkspaceJoinBloc();

  final _controller1 = TextEditingController();
  final _controller2 = TextEditingController();
  final _controller3 = TextEditingController();
  final _controller4 = TextEditingController();
  final _controller5 = TextEditingController();
  final _controller6 = TextEditingController();

  final _focusNode1 = FocusNode();
  final _focusNode2 = FocusNode();
  final _focusNode3 = FocusNode();
  final _focusNode4 = FocusNode();
  final _focusNode5 = FocusNode();
  final _focusNode6 = FocusNode();

  @override
  void initState() {
    super.initState();
  }

  @override
  void dispose() {
    _controller1.dispose();
    _controller2.dispose();
    _controller3.dispose();
    _controller4.dispose();
    _controller5.dispose();
    _controller6.dispose();

    _focusNode1.dispose();
    _focusNode2.dispose();
    _focusNode3.dispose();
    _focusNode4.dispose();
    _focusNode5.dispose();
    _focusNode6.dispose();

    super.dispose();
  }

  void _onInputChanged() {
    _bloc.setCanSendRequest(_controller1.text.trim().length > 0 &&
        _controller2.text.trim().length > 0 &&
        _controller3.text.trim().length > 0 &&
        _controller4.text.trim().length > 0 &&
        _controller5.text.trim().length > 0 &&
        _controller6.text.trim().length > 0);
  }

  void _sendRequest() async {
    final WorkspaceJoinResult res = await _bloc.sendRequest(
        _controller1.text.trim() +
            _controller2.text.trim() +
            _controller3.text.trim() +
            _controller4.text.trim() +
            _controller5.text.trim() +
            _controller6.text.trim());

    /// if we were not successful in sending the request, then show
    /// appropriate error based on the join result enum returned
    if (res != WorkspaceJoinResult.Sent) {
      final bool isOwner = res == WorkspaceJoinResult.IsOwner;
      final bool isUser = res == WorkspaceJoinResult.AlreadyJoined;
      final bool isInvalid = res == WorkspaceJoinResult.InvalidToken;

      showSharedModalAlert(
        context,
        isUser
            ? Text("Already a Member")
            : isOwner
                ? Text("Hi Team Owner!")
                : isInvalid ? Text('Invalid Team Code') : Text("Server Error"),
        content: isUser
            ? Text(
                "You're already a member of this team, so you don't need to put in a request to join.")
            : isOwner
                ? Text(
                    "You're the owner of this team, so you don't need to put in a request to join.")
                : isInvalid
                    ? Text(
                        "You've entered an invalid team code. Double check your team's code and try again.")
                    : Text(
                        "Something happened in the process of sending this request. Please try again."),
        actions: [
          ModalAlertAction(
              label: isUser || isOwner ? "Got it!" : "Okay",
              onPressed: () {
                Navigator.pop(context);

                /// if this was an invalid token or some other error, then reset the page
                /// so that the form shows again, otherwise, send the user to the connect pages
                if (isOwner || isUser) {
                  Navigator.of(context)
                      .pushReplacementNamed(AppRouting.getConnectPages);
                } else {
                  /// reset the controllers
                  _controller1.text = "";
                  _controller2.text = "";
                  _controller3.text = "";
                  _controller4.text = "";
                  _controller5.text = "";
                  _controller6.text = "";

                  /// reset page state to idle and ability to send
                  _bloc.setCanSendRequest(false);
                  _bloc.setPageState(WorkspaceJoinPageState.Idle);
                }
              }),
        ],
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        elevation: 0,
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
      ),
      body: SafeArea(
        bottom: true,
        child: Container(
          width: double.infinity,
          padding: EdgeInsets.all(16),
          child: StreamBuilder<WorkspaceJoinPageState>(
            stream: _bloc.pageState,
            builder: (context, snapshot) {
              final WorkspaceJoinPageState state =
                  snapshot.data ?? WorkspaceJoinPageState.Idle;

              return state == WorkspaceJoinPageState.Idle ||
                      state == WorkspaceJoinPageState.Sending ||
                      state == WorkspaceJoinPageState.Error
                  ? _buildAccessCodeForm(state)
                  : _buildSuccess();
            },
          ),
        ),
      ),
    );
  }

  Widget _buildAccessCodeForm(WorkspaceJoinPageState state) {
    if (state == WorkspaceJoinPageState.Sending) {
      return FadeInTopBottom(
        10,
        _buildSendingRequest("Sending request to join...", ""),
        350,
        begin: -20,
      );
    } else if (state == WorkspaceJoinPageState.Error) {
      return FadeInTopBottom(
        10,
        _buildSendingRequest("", ""),
        350,
        begin: -20,
      );
    } else {
      return Column(
        children: <Widget>[
          Expanded(
            child: Padding(
              padding: EdgeInsets.symmetric(horizontal: 16),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Text("Join a Business Pro Team",
                      style: TextStyle(
                          fontSize: 22.0, fontWeight: FontWeight.w600)),
                  SizedBox(height: 2.0),
                  Text(
                    "Enter your team's join code.",
                    style: TextStyle(color: Theme.of(context).hintColor),
                  ),
                  SizedBox(height: 24.0),
                  Container(
                    width: 300,
                    child: Row(
                      children: <Widget>[
                        Expanded(
                          child: _buildTextField(
                              controller: _controller1,
                              focusNode: _focusNode1,
                              onChanged: (val) {
                                if (val != null && val.trim().length > 0) {
                                  FocusScope.of(context)
                                      .requestFocus(_focusNode2);
                                  _onInputChanged();
                                }
                              }),
                        ),
                        SizedBox(width: 8),
                        Expanded(
                          child: _buildTextField(
                              controller: _controller2,
                              focusNode: _focusNode2,
                              onChanged: (val) {
                                if (val != null && val.trim().length > 0) {
                                  FocusScope.of(context)
                                      .requestFocus(_focusNode3);
                                  _onInputChanged();
                                }
                              }),
                        ),
                        SizedBox(width: 8),
                        Expanded(
                          child: _buildTextField(
                              controller: _controller3,
                              focusNode: _focusNode3,
                              onChanged: (val) {
                                if (val != null && val.trim().length > 0) {
                                  FocusScope.of(context)
                                      .requestFocus(_focusNode4);
                                  _onInputChanged();
                                }
                              }),
                        ),
                        SizedBox(width: 8),
                        Expanded(
                          child: _buildTextField(
                              controller: _controller4,
                              focusNode: _focusNode4,
                              onChanged: (val) {
                                if (val != null && val.trim().length > 0) {
                                  FocusScope.of(context)
                                      .requestFocus(_focusNode5);
                                  _onInputChanged();
                                }
                              }),
                        ),
                        SizedBox(width: 8),
                        Expanded(
                          child: _buildTextField(
                              controller: _controller5,
                              focusNode: _focusNode5,
                              onChanged: (val) {
                                if (val != null && val.trim().length > 0) {
                                  FocusScope.of(context)
                                      .requestFocus(_focusNode6);
                                  _onInputChanged();
                                }
                              }),
                        ),
                        SizedBox(width: 8),
                        Expanded(
                          child: _buildTextField(
                              controller: _controller6,
                              focusNode: _focusNode6,
                              onChanged: (val) {
                                if (val != null && val.trim().length > 0) {
                                  FocusScope.of(context)
                                      .requestFocus(FocusNode());
                                  _onInputChanged();
                                }
                              }),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          FadeInBottomTop(
              5,
              Padding(
                padding: EdgeInsets.only(top: kToolbarHeight),
                child: StreamBuilder<bool>(
                    stream: _bloc.canSendRequest,
                    builder: (context, snapshot) {
                      final bool canSend =
                          snapshot.data != null && snapshot.data == true;

                      return PrimaryButton(
                        context: context,
                        buttonColor: Theme.of(context).primaryColor,
                        label: "Send Request to Join",
                        onTap: canSend ? _sendRequest : null,
                      );
                    }),
              ),
              350),
        ],
      );
    }
  }

  Widget _buildSendingRequest(String content, String subtitle,
      {bool success = false}) {
    Shader linearGradient = LinearGradient(
      colors: <Color>[
        success ? AppColors.successGreen : Theme.of(context).primaryColor,
        AppColors.successGreen,
        success ? AppColors.successGreen : Colors.yellowAccent
      ],
    ).createShader(
      Rect.fromLTRB(275.0, 0.0, 0.0, 28.0),
    );

    return Stack(
      children: <Widget>[
        AnimatedContainer(
          duration: Duration(
            milliseconds: 350,
          ),
          width: double.infinity,
          padding: EdgeInsets.only(
              bottom: success ? kToolbarHeight * 2 : kToolbarHeight),
          child: Column(
            mainAxisSize: MainAxisSize.max,
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Text(
                content,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 24.0,
                  fontWeight: FontWeight.bold,
                  foreground: Paint()..shader = linearGradient,
                ),
              ),
              Visibility(
                visible: subtitle != "",
                child: Padding(
                  padding: EdgeInsets.only(top: 4.0),
                  child: Text(
                    subtitle,
                    textAlign: TextAlign.center,
                  ),
                ),
              )
            ],
          ),
        ),
        Align(
          alignment: Alignment.bottomCenter,
          child: Visibility(
            visible: success,
            child: FadeInBottomTop(
                35,
                Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    PrimaryButton(
                      label: "Return to Personal Profiles",
                      onTap: () => Navigator.of(context).pop(),
                    ),
                  ],
                ),
                350),
          ),
        )
      ],
    );
  }

  Widget _buildSuccess() {
    return Stack(
      alignment: Alignment.center,
      children: <Widget>[
        FadeOutOpacityOnly(
          0,
          _buildSendingRequest("Sending request to join...", ""),
        ),
        FadeInTopBottom(
          25,
          _buildSendingRequest(
            "Request Sent",
            "You will be notified if your request has been accepted.",
            success: true,
          ),
          350,
          begin: -20,
        ),
      ],
    );
  }

  Widget _buildTextField({
    TextEditingController controller,
    FocusNode focusNode,
    bool enabled,
    Function onChanged,
  }) {
    final Color color = controller == _controller1
        ? Color(0xFFc1ed4e)
        : controller == _controller2
            ? Color(0xFF7ad753)
            : controller == _controller3
                ? Color(0xFF58c06b)
                : controller == _controller4
                    ? Color(0xFF4ba69f)
                    : controller == _controller5
                        ? Color(0xFF418cdd)
                        : Theme.of(context).primaryColor;
    return TextField(
      controller: controller,
      focusNode: focusNode,
      textAlign: TextAlign.center,
      textCapitalization: TextCapitalization.characters,
      autofocus: focusNode == _focusNode1 ? true : false,
      enableSuggestions: false,
      autocorrect: false,
      cursorWidth: 4,
      cursorRadius: Radius.circular(4),
      cursorColor: color,
      style: TextStyle(
          fontSize: 26,
          color: color,
          letterSpacing: 0.0,
          fontWeight: FontWeight.w700),
      enabled: enabled,
      maxLength: 1,
      decoration: InputDecoration(
        contentPadding:
            EdgeInsets.only(left: 5.0, right: 0, top: 16.0, bottom: 16),
        counter: Container(height: 0),
        border:
            OutlineInputBorder(borderSide: BorderSide(color: color, width: 4)),
        disabledBorder:
            OutlineInputBorder(borderSide: BorderSide(color: color, width: 4)),
        enabledBorder:
            OutlineInputBorder(borderSide: BorderSide(color: color, width: 4)),
        errorBorder:
            OutlineInputBorder(borderSide: BorderSide(color: color, width: 4)),
        focusedBorder:
            OutlineInputBorder(borderSide: BorderSide(color: color, width: 4)),
      ),
      onChanged: onChanged,
    );
  }
}
