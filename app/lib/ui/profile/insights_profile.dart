import 'dart:async';

import 'package:flutter/material.dart';
import 'package:rydr_app/models/responses/publisher_insights_growth.dart';
import 'package:rydr_app/ui/profile/blocs/insights_profile.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/ui/profile/widgets/insights_profile_imp_reach.dart';
import 'package:rydr_app/ui/profile/widgets/insights_profile_views.dart';
import 'package:rydr_app/ui/profile/widgets/insights_profile_clicks.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

class ProfileInteractionsDiscovery extends StatefulWidget {
  final PublisherAccount profile;

  ProfileInteractionsDiscovery(this.profile);

  @override
  _ProfileInteractionsDiscoveryState createState() =>
      _ProfileInteractionsDiscoveryState();
}

class _ProfileInteractionsDiscoveryState
    extends State<ProfileInteractionsDiscovery>
    with AutomaticKeepAliveClientMixin {
  final InsightsProfileBloc _bloc = InsightsProfileBloc();

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();

    _bloc.loadData(widget.profile);
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  Future<void> _refresh() async => _bloc.loadData(widget.profile, true);

  @override
  Widget build(BuildContext context) {
    super.build(context);

    return Scaffold(
      appBar: AppBar(
        leading: AppBarBackButton(context),
        title: Text('Profile Interactions and Discovery'),
      ),
      body: StreamBuilder<PublisherInsightsGrowthResponse>(
        stream: _bloc.growthResponse,
        builder: (context, snapshot) {
          final bool isLoading =
              snapshot.connectionState == ConnectionState.waiting;
          final PublisherInsightsGrowthResponse growthResponse = snapshot.data;

          return growthResponse?.error == null
              ? RefreshIndicator(
                  displacement: 0.0,
                  backgroundColor: Theme.of(context).appBarTheme.color,
                  color: Theme.of(context).textTheme.bodyText2.color,
                  onRefresh: _refresh,
                  child: ListView(
                    children: <Widget>[
                      ProfileInsightsProfileImpReach(
                        profile: widget.profile,
                        growthResponse: growthResponse,
                        loading: isLoading,
                        bloc: _bloc,
                      ),
                      sectionDivider(context),
                      ProfileInsightsProfileViews(
                        profile: widget.profile,
                        growthResponse: growthResponse,
                        loading: isLoading,
                        bloc: _bloc,
                      ),
                      sectionDivider(context),
                      ProfileInsightsProfileClicks(
                        profile: widget.profile,
                        growthResponse: growthResponse,
                        loading: isLoading,
                        bloc: _bloc,
                      ),
                    ],
                  ),
                )
              : RetryError(
                  error: snapshot.data.error,
                  fullSize: true,
                  onRetry: () => _bloc.loadData(widget.profile),
                );
        },
      ),
    );
  }
}
