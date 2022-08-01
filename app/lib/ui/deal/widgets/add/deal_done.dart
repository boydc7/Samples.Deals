import 'package:flutter/material.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/ui/deal/blocs/add_deal.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:rydr_app/ui/deal/utils.dart';

class DealAddDone extends StatelessWidget {
  final DealAddBloc bloc;

  DealAddDone(this.bloc);

  @override
  Widget build(BuildContext context) {
    /// remove focus from previous screen
    FocusScope.of(context).requestFocus(FocusNode());

    final Size size = MediaQuery.of(context).size;
    final double referenceCircle = size.width - 32;
    final String marketplaceActiveUrl = 'assets/icons/marketplace-active.svg';
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: SafeArea(
        bottom: true,
        top: true,
        child: Container(
          padding: EdgeInsets.only(left: 16, right: 16, bottom: 16.0),
          child: Column(
            children: <Widget>[
              Expanded(
                flex: 2,
                child: Stack(
                  alignment: Alignment.center,
                  children: <Widget>[
                    Container(
                      width: size.width,
                      child: Stack(
                        overflow: Overflow.visible,
                        alignment: Alignment.center,
                        children: <Widget>[
                          Container(
                            width: referenceCircle,
                            height: referenceCircle,
                            decoration: BoxDecoration(
                                color: AppColors.grey300.withOpacity(0.05),
                                borderRadius: BorderRadius.circular(
                                    (referenceCircle) / 2)),
                          ),
                          Container(
                            width: referenceCircle * 0.75,
                            height: referenceCircle * 0.75,
                            decoration: BoxDecoration(
                                color: AppColors.grey300.withOpacity(0.05),
                                borderRadius: BorderRadius.circular(
                                    (referenceCircle * 0.75) / 2)),
                          ),
                          Container(
                            width: referenceCircle * 0.5,
                            height: referenceCircle * 0.5,
                            decoration: BoxDecoration(
                                color: AppColors.grey300.withOpacity(0.05),
                                borderRadius: BorderRadius.circular(
                                    (referenceCircle * 0.5) / 2)),
                          ),
                          Container(
                            width: referenceCircle * 0.25,
                            height: referenceCircle * 0.25,
                            decoration: BoxDecoration(
                                color: AppColors.grey300.withOpacity(0.05),
                                borderRadius: BorderRadius.circular(
                                    (referenceCircle * 0.25) / 2)),
                          ),
                        ],
                      ),
                    ),
                    Container(
                      color: Colors.transparent,
                      width: size.width,
                      child: Stack(
                        alignment: Alignment.center,
                        children: <Widget>[
                          FadeInRightLeft(
                              5,
                              Container(
                                width: 110,
                                height: 110,
                                margin: EdgeInsets.only(left: 88.0),
                                decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(100),
                                    boxShadow: AppShadows.elevation[0]),
                                child: Center(
                                  child: Stack(
                                    alignment: Alignment.center,
                                    children: <Widget>[
                                      SizedBox(
                                        height: 60,
                                        width: 60,
                                        child: SvgPicture.asset(
                                            marketplaceActiveUrl,
                                            color:
                                                Theme.of(context).primaryColor),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                              1000),
                          FadeInLeftRight(
                              5,
                              Container(
                                width: 110,
                                height: 110,
                                margin: EdgeInsets.only(right: 88.0),
                                child: UserAvatar(
                                  appState.currentProfile,
                                  width: 110,
                                ),
                                decoration: BoxDecoration(
                                    borderRadius: BorderRadius.circular(100),
                                    boxShadow: AppShadows.elevation[0]),
                              ),
                              1000)
                        ],
                      ),
                    )
                  ],
                ),
              ),
              Expanded(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Text('Published!',
                        style: Theme.of(context).textTheme.headline4),
                    SizedBox(height: 8.0),
                    Padding(
                      padding: EdgeInsets.symmetric(horizontal: 16.0),
                      child: Text(
                          'Your RYDR is now live in our Marketplace. We will notify you when creators start requesting.',
                          textAlign: TextAlign.center,
                          style: Theme.of(context).textTheme.bodyText2),
                    ),
                  ],
                ),
              ),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                children: <Widget>[
                  Column(
                    children: <Widget>[
                      IconButton(
                        highlightColor: Colors.transparent,

                        /// NOTE! once we want to support events we'll want to send the user
                        /// to interstitial page for choosing deal type here...
                        onPressed: () => Navigator.of(context)
                            .pushNamedAndRemoveUntil(AppRouting.getDealAddDeal,
                                (Route<dynamic> route) => false),
                        icon: Icon(
                          AppIcons.plus,
                          color: dark ? AppColors.white : AppColors.grey800,
                        ),
                      ),
                      Text('new', style: Theme.of(context).textTheme.caption)
                    ],
                  ),
                  !bloc.deal.isPrivateDeal
                      ? Column(
                          children: <Widget>[
                            IconButton(
                              highlightColor: Colors.transparent,
                              onPressed: () =>
                                  showDealShare(context, bloc.deal),
                              icon: Icon(AppIcons.share,
                                  color: Theme.of(context).primaryColor),
                            ),
                            Text('share',
                                style: Theme.of(context)
                                    .textTheme
                                    .caption
                                    .merge(TextStyle(
                                        color: Theme.of(context).primaryColor)))
                          ],
                        )
                      : Container(),
                  Column(
                    children: <Widget>[
                      IconButton(
                        highlightColor: Colors.transparent,
                        onPressed: () => Navigator.of(context)
                            .pushNamedAndRemoveUntil(AppRouting.getDealsActive,
                                (Route<dynamic> route) => false),
                        icon: Icon(AppIcons.check,
                            color: dark ? AppColors.white : AppColors.grey800),
                      ),
                      Text('done', style: Theme.of(context).textTheme.caption)
                    ],
                  )
                ],
              )
            ],
          ),
        ),
      ),
    );
  }
}
