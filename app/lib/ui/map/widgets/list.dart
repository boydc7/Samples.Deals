import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/widgets.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/ui/map/blocs/list.dart';
import 'package:rydr_app/ui/map/widgets/list_deal.dart';
import 'package:rydr_app/ui/map/widgets/list_header.dart';
import 'package:rydr_app/ui/map/widgets/list_noresults.dart';
import 'package:rydr_app/ui/map/widgets/list_publishers.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';

import 'package:rydr_app/models/deal.dart';

import 'package:rydr_app/ui/map/blocs/map.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

class MapList extends StatefulWidget {
  final MapBloc mapBloc;

  final Function loadList;
  final Function redoSearch;
  final Function goToDeal;
  final Function goToLocation;
  final Function goToMyLocation;
  final Function goBackToList;
  final bool isPlaceList;

  MapList({
    @required this.mapBloc,
    @required this.loadList,
    @required this.redoSearch,
    @required this.goToDeal,
    @required this.goToLocation,
    @required this.goToMyLocation,
    @required this.goBackToList,
    @required this.isPlaceList,
  });

  @override
  State<StatefulWidget> createState() => _MapListState();
}

class _MapListState extends State<MapList> with AutomaticKeepAliveClientMixin {
  final _listBloc = MapListBloc();

  StreamSubscription _subLoading;
  StreamSubscription _subMapTapped;
  BuildContext _contextList;
  ScrollController _scrollController;
  bool _darkMode;
  double _initialChildSize;
  ThemeData _theme;

  @override
  void initState() {
    super.initState();

    /// set initial size of sheet based on whether its place or deals list
    _initialChildSize = widget.isPlaceList ? 0.6 : 0.49;

    /// listen to when we load a new list of deals/places and reset
    /// the sheet to initial child size position
    _subLoading = widget.isPlaceList
        ? widget.mapBloc.loadingPlace.listen((bool loading) {
            if (widget.mapBloc.pageIndexPlace == 0) {
              DraggableScrollableActuator.reset(_contextList);
            }
          })
        : widget.mapBloc.loading.listen((bool loading) {
            if (widget.mapBloc.pageIndex == 0) {
              DraggableScrollableActuator.reset(_contextList);
            }
          });

    /// reset the list when map is tapped and we're higher than initial size
    _subMapTapped = widget.mapBloc.mapTapped.listen((bool tapped) {
      if (tapped == true && _listBloc.extent > _initialChildSize) {
        DraggableScrollableActuator.reset(_contextList);
      }
    });
  }

  @override
  void dispose() {
    _subLoading?.cancel();
    _subMapTapped?.cancel();
    _listBloc?.dispose();

    /// NOTE! purposely not disposing _scrollController
    /// as it seems to cause issues on re-builds where we've alrady disposed it...

    super.dispose();
  }

  bool _onDragSheet(DraggableScrollableNotification notification) {
    final double extent = notification.extent;

    _listBloc.setExtent(extent);

    if (extent > 0.9 && _listBloc.showButtons.value == true) {
      _listBloc.setShowButtons(false);
    } else if (extent <= 0.9 && _listBloc.showButtons.value == false) {
      _listBloc.setShowButtons(true);
    }

    return true;
  }

  void _loadMore() => widget.loadList().then((_) {
        /// once done loading more deals, and we have more... then we can try to scroll
        /// the list down a bit to bring the next deal(s) into view
        _scrollController.animateTo(
            _scrollController.position.maxScrollExtent + 150,
            duration: Duration(milliseconds: 250),
            curve: Curves.easeOut);
      });

  void _redoSearch() => widget.redoSearch();

  void _handleDealTap(Deal deal) => widget.goToDeal(deal);

