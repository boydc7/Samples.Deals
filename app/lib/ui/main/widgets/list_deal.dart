import 'package:flutter/material.dart';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/app/icons.dart';

import 'package:rydr_app/ui/deal/widgets/shared/notice.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';

import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';

import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:shimmer/shimmer.dart';

/// a list tile representing a 'RYDR' which is only used by listDeals
/// which is only accessible to a business viewing their deals
class ListDeal extends StatelessWidget {
  final Deal deal;
  final Function handleTap;
  final Function handleTapInsights;
  final Function handleReactivate;
  final Function handleArchive;
  final Function handleDelete;
  final Function handleRecreate;
  final Function handleExpirationDate;

  ListDeal({
    @required this.deal,
    @required this.handleTap,
    @required this.handleTapInsights,
    @required this.handleReactivate,
    @required this.handleArchive,
    @required this.handleDelete,
    @required this.handleRecreate,
    @required this.handleExpirationDate,
  });

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final bool isPaused = deal.status == DealStatus.paused;
    final bool isDraft = deal.status == DealStatus.draft;
    final bool isArchived = deal.status == DealStatus.completed;
    final bool isExpired = deal.expirationInfo.isExpired == true;
    final bool hasCompleted =
        deal.getStatAsInt(DealStatType.currentCompleted) > 0;

