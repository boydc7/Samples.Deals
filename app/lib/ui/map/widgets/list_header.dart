import 'dart:math';

import 'package:collection/collection.dart';
import 'package:flutter/material.dart';
import 'package:flutter/widgets.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/tags_config.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/tag.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/ui/map/blocs/map.dart';

class ListHeader extends StatelessWidget {
  final MapBloc mapBloc;
  final Function loadList;
  final Function goBackToList;
  final bool isPlaceList;

  ListHeader({
    @required this.mapBloc,
    @required this.loadList,
    @required this.goBackToList,
    @required this.isPlaceList,
  });

  void _sort(DealSort sort) => loadList(sort: sort, reset: true);

  void _filterDealType(DealType dealType) => loadList(
      reset: true,
      dealType: dealType != mapBloc.currentDealType ? dealType : null);

  void _filterDealTags(List<Tag> tags) => loadList(
      reset: true,
      dealTags:
          ListEquality().equals(mapBloc.currentDealTags, tags) ? null : tags);

  void _clearDealPublisher() => loadList(reset: true, dealPublisher: null);

  void _goToInvites(BuildContext context) =>
      Navigator.of(context).pushNamed(AppRouting.getRequestsInvited);

  @override
  Widget build(BuildContext context) {
    return SliverPersistentHeader(
      pinned: true,
      delegate: _SliverAppBarListDelegate(
        mapBloc: mapBloc,
        onSort: _sort,
        onFilterDealType: _filterDealType,
        onFilterDealTags: _filterDealTags,
        onClearDealPublisher: _clearDealPublisher,
        onFilterInvites: () => _goToInvites(context),
        onBackToList: goBackToList,
        isPlaceList: isPlaceList,
      ),
    );
  }
}

class _SliverAppBarListDelegate extends SliverPersistentHeaderDelegate {
  final MapBloc mapBloc;
  final Function onSort;
  final Function onFilterDealType;
  final Function onFilterDealTags;
  final Function onClearDealPublisher;
  final Function onFilterInvites;
  final Function onBackToList;
  final bool isPlaceList;

  ThemeData _theme;
  bool _darkMode;

  _SliverAppBarListDelegate({
    @required this.mapBloc,
    @required this.onSort,
    @required this.onFilterDealType,
    @required this.onFilterDealTags,
    @required this.onClearDealPublisher,
    @required this.onFilterInvites,
    @required this.onBackToList,
    @required this.isPlaceList,
  });

  double scrollAnimationValue(double shrinkOffset) {
    double maxScrollAllowed = maxExtent - minExtent;
    return ((maxScrollAllowed - shrinkOffset) / maxScrollAllowed)
        .clamp(0, 1)
        .toDouble();
  }

  double scrollAnimationPadding(double shrinkOffset, double paddingTop) {
    if (isPlaceList || mapBloc.currentDealPublisher != null) {
      return shrinkOffset >= 0
          ? (paddingTop * 1.25) - shrinkOffset.clamp(0, 4).toDouble()
          : 30.0;
    } else {
      return shrinkOffset >= 0
          ? paddingTop + shrinkOffset.clamp(10, 16).toDouble()
          : 30.0;
    }
  }

