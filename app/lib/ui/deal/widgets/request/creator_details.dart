import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_metric.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/ui/deal/blocs/user_profile_card.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:shimmer/shimmer.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/publisher_account.dart';

import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

/// Shows details about the creator on a request thats eitehr an invite
/// or when its in progress or redeemed
class RequestCreatorDetails extends StatefulWidget {
  final Deal deal;

  RequestCreatorDetails(this.deal);

  @override
  State<StatefulWidget> createState() {
    return _RequestCreatorDetailsState();
  }
}

class _RequestCreatorDetailsState extends State<RequestCreatorDetails> {
  RequestUserProfileCardBloc _bloc;

  final NumberFormat f = NumberFormat.compact();
  final NumberFormat p = NumberFormat.decimalPercentPattern(decimalDigits: 1);
  final NumberFormat c = NumberFormat.compactSimpleCurrency();

  ThemeData _theme;
  bool _darkMode;

  @override
  void initState() {
    super.initState();

    _bloc = RequestUserProfileCardBloc(widget.deal);
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _goToProfile() {
    Utils.goToProfile(
      context,
      widget.deal.request.publisherAccount,
      widget.deal,
    );
  }

  void _goToDialog() =>
      Navigator.of(context).pushNamed(AppRouting.getRequestDialogRoute(
          widget.deal.id, widget.deal.request.publisherAccount.id));

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;

    /// creators don't see this on the request
    if (appState.currentProfile.isCreator) {
      return Container();
    }

    return widget.deal.request.canViewRequestersProfile
        ? _buildFullView(widget.deal.request.publisherAccount)
        : _buildSimpleView(widget.deal.request.publisherAccount);
  }

  /// this only shows the creators profile avatar and handle/display name
  /// we use this for when the creator is still soft linked, or the request
  /// is in a status where we don't support full stats (e.g. invites, cancelled, etc.)
  Widget _buildSimpleView(PublisherAccount user) => GestureDetector(
        onTap: _goToProfile,
        child: Column(
          children: <Widget>[
            sectionDivider(context),
            ListTile(
              leading: UserAvatar(user),
              title: Text(
                user.isAccountSoft
                    ? user.userName
                    : '${user.userName} · ${Utils.formatDoubleForDisplay(user.publisherMetrics.followedBy)} followers',
                style: TextStyle(
                  fontWeight: FontWeight.w600,
                  color: _theme.textTheme.bodyText2.color,
                ),
              ),
              subtitle: Text(
                user.nameDisplay,
                overflow: TextOverflow.ellipsis,
                style: _theme.textTheme.bodyText2.merge(
                  TextStyle(color: AppColors.grey300),
                ),
              ),
            ),
          ],
        ),
      );

