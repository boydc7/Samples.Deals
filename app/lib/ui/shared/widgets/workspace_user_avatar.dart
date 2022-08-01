import 'package:flutter/material.dart';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';

class WorkspaceUserAvatar extends StatelessWidget {
  final WorkspaceUser user;

  WorkspaceUserAvatar(this.user);

  @override
  Widget build(BuildContext context) {
    final double width = 40.0;
    final Widget noUser = Container(
      alignment: Alignment.center,
      width: width,
      height: width,
      decoration: BoxDecoration(
        color: Theme.of(context).canvasColor,
        borderRadius: BorderRadius.circular(width),
      ),
      child: Icon(
        AppIcons.user,
        size: 18.0,
        color: Theme.of(context).textTheme.bodyText1.color,
      ),
    );

    /// guard against null-user which can happen on re-builds of widget
    /// while user has/had been removed from the device...
    if (user.avatar == null) {
      return noUser;
    }

    return Container(
      decoration: BoxDecoration(
        color: Theme.of(context).canvasColor,
        borderRadius: BorderRadius.circular(width),
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(width * 2),
        child: CachedNetworkImage(
          width: width - 1,
          imageUrl: user.avatar,
          placeholder: (context, url) => CircularProgressIndicator(
            strokeWidth: 1,
            valueColor:
                AlwaysStoppedAnimation<Color>(Theme.of(context).hintColor),
          ),
          errorWidget: (context, url, error) => ImageError(
            logUrl: url,
            logParentName: 'shared/widgets/workspace_user_avatar.dart',
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
    );
  }
}
