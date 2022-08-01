import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/responses/deals.dart';
import 'package:rydr_app/ui/main/blocs/list.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/list_helper.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/main/widgets/list_noresults.dart';
import 'package:rydr_app/ui/main/widgets/list_deal.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';
import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/ui/deal/utils.dart';

const int pageSize = 25;

class ListDeals extends StatefulWidget {
  final ListPageArguments arguments;

  ListDeals({
    Key key,
    this.arguments,
  }) : super(key: key);

  @override
  ListDealsState createState() => ListDealsState();
}

class ListDealsState extends State<ListDeals>
    with AutomaticKeepAliveClientMixin {
  final _bloc = ListBloc();
  final _scrollController = ScrollController();

  ListHelper listHelper = ListHelper();
  ListPageArguments filterArguments;

  ThemeData _theme;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();

    /// make a copy of incoming arguments so we can modify them locally
    filterArguments = widget.arguments ?? ListPageArguments();

    _scrollController.addListener(_onScroll);

    _bloc.loadList(filterArguments, reset: true);
    _bloc.loadPlaces();
  }

  @override
  void dispose() {
    _bloc.dispose();
    _scrollController.dispose();

    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.offset > 10) {
      _bloc.setShowSmallFab(true);
    } else {
      _bloc.setShowSmallFab(false);
    }

    if (_scrollController.offset >=
            _scrollController.position.maxScrollExtent &&
        _bloc.hasMore &&
        !_bloc.isLoading) {
      _bloc.loadList(filterArguments);
    }
  }

  void _showLocations() {
    showSharedModalBottomInfo(context,
        title: "Filter by Location",
        initialRatio: 0.4,
        child: StreamBuilder<List<Place>>(
            stream: _bloc.places,
            builder: (context, snapshot) => snapshot.data == null ||
                    snapshot.data.isEmpty
                ? Container(child: Text("No locations are setup"))
                : Column(
                    children: snapshot.data
                        .map((Place p) => ListTile(
                              onTap: () => _filterByPlace(p),
                              title: Text(
                                p.name,
                                style:
                                    Theme.of(context).textTheme.bodyText1.merge(
                                          TextStyle(
                                              fontWeight: FontWeight.w500,
                                              color: Theme.of(context)
                                                  .textTheme
                                                  .bodyText2
                                                  .color),
                                        ),
                              ),
                              subtitle: p.address != null &&
                                      p.address.name != null
                                  ? Text(
                                      p.address.name,
                                      overflow: TextOverflow.ellipsis,
                                      style: TextStyle(
                                          color: Theme.of(context).hintColor),
                                    )
                                  : null,
                            ))
                        .toList())));
  }

  void _filterByPlace(Place place) {
    Navigator.of(context).pop();

    _applyFilters(
        place.name,
        ListPageArguments(
            isRequests: false,
            sortDeals: DealSort.closest,
            filterDealStatus: [DealStatus.published],
            filterDealPlaceId: place.id,
            filterDealPlaceName: place.name));
  }

  void _applyFilters(String title, ListPageArguments args) {
    filterArguments = args;

    _bloc.loadList(filterArguments, reset: true);

    /// jump the scroll controller back to the top of the list
    _scrollController.jumpTo(0.0);
  }

  /// when re-creating a deal we're using an expired deal, so before sending it to the add-deal page
  /// we'll extends the expiration date set on it to a date in the future

  /// NOTE! once we want to support events we'll want to send the user
  /// to interstitial page for choosing deal type here...
  void handleRecreate(Deal deal) => Navigator.of(context).pushNamed(
      AppRouting.getDealAddDeal,
      arguments: deal..expirationDate = DateTime.now().add(Duration(days: 14)));

  void handleReactivate(Deal deal) => showSharedModalAlert(
        context,
        Text("Reactivate RYDR"),
        content:
            Text("This will make your RYDR visible in the public marketplace."),
        actions: <ModalAlertAction>[
          ModalAlertAction(
              label: "Not Now", onPressed: () => Navigator.of(context).pop()),
          ModalAlertAction(
            label: "Reactivate",
            isDefaultAction: true,
            isDestructiveAction: false,
            onPressed: () {
              Navigator.of(context).pop();

              showSharedLoadingLogo(
                context,
                content: "Reactivating RYDR",
              );

              _bloc.reactivateDeal(deal).then((success) {
                Navigator.of(context).pop();

                if (!success) {
                  showSharedModalError(
                    context,
                    title: 'Unable to reactive this RYDR',
                    subTitle: 'Please try again in a few moments.',
                  );
                }
              });
            },
          ),
        ],
      );

  void handleArchive(Deal deal) {
    showSharedModalAlert(
      context,
      Text("Archive RYDR"),
      content: Text(
          "This will remove this expired RYDR from your list and move it into Profile > Options > Account > Archived RYDRs. \n\nYou can also reactivate or recreate this RYDR to put it back into the Marketplace."),
      actions: <ModalAlertAction>[
        ModalAlertAction(
          isDestructiveAction: true,
          label: "Archive",
          onPressed: () {
            Navigator.of(context).pop();

            showSharedLoadingLogo(
              context,
              content: "Archiving RYDR",
            );

            _bloc.archiveOrDeleteDeal(deal).then((success) {
              Navigator.of(context).pop();

              if (!success) {
                showSharedModalError(
                  context,
                  title: 'Unable to archive RYDR',
                  subTitle: 'Please try again in a few moments.',
                );
              }
            });
          },
        ),
        ModalAlertAction(
          label: "Cancel",
          onPressed: () => Navigator.of(context).pop(),
        ),
      ],
    );
  }

  void handleDelete(Deal deal) {
    showSharedModalAlert(
      context,
      Text("Delete Draft RYDR"),
      actions: <ModalAlertAction>[
        ModalAlertAction(
          isDestructiveAction: true,
          label: "Delete",
          onPressed: () {
            Navigator.of(context).pop();

            showSharedLoadingLogo(
              context,
              content: "Deleting Draft",
            );

            _bloc.archiveOrDeleteDeal(deal, true).then((success) {
              Navigator.of(context).pop();

              if (!success) {
                showSharedModalError(
                  context,
                  title: 'Unable to delete RYDR',
                  subTitle: 'Please try again in a few moments.',
                );
              }
            });
          },
        ),
        ModalAlertAction(
          label: "Cancel",
          onPressed: () => Navigator.of(context).pop(),
        ),
      ],
    );
  }

  void handleExpirationDate(Deal deal) {
    showDealDatePicker(
        context: context,
        reactivate: true,
        onCancel: () => Navigator.of(context).pop(),
        onContinue: (DateTime newValue) {
          Navigator.of(context).pop();

          showSharedLoadingLogo(context);

          _bloc.updateExpiration(deal, newValue).then((success) {
            Navigator.of(context).pop();

            if (!success) {
              showSharedModalError(context,
                  title: "Unable to extend this RYDR",
                  subTitle: "Please try again in a few moments");
            }
          });
        });
  }

  void goToDeal(Deal deal) => deal.status == DealStatus.draft
      ? deal.dealType == DealType.Event
          ? Navigator.of(context)
              .pushNamed(AppRouting.getDealAddEvent, arguments: deal)
          : Navigator.of(context)
              .pushNamed(AppRouting.getDealAddDeal, arguments: deal)
      : Navigator.of(context).pushNamed(AppRouting.getDealEditRoute(deal.id));

  void goToInsights(Deal deal) => Navigator.of(context)
      .pushNamed(AppRouting.getDealInsightsRoute(deal.id), arguments: deal);

  @override
  Widget build(BuildContext context) {
    super.build(context);

    _theme = Theme.of(context);

    /// NOTE: if we use filterArguments vs. widget.arguments where for some reason
    /// the layout prop is reset and does not persit... not sure why
    return widget.arguments.layoutType == ListPageLayout.StandAlone
        ? _buildStandAlone()
        : _buildIntegrated();
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
          children: <Widget>[_buildBody()],
        ),
      );

  Widget _buildIntegrated() => NestedScrollView(
        headerSliverBuilder: (context, innerBoxScrolled) =>
            [_buildIntegratedAppBar()],
        body: _buildBody(),
      );

  Widget _buildList(DealsResponse res) => Stack(
        children: <Widget>[
          RefreshIndicator(
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
                    children: <Widget>[_buildNoResults()],
                  )
                : ListView.builder(
                    controller: _scrollController,
                    padding: EdgeInsets.only(
                        bottom: kToolbarHeight +
                            MediaQuery.of(context).padding.bottom),
                    physics: AlwaysScrollableScrollPhysics(),
                    itemCount: res.models.length,
                    itemBuilder: (BuildContext context, int index) {
                      final Deal deal = res.models[index];
                      return ListDeal(
                        deal: res.models[index],
                        handleTap: () => goToDeal(deal),
                        handleTapInsights: () => goToInsights(deal),
                        handleArchive: () => handleArchive(deal),
                        handleDelete: () => handleDelete(deal),
                        handleReactivate: () => handleReactivate(deal),
                        handleRecreate: () => handleRecreate(deal),
                        handleExpirationDate: () => handleExpirationDate(deal),
                      );
                    }),
          ),
          _buildAddFab(),
        ],
      );

  Widget _buildStandAloneAppBar() {
    final String dealStatusFilters =
        filterArguments != null && filterArguments.filterDealStatus != null
            ? filterArguments.filterDealStatus
                .map((status) => dealStatusToStringDisplay(status))
                .join(', ')
            : "";

    final String titleSuffix = "RYDR's";

    final String titleLabel = filterArguments == null
        ? ""
        : filterArguments.filterDealStatus != null
            ? dealStatusFilters
            : filterArguments.filterDealName != null
                ? filterArguments.filterDealName
                : filterArguments.filterDealPublisherAccountName != null
                    ? filterArguments.filterDealPublisherAccountName
                    : "";

    return AppBar(
      leading: AppBarBackButton(context),
      title: Text(titleLabel + " " + titleSuffix),
    );
  }

  Widget _buildIntegratedAppBar() => SliverAppBar(
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
          child: StreamBuilder<ListPageArguments>(
            stream: _bloc.filterArgs,
            builder: (context, snapshot) {
              final ListPageArguments args =
                  snapshot.data ?? ListPageArguments();
              return ListView(
                  padding: EdgeInsets.symmetric(horizontal: 16),
                  physics: AlwaysScrollableScrollPhysics(),
                  scrollDirection: Axis.horizontal,
                  children: [
                    /// show a chip for filtering by a location but only if the business has at least one
                    /// location configured
                    StreamBuilder<List<Place>>(
                      stream: _bloc.places,
                      builder: (context, snapshot) => snapshot.data == null ||
                              snapshot.data.isEmpty ||
                              snapshot.data.length == 1
                          ? Container(width: 0, height: 0)
                          : _buildChip(
                              args.filterDealPlaceId != null
                                  ? args.filterDealPlaceName.length > 20
                                      ? args.filterDealPlaceName
                                              .substring(0, 20) +
                                          '...'
                                      : args.filterDealPlaceName
                                  : "All Locations",
                              true,
                              ListPageArguments(
                                  isRequests: false,
                                  sortDeals: DealSort.newest,
                                  filterDealStatus: [DealStatus.published]),
                              isFilter: true,
                              isFilterClearable: args.filterDealPlaceId != null,
                              onTap: args.filterDealPlaceId != null
                                  ? null
                                  : _showLocations),
                    ),
                    _buildChip(
                        "Newest",
                        args.sortDeals == DealSort.newest &&
                            args.filterDealStatus
                                .contains(DealStatus.published),
                        ListPageArguments(
                            isRequests: false,
                            sortDeals: DealSort.newest,
                            filterDealStatus: [DealStatus.published])),
                    _buildChip(
                        "Expiring",
                        args.sortDeals == DealSort.expiring &&
                            args.filterDealStatus
                                .contains(DealStatus.published),
                        ListPageArguments(
                            isRequests: false,
                            sortDeals: DealSort.expiring,
                            includeExpired: false,
                            filterDealStatus: [DealStatus.published])),
                    _buildChip(
                        "Min Followers",
                        args.sortDeals == DealSort.followerValue &&
                            args.filterDealStatus
                                .contains(DealStatus.published),
                        ListPageArguments(
                            isRequests: false,
                            sortDeals: DealSort.followerValue,
                            filterDealStatus: [DealStatus.published])),
                    _buildChip(
                      "Paused",
                      args.sortDeals == DealSort.newest &&
                          args.filterDealStatus.contains(DealStatus.paused),
                      ListPageArguments(
                          isRequests: false,
                          sortDeals: DealSort.newest,
                          filterDealStatus: [DealStatus.paused]),
                      isFilter: true,
                      isFilterClearable: args.sortDeals == DealSort.newest &&
                          args.filterDealStatus.contains(DealStatus.paused),
                      onTap: args.sortDeals == DealSort.newest &&
                              args.filterDealStatus.contains(DealStatus.paused)
                          ? () => _applyFilters(
                                "Newest",
                                ListPageArguments(
                                    isRequests: false,
                                    sortDeals: DealSort.newest,
                                    filterDealStatus: [DealStatus.published]),
                              )
                          : () => _applyFilters(
                                "Paused",
                                ListPageArguments(
                                    isRequests: false,
                                    sortDeals: DealSort.newest,
                                    filterDealStatus: [DealStatus.paused]),
                              ),
                    ),

                    _buildChip(
                      "Draft",
                      args.sortDeals == DealSort.newest &&
                          args.filterDealStatus.contains(DealStatus.draft),
                      ListPageArguments(
                          isRequests: false,
                          sortDeals: DealSort.newest,
                          filterDealStatus: [DealStatus.draft]),
                      isFilter: true,
                      isFilterClearable: args.sortDeals == DealSort.newest &&
                          args.filterDealStatus.contains(DealStatus.draft),
                      onTap: args.sortDeals == DealSort.newest &&
                              args.filterDealStatus.contains(DealStatus.draft)
                          ? () => _applyFilters(
                                "Newest",
                                ListPageArguments(
                                    isRequests: false,
                                    sortDeals: DealSort.newest,
                                    filterDealStatus: [DealStatus.published]),
                              )
                          : () => _applyFilters(
                                "Draft",
                                ListPageArguments(
                                    isRequests: false,
                                    sortDeals: DealSort.newest,
                                    filterDealStatus: [DealStatus.draft]),
                              ),
                    ),
                  ]);
            },
          ),
        ),
      );

  Widget _buildAddFab() => Positioned(
      right: 16,
      bottom: MediaQuery.of(context).padding.bottom + 16,
      child: FadeInScaleUp(
        10,
        StreamBuilder<bool>(
          stream: _bloc.smallFab,
          builder: (context, snapshot) => InkWell(
            borderRadius: BorderRadius.circular(40),

            /// onTap: () => Navigator.of(context).pushNamed(AppRouting.getDealAdd),
            onTap: () =>
                Navigator.of(context).pushNamed(AppRouting.getDealAddDeal),
            child: AnimatedContainer(
              duration: Duration(milliseconds: 250),
              height: 56,
              width: snapshot.data != null && snapshot.data == true ? 56 : 110,
              decoration: BoxDecoration(
                boxShadow: AppShadows.elevation[1],
                borderRadius: BorderRadius.circular(40),
                color: Theme.of(context).textTheme.bodyText1.color,
              ),
              child: Align(
                alignment: Alignment.center,
                child: ListView(
                  padding: EdgeInsets.only(left: 16),
                  scrollDirection: Axis.horizontal,
                  children: <Widget>[
                    Icon(
                      AppIcons.plus,
                      color: Theme.of(context).appBarTheme.color,
                    ),
                    Container(
                      height: 56,
                      padding: EdgeInsets.only(left: 8),
                      child: Center(
                        child: AnimatedOpacity(
                          duration: Duration(milliseconds: 250),
                          opacity:
                              snapshot.data != null && snapshot.data == true
                                  ? 0
                                  : 1,
                          child: Text(
                            "NEW",
                            style: Theme.of(context).textTheme.bodyText1.merge(
                                  TextStyle(
                                    fontSize: 17,
                                    color: Theme.of(context).appBarTheme.color,
                                    letterSpacing: 0.7,
                                  ),
                                ),
                          ),
                        ),
                      ),
                    )
                  ],
                ),
              ),
            ),
          ),
        ),
      ));

  Widget _buildChip(
    String label,
    bool isCurrent,
    ListPageArguments args, {
    Function onTap,
    bool isFilter = false,
    bool isFilterClearable = false,
    bool addMargin = true,
  }) =>
      Container(
          margin: EdgeInsets.only(right: addMargin ? 8 : 0),
          child: GestureDetector(
            child: Chip(
              avatar: isFilter
                  ? isFilterClearable
                      ? Icon(AppIcons.timesCircle,
                          size: 19, color: _theme.textTheme.bodyText1.color)
                      : Icon(
                          isCurrent ? AppIcons.filterSolid : AppIcons.filterReg,
                          size: 15,
                          color: isCurrent
                              ? _theme.textTheme.bodyText1.color
                              : _theme.hintColor)
                  : Stack(
                      alignment: Alignment.center,
                      children: <Widget>[
                        Icon(AppIcons.sort,
                            size: 17,
                            color: isCurrent
                                ? _theme.textTheme.bodyText1.color
                                : _theme.hintColor),
                        isCurrent
                            ? Icon(AppIcons.sortDownSolid,
                                size: 17,
                                color: _theme.textTheme.bodyText1.color)
                            : Container(height: 0, width: 0),
                      ],
                    ),
              backgroundColor: _theme.appBarTheme.color,
              shape: OutlineInputBorder(
                borderRadius: BorderRadius.circular(40),
                borderSide: BorderSide(
                  width: 1.25,
                  color: isCurrent
                      ? _theme.textTheme.bodyText1.color
                      : _theme.hintColor,
                ),
              ),
              label: Text(label),
              labelStyle: TextStyle(
                fontWeight: isCurrent ? FontWeight.w600 : FontWeight.w500,
                color: isCurrent
                    ? _theme.textTheme.bodyText1.color
                    : _theme.hintColor,
              ),
            ),
            onTap: () => onTap != null ? onTap() : _applyFilters(label, args),
          ));
}