  /// full set of creator details & stats when the status of the request
  /// allows the business to see more of their stats
  Widget _buildFullView(PublisherAccount user) {
    final PublisherMetrics metrics = user.publisherMetrics;
    //final double postCpm = metrics.postCPM(widget.deal.value) ?? 0;
    //final double storyCpm = metrics.storyCPM(widget.deal.value) ?? 0;

    return GestureDetector(
      onTap: _goToProfile,
      child: Column(
        children: <Widget>[
          sectionDivider(context),
          ListTile(
            leading: UserAvatar(user),
            title: Text(
              user.userName,
              style: TextStyle(
                fontWeight: FontWeight.w600,
                color: _theme.textTheme.bodyText2.color,
              ),
            ),
            subtitle: Text(
              '${Utils.formatDoubleForDisplay(user.publisherMetrics.followedBy)} followers',

              ///'${postCpm > 0 ? c.format(postCpm) + " post CPM" : ""} ${storyCpm > 0 ? " · " : ""} ${storyCpm > 0 ? c.format(storyCpm) + " story CPM" : ""}',
              overflow: TextOverflow.ellipsis,
              style: _theme.textTheme.bodyText2.merge(
                TextStyle(color: AppColors.grey300),
              ),
            ),
            trailing: Icon(widget.deal.request.publisherAccount.isPrivate
                ? AppIcons.lock
                : AppIcons.angleRight),
          ),
          Column(
            children: <Widget>[
              _buildRecentMedia(),
              Visibility(
                visible: widget.deal.request.publisherAccount.isAccountFull,
                child: Column(
                  children: <Widget>[
                    _buildStat(
                      "Reach & Impressions",
                      "${f.format(metrics.avgStoryReach?.toInt() ?? 0)} / ${f.format(metrics.avgStoryImpressions?.toInt() ?? 0)}",
                      header: true,
                      headerLabel: "Stories",
                    ),
                    _buildStat(
                      "Reach & Impressions",
                      "${f.format(metrics.avgPostReach?.toInt() ?? 0)} / ${f.format(metrics.avgPostImpressions?.toInt() ?? 0)}",
                      header: true,
                      headerLabel: "Posts",
                    ),
                    Divider(indent: 72, height: 1),
                    SizedBox(height: 12),
                    _buildStat(
                      "Like to Follower Ratio",
                      p.format(metrics.avgLikes / metrics.followedBy ?? 0),
                    ),
                    Divider(indent: 72, height: 1),
                    SizedBox(height: 12),
                    _buildStat(
                      "Likes",
                      f.format(metrics.avgLikes),
                      header: true,
                      headerLabel: "Average",
                    ),
                    _buildStat(
                      "Comments",
                      f.format(metrics.avgComments),
                    ),
                    _buildStat(
                      "Saves",
                      f.format(metrics.avgSaves),
                    ),
                  ],
                ),
              ),
              Divider(indent: 72, height: 1),
              SizedBox(height: 12),
              _buildStat(
                "Posts",
                f.format(metrics.media.toInt() ?? 0),
                header: true,
                headerLabel: "Total",
              ),
              _buildStat(
                "Followers",
                f.format(metrics.followedBy.toInt() ?? 0),
              ),
              _buildStat(
                "Following",
                f.format(metrics.follows.toInt() ?? 0),
              ),
            ],
          ),
          widget.deal.request.status == DealRequestStatus.inProgress ||
                  widget.deal.request.status == DealRequestStatus.redeemed
              ? Padding(
                  padding:
                      EdgeInsets.only(left: 16.0, right: 16, bottom: 8, top: 4),
                  child: PrimaryButton(
                    context: context,
                    label: "Send Message",
                    onTap: _goToDialog,
                  ),
                )
              : Container(),
          Padding(
            padding: EdgeInsets.only(left: 16.0, right: 16, bottom: 20, top: 4),
            child: SecondaryButton(
              context: context,
              fullWidth: true,
              label: widget.deal.request.publisherAccount.isAccountBasic
                  ? "View Instagram"
                  : "View Profile",
              onTap: _goToProfile,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildRecentMedia() => StreamBuilder<PublisherMediasResponse>(
        stream: _bloc.mediaResponse,
        builder: (context, snapshot) {
          return snapshot.connectionState == ConnectionState.waiting
              ? Container(
                  height: 80,
                  margin: EdgeInsets.only(bottom: 16),
                  child: ListView(
                      scrollDirection: Axis.horizontal,
                      padding: EdgeInsets.only(left: 72),
                      children: List.generate(
                          5,
                          (_) => Shimmer.fromColors(
                                baseColor: _darkMode
                                    ? Color(0xFF121212)
                                    : AppColors.white100,
                                highlightColor: _darkMode
                                    ? Colors.black
                                    : AppColors.white50,
                                child: Container(
                                  width: 80,
                                  height: 80,
                                  margin: EdgeInsets.only(right: 8),
                                  decoration: BoxDecoration(
                                    color: _theme.appBarTheme.color,
                                    border: Border.all(
                                        color: _theme.dividerColor, width: 0.5),
                                    borderRadius: BorderRadius.circular(6),
                                  ),
                                ),
                              ))),
                )
              : snapshot.data.error != null
                  ? Container()
                  : Container(
                      height: 80,
                      margin: EdgeInsets.only(bottom: 16),
                      child: ListView(
                        scrollDirection: Axis.horizontal,
                        padding: EdgeInsets.only(left: 72),
                        children: snapshot.data.models
                            .asMap()
                            .map(
                              (int index, PublisherMedia m) => MapEntry(
                                index,
                                FadeInOpacityOnly(
                                  (1.5 * index).toDouble(),
                                  CachedNetworkImage(
                                    imageUrl: m.previewUrl,
                                    imageBuilder: (context, imageProvider) =>
                                        Container(
                                      width: 80,
                                      height: 80,
                                      margin: EdgeInsets.only(right: 8),
                                      decoration: BoxDecoration(
                                        color: _theme.appBarTheme.color,
                                        border: Border.all(
                                            color: _theme.dividerColor,
                                            width: 0.5),
                                        borderRadius: BorderRadius.circular(6),
                                        image: DecorationImage(
                                          alignment: Alignment.center,
                                          fit: BoxFit.cover,
                                          image: imageProvider,
                                        ),
                                      ),
                                    ),
                                    errorWidget: (context, url, error) =>
                                        ImageError(
                                      logUrl: url,
                                      logParentName:
                                          'deal/widgets/request/creator_details.dart > _buildRecentMedia',
                                    ),
                                  ),
                                ),
                              ),
                            )
                            .values
                            .toList(),
                      ),
                    );
        },
      );

  Widget _buildStat(
    String label,
    String quantity, {
    bool header = false,
    String headerLabel,
  }) {
    Widget content = Padding(
      padding: EdgeInsets.only(
          right: 16.0, bottom: 16.0, top: 4.0, left: header ? 0 : 72),
      child: Column(
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  label,
                  style: _theme.textTheme.bodyText2,
                ),
              ),
              Text(quantity, style: _theme.textTheme.bodyText2)
            ],
          ),
        ],
      ),
    );

    if (header) {
      return Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 72,
            padding: EdgeInsets.only(top: 6.0, right: 2, left: 16.0),
            child: Text(
              headerLabel,
              textAlign: TextAlign.left,
              overflow: TextOverflow.ellipsis,
              style: _theme.textTheme.caption.merge(
                TextStyle(
                  color: _theme.hintColor,
                ),
              ),
            ),
          ),
          Expanded(
            child: content,
          )
        ],
      );
    } else {
      return content;
    }
  }
}
