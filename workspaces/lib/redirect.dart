import 'dart:async';
import 'package:flutter/material.dart';
import 'package:rydrworkspaces/app_bloc.dart';
import 'package:rydrworkspaces/ui/shared/widgets/buttons.dart';

class RedirectPage extends StatefulWidget {
  @override
  _RedirectPageState createState() => _RedirectPageState();
}

class _RedirectPageState extends State<RedirectPage> {
  final AppBloc _bloc = AppBloc();
  StreamSubscription _onAppStateChange;

  @override
  void dispose() {
    _onAppStateChange?.cancel();
    _bloc.dispose();

    super.dispose();
  }

  @override
  void initState() {
    super.initState();

    _bloc.initUser();

    _onAppStateChange = _bloc.state.listen(_onAppStateChanged);
  }

  void _onAppStateChanged(AppPageState state) {}

  @override
  Widget build(BuildContext context) {
    print('redirect page');
    return Scaffold(
      body: SafeArea(
        top: true,
        child: StreamBuilder(
          stream: _bloc.state,
          builder: (ctx, snapshot) {
            final state = snapshot.data ?? AppPageState.loading;

            return Container(
              width: double.infinity,
              padding: EdgeInsets.symmetric(
                horizontal: 16,
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  Visibility(
                    visible: state == AppPageState.loading,
                    child: LoadingLogo(radius: 72),
                  ),
                  Visibility(
                    visible: state == AppPageState.errorInternet,
                    child: _errorNoInternet(),
                  ),
                  Visibility(
                    visible: state == AppPageState.errorTimeout,
                    child: _errorTimeout(),
                  ),
                  Visibility(
                    visible: state == AppPageState.errorServer,
                    child: _errorServer(),
                  ),
                ],
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _errorNoInternet() {
    return Column(
      children: <Widget>[
        Text(
          "Please connect to WIFI or check your cellular connection..",
          style: TextStyle(
            color: Colors.white,
          ),
        ),
        SizedBox(height: 16),
        _retryButton(),
      ],
    );
  }

  Widget _errorTimeout() {
    return Column(
      children: <Widget>[
        Text(
          "It took a little longer than expected to load RYDR...",
          style: TextStyle(
            color: Colors.white,
          ),
        ),
        SizedBox(height: 16),
        _retryButton(),
      ],
    );
  }

  Widget _errorServer() {
    return Column(
      children: <Widget>[
        Text(
          "We were unable to load your profile...",
          style: TextStyle(
            color: Colors.white,
          ),
        ),
        SizedBox(height: 16),
        _retryButton(),
      ],
    );
  }

  Widget _retryButton() {
    return SecondaryButton(
      fullWidth: true,
      label: "Retry",
      onTap: () => _bloc.initUser(),
    );
  }
}

class LoadingLogo extends StatelessWidget {
  final double radius;
  final Color color;
  final bool background;

  LoadingLogo({
    @required this.radius,
    this.color,
    this.background = false,
  });

  @override
  Widget build(BuildContext context) {
    return Stack(
      alignment: Alignment.center,
      children: <Widget>[
        Container(
          width: radius,
          height: radius,
          decoration: BoxDecoration(
            color: background
                ? Theme.of(context).scaffoldBackgroundColor.withOpacity(0.5)
                : Colors.transparent,
            borderRadius: BorderRadius.circular(100.0),
          ),
          child: CircularProgressIndicator(
            strokeWidth: 1.0,
            valueColor: AlwaysStoppedAnimation<Color>(color == null
                ? Theme.of(context).chipTheme.backgroundColor
                : color),
          ),
        )
      ],
    );
  }
}