  void _filterPublisher(PublisherAccount profile) => widget.loadList(
        reset: true,
        dealPublisher: widget.mapBloc.currentDealPublisher?.id == profile.id
            ? null
            : profile,
      );

  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);

    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;

    return NotificationListener<DraggableScrollableNotification>(
      onNotification: _onDragSheet,
      child: DraggableScrollableActuator(
        child: DraggableScrollableSheet(
          initialChildSize: _initialChildSize,
          maxChildSize: 0.91,
          minChildSize: 0.21,
          builder: (BuildContext context, ScrollController scrollController) {
            _contextList = context;
            _scrollController = scrollController;

            return StreamBuilder(
              stream: widget.isPlaceList
                  ? widget.mapBloc.loadingPlace
                  : widget.mapBloc.loading,
              builder: (context, snapshot) {
                final bool loading =
                    snapshot.data == null || snapshot.data == true;

                /// differentiate between place vs. regular list which uses separate set of props
                /// to keep them separated as we re-use the list widget for two different sheets
                final bool isFirstPage = widget.isPlaceList
                    ? widget.mapBloc.pageIndexPlace == 0
                    : widget.mapBloc.pageIndex == 0;
                final bool hasResults = widget.isPlaceList
                    ? widget.mapBloc.hasResultsPlace
                    : widget.mapBloc.hasResults;
                final bool hasMoreResults = widget.isPlaceList
                    ? widget.mapBloc.hasMoreResultsPlace
                    : widget.mapBloc.hasMoreResults;

                return CustomScrollView(
                  controller: scrollController,
                  slivers: <Widget>[
                    _buildHeaderButtons(),
                    StreamBuilder<MapListResponse>(
                      stream: widget.isPlaceList
                          ? widget.mapBloc.mapListPlace
                          : widget.mapBloc.mapList,
                      builder: (context, snapshot) {
                        return snapshot.data == null || (loading && isFirstPage)
                            ? SliverToBoxAdapter(child: Container())
                            : ListHeader(
                                mapBloc: widget.mapBloc,
                                loadList: widget.loadList,
                                goBackToList: widget.goBackToList,
                                isPlaceList: widget.isPlaceList,
                              );
                      },
                    ),
                    StreamBuilder<MapListResponse>(
                      stream: widget.isPlaceList
                          ? widget.mapBloc.mapListPlace
                          : widget.mapBloc.mapList,
                      builder: (context, snapshot) {
                        /// check for error on the response
                        if (snapshot.data != null &&
                            snapshot.data.error != null) {
                          return _buildError(snapshot.data.error);
                        }

                        return snapshot.data == null || (loading && isFirstPage)
                            ? SliverToBoxAdapter(child: Container())
                            : !hasResults && isFirstPage
                                ? ListNoResults(
                                    mapBloc: widget.mapBloc,
                                    goToLocation: widget.goToLocation,
                                    isPlaceList: widget.isPlaceList,
                                  )
                                : _buildResultsListView(
                                    snapshot.data.deals,
                                    hasMoreResults,
                                  );
                      },
                    ),

                    /// if we're loading the first page, then show a loading shimmer
                    /// otherwise we'll have a button to load more below an existin list

                    loading && isFirstPage
                        ? SliverList(
                            delegate: SliverChildListDelegate([
                              Container(
                                color: _theme.scaffoldBackgroundColor,
                                padding: EdgeInsets.all(16),
                                child: LoadingListShimmer(),
                              )
                            ]),
                          )
                        : SliverFillRemaining(
                            fillOverscroll: true,
                            hasScrollBody: false,
                            child: Container(
                                color: _theme.scaffoldBackgroundColor),
                          ),
                  ],
                );
              },
            );
          },
        ),
      ),
    );
  }

  /// builds the header buttons above the silding panel showing sort and location buttons
  Widget _buildHeaderButtons() => SliverList(
        delegate: SliverChildListDelegate([
          Stack(
            overflow: Overflow.visible,
            alignment: Alignment.bottomCenter,
            children: <Widget>[
              Container(
                height: 40.0,
                margin: EdgeInsets.only(bottom: 8.0),
                padding: EdgeInsets.symmetric(horizontal: 12.0),
                width: double.infinity,
                child: StreamBuilder<bool>(
                    stream: _listBloc.showButtons,
                    builder: (context, snapshot) {
                      final bool showButtons = snapshot.data == true;

                      return Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: <Widget>[
                          Container(
                            width: 40.0,
                            height: 40.0,
                          ),
                          Expanded(
                            child: AnimatedOpacity(
                              duration: Duration(milliseconds: 250),
                              opacity: !showButtons ? 0.0 : 1.0,
                              child: _buildRedoSearchButton(),
                            ),
                          ),
                          AnimatedOpacity(
                            duration: Duration(milliseconds: 250),
                            opacity: !showButtons ? 0.0 : 1.0,
                            child: _buildLocationServicesButton(),
                          ),
                        ],
                      );
                    }),
              ),
            ],
          )
        ]),
      );

  /// if the parent says we should re-do the search (because the map was moved)
  /// then build the re-do search button that goes above the panel
  /// NOTE: place list never needs a redo-search button
  Widget _buildRedoSearchButton() => widget.isPlaceList
      ? Container()
      : StreamBuilder<bool>(
          stream: widget.mapBloc.redoSearch,
          builder: (context, snapshot) {
            return snapshot.data == true
                ? AnimatedOpacity(
                    duration: const Duration(milliseconds: 200),
                    opacity: 1,
                    child: GestureDetector(
                      onTap: _redoSearch,
                      child: Container(
                        height: 40.0,
                        margin: EdgeInsets.symmetric(horizontal: 32.0),
                        decoration: BoxDecoration(
                            color: _darkMode
                                ? Theme.of(context)
                                    .appBarTheme
                                    .color
                                    .withOpacity(0.85)
                                : AppColors.white,
                            boxShadow: AppShadows.elevation[0],
                            borderRadius: BorderRadius.circular(40.0)),
                        child: Center(
                          child: Text('Search this area',
                              style: TextStyle(
                                  fontWeight: FontWeight.w600,
                                  fontSize: 14.0,
                                  color: _theme.primaryColor)),
                        ),
                      ),
                    ))
                : Container();
          });

  /// loading services button can be in 'loading' (greyed-out) state,
  /// or otherwise on/off depending on whether location services are on or denied
  Widget _buildLocationServicesButton() => Container(
      width: 40.0,
      height: 40.0,
      decoration: BoxDecoration(
        color: _darkMode
            ? _theme.appBarTheme.color.withOpacity(0.85)
            : Colors.white,
        boxShadow: AppShadows.elevation[0],
        borderRadius: BorderRadius.circular(50.0),
      ),
      child: StreamBuilder<bool>(
          stream: widget.mapBloc.locationServices,
          builder: (context, snapshot) {
            final bool enabled = snapshot.data == true || snapshot.data == null;

            return StreamBuilder<bool>(
                stream: widget.isPlaceList
                    ? widget.mapBloc.loadingPlace
                    : widget.mapBloc.loading,
                builder: (context, snapshot) {
                  final bool loading = snapshot.data == true;
                  return IconButton(
                      color: _theme.textTheme.bodyText2.color,
                      onPressed: loading ? null : widget.goToMyLocation,
                      icon: Container(
                          width: 40.0,
                          child: Icon(
                            enabled
                                ? AppIcons.locationArrowSolid
                                : AppIcons.locationSlash,
                            size: enabled ? 18.0 : 20.0,
                            color: loading
                                ? AppColors.grey300
                                : _theme.primaryColor,
                          )));
                });
          }));

  Widget _buildError(DioError error) => SliverList(
        delegate: SliverChildListDelegate([
          Container(
            color: _theme.scaffoldBackgroundColor,
            padding: EdgeInsets.all(16),
            child: RetryError(
              error: error,
              onRetry: _redoSearch,
              fullSize: false,
            ),
          )
        ]),
      );

  Widget _buildResultsListView(List<Deal> deals, bool hasMoreResults) =>
      SliverList(
        delegate: SliverChildBuilderDelegate(
          (context, index) {
            return Container(
              color: Theme.of(context).scaffoldBackgroundColor,
              child: Column(
                children: <Widget>[
                  /// inject every 4th deal
                  (index > 0 && index % 4 == 0)
                      ? ListPublishers(
                          mapBloc: widget.mapBloc,
                          index: index,
                          onFilterPublisher: _filterPublisher,
                        )
                      : Container(),

                  GestureDetector(
                    onTap: () => _handleDealTap(deals[index]),
                    child: MapListDeal(deals[index]),
                  ),

                  /// if we have less than x deals then we'd never have injected
                  /// the publishers, so do that now instead
                  index == deals.length - 1 && deals.length < 4
                      ? ListPublishers(
                          mapBloc: widget.mapBloc,
                          index: index,
                          onFilterPublisher: _filterPublisher,
                        )
                      : Container(),

                  index == deals.length - 1
                      ? _buildLoadMore(hasMoreResults)
                      : Container(),
                ],
              ),
            );
          },
          childCount: deals.length,
        ),
      );

  /// text button that would show at the end of a list of results IF we have more
  /// results still than currently loaded...
  Widget _buildLoadMore(bool hasMoreResults) => Padding(
      padding: EdgeInsets.only(bottom: 16.0, top: 8.0),
      child: StreamBuilder(
        stream: widget.isPlaceList
            ? widget.mapBloc.loadingPlace
            : widget.mapBloc.loading,
        builder: (context, snapshot) => TextButton(
          label: hasMoreResults
              ? "Load More"
              : snapshot.data == true ? "Loading..." : "",
          color: _theme.primaryColor,
          onTap:
              snapshot.data == true ? null : hasMoreResults ? _loadMore : null,
        ),
      ));
}
