import 'dart:async';

import 'package:flutter/material.dart';
import 'package:rydr_app/ui/profile/blocs/insights_followers.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';

import 'package:rydr_app/ui/profile/widgets/insights_followers_locations.dart';
import 'package:rydr_app/ui/profile/widgets/insights_followers_growth.dart';
import 'package:rydr_app/ui/profile/widgets/insights_followers_age.dart';

class ProfileInsightsFollowers extends StatefulWidget {
  final PublisherAccount profile;
  final Deal deal;

  ProfileInsightsFollowers(this.profile, [this.deal]);

  @override
  _ProfileInsightsFollowersState createState() =>
      _ProfileInsightsFollowersState();
}

class _ProfileInsightsFollowersState extends State<ProfileInsightsFollowers> {
  InsightsFollowerBloc _bloc;
  bool refresh = false;

  @override
  void initState() {
    super.initState();

    _bloc = InsightsFollowerBloc(widget.profile, widget.deal);
    _bloc.load(false);
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  Future<void> _refresh() async => _bloc.load(true);

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          title: Text('Follower Insights'),
        ),
        body: RefreshIndicator(
          displacement: 0.0,
          backgroundColor: Theme.of(context).appBarTheme.color,
          color: Theme.of(context).textTheme.bodyText2.color,
          onRefresh: _refresh,
          child: ListView(
            children: <Widget>[
              ProfileInsightsFollowersGrowth(_bloc),
              sectionDivider(context),
              ProfileInsightsLocations(_bloc),
              sectionDivider(context),
              ProfileInsightsAgeAndGender(_bloc),
            ],
          ),
        ),
      );
}
