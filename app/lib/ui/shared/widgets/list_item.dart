import 'package:flutter/material.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';

Widget sectionDivider(BuildContext context) {
  final bool darkTheme = Theme.of(context).brightness == Brightness.dark;

  return Container(
    width: double.infinity,
    height: darkTheme ? 12.0 : 6.0,
    decoration: BoxDecoration(
      border: Border(
        bottom: BorderSide(
            color: darkTheme ? Color(0xFF090909) : Colors.grey.shade300,
            width: 0.5),
        top: BorderSide(
            color: darkTheme ? Color(0xFF090909) : Colors.grey.shade300,
            width: 0.5),
      ),
      color: darkTheme ? Color(0xFF0D0D0D) : Colors.grey.shade200,
    ),
  );
}

Widget basicListItem({
  BuildContext context,
  bool noPaddingHorizontal = false,
  bool normalPaddingHorizontal = false,
  bool noPaddingVertical = false,
  bool isThreeLine = false,
  bool isInvite = false,
  bool isEvent = false,
  bool wasInvite = false,
  bool isVirtual = false,
  bool isDelinquent = false,
  double paddingVert = 4.0,
  Function onTap,
  Widget leading,
  String title,
  String titleSuffix,
  String subtitle,
  Widget widgetTitle,
  Widget widgetSubtitle,
  Widget trailing,
}) {
  bool dark = Theme.of(context).brightness == Brightness.dark;
  return ListTile(
    contentPadding: EdgeInsets.symmetric(
        horizontal:
            noPaddingHorizontal ? 0.0 : normalPaddingHorizontal ? 16.0 : 8.0,
        vertical: noPaddingVertical
            ? 0.0
            : paddingVert == null ? paddingVert : paddingVert),
    onTap: onTap,
    leading: leading,
    title: title != null
        ? titleSuffix != null
            ? Row(
                children: <Widget>[
                  Text(
                    title,
                    style: Theme.of(context).textTheme.bodyText1.merge(
                          TextStyle(
                            color: isDelinquent
                                ? AppColors.errorRed
                                : Theme.of(context).textTheme.bodyText1.color,
                          ),
                        ),
                  ),
                  Expanded(
                    child: Padding(
                      padding: EdgeInsets.only(left: 8.0),
                      child: Text(
                        titleSuffix,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.bodyText2.merge(
                              TextStyle(
                                color: isDelinquent
                                    ? AppColors.errorRed
                                    : Theme.of(context)
                                        .textTheme
                                        .bodyText2
                                        .color,
                              ),
                            ),
                        strutStyle: StrutStyle(
                          height: Theme.of(context).textTheme.bodyText2.height,
                          forceStrutHeight: true,
                        ),
                      ),
                    ),
                  ),
                ],
              )
            : Text(
                title,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodyText1,
              )
        : widgetTitle,
    subtitle: subtitle != null
        ? Row(
            mainAxisAlignment: MainAxisAlignment.start,
            children: <Widget>[
              isInvite && isEvent
                  ? Container(
                      height: 18,
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(18),
                        color: Utils.getRequestStatusColor(
                            DealRequestStatus.invited, dark),
                      ),
                      margin: EdgeInsets.only(right: 8),
                      padding: EdgeInsets.symmetric(horizontal: 8),
                      child: Row(
                        children: <Widget>[
                          Padding(
                            padding: EdgeInsets.only(bottom: 1),
                            child: Icon(AppIcons.calendarStarSolid,
                                size: 11.0,
                                color:
                                    Theme.of(context).scaffoldBackgroundColor),
                          ),
                          SizedBox(width: 4.0),
                          Text(
                            appState.currentProfile.isBusiness
                                ? "EVENT INVITE SENT"
                                : "EVENT INVITE",
                            style: Theme.of(context).textTheme.caption.merge(
                                  TextStyle(
                                    fontSize: 10,
                                    color: Theme.of(context)
                                        .scaffoldBackgroundColor,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                          ),
                        ],
                      ),
                    )
                  : isInvite
                      ? Container(
                          height: 18,
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(18),
                            color: Utils.getRequestStatusColor(
                                DealRequestStatus.invited, dark),
                          ),
                          margin: EdgeInsets.only(right: 8),
                          padding: EdgeInsets.symmetric(horizontal: 8),
                          child: Row(
                            children: <Widget>[
                              Padding(
                                padding: EdgeInsets.only(bottom: 1),
                                child: Icon(AppIcons.starsSolid,
                                    size: 11.0,
                                    color: Theme.of(context)
                                        .scaffoldBackgroundColor),
                              ),
                              SizedBox(width: 4.0),
                              Text(
                                appState.currentProfile.isBusiness
                                    ? "INVITE SENT"
                                    : "INVITE",
                                style:
                                    Theme.of(context).textTheme.caption.merge(
                                          TextStyle(
                                            fontSize: 10,
                                            color: Theme.of(context)
                                                .scaffoldBackgroundColor,
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                              ),
                            ],
                          ),
                        )
                      : wasInvite
                          ? Container(
                              height: 18,
                              width: 18,
                              decoration: BoxDecoration(
                                borderRadius: BorderRadius.circular(18),
                                color: Utils.getRequestStatusColor(
                                    DealRequestStatus.invited, dark),
                              ),
                              margin: EdgeInsets.only(right: 8),
                              child: Center(
                                child: Padding(
                                  padding: EdgeInsets.only(bottom: 1),
                                  child: Icon(AppIcons.starsSolid,
                                      size: 11.0,
                                      color: Theme.of(context)
                                          .scaffoldBackgroundColor),
                                ),
                              ),
                            )
                          : isVirtual
                              ? Container(
                                  height: 18,
                                  decoration: BoxDecoration(
                                    borderRadius: BorderRadius.circular(18),
                                    color: Colors.deepOrange,
                                  ),
                                  margin: EdgeInsets.only(right: 8),
                                  padding: EdgeInsets.symmetric(horizontal: 6),
                                  child: Center(
                                    child: Text(
                                      "V",
                                      style: Theme.of(context)
                                          .textTheme
                                          .caption
                                          .merge(
                                            TextStyle(
                                              fontSize: 10,
                                              color: Theme.of(context)
                                                  .scaffoldBackgroundColor,
                                              fontWeight: FontWeight.bold,
                                            ),
                                          ),
                                    ),
                                  ),
                                )
                              : Container(),
              Expanded(
                child: Text(
                  subtitle,
                  overflow: TextOverflow.ellipsis,
                  maxLines: isThreeLine ? 2 : 1,
                  style: Theme.of(context).textTheme.subtitle2.merge(
                        TextStyle(
                          color: isDelinquent
                              ? AppColors.errorRed
                              : Theme.of(context).textTheme.subtitle2.color,
                        ),
                      ),
                ),
              ),
            ],
          )
        : widgetSubtitle,
    trailing: trailing,
  );
}

Widget rydrListItem({
  @required BuildContext context,
  IconData icon,
  String iconSvgUrl,
  double iconSvgWidth = 28.0,
  String title,
  String subtitle,
  Function onTap,
  bool subtitleIsHint = false,
  bool lastInList = false,
  bool loading = false,
  bool isBasic = false,
}) {
  return GestureDetector(
    onTap: loading ? null : onTap != null ? onTap : null,
    child: Container(
      color: Colors.transparent,
      child: Column(
        children: <Widget>[
          SizedBox(
            height: 12.0,
          ),
          Row(
            mainAxisAlignment: MainAxisAlignment.start,
            crossAxisAlignment: subtitle != null && subtitle != ''
                ? CrossAxisAlignment.start
                : CrossAxisAlignment.center,
            children: <Widget>[
              icon == null
                  ? Stack(
                      alignment: Alignment.center,
                      children: <Widget>[
                        Container(
                          width: 72,
                          height: 40,
                        ),
                        SvgPicture.asset(
                          iconSvgUrl,
                          width: iconSvgWidth,
                          color: Theme.of(context).brightness == Brightness.dark
                              ? Theme.of(context).appBarTheme.iconTheme.color
                              : Theme.of(context).iconTheme.color,
                        ),
                      ],
                    )
                  : Container(
                      width: 72,
                      height: 40,
                      child: Icon(
                        icon,
                        color: Theme.of(context).brightness == Brightness.dark
                            ? Theme.of(context).appBarTheme.iconTheme.color
                            : Theme.of(context).iconTheme.color,
                      ),
                    ),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    SizedBox(
                      height: 4.0,
                    ),
                    Text(title, style: Theme.of(context).textTheme.bodyText1),
                    SizedBox(
                      height: 6.0,
                    ),
                    subtitle != null && subtitle != ''
                        ? Padding(
                            padding: EdgeInsets.only(right: 16.0),
                            child: Text(subtitle,
                                overflow: subtitleIsHint
                                    ? TextOverflow.ellipsis
                                    : TextOverflow.visible,
                                style: subtitleIsHint
                                    ? Theme.of(context)
                                        .textTheme
                                        .bodyText2
                                        .merge(TextStyle(
                                            color: Theme.of(context).hintColor))
                                    : Theme.of(context).textTheme.bodyText2),
                          )
                        : Container(
                            height: 0,
                          )
                  ],
                ),
              ),
              loading
                  ? Container(
                      width: 24,
                      height: 40,
                      margin: EdgeInsets.only(right: 16, top: 4),
                      child: Center(
                        child: SizedBox(
                          width: 24,
                          height: 24,
                          child: CircularProgressIndicator(
                            strokeWidth: 1.5,
                            valueColor: AlwaysStoppedAnimation<Color>(
                              Theme.of(context).hintColor.withOpacity(0.5),
                            ),
                          ),
                        ),
                      ),
                    )
                  : isBasic
                      ? Container(
                          margin:
                              EdgeInsets.only(top: 16, left: 8.0, right: 16.0),
                          child: Icon(
                            AppIcons.lock,
                            size: 20,
                            color: Theme.of(context).primaryColor,
                          ),
                        )
                      : onTap != null
                          ? Container(
                              width: 10.0,
                              margin: EdgeInsets.only(left: 16.0, right: 16.0),
                              child: Icon(AppIcons.angleRight),
                            )
                          : Container(width: 0, height: 0),
            ],
          ),
          SizedBox(
            height: subtitle != null && subtitle != '' ? 20.0 : 12.0,
          ),
          Visibility(
            visible: !lastInList,
            child: Divider(
              height: 1,
              indent: 72.0,
            ),
          )
        ],
      ),
    ),
  );
}
