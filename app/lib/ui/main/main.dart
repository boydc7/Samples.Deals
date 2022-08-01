import 'dart:async';
import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/ui/main/blocs/home.dart';

import 'package:rydr_app/ui/main/list_deals.dart';
import 'package:rydr_app/ui/main/list_requests.dart';
import 'package:rydr_app/ui/main/widgets/home.dart';
import 'package:rydr_app/ui/main/widgets/profile_button.dart';

class MainPage extends StatefulWidget {
  final ListPageArguments arguments;

  MainPage({
    this.arguments,
  });

  @override
  _MainPageState createState() => _MainPageState();
}

class _MainPageState extends State<MainPage>
    with SingleTickerProviderStateMixin {
  final GlobalKey<ListDealsState> listActiveKey = GlobalKey();
  final GlobalKey<ListRequestsState> listRequestsKey = GlobalKey();
  final HomeBloc _bloc = HomeBloc();

  TabController _tabController;

  ListPageArguments _requestArgs = ListPageArguments(
    layoutType: ListPageLayout.Integrated,
    filterRequestStatus: [
      DealRequestStatus.requested,
      DealRequestStatus.invited,
      DealRequestStatus.inProgress,
      DealRequestStatus.redeemed,
      DealRequestStatus.completed,
    ],
  );

  ListPageArguments _dealArgs = ListPageArguments(
    layoutType: ListPageLayout.Integrated,
    isRequests: false,
    sortDeals: DealSort.newest,
    filterDealStatus: [DealStatus.published],
  );

  @override
  void initState() {
    super.initState();

    /// if we have a deep link in appState then process it
    /// as this is the main / entry page of a business for when the app starts
    /// and we'd be sending them here after they'd tap a deep link and the app was started
    if (appState.deepLink != null) {
      Future.delayed(Duration(seconds: 1), () {
        appState.processDeepLink();
      });
    }

    /// initialize the tab controller and set the initial tab to the one
    /// assigned to the incoming filter arguments (defaults to 0 if we have none)
    _tabController = TabController(
      vsync: this,
      initialIndex: widget.arguments?.mainScaffoldTab ?? 0,
      length: 3,
    );

    _bloc.setTab(widget.arguments?.mainScaffoldTab ?? 0);

    /// listen to changes on the tab controller and update state
    /// which will update things like the appbar title and others
    _tabController.addListener(() {
      /// once index has changed we can log this as a screen view in analytics
      if (!_tabController.indexIsChanging) {
        _bloc.setTab(_tabController.index);

        AppAnalytics.instance.logScreen(_tabController.index == 0
            ? 'home'
            : _tabController.index == 1 ? 'deals' : 'requests');
      }
    });
  }

  @override
  void dispose() {
    _bloc.dispose();
    _tabController.dispose();
    super.dispose();
  }

  void _handleTap(int page) {
    _tabController.animateTo(
      page,
      duration: Duration(milliseconds: 300),
      curve: Curves.fastOutSlowIn,
    );

    _bloc.setTab(page);
  }

  void _handleFilterDeals() {
    _handleTap(1);
  }

  void _handleFilterRequests(DealRequestStatus requestStatus) {
    if (listRequestsKey.currentState != null) {
      _requestArgs = ListPageArguments(
          layoutType: ListPageLayout.Integrated,
          filterRequestStatus: [requestStatus]);
      _handleTap(2);

      listRequestsKey.currentState.applyFilters("", _requestArgs);
    } else {
      if (requestStatus == DealRequestStatus.requested) {
        Navigator.of(context).pushNamed(AppRouting.getRequestsPending);
      } else if (requestStatus == DealRequestStatus.invited) {
        Navigator.of(context).pushNamed(AppRouting.getRequestsInvited);
      } else if (requestStatus == DealRequestStatus.inProgress) {
        Navigator.of(context).pushNamed(AppRouting.getRequestsInProgress);
      } else if (requestStatus == DealRequestStatus.redeemed) {
        Navigator.of(context).pushNamed(AppRouting.getRequestsRedeemed);
      } else if (requestStatus == DealRequestStatus.completed) {
        Navigator.of(context).pushNamed(AppRouting.getRequestsCompleted);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      extendBody: true,
      appBar: PreferredSize(
        child: StreamBuilder<int>(
          stream: _bloc.tabIndex,
          builder: (context, snapshot) {
            return AppBar(
              automaticallyImplyLeading: false,
              title: Text(snapshot.data == 0
                  ? "${appState.currentProfile.userName} Insights"
                  : snapshot.data == 1 ? "Marketplace" : "Your RYDRs"),
              elevation:
                  snapshot.data == 0 || snapshot.data == null ? 1.0 : 0.0,
              actions: <Widget>[ProfileButton()],
            );
          },
        ),
        preferredSize: Size.fromHeight(kToolbarHeight),
      ),
      body: TabBarView(
        controller: _tabController,
        children: <Widget>[
          Home(_handleFilterDeals, _handleFilterRequests),
          ListDeals(key: listActiveKey, arguments: _dealArgs),
          ListRequests(key: listRequestsKey, arguments: _requestArgs),
        ],
      ),
      bottomNavigationBar: StreamBuilder<int>(
        stream: _bloc.tabIndex,
        builder: (context, snapshot) =>
            BottomNav(snapshot.data ?? 0, _handleTap, _tabController),
      ),
    );
  }
}

class BottomNav extends StatelessWidget {
  final int currentIndex;
  final Function onTap;
  final TabController controller;

  BottomNav(this.currentIndex, this.onTap, this.controller);

  @override
  Widget build(BuildContext context) {
    bool dark = Theme.of(context).brightness == Brightness.dark;
    return BottomAppBar(
      child: TabBar(
        labelColor: Theme.of(context).tabBarTheme.labelColor,
        unselectedLabelColor:
            Theme.of(context).tabBarTheme.unselectedLabelColor,
        controller: controller,
        indicator: UnderlineTabIndicator(
          borderSide: BorderSide(
              color: dark
                  ? AppColors.white.withOpacity(0.87)
                  : Theme.of(context).textTheme.bodyText2.color,
              width: 1.4),
          insets: EdgeInsets.fromLTRB(0.0, 0.0, 0.0, kMinInteractiveDimension),
        ),
        tabs: [
          Tab(text: "Insights"),
          Tab(text: "Marketplace"),
          Tab(text: "RYDRs"),
        ],
      ),
    );
  }
}
