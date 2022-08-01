import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/deal_request.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/deals.dart';
import 'package:rydr_app/models/responses/publisher_account_stats.dart';
import 'package:rydr_app/ui/main/blocs/list.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/ui/shared/widgets/list_helper.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/main/widgets/list_noresults.dart';
import 'package:rydr_app/ui/main/widgets/list_request.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';
import 'package:rydr_app/models/list_page_arguments.dart';

const int pageSize = 25;

class ListRequests extends StatefulWidget {
  final ListPageArguments arguments;

  ListRequests({
    Key key,
    this.arguments,
  }) : super(key: key);

  @override
  ListRequestsState createState() => ListRequestsState();
}

class ListRequestsState extends State<ListRequests>
    with AutomaticKeepAliveClientMixin {
  final _bloc = ListBloc();
  final _scrollController = ScrollController();
  final _filterController = ScrollController();

  StreamSubscription _onRequestUpdated;
  ListHelper listHelper = ListHelper();
  ListPageArguments filterArguments;

  ThemeData _theme;
  bool _darkMode;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();

    /// make a copy of incoming arguments so we can modify them locally
    filterArguments = widget.arguments ?? ListPageArguments();

    _scrollController.addListener(_onScroll);

    _bloc.loadList(filterArguments, reset: true);

    _onRequestUpdated = appState.updatedRequest.listen(
        (DealRequestChange change) => _bloc.handleRequestChanged(change));
  }

  @override
  void dispose() {
    _bloc.dispose();
    _scrollController.dispose();
    _onRequestUpdated?.cancel();

    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.offset >=
            _scrollController.position.maxScrollExtent &&
        _bloc.hasMore &&
        !_bloc.isLoading) {
      _bloc.loadList(filterArguments);
    }
  }

  void applyFilters(String title, ListPageArguments args) {
    filterArguments = args;

    _scrollController.jumpTo(0);

    /// if we have a status past 'invited' then scroll the listview a bit
    /// to ensure the selected filter is now in view on the UI, otherwise move to front
    final double animateTo = args.filterRequestStatus.any((s) =>
                s == DealRequestStatus.inProgress ||
                s == DealRequestStatus.redeemed ||
                s == DealRequestStatus.completed) &&
            args.filterRequestStatus.length == 1
        ? 150
        : 0;

    Future.delayed(
        const Duration(milliseconds: 250),
        () => _filterController.animateTo(animateTo,
            duration: Duration(milliseconds: 250), curve: Curves.easeIn));

    _bloc.loadList(filterArguments, reset: true);
  }

  /// NOTE: purpusefully not sending the already loaded deal/request along with the route
  /// so that we're guaranteed to load a fresh copy of the request, mainly to account for
  /// chagnes on copmleted requests to correctly reflect the completion stats/images
  void goToRequest(Deal deal, String route) =>
      Navigator.of(context).pushNamed(route);

  @override
  Widget build(BuildContext context) {
    super.build(context);

    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;

    /// NOTE: if we use filterArguments vs. widget.arguments where for some reason
    /// the layout prop is reset and does not persit... not sure why
    return widget.arguments.layoutType == ListPageLayout.StandAlone
        ? _buildStandAlone()
        : widget.arguments.layoutType == ListPageLayout.Integrated
            ? _buildIntegrated()
            : _buildInjected();
  }

  Widget _buildBody() => StreamBuilder<DealsResponse>(
        stream: _bloc.dealsResponse,
        builder: (context, snapshot) => snapshot.data == null
            ? _buildLoading()
            : snapshot.data != null && snapshot.data.error == null
                ? _buildList(snapshot.data)
                : _buildError(snapshot.data),
      );

  Widget _buildLoading() => ListView(
        children: <Widget>[
          Padding(padding: EdgeInsets.all(16), child: LoadingListShimmer())
        ],
      );

  Widget _buildError(DealsResponse res) => Padding(
      padding: EdgeInsets.all(16),
      child: RetryError(
        error: res.error,
        onRetry: () => _bloc.loadList(filterArguments, reset: true),
      ));

  Widget _buildNoResults() => ListNoResults(filterArguments);

  Widget _buildStandAlone() => Scaffold(
        appBar: _buildStandAloneAppBar(),
        body: Stack(
          children: <Widget>[_buildBody(), _buildBottomBar()],
        ),
      );

  Widget _buildIntegrated() => NestedScrollView(
        headerSliverBuilder: (context, innerBoxScrolled) =>
            [_buildIntegratedAppBar()],
        body: _buildBody(),
      );

  Widget _buildInjected() => NestedScrollView(
        headerSliverBuilder: (context, innerBoxScrolled) =>
            [_buildInjectedAppBar()],
        body: _buildBody(),
      );

  Widget _buildList(DealsResponse res) => RefreshIndicator(
        displacement: 0.0,
        backgroundColor: _theme.appBarTheme.color,
        color: _theme.textTheme.bodyText2.color,
        onRefresh: () => _bloc.loadList(
          filterArguments,
          reset: true,
          forceRefresh: true,
        ),
        child: res.models.isEmpty
            ? ListView(
                controller: _scrollController,
                children: <Widget>[_buildNoResults()])
            : ListView.builder(
                controller: _scrollController,
                padding: EdgeInsets.only(
                    bottom:
                        kToolbarHeight + MediaQuery.of(context).padding.bottom),
                physics: AlwaysScrollableScrollPhysics(),
                itemCount: res.models.length,
                itemBuilder: (BuildContext context, int index) {
                  final Deal deal = res.models[index];
                  final Deal lastDeal =
                      index > 0 ? res.models[index - 1] : null;

                  final DateTime lastChange = lastDeal != null &&
                          lastDeal.request.statusChanges.length > 0
                      ? lastDeal.request.lastStatusChange.occurredOnDateTime
                      : lastDeal != null ? lastDeal.request.requestedOn : null;
                  final DateTime currentChange =
                      deal.request.statusChanges.length > 0
                          ? deal.request.lastStatusChange.occurredOnDateTime
                          : deal.request.requestedOn;

                  return Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      listHelper.addDateHeader(
                          context, lastChange, currentChange, index),
                      ListRequest(deal, goToRequest),
                    ],
                  );
                }),
      );

  Widget _buildBottomBar() {
    if (appState.currentProfile.isBusiness ||
        filterArguments.layoutType != ListPageLayout.StandAlone) {
      return Container();
    }

    final int delinquentLimit = appState.currentProfile.maxDelinquent;

    Widget _badge(int index, int queue) => Expanded(
          child: Container(
            margin: EdgeInsets.only(right: 2),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(4.0),
              child: Container(
                height: 6.0,
                color: index < queue ? _theme.errorColor : _theme.hintColor,
              ),
            ),
          ),
        );

    Widget _icon(int index, int queue) => Container(
          width: 60,
          height: 60,
          margin: EdgeInsets.symmetric(horizontal: 4),
          child: Center(
            child: Stack(
              alignment: Alignment.center,
              children: <Widget>[
                Icon(
                  index < queue ? AppIcons.timesCircle : AppIcons.circle,
                  size: 40,
                  color: index < queue ? _theme.errorColor : _theme.hintColor,
                ),
                index < queue
                    ? Container()
                    : SizedBox(
                        height: 18.0,
                        child: SvgPicture.asset(
                          'assets/icons/rydr-r.svg',
                          color: _theme.hintColor,
                        ),
                      ),
              ],
            ),
          ),
        );

    if (filterArguments.filterRequestStatus != null &&
        filterArguments.filterRequestStatus
            .contains(DealRequestStatus.redeemed)) {
      return StreamBuilder<PublisherAccountStatsResponse>(
        stream: appState.currentProfileStats,
        builder: (context, snapshot) {
          final int currentDelinquent = snapshot.data == null
              ? 0
              : snapshot.data.model
                  .tryGetDealStatValue(DealStatType.currentDelinquent);

          return currentDelinquent == 0
              ? Container()
              : SafeArea(
                  top: true,
                  bottom: false,
                  child: SizedBox.expand(
                    child: DraggableScrollableSheet(
                      initialChildSize: 0.2,
                      minChildSize: 0.2,
                      maxChildSize: 0.5,
                      builder: (BuildContext context,
                          ScrollController scrollController) {
                        final Size size = MediaQuery.of(context).size;
                        final double pos = scrollController?.hasClients == false
                            ? 1.0
                            : (size.height /
                                scrollController?.position?.viewportDimension);

                        final bool showDetail =
                            scrollController?.hasClients == false
                                ? false
                                : pos < 4.5 ? true : false;
                        final double topOpacity =
                            scrollController?.hasClients == false
                                ? 1.0
                                : pos < 1.12 ? 0.0 : 1.0;
                        final double bottomOpacity =
                            scrollController?.hasClients == false
                                ? 1.0
                                : pos > 7 ? 0.0 : 1.0;

                        return Container(
                          decoration: BoxDecoration(
                            color: _darkMode
                                ? _theme.canvasColor
                                : _theme.appBarTheme.color,
                            borderRadius: BorderRadius.only(
                              topLeft: Radius.circular(16),
                              topRight: Radius.circular(16),
                            ),
                            boxShadow: AppShadows.elevation[1],
                          ),
                          child: ListView(
                            padding: EdgeInsets.only(top: 12),
                            controller: scrollController,
                            children: <Widget>[
                              AnimatedOpacity(
                                duration: Duration(milliseconds: 250),
                                opacity: topOpacity,
                                child: Container(
                                  margin: EdgeInsets.only(bottom: 8),
                                  alignment: Alignment.center,
                                  child: SizedBox(
                                    height: 4.0,
                                    width: 40.0,
                                    child: Container(
                                      decoration: BoxDecoration(
                                        color: _darkMode
                                            ? _theme.hintColor
                                            : _theme.canvasColor,
                                        borderRadius:
                                            BorderRadius.circular(8.0),
                                      ),
                                    ),
                                  ),
                                ),
                              ),
                              GestureDetector(
                                onTap: () => Navigator.of(context).pushNamed(
                                    AppRouting.getRequestsDelinquent),
                                child: Padding(
                                  padding:
                                      EdgeInsets.only(left: 16.0, right: 16.0),
                                  child: Column(
                                    mainAxisSize: MainAxisSize.min,
                                    children: <Widget>[
                                      AnimatedSwitcher(
                                        duration: Duration(milliseconds: 350),
                                        child: showDetail
                                            ? Padding(
                                                padding: EdgeInsets.only(
                                                    top: 16.0, bottom: 6),
                                                child: Text(
                                                  "You are only allowed\n$delinquentLimit incomplete RYDRs.",
                                                  style: _theme
                                                      .textTheme.headline6,
                                                  textAlign: TextAlign.center,
                                                ),
                                              )
                                            : Text(
                                                "Incomplete RYDRs",
                                                style:
                                                    _theme.textTheme.bodyText1,
                                              ),
                                      ),
                                      AnimatedSwitcher(
                                        duration: Duration(milliseconds: 350),
                                        child: showDetail
                                            ? Padding(
                                                padding: EdgeInsets.only(
                                                    bottom: 16.0, top: 8),
                                                child: Text(
                                                  "The below red RYDRs have been marked delinquent.",
                                                  style: _theme
                                                      .textTheme.caption
                                                      .merge(
                                                    TextStyle(
                                                        color:
                                                            _theme.hintColor),
                                                  ),
                                                ),
                                              )
                                            : Text(
                                                "Marked as redeemed, but never completed.",
                                                style: _theme.textTheme.caption
                                                    .merge(
                                                  TextStyle(
                                                      color: _theme.hintColor),
                                                ),
                                              ),
                                      ),
                                      SizedBox(height: 16.0),
                                      AnimatedOpacity(
                                        duration: Duration(milliseconds: 250),
                                        opacity: bottomOpacity,
                                        child: Row(
                                          children: <Widget>[
                                            Expanded(
                                              child: Column(
                                                children: <Widget>[
                                                  AnimatedSwitcher(
                                                    duration: Duration(
                                                        milliseconds: 350),
                                                    child: showDetail
                                                        ? Padding(
                                                            padding: EdgeInsets
                                                                .symmetric(
                                                                    horizontal:
                                                                        16),
                                                            child: Wrap(
                                                              runAlignment:
                                                                  WrapAlignment
                                                                      .center,
                                                              children:
                                                                  List.generate(
                                                                delinquentLimit,
                                                                (index) => _icon(
                                                                    index,
                                                                    currentDelinquent),
                                                              ),
                                                            ),
                                                          )
                                                        : Row(
                                                            children:
                                                                List.generate(
                                                              delinquentLimit,
                                                              (index) => _badge(
                                                                  index,
                                                                  currentDelinquent),
                                                            ),
                                                          ),
                                                  ),
                                                  showDetail
                                                      ? Padding(
                                                          padding:
                                                              EdgeInsets.only(
                                                                  top: 32),
                                                          child: Column(
                                                            mainAxisSize:
                                                                MainAxisSize
                                                                    .min,
                                                            crossAxisAlignment:
                                                                CrossAxisAlignment
                                                                    .center,
                                                            children: <Widget>[
                                                              SecondaryButton(
                                                                label:
                                                                    "Contest a Delinquent RYDR",
                                                                onTap: () {
                                                                  Utils.launchUrl(
                                                                      context,
                                                                      Uri.encodeFull(
                                                                          "mailto:contest@getrydr.com?subject=Contesting Delinquent RYDR: ${appState.currentProfile.userName} (${appState.currentProfile.id})&body=Please give a reason for your delinquent RYDR..."));
                                                                },
                                                              ),
                                                              SizedBox(
                                                                  height: 8),
                                                              Text(
                                                                "or email contest@getrydr.com",
                                                                textAlign:
                                                                    TextAlign
                                                                        .center,
                                                                style: Theme.of(
                                                                        context)
                                                                    .textTheme
                                                                    .caption
                                                                    .merge(
                                                                      TextStyle(
                                                                        fontSize:
                                                                            10,
                                                                      ),
                                                                    ),
                                                              ),
                                                            ],
                                                          ),
                                                        )
                                                      : Padding(
                                                          padding:
                                                              EdgeInsets.only(
                                                                  top: 16.0),
                                                          child: Text(
                                                            "You are only allowed $delinquentLimit incomplete RYDRs.",
                                                            style: _theme
                                                                .textTheme
                                                                .caption
                                                                .merge(
                                                              TextStyle(
                                                                  color: _theme
                                                                      .errorColor),
                                                            ),
                                                          ),
                                                        ),
                                                ],
                                              ),
                                            ),
                                          ],
                                        ),
                                      ),
                                      SizedBox(height: 12.0),
                                    ],
                                  ),
                                ),
                              ),
                            ],
                          ),
                        );
                      },
                    ),
                  ),
                );
        },
      );
    } else {
      return Container();
    }
  }

  Widget _buildStandAloneAppBar() {
    /// translate status filters from the filter arguments into a comma-separate list
    /// of what they represent in a display friendly manner
    final String requestStatusFilters =
        filterArguments != null && filterArguments.filterRequestStatus != null
            ? filterArguments.filterRequestStatus
                .map((status) => dealRequestStatusToStringDisplay(status))
                .join(', ')
            : "";

    final String titleSuffix = widget.arguments.isCreatorHistory
        ? ""
        : requestStatusFilters == "" ? "Requests" : "";

    final String titleHistory =
        "History with ${widget.arguments.filterDealPublisherAccountName}";

    final String titleLabel = filterArguments == null
        ? ""
        : filterArguments.isCreatorHistory != false
            ? titleHistory
            : filterArguments.filterRequestStatus != null
                ? requestStatusFilters
                : filterArguments.filterDealName != null
                    ? filterArguments.filterDealName
                    : filterArguments.filterDealPublisherAccountName != null
                        ? filterArguments.filterDealPublisherAccountName
                        : "";

    return AppBar(
      leading: AppBarBackButton(context),
      title: titleLabel == "In-Progress"
          ? Text("Approved RYDRs")
          : titleLabel == "Redeemed"
              ? Column(
                  mainAxisSize: MainAxisSize.min,
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Text("Action Required"),
                    Text(
                      "Redeemed but not complete",
                      style: _theme.textTheme.caption.merge(
                        TextStyle(color: _theme.hintColor),
                      ),
                    ),
                  ],
                )
              : Text(titleLabel + " " + titleSuffix),
    );
  }

  Widget _buildIntegratedAppBar() {
    return SliverAppBar(
      forceElevated: true,
      floating: true,
      elevation: 1.0,
      pinned: true,
      snap: true,
      centerTitle: true,
      titleSpacing: 0,
      automaticallyImplyLeading: false,
      backgroundColor: _theme.appBarTheme.color,
      title: Container(
        height: kToolbarHeight,
        child: Container(
          height: kToolbarHeight,
          child: Center(child: _buildChips()),
        ),
      ),
    );
  }

  Widget _buildInjectedAppBar() {
    return SliverAppBar(
      forceElevated: true,
      floating: true,
      snap: true,
      elevation: 0,
      centerTitle: true,
      titleSpacing: 0,
      automaticallyImplyLeading: false,
      backgroundColor: _theme.scaffoldBackgroundColor,
      title: Container(
        height: kToolbarHeight,
        child: Container(
          height: kToolbarHeight,
          child: Center(child: _buildChips()),
        ),
      ),
    );
  }

  Widget _buildChips() => StreamBuilder<ListPageArguments>(
      stream: _bloc.filterArgs,
      builder: (context, snapshot) {
        final ListPageArguments args = snapshot.data ?? ListPageArguments();

        return ListView(
            padding: EdgeInsets.symmetric(horizontal: 16),
            physics: AlwaysScrollableScrollPhysics(),
            scrollDirection: Axis.horizontal,
            controller: _filterController,
            children: [
              _buildChip(
                  "All",
                  _areRequestStatusFiltersTheSame(args.filterRequestStatus, [
                    DealRequestStatus.requested,
                    DealRequestStatus.invited,
                    DealRequestStatus.inProgress,
                    DealRequestStatus.redeemed,
                    DealRequestStatus.completed,
                  ]),
                  ListPageArguments(filterRequestStatus: [
                    DealRequestStatus.requested,
                    DealRequestStatus.invited,
                    DealRequestStatus.inProgress,
                    DealRequestStatus.redeemed,
                    DealRequestStatus.completed,
                  ])),
              SizedBox(width: 8),
              _buildChip(
                  "Requested",
                  _areRequestStatusFiltersTheSame(
                      args.filterRequestStatus, [DealRequestStatus.requested]),
                  ListPageArguments(
                      filterRequestStatus: [DealRequestStatus.requested]),
                  DealRequestStatus.requested),
              SizedBox(width: 8),
              _buildChip(
                  "Invited",
                  _areRequestStatusFiltersTheSame(
                      args.filterRequestStatus, [DealRequestStatus.invited]),
                  ListPageArguments(
                      filterRequestStatus: [DealRequestStatus.invited]),
                  DealRequestStatus.invited),
              SizedBox(width: 8),
              _buildChip(
                  "In-Progress",
                  _areRequestStatusFiltersTheSame(
                      args.filterRequestStatus, [DealRequestStatus.inProgress]),
                  ListPageArguments(
                      filterRequestStatus: [DealRequestStatus.inProgress]),
                  DealRequestStatus.inProgress),
              SizedBox(width: 8),
              _buildChip(
                  "Redeemed",
                  _areRequestStatusFiltersTheSame(
                      args.filterRequestStatus, [DealRequestStatus.redeemed]),
                  ListPageArguments(
                      filterRequestStatus: [DealRequestStatus.redeemed]),
                  DealRequestStatus.redeemed),
              SizedBox(width: 8),
              _buildChip(
                  "Completed",
                  _areRequestStatusFiltersTheSame(
                      args.filterRequestStatus, [DealRequestStatus.completed]),
                  ListPageArguments(
                      filterRequestStatus: [DealRequestStatus.completed]),
                  DealRequestStatus.completed),
            ]);
      });

  Widget _buildChip(
    String label,
    bool isCurrent,
    ListPageArguments args, [
    DealRequestStatus status,
  ]) =>
      GestureDetector(
        child: Chip(
          backgroundColor: _theme.appBarTheme.color,
          shape: OutlineInputBorder(
            borderRadius: BorderRadius.circular(40),
            borderSide: BorderSide(
              width: 1.25,
              color: isCurrent
                  ? status == null
                      ? _theme.textTheme.bodyText1.color
                      : Utils.getRequestStatusColor(status, _darkMode)
                  : _theme.hintColor,
            ),
          ),
          label: Text(label),
          labelStyle: TextStyle(
            fontWeight: FontWeight.w500,
            color: isCurrent
                ? status == null
                    ? _theme.textTheme.bodyText1.color
                    : Utils.getRequestStatusColor(status, _darkMode)
                : _theme.hintColor,
          ),
        ),
        onTap: () => applyFilters(label, args),
      );

  /// string comparision of filter arrays
  bool _areRequestStatusFiltersTheSame(
    List<DealRequestStatus> currentFilter,
    List<DealRequestStatus> matchingFilter,
  ) =>
      currentFilter != null && matchingFilter != null
          ? currentFilter
                  .map((DealRequestStatus s) => dealRequestStatusToString(s))
                  .join('') ==
              matchingFilter
                  .map((DealRequestStatus s) => dealRequestStatusToString(s))
                  .join('')
          : false;
}
