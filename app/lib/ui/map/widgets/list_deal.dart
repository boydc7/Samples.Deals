import 'package:flutter/material.dart';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/ui/deal/widgets/shared/notice.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/place.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:shimmer/shimmer.dart';

class MapListDeal extends StatelessWidget {
  final Deal deal;

  MapListDeal(this.deal);

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final bool _isVirtual =
        deal?.dealType != null ? deal?.dealType == DealType.Virtual : false;

    return Column(
      children: <Widget>[
        Container(
          color: Colors.transparent,
          padding: EdgeInsets.only(left: 16.0, top: 20.0, bottom: 20.0),
          child: Column(
            children: <Widget>[
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Stack(
                    overflow: Overflow.visible,
                    children: <Widget>[
                      _buildImage(dark),
                      _buildBusinessAvatar(context, deal),
                    ],
                  ),
                  SizedBox(width: 16.0),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        DealNotice(deal),
                        Padding(
                          padding: EdgeInsets.only(right: 12.0),
                          child: Text(
                            deal.titleClean,
                            overflow: TextOverflow.ellipsis,
                            maxLines: 2,
                            style: TextStyle(
                              color:
                                  Theme.of(context).textTheme.headline4.color,
                              fontSize: 17.5,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                        SizedBox(height: 4.0),
                        _buildPlaceNameAndAddress(context, deal.place),
                        _buildDealDetailRow(context, deal, _isVirtual),
                      ],
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
        Divider(height: 1)
      ],
    );
  }

  Widget _buildDetail(BuildContext context, String value,
      {bool post = false,
      bool both = false,
      bool location = false,
      bool ageRestricted = false}) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final String plural =
        value == "1" ? post ? " post" : " story" : post ? " posts" : " stories";
    final bool virtual = value == "Virtual";
    return Container(
      margin: EdgeInsets.only(
          right: both && post
              ? 0.0
              : deal.dealType == DealType.Event && !location ? 12.0 : 4.0),
      constraints: BoxConstraints(minWidth: 22.0),
      transform: Matrix4.translationValues(
          both && post ? -8.0 : both && location ? -6.0 : 0.0, 0.0, 0.0),
      decoration: BoxDecoration(
        color: virtual
            ? Colors.deepOrange
            : ageRestricted
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
            location ||
                    ageRestricted ||
                    virtual ||
                    deal.dealType == DealType.Event
                ? value.toString()
                : value.toString() + plural,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(
                      fontWeight: ageRestricted ||
                              virtual ||
                              deal.dealType == DealType.Event
                          ? FontWeight.w600
                          : FontWeight.normal,
                      color: ageRestricted || virtual
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

  Widget _buildDealDetailRow(BuildContext context, Deal deal, bool isVirtual) {
    List<Widget> mediaLineItems = [];
    if (deal.receiveType != null) {
      if (deal.minAge == 21) {
        mediaLineItems.add(
          _buildDetail(context, "21+", location: false, ageRestricted: true),
        );
      }
      if (isVirtual) {
        mediaLineItems.add(
          _buildDetail(context, "Virtual",
              location: false, ageRestricted: false),
        );
      }
      if (deal.requestedStories > 0 &&
          deal.requestedPosts > 0 &&
          deal.dealType != DealType.Event) {
        mediaLineItems.add(
          _buildDetail(context, deal.requestedStories.toString(), both: true),
        );
        mediaLineItems.add(
          _buildDetail(context, deal.requestedPosts.toString(),
              both: true, post: true),
        );
      } else {
        if (deal.requestedStories > 0 && deal.dealType != DealType.Event) {
          mediaLineItems.add(
            _buildDetail(context, deal.requestedStories.toString()),
          );
        }
        if (deal.requestedPosts > 0 && deal.dealType != DealType.Event) {
          mediaLineItems.add(
            _buildDetail(context, deal.requestedPosts.toString(), post: true),
          );
        }
        if (deal.dealType == DealType.Event) {
          mediaLineItems.add(
            _buildDetail(
                context, Utils.formatDateShortWithTime(deal.startDate)),
          );
        }
      }
      if (appState.currentProfile.isCreator) {
        mediaLineItems.add(
          _buildDetail(context, deal.distanceInMilesDisplay,
              location: true,
              both: deal.requestedStories > 0 && deal.requestedPosts > 0),
        );
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
      return Container(
        padding: EdgeInsets.only(top: 6.0),
        height: 30,
        child: ListView(
          scrollDirection: Axis.horizontal,
          children: mediaLineItems,
        ),
      );
    } else {
      return Container(height: 0, width: 0);
    }
  }

  /// if we have a place (we should always have one)
  /// then this will display the place name and address on two lines
  Widget _buildPlaceNameAndAddress(BuildContext context, Place place) {
    return place == null
        ? Container()
        : Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                place.name ?? "",
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: 12.0,
                  fontWeight: FontWeight.w500,
                  color: Theme.of(context).hintColor,
                ),
              ),
            ],
          );
  }

  Widget _buildImage(bool dark) {
    bool greyScale = deal.status == DealStatus.paused ||
        deal.status == DealStatus.deleted ||
        deal.status == DealStatus.draft;
    return ClipRRect(
      borderRadius: BorderRadius.circular(8.0),
      child: deal.publisherMedias != null && deal.publisherMedias.isNotEmpty
          ? CachedNetworkImage(
              imageUrl: deal.publisherMedias[0].previewUrl,
              imageBuilder: (context, imageProvider) => Container(
                width: 96.0,
                height: 64.0,
                foregroundDecoration: BoxDecoration(
                  backgroundBlendMode: greyScale ? BlendMode.saturation : null,
                  color: greyScale
                      ? Theme.of(context).scaffoldBackgroundColor
                      : null,
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
                child: Container(width: 96.0, height: 64, color: Colors.white),
              ),
              errorWidget: (context, url, error) => ImageError(
                logUrl: url,
                logParentName: 'map/widgets/list_deal.dart > _buildImage',
                errorWidget: Container(
                  width: 96.0,
                  height: 64,
                  color: AppColors.grey300.withOpacity(0.2),
                ),
              ),
            )
          : Container(
              width: 96.0,
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
  }

  Widget _buildBusinessAvatar(BuildContext context, Deal deal) {
    if (appState.currentProfile.isCreator) {
      return Positioned(
        bottom: -14,
        right: 33,
        child: Stack(
          alignment: Alignment.center,
          children: <Widget>[
            Container(
              height: 28.0,
              width: 28.0,
              decoration: BoxDecoration(
                color: Theme.of(context).scaffoldBackgroundColor,
                borderRadius: BorderRadius.circular(30.0),
              ),
            ),
            CachedNetworkImage(
              imageUrl: deal.publisherAccount.profilePicture,
              imageBuilder: (context, imageProvider) => Container(
                height: 24.0,
                width: 24.0,
                decoration: BoxDecoration(
                  image: DecorationImage(
                    fit: BoxFit.cover,
                    image: imageProvider,
                  ),
                  borderRadius: BorderRadius.circular(30.0),
                ),
              ),
              errorWidget: (context, url, error) => ImageError(
                logUrl: url,
                logParentName:
                    'map/widgets/list_deal.dart > _buildBusinessAvatar',
              ),
            ),
          ],
        ),
      );
    } else {
      return Container();
    }
  }
}
