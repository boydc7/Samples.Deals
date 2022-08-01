import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydrworkspaces/app/theme.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';
import 'package:rydrworkspaces/models/publisher_media.dart';
import 'package:rydrworkspaces/models/publisher_metric.dart';
import 'package:rydrworkspaces/models/responses/publisher_media.dart';
import 'package:rydrworkspaces/ui/deals/blocs/profile_card.dart';

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
  final ProfileCardBloc _bloc = ProfileCardBloc();

  final NumberFormat f = NumberFormat.compact();
  final NumberFormat p = NumberFormat.decimalPercentPattern(decimalDigits: 1);
  final NumberFormat c = NumberFormat.compactSimpleCurrency();

  ThemeData _theme;

  @override
  void initState() {
    super.initState();

    _bloc.loadMedia(widget.deal.request.publisherAccount.id);
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _goToProfile() => null;
  void _goToDialog() => null;
/*
  void _goToProfile() => Navigator.of(context).pushNamed(
        AppRouting.getProfileRoute(widget.deal.request.publisherAccount.id),
        arguments: widget.deal,
      );

  void _goToDialog() =>
      Navigator.of(context).pushNamed(AppRouting.getRequestDialogRoute(
          widget.deal.id, widget.deal.request.publisherAccount.id));
          */

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);
    final PublisherAccount user = widget.deal.request.publisherAccount;
    final PublisherMetrics metrics = user.publisherMetrics;
    final double postCpm = metrics.postCPM(widget.deal.value) ?? 0;
    final double storyCpm = metrics.storyCPM(widget.deal.value) ?? 0;

    return GestureDetector(
      onTap: _goToProfile,
      child: Column(
        children: <Widget>[
          ListTile(
            leading: CircleAvatar(
              backgroundImage: NetworkImage(
                  widget.deal.request.publisherAccount.profilePicture),
            ),
            title: Text(
              user.userName,
              style: TextStyle(
                fontWeight: FontWeight.w600,
                color: _theme.textTheme.bodyText1.color,
              ),
            ),
            subtitle: Text(
              '${postCpm > 0 ? c.format(postCpm) + " post CPM" : ""} ${storyCpm > 0 ? " · " : ""} ${storyCpm > 0 ? c.format(storyCpm) + " story CPM" : ""}',
              overflow: TextOverflow.ellipsis,
              style: _theme.textTheme.bodyText1.merge(
                TextStyle(color: AppColors.grey300),
              ),
            ),
            trailing: Icon(Icons.arrow_right),
          ),
          _buildRecentMedia(),
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
          widget.deal.request.status == DealRequestStatus.inProgress ||
                  widget.deal.request.status == DealRequestStatus.redeemed
              ? Padding(
                  padding:
                      EdgeInsets.only(left: 16.0, right: 16, bottom: 8, top: 4),
                  child: MaterialButton(
                    child: Text("Send Message"),
                    color: _theme.primaryColor,
                    onPressed: _goToDialog,
                  ),
                )
              : Container(),
        ],
      ),
    );
  }

  Widget _buildRecentMedia() => StreamBuilder<PublisherMediaResponse>(
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
                          (_) => Container(
                                width: 80,
                                height: 80,
                                margin: EdgeInsets.only(right: 8),
                                decoration: BoxDecoration(
                                  color: _theme.appBarTheme.color,
                                  border: Border.all(
                                      color: _theme.dividerColor, width: 0.5),
                                  borderRadius: BorderRadius.circular(6),
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
                        children: snapshot.data.media
                            .asMap()
                            .map(
                              (int index, PublisherMedia m) => MapEntry(
                                index,
                                Container(
                                  width: 80,
                                  height: 80,
                                  margin: EdgeInsets.only(right: 8),
                                  decoration: BoxDecoration(
                                    color: _theme.appBarTheme.color,
                                    border: Border.all(
                                        color: _theme.dividerColor, width: 0.5),
                                    borderRadius: BorderRadius.circular(6),
                                    image: DecorationImage(
                                      alignment: Alignment.center,
                                      fit: BoxFit.cover,
                                      image: NetworkImage(m.previewUrl),
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
                  style: _theme.textTheme.bodyText1,
                ),
              ),
              Text(quantity, style: _theme.textTheme.bodyText1)
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
