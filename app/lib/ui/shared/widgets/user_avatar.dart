import 'package:flutter/material.dart';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';

class UserAvatar extends StatelessWidget {
  final PublisherAccount profile;
  final double width;
  final bool hideBorder;
  final bool linkToIg;
  final bool showPaid;
  final bool isPostDetected;
  final DealRequestStatus requestStatus;

  UserAvatar(
    this.profile, {
    this.width = 40.0,
    this.hideBorder = false,
    this.linkToIg = false,
    this.showPaid = false,
    this.requestStatus,
    this.isPostDetected = false,
  });

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final Widget noUser = Container(
      alignment: Alignment.center,
      width: width,
      height: width,
      decoration: BoxDecoration(
        color: isPostDetected
            ? Theme.of(context).primaryColor
            : Theme.of(context).canvasColor,
        borderRadius: BorderRadius.circular(width),
        border: Border.all(
          width: isPostDetected ? 0.0 : 1.0,
          color: Color(0xd9d9d9),
        ),
      ),
      child: isPostDetected
          ? Stack(
              overflow: Overflow.visible,
              children: <Widget>[
                Icon(
                  AppIcons.cameraRetro,
                  size: 18.0,
                  color: Theme.of(context).scaffoldBackgroundColor,
                ),
                Positioned(
                  top: -3,
                  left: -3,
                  child: Icon(
                    AppIcons.starChristmasSolid,
                    size: 12.0,
                    color: Theme.of(context).scaffoldBackgroundColor,
                  ),
                ),
              ],
            )
          : Icon(
              AppIcons.user,
              size: 18.0,
              color: Theme.of(context).scaffoldBackgroundColor,
            ),
    );

    /// guard against null-user which can happen on re-builds of widget
    /// while user has/had been removed from the device...
    if (profile == null || profile.profilePicture == null) {
      return noUser;
    }

    if (requestStatus != null) {
      return GestureDetector(
        onTap: linkToIg
            ? () => Utils.launchUrl(
                  context,
                  "https://instagram.com/${profile.userName}",
                  trackingName: 'profile',
                )
            : null,
        child: Stack(
          alignment: Alignment.bottomRight,
          children: <Widget>[
            UserAvatar(profile),
            Stack(
              alignment: Alignment.center,
              children: <Widget>[
                Container(
                  height: 12.0,
                  width: 12.0,
                  decoration: BoxDecoration(
                    color: Theme.of(context).scaffoldBackgroundColor,
                    borderRadius: BorderRadius.circular(10.0),
                  ),
                ),
                Container(
                  height: 8.0,
                  width: 8.0,
                  decoration: BoxDecoration(
                    color: Utils.getRequestStatusColor(requestStatus, dark),
                    borderRadius: BorderRadius.circular(10.0),
                  ),
                )
              ],
            ),
          ],
        ),
      );
    }

    return GestureDetector(
      onTap: linkToIg
          ? () => Utils.launchUrl(
                context,
                "https://instagram.com/${profile.userName}",
                trackingName: 'profile',
              )
          : null,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          Visibility(
            visible: showPaid,
            child: Container(
              width: width + 5,
              height: width + 5,
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  colors: [
                    Theme.of(context).primaryColor,
                    AppColors.successGreen,
                    Colors.yellowAccent,
                  ],
                  stops: [0.1, 0.5, 0.9],
                  begin: Alignment.topRight,
                  end: Alignment.bottomLeft,
                ),
                borderRadius: BorderRadius.circular(width),
              ),
            ),
          ),
          Visibility(
            visible: showPaid,
            child: Container(
              width: width + 2,
              height: width + 2,
              decoration: BoxDecoration(
                color: Theme.of(context).appBarTheme.color,
                borderRadius: BorderRadius.circular(width),
              ),
            ),
          ),
          Container(
            width: width - 1,
            height: width - 1,
            decoration: BoxDecoration(
              color: Theme.of(context).canvasColor,
              borderRadius: BorderRadius.circular(width),
              border: Border.all(
                width: hideBorder ? 0.0 : 1.0,
                color: dark ? AppColors.grey800 : Color(0xd9d9d9),
              ),
            ),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(width * 2),
              child: CachedNetworkImage(
                width: width - 1,
                imageUrl: profile.profilePicture,
                placeholder: (context, url) => CircularProgressIndicator(
                  strokeWidth: 1,
                  valueColor: AlwaysStoppedAnimation<Color>(
                      Theme.of(context).hintColor),
                ),
                errorWidget: (context, url, error) => ImageError(
                  logUrl: url,
                  logParentName: 'shared/widgets/user_avatar.dart',
                  logPublisherAccountId: profile.id,
                  errorWidget: Container(
                    width: width,
                    height: width,
                    color: AppColors.blue100,
                    child: Center(
                      child: Icon(
                        AppIcons.userAstronaut,
                        size: 20.0,
                        color: AppColors.white,
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
