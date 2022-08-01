import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:shimmer/shimmer.dart';

class MediaCachedImage extends StatelessWidget {
  final String imageUrl;
  final double width;
  final double height;
  final double marginRight;

  MediaCachedImage({
    this.imageUrl,
    this.width,
    this.height,
    this.marginRight = 0.0,
  });

  @override
  Widget build(BuildContext context) {
    final bool darkMode = Theme.of(context).brightness == Brightness.dark;

    return CachedNetworkImage(
      imageUrl: imageUrl,
      imageBuilder: (context, imageProvider) => Container(
        margin: EdgeInsets.only(right: marginRight),
        decoration: BoxDecoration(
          image: DecorationImage(
            image: imageProvider,
            fit: BoxFit.cover,
          ),
        ),
      ),
      placeholder: (context, url) => Shimmer.fromColors(
          baseColor: darkMode ? Color(0xFF121212) : AppColors.white100,
          highlightColor: darkMode ? Colors.black : AppColors.white50,
          child: Container(
            color: Colors.yellow,
            width: width,
            height: height,
            margin: EdgeInsets.only(right: marginRight),
          )),
      errorWidget: (context, url, error) => ImageError(
          logUrl: url,
          logParentName: 'profile/widgets/insights_media_helpers.dart',
          errorWidget: Container(
              alignment: Alignment.center,
              color: AppColors.grey300.withOpacity(0.2),
              width: width,
              height: height,
              margin: EdgeInsets.only(right: marginRight))),
    );
  }
}
