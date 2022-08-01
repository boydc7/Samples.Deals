import 'dart:ui';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';

class DealHeaderAppBar extends SliverPersistentHeaderDelegate {
  final Deal deal;
  final double expandedHeight;

  final double imageHeight = 220;

  DealHeaderAppBar({this.deal, this.expandedHeight});

  @override
  Widget build(
    BuildContext context,
    double shrinkOffset,
    bool overlapsContent,
  ) =>
      Container(
        decoration: BoxDecoration(
          color: Theme.of(context).scaffoldBackgroundColor,
          borderRadius: BorderRadius.only(
            topLeft: Radius.circular(16.0),
            topRight: Radius.circular(16.0),
          ),
        ),
        child: ClipRRect(
          borderRadius: BorderRadius.only(
            topLeft: Radius.circular(16.0),
            topRight: Radius.circular(16.0),
          ),
          child: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              deal.publisherMedias != null && deal.publisherMedias.length > 0
                  ? _buildImg(context, deal.publisherMedias[0])
                  : _buildImgPlaceholder(context),
              DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.bottomCenter,
                    end: Alignment.center,
                    colors: <Color>[
                      Colors.black38,
                      Colors.black.withOpacity(0),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      );

  Widget _buildImgPlaceholder(BuildContext context) => Container(
        width: double.infinity,
        height: imageHeight,
        color: Theme.of(context).canvasColor,
      );

  Widget _buildImg(BuildContext context, PublisherMedia post) =>
      CachedNetworkImage(
        imageUrl: post.previewUrl,
        imageBuilder: (context, imageProvider) => Container(
          width: double.infinity,
          height: imageHeight,
          decoration: BoxDecoration(
            color: Theme.of(context).canvasColor,
            image: DecorationImage(
              alignment: Alignment.center,
              fit: BoxFit.cover,
              image: imageProvider,
            ),
          ),
        ),
        errorWidget: (context, url, error) => ImageError(
          logUrl: url,
          logParentName: 'map/widgets/deal.dart > _buildImg',
        ),
      );

  @override
  double get maxExtent => expandedHeight;

  @override
  double get minExtent => expandedHeight <= 50 ? expandedHeight : 50;

  @override
  bool shouldRebuild(SliverPersistentHeaderDelegate oldDelegate) => true;
}