    return Container(
      color: isPaused
          ? dark ? Color(0xFF0F0F0F) : Theme.of(context).appBarTheme.color
          : Theme.of(context).scaffoldBackgroundColor,
      child: Column(
        children: <Widget>[
          GestureDetector(
            onTap: handleTap,
            child: Container(
              color: isPaused
                  ? dark
                      ? Color(0xFF0F0F0F)
                      : Theme.of(context).appBarTheme.color
                  : Theme.of(context).scaffoldBackgroundColor,
              padding: EdgeInsets.only(
                  left: 16.0, right: 16.0, top: 20.0, bottom: 20.0),
              child: Column(
                children: <Widget>[
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Stack(
                        overflow: Overflow.visible,
                        children: <Widget>[
                          _buildImage(
                            dark,
                            isPaused,
                            isExpired,
                          ),
                          _buildImageAutoApproveCheck(context, isPaused),
                        ],
                      ),
                      SizedBox(width: 16.0),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            DealNotice(deal),
                            _buildTitle(context, isPaused),
                            SizedBox(height: 4.0),
                            _buildPlaceNameAndAddress(context),
                            _buildDealDetailRow(context, dark),
                            _buildPendingAvatars(context),
                          ],
                        ),
                      ),

                      /// add delete button for draft deals
                      isDraft
                          ? IconButton(
                              icon: Icon(AppIcons.trash),
                              onPressed: handleDelete,
                              color: AppColors.grey300,
                            )
                          : Container(width: 0),

                      hasCompleted
                          ? IconButton(
                              highlightColor: Colors.transparent,
                              icon: Icon(
                                AppIcons.analytics,
                                size: 20,
                              ),
                              color: AppColors.grey300,
                              onPressed: handleTapInsights,
                            )
                          : Container(width: 0)
                    ],
                  ),
                  _buildReactivateButton(context, isPaused),
                  _buildRecreateArchiveButtons(
                      context, isExpired, isArchived, isDraft),
                ],
              ),
            ),
          ),
          Divider(height: 1)
        ],
      ),
    );
  }

  Widget _buildDetail(
    BuildContext context,
    bool dark,
    String value, {
    bool post = false,
    bool both = false,
    bool location = false,
    bool ageRestricted = false,
  }) {
    final String plural =
        value == "1" ? post ? " post" : " story" : post ? " posts" : " stories";
    return Container(
      margin: EdgeInsets.only(right: both && post ? 0.0 : 4.0),
      constraints: BoxConstraints(minWidth: 22.0),
      transform: Matrix4.translationValues(
          both && post ? -8.0 : both && location ? -6.0 : 0.0, 0.0, 0.0),
      decoration: BoxDecoration(
        color: ageRestricted
            ? dark ? Theme.of(context).primaryColor : AppColors.blue100
            : Theme.of(context).canvasColor,
        borderRadius: BorderRadius.circular(20.0),
        border: Border.all(
          width: both && post ? 2.0 : 0,
          color: Theme.of(context).scaffoldBackgroundColor,
        ),
      ),
      padding: EdgeInsets.symmetric(
        horizontal: 8.0,
        vertical: 4.0,
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Visibility(
            visible: location,
            child: Padding(
              padding: EdgeInsets.only(
                right: 2.0,
                bottom: 1.0,
              ),
              child: Icon(
                AppIcons.mapMarkerAltSolid,
                color: Theme.of(context).hintColor,
                size: 10,
              ),
            ),
          ),
          Text(
            location || ageRestricted
                ? value.toString()
                : value.toString() + plural,
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(
                      fontWeight:
                          ageRestricted ? FontWeight.w600 : FontWeight.normal,
                      color: ageRestricted
                          ? dark
                              ? Theme.of(context).scaffoldBackgroundColor
                              : Colors.white
                          : Theme.of(context).hintColor,
                      fontSize: 11),
                ),
          ),
        ],
      ),
    );
  }

  Widget _buildDealDetailRow(BuildContext context, bool dark) {
    List<Widget> mediaLineItems = [];
    if (deal.receiveType != null) {
      if (deal.minAge == 21) {
        mediaLineItems.add(
          _buildDetail(context, dark, "21+",
              location: false, ageRestricted: true),
        );
      }
      if (deal.requestedStories > 0 && deal.requestedPosts > 0) {
        mediaLineItems.add(
          _buildDetail(context, dark, deal.requestedStories.toString(),
              both: true),
        );
        mediaLineItems.add(
          _buildDetail(context, dark, deal.requestedPosts.toString(),
              both: true, post: true),
        );
      } else {
        if (deal.requestedStories > 0) {
          mediaLineItems.add(
            _buildDetail(context, dark, deal.requestedStories.toString()),
          );
        }
        if (deal.requestedPosts > 0) {
          mediaLineItems.add(
            _buildDetail(context, dark, deal.requestedPosts.toString(),
                post: true),
          );
        }
      }
    } else {
      return Container(
        height: 0.0,
        width: 0,
      );
    }

    if (deal.request == null ||
        deal.request.status != DealRequestStatus.denied &&
            deal.request.status != DealRequestStatus.cancelled &&
            deal.request.status != DealRequestStatus.delinquent) {
      return Padding(
        padding: EdgeInsets.only(top: 6.0),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.start,
          children: mediaLineItems,
        ),
      );
    } else {
      return Container(height: 0, width: 0);
    }
  }

  /// re-activate button for deals that are currently paused
  /// will update widget state (not the most elegant but effective)
  Widget _buildReactivateButton(BuildContext context, bool isPaused) =>
      Visibility(
        visible: isPaused,
        child: Padding(
          padding: EdgeInsets.only(top: 16.0),
          child: SecondaryButton(
            context: context,
            fullWidth: true,
            label: "Paused – Reactivate",
            onTap: handleReactivate,
          ),
        ),
      );

  /// buttons below the deal for re-creating expired deals and for archiving
  /// deals that are expired but not yet archvied - these only show when
  /// "expired" if the deal is indeed expired
  /// "archive" if the deal is expired but not yet archived
  Widget _buildRecreateArchiveButtons(
    BuildContext context,
    bool isExpired,
    bool isArchived,
    bool isDraft,
  ) =>
      !isExpired || isArchived || isDraft
          ? Container()
          : Padding(
              padding: EdgeInsets.only(top: 12.0),
              child: Row(
                children: <Widget>[
                  Expanded(
                      child: SecondaryButton(
                    label: "Extend RYDR",
                    onTap: handleExpirationDate,
                    primary: true,
                    context: context,
                  )),
                  SizedBox(width: 8.0),
                  SecondaryButton(
                    label: "Recreate",
                    onTap: handleRecreate,
                    context: context,
                  ),
                  IconButton(
                    onPressed: handleArchive,
                    icon: Icon(
                      AppIcons.archive,
                    ),
                  ),
                ],
              ),
            );

  /// shows avatars of last 5 requests' creators' avatars
  /// only will show if we have actuall requests pending
  Widget _buildPendingAvatars(BuildContext context) {
    if (deal.pendingRecentRequesters == null ||
        deal.pendingRecentRequesters.length == 0) {
      return Container(
        width: 0,
        height: 0,
      );
    }

    List<Widget> pendingRequests = [];
    double fromRight = 0.0;
    double containerWidth = 22;
    int more = deal.pendingRecentRequesters.length - 4;
    int counter = 0;

    deal.pendingRecentRequesters
        .take(4)
        .toList()
        .asMap()
        .forEach((int index, PublisherAccount u) {
      if (counter < 5) {
        pendingRequests.add(Positioned(
            right: fromRight,
            child: Stack(
              alignment: Alignment.center,
              children: <Widget>[
                Container(
                  height: 44.0,
                  width: 44.0,
                  decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(30.0),
                      color: Theme.of(context).scaffoldBackgroundColor),
                ),
                UserAvatar(u, hideBorder: true),
              ],
            )));

        fromRight += 22;
        containerWidth += 22;
      }

      counter++;
    });

    if (more > 0) {
      pendingRequests.insert(
          0,
          Positioned(
            left: 90.0,
            child: SizedBox(
              height: 40.0,
              width: 40.0,
              child: CircleAvatar(
                backgroundColor: AppColors.grey300.withOpacity(0.2),
                foregroundColor: Theme.of(context).primaryColor,
                child: Text(
                  '...',
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(color: Theme.of(context).hintColor),
                      ),
                ),
              ),
            ),
          ));
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        SizedBox(height: 6.0),
        Container(
          width: containerWidth,
          alignment: Alignment.centerLeft,
          height: 44,
          child: GestureDetector(
            onTap: () {
              Navigator.of(context).pushNamed(
                AppRouting.getRequestsPending,
                arguments: ListPageArguments(
                  layoutType: ListPageLayout.StandAlone,
                  filterDealId: deal.id,
                  filterDealName: deal.title,
                  filterRequestStatus: [
                    DealRequestStatus.requested,
                    DealRequestStatus.invited,
                  ],
                ),
              );
            },
            child: Stack(
              overflow: Overflow.visible,
              children: pendingRequests,
              alignment: Alignment.centerLeft,
            ),
          ),
        ),
        SizedBox(height: 2.0),
      ],
    );
  }

  /// if we have a place (we should always have one)
  /// then this will display the place name and address on two lines
  Widget _buildPlaceNameAndAddress(BuildContext context) => deal.place == null
      ? Container()
      : Row(
          children: <Widget>[
            deal.dealType == DealType.Virtual
                ? Text(
                    "Virtual • ",
                    style: TextStyle(
                      fontSize: 12.0,
                      fontWeight: FontWeight.bold,
                      color: Colors.deepOrange,
                    ),
                  )
                : Container(height: 0, width: 0),
            Flexible(
              child: Text(
                deal.place.name ?? "Location Unknown",
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: 12.0,
                  fontWeight: FontWeight.w500,
                  color: Theme.of(context).hintColor,
                ),
              ),
            ),
          ],
        );

  /// Builds the image in the list and will add opacity for paused
  /// and archived deals to indicate the difference from active ones
  Widget _buildImage(
    bool dark,
    bool isPaused,
    bool isExpired,
  ) =>
      ClipRRect(
        borderRadius: BorderRadius.circular(8.0),
        child: deal.publisherMedias != null && deal.publisherMedias.isNotEmpty
            ? CachedNetworkImage(
                imageUrl: deal.publisherMedias[0].previewUrl,
                imageBuilder: (context, imageProvider) => Container(
                  width: 64.0,
                  height: 64.0,
                  foregroundDecoration: BoxDecoration(
                    backgroundBlendMode: BlendMode.saturation,
                    color: Theme.of(context)
                        .scaffoldBackgroundColor
                        .withOpacity(isPaused ? 0.7 : isExpired ? 1.0 : 0.0),
                  ),
                  decoration: BoxDecoration(
                    color: Theme.of(context).appBarTheme.color,
                    image: DecorationImage(
                      alignment: Alignment.center,
                      fit: BoxFit.cover,
                      image: imageProvider,
                    ),
                  ),
                ),
                placeholder: (context, url) => Shimmer.fromColors(
                  baseColor: dark ? Color(0xFF121212) : AppColors.white100,
                  highlightColor: dark ? Colors.black : AppColors.white50,
                  child:
                      Container(width: 64.0, height: 64, color: Colors.white),
                ),
                errorWidget: (context, url, error) => ImageError(
                  logUrl: url,
                  logParentName: 'main/widgets/list_deal.dart > _buildImage',
                  errorWidget: Container(
                    width: 64.0,
                    height: 64,
                    color: AppColors.grey300.withOpacity(0.2),
                  ),
                ),
              )
            : Container(
                width: 64.0,
                height: 64.0,
                color: AppColors.white20,
                child: Center(
                  child: Stack(
                    alignment: Alignment.center,
                    children: <Widget>[
                      SizedBox(
                        height: 18.0,
                        child: SvgPicture.asset(
                          'assets/icons/rydr-r.svg',
                          color: AppColors.grey300.withOpacity(0.33),
                        ),
                      ),
                      Container(
                        width: 36.0,
                        height: 36.0,
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(40.0),
                          border: Border.all(
                            color: AppColors.grey300.withOpacity(0.33),
                          ),
                        ),
                      )
                    ],
                  ),
                ),
              ),
      );

  /// for auto-approve deals this will position a gree checkmark
  /// to the bottom-right of the image (with opacity if the deal is paused)
  Widget _buildImageAutoApproveCheck(
    BuildContext context,
    bool isPaused,
  ) =>
      deal.autoApproveRequests
          ? Positioned(
              bottom: -2,
              right: -2,
              child: Stack(
                alignment: Alignment.center,
                children: <Widget>[
                  Container(
                    height: 18.0,
                    width: 18.0,
                    decoration: BoxDecoration(
                      color: Theme.of(context).scaffoldBackgroundColor,
                      borderRadius: BorderRadius.circular(10.0),
                    ),
                  ),
                  Container(
                    height: 14.0,
                    width: 14.0,
                    decoration: BoxDecoration(
                      color: isPaused
                          ? AppColors.successGreen.withOpacity(0.25)
                          : AppColors.successGreen,
                      borderRadius: BorderRadius.circular(10.0),
                    ),
                    child: Center(
                      child: Icon(
                        AppIcons.checkReg,
                        size: 10.0,
                        color: AppColors.white,
                      ),
                    ),
                  )
                ],
              ),
            )
          : Container();

  /// builds the title, cleaning up the name to ensure we get 'nice' line breaks
  Widget _buildTitle(
    BuildContext context,
    bool isPaused,
  ) =>
      Padding(
        padding: EdgeInsets.only(right: 8.0),
        child: Text(
          deal.titleClean,
          overflow: TextOverflow.ellipsis,
          maxLines: 2,
          style: TextStyle(
            color: isPaused
                ? AppColors.grey300
                : Theme.of(context).textTheme.headline4.color,
            fontSize: 16,
            fontWeight: FontWeight.w600,
          ),
        ),
      );
}