  @override
  Widget build(
    BuildContext context,
    double shrinkOffset,
    bool overlapsContent,
  ) {
    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;

    final double visibleMainHeight = max(maxExtent - shrinkOffset, minExtent);
    final double animationVal = scrollAnimationValue(shrinkOffset);
    final double titlePaddingTop = scrollAnimationPadding(shrinkOffset, 24.0);

    return ClipRRect(
      borderRadius: BorderRadius.only(
        topLeft: Radius.circular(16),
        topRight: Radius.circular(16),
      ),
      child: Container(
        height: visibleMainHeight,
        width: MediaQuery.of(context).size.width,
        color: _theme.scaffoldBackgroundColor,
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            _buildGrabber(context),
            _buildTitle(context, titlePaddingTop),
            isPlaceList || mapBloc.currentDealPublisher != null
                ? Container()
                : _buildFiltersRow(context, animationVal),
            Align(
              alignment: Alignment.bottomCenter,
              child: Divider(height: 1),
            ),
          ],
        ),
      ),
    );
  }

  /// shows/hides the little grabber 'notch' atop the list sheet
  Widget _buildGrabber(BuildContext context) => Positioned(
        top: 16,
        child: Container(
            width: MediaQuery.of(context).size.width,
            alignment: Alignment.center,
            child: Container(
              height: 4.0,
              width: 28.0,
              decoration: BoxDecoration(
                  color: _darkMode ? Colors.white24 : _theme.canvasColor,
                  borderRadius: BorderRadius.circular(8.0)),
            )),
      );

  /// builds the title header both for filtering by a place and no place
  /// if we're filtering by a specific publisher account then just show their name/avatar
  Widget _buildTitle(BuildContext context, double titlePaddingTop) {
    final PublisherAccount dealPublisher = mapBloc.currentDealPublisher;

    /// if we're on a list of places, or filtering by a specific publisher
    /// then a down arrow will go back to the map or clear the publisher filter
    final Widget leading = isPlaceList
        ? IconButton(
            onPressed: onBackToList,
            icon: Icon(AppIcons.chevronDown),
          )
        : dealPublisher != null
            ? IconButton(
                onPressed: onClearDealPublisher,
                icon: Icon(AppIcons.chevronDown),
              )
            : Container(width: kMinInteractiveDimension);

    return Positioned(
      top: titlePaddingTop,
      child: SizedBox(
        width: MediaQuery.of(context).size.width,
        child: Row(
          mainAxisSize: MainAxisSize.max,
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            leading,
            Expanded(
              child: StreamBuilder<MapListHeader>(
                stream: isPlaceList
                    ? mapBloc.headerPlace
                    : dealPublisher != null
                        ? mapBloc.headerPublisher
                        : mapBloc.headerList,
                builder: (context, snapshot) => Padding(
                    padding: EdgeInsets.only(left: 16, right: 16),
                    child: Column(
                      mainAxisSize: MainAxisSize.max,
                      mainAxisAlignment: MainAxisAlignment.start,
                      crossAxisAlignment: CrossAxisAlignment.center,
                      children: <Widget>[
                        Text(
                            snapshot.data == null
                                ? "Explore nearby..."
                                : snapshot.data.title,
                            overflow: TextOverflow.ellipsis,
                            textAlign: TextAlign.center,
                            style: _theme.textTheme.headline6
                                .merge(TextStyle(fontWeight: FontWeight.w600))),
                        snapshot.data != null && snapshot.data.subTitle != null
                            ? Padding(
                                padding: EdgeInsets.only(top: 2),
                                child: Text(snapshot.data.subTitle,
                                    textAlign: TextAlign.center,
                                    style: TextStyle(
                                        fontSize: 12.0,
                                        color: _theme.hintColor)))
                            : Container(),
                      ],
                    )),
              ),
            ),
            Container(width: kMinInteractiveDimension),
          ],
        ),
      ),
    );
  }

  /// builds row of chips filters, sorts, etc. atop the list
  Widget _buildFiltersRow(BuildContext context, double animationVal) {
    final DealType dealType = mapBloc.currentDealType;

    return Opacity(
      opacity: animationVal,
      child: Padding(
        padding: EdgeInsets.only(top: kToolbarHeight),
        child: Container(
          height: 44,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: EdgeInsets.symmetric(horizontal: 16),
            children: <Widget>[
              Row(
                children: <Widget>[
                  _buildFilterVirtual(context, dealType == DealType.Virtual),
                  _buildFilterEvents(context, dealType == DealType.Event),
                  _buildFilterInvites(context),
                ],
              ),
              _buildTagFilters(context),
              // _sortChip("Closest to me", DealSort.closest),
              // _sortChip("Expiring Soon", DealSort.expiring),
              _sortChip("Newest", DealSort.newest),
              // _sortChip("Closest to my follower count",
              // DealSort.followerValue),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildFilterVirtual(BuildContext context, bool isVirtual) =>
      _actionChip(
        label: "Virtual",
        labelColor: isVirtual
            ? Theme.of(context).scaffoldBackgroundColor
            : Colors.deepOrange,
        backgroundColor: isVirtual ? Colors.deepOrange : null,
        icon: isVirtual ? AppIcons.times : AppIcons.globe,
        onPressed: () => onFilterDealType(DealType.Virtual),
      );

  Widget _buildFilterEvents(BuildContext context, bool isEvents) => 1 == 0
      ? _actionChip(
          label: "Events",
          labelColor: isEvents
              ? Theme.of(context).scaffoldBackgroundColor
              : Theme.of(context).primaryColor,
          backgroundColor: isEvents ? Theme.of(context).primaryColor : null,
          onPressed: () => onFilterDealType(DealType.Event),
          icon: isEvents ? AppIcons.times : AppIcons.calendarStarSolid,
        )
      : Container();

  Widget _buildFilterInvites(BuildContext context) => _actionChip(
        label: "Invites",
        labelColor:
            Utils.getRequestStatusColor(DealRequestStatus.invited, _darkMode),
        borderColor:
            Utils.getRequestStatusColor(DealRequestStatus.invited, _darkMode),
        onPressed: onFilterInvites,
        icon: AppIcons.starsSolid,
      );

  Widget _buildTagFilters(BuildContext context) {
    /// if we have a current tag, then only show that one
    /// and the user would have to remove it to get the other ones back
    final currentTags = mapBloc.currentDealTags;

    return currentTags != null && currentTags.isNotEmpty
        ? Row(
            children: currentTags
                .map((Tag t) => _actionChip(
                      label: t.value,
                      labelColor: Theme.of(context).scaffoldBackgroundColor,
                      backgroundColor: Theme.of(context).primaryColor,
                      icon: AppIcons.times,
                      onPressed: () => onFilterDealTags([t]),
                    ))
                .toList())
        :

        /// list of preset tags we use for list filtering, these are stored both
        /// in assets json and in remote config
        Row(
            children: tagsConfig.tagsDealsFilters
                .map((Tag t) => _actionChip(
                      label: t.value,
                      labelColor: Theme.of(context).textTheme.bodyText2.color,
                      backgroundColor:
                          Theme.of(context).scaffoldBackgroundColor,
                      icon: getTagIcon(t.value),
                      onPressed: () => onFilterDealTags([t]),
                    ))
                .toList());
  }

  Widget _sortChip(String label, DealSort sort) {
    final bool isCurrent = sort == mapBloc.currentSort;

    return _actionChip(
      label: label,
      labelColor: isCurrent
          ? _theme.textTheme.bodyText2.color
          : _theme.hintColor.withOpacity(0.85),
      borderColor: isCurrent
          ? _theme.textTheme.bodyText2.color
          : _theme.hintColor.withOpacity(0.75),
      onPressed: () => onSort(sort),
      icon: isCurrent ? AppIcons.checkReg : null,
    );
  }

  Widget _actionChip({
    @required String label,
    @required Color labelColor,
    Color borderColor,
    Color backgroundColor,
    IconData icon,
    Color iconColor,
    @required Function onPressed,
  }) =>
      Padding(
          child: ActionChip(
            pressElevation: 1.0,
            backgroundColor: backgroundColor ?? _theme.scaffoldBackgroundColor,
            onPressed: onPressed,
            avatar: icon == null
                ? null
                : Icon(icon, color: iconColor ?? labelColor, size: 16),
            label: Text(label),
            labelStyle: TextStyle(
              color: labelColor,
              fontWeight: FontWeight.w500,
            ),
            shape: OutlineInputBorder(
              borderSide: BorderSide(
                color: borderColor ?? labelColor,
                width: 1.25,
              ),
              borderRadius: BorderRadius.circular(40),
            ),
          ),
          padding: EdgeInsets.only(right: 8));

  @override
  double get maxExtent =>
      isPlaceList || mapBloc.currentDealPublisher != null ? 88.0 : 120.0;

  @override
  double get minExtent => kToolbarHeight + 24.0;

  @override
  bool shouldRebuild(SliverPersistentHeaderDelegate oldDelegate) {
    return true;
  }
}
