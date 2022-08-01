import 'package:flutter/material.dart';
import 'package:flutter/widgets.dart';
import 'package:rydrworkspaces/services/auth_service.dart';

class LoginPage extends StatefulWidget {
  @override
  _LoginPageState createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  @override
  Widget build(BuildContext context) {
    print('login page');
    return Scaffold(
      primary: false,
      appBar: null,
      body: Column(
        children: [
          Expanded(
            child: ListView(
              children: <Widget>[
                RaisedButton(
                  onPressed: () async {
                    await FacebookAuthService.tryAuthenticate();
                  },
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      SizedBox(width: 6.0),
                      Text(
                        'Continue with Facebook',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontWeight: FontWeight.w600,
                          fontSize: 20.0,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          )
        ],
      ),
    );
  }
}
