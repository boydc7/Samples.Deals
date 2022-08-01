import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/connect/connect_profile.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class ConnectFacebookModifyPage extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    final Widget dot = Padding(
      padding: EdgeInsets.symmetric(vertical: 4),
      child: Text(
        "∙",
        textAlign: TextAlign.center,
        style: Theme.of(context).textTheme.bodyText1.merge(
              TextStyle(
                color: Theme.of(context).hintColor,
              ),
            ),
      ),
    );

    Widget primaryText(String txt) => Text(
          txt,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodyText1.merge(
                TextStyle(
                  color: Theme.of(context).primaryColor,
                ),
              ),
        );

    return Scaffold(
      appBar: AppBar(
        leading: AppBarCloseButton(context),
        backgroundColor: Colors.transparent,
        elevation: 0,
      ),
      body: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          Container(
            padding: EdgeInsets.only(bottom: 16.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: <Widget>[
                SizedBox(height: kToolbarHeight),
                Stack(
                  overflow: Overflow.visible,
                  children: <Widget>[
                    Transform.translate(
                      offset: Offset(50, 0),
                      child: Transform.rotate(
                        angle: 0.261799,
                        child: SizedBox(
                          width: 80,
                          child: AspectRatio(
                            aspectRatio: 0.56,
                            child: Container(
                              decoration: BoxDecoration(
                                boxShadow: AppShadows.elevation[0],
                              ),
                              child: ClipRRect(
                                borderRadius: BorderRadius.circular(4),
                                child: Container(
                                  decoration: BoxDecoration(
                                      borderRadius: BorderRadius.circular(4),
                                      border: Border.all(
                                          color: Theme.of(context)
                                              .scaffoldBackgroundColor,
                                          width: 2),
                                      image: DecorationImage(
                                          image: AssetImage(
                                              'assets/facebook-3.png'))),
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                    SizedBox(
                      width: 80,
                      child: AspectRatio(
                        aspectRatio: 0.56,
                        child: Container(
                          decoration: BoxDecoration(
                            boxShadow: AppShadows.elevation[0],
                          ),
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(4),
                            child: Container(
                              decoration: BoxDecoration(
                                  borderRadius: BorderRadius.circular(4),
                                  border: Border.all(
                                      color: Theme.of(context)
                                          .scaffoldBackgroundColor,
                                      width: 2),
                                  image: DecorationImage(
                                      image:
                                          AssetImage('assets/facebook-2.png'))),
                            ),
                          ),
                        ),
                      ),
                    ),
                    Transform.translate(
                      offset: Offset(-50, 0),
                      child: Transform.rotate(
                        angle: -0.261799,
                        child: SizedBox(
                          width: 80,
                          child: AspectRatio(
                            aspectRatio: 0.56,
                            child: Container(
                              decoration: BoxDecoration(
                                boxShadow: AppShadows.elevation[0],
                              ),
                              child: ClipRRect(
                                borderRadius: BorderRadius.circular(4),
                                child: Container(
                                  decoration: BoxDecoration(
                                      borderRadius: BorderRadius.circular(4),
                                      border: Border.all(
                                          color: Theme.of(context)
                                              .scaffoldBackgroundColor,
                                          width: 2),
                                      image: DecorationImage(
                                          image: AssetImage(
                                              'assets/facebook-1.png'))),
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
                SizedBox(height: 32),
                Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16),
                  child: Column(
                    children: <Widget>[
                      Text(
                        "Did you forget to add an\nInstagram account?",
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.headline6.merge(
                            TextStyle(
                                color: dark
                                    ? Colors.white
                                    : Theme.of(context)
                                        .textTheme
                                        .headline6
                                        .color)),
                      ),
                      SizedBox(height: 8),
                      Text(
                        "We understand, it happens all the time. Follow these steps carefully and you'll be all set.",
                        textAlign: TextAlign.center,
                      ),
                    ],
                  ),
                ),
                Expanded(
                    child: Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      primaryText("Continue with Facebook"),
                      dot,
                      primaryText("Edit Settings"),
                      dot,
                      primaryText("Select NEW Instagram Business Pages"),
                      Text(
                        "Removing pages will result in lost data",
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.caption.merge(
                              TextStyle(
                                color: AppColors.errorRed,
                              ),
                            ),
                      ),
                      dot,
                      primaryText("Select NEW corresponding Facebook Pages"),
                      Text(
                        "These should be the same as the previous step",
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.caption.merge(
                              TextStyle(
                                color: Theme.of(context).hintColor,
                              ),
                            ),
                      ),
                      dot,
                      primaryText("Done!"),
                    ],
                  ),
                )),
                SafeArea(
                  bottom: true,
                  top: false,
                  child: Column(
                    children: <Widget>[
                      Padding(
                        padding: EdgeInsets.symmetric(horizontal: 16.0),
                        child: Container(
                          width: double.infinity,
                          child: InkWell(
                            onTap: () =>
                                Navigator.of(context).push(MaterialPageRoute(
                                    builder: (context) => ConnectProfilePage(
                                          switchFacebookAcount: true,
                                        ))),
                            child: Container(
                              decoration: BoxDecoration(
                                borderRadius: BorderRadius.circular(4.0),
                                boxShadow: AppShadows.elevation[0],
                                color: Color(0xff4267B2),
                              ),
                              padding: EdgeInsets.symmetric(
                                  horizontal: 16.0, vertical: 8.5),
                              child: Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: <Widget>[
                                  Icon(
                                    AppIcons.facebook,
                                    size: 16.0,
                                    color: Colors.white,
                                  ),
                                  SizedBox(width: 6.0),
                                  Text(
                                    'Continue with Facebook',
                                    textAlign: TextAlign.center,
                                    style: TextStyle(
                                        fontWeight: FontWeight.w600,
                                        fontSize: 16.0,
                                        color: Colors.white),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                )
              ],
            ),
          ),
        ],
      ),
    );
  }
}
