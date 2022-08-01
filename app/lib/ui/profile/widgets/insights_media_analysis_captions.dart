import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media_vision.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_detail.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_helpers.dart';

class ProfileInsightsMediaAnalysisCaptions extends StatefulWidget {
  final PublisherAccount profile;
  final PublisherAccountMediaVisionSection section;

  ProfileInsightsMediaAnalysisCaptions(this.profile, this.section);

  @override
  _ProfileInsightsMediaAnalysisCaptionsState createState() =>
      _ProfileInsightsMediaAnalysisCaptionsState();
}

class _ProfileInsightsMediaAnalysisCaptionsState
    extends State<ProfileInsightsMediaAnalysisCaptions>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final bool darkMode = Theme.of(context).brightness == Brightness.dark;

    return widget.section == null
        ? Container(
            height: 0,
          )
        : Column(
            children: [
              Padding(
                padding: EdgeInsets.only(left: 16.0, top: 16.0, right: 16.0),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: <Widget>[
                    Text(
                      "Post Captions",
                      style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                    ),
                    Text(
                      "${widget.section.totalCount}",
                      style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                    ),
                  ],
                ),
              ),
              Padding(
                padding: EdgeInsets.symmetric(horizontal: 8.0),
                child: Column(
                  children: <Widget>[
                    Row(
                      children: <Widget>[
                        _buildCaption(context, darkMode, AppColors.successGreen,
                            widget.section.items[0]),
                        _buildCaption(context, darkMode, AppColors.errorRed,
                            widget.section.items[1]),
                      ],
                    ),
                    SizedBox(height: 8.0),
                    Row(
                      children: <Widget>[
                        _buildCaption(
                            context,
                            darkMode,
                            Theme.of(context).textTheme.bodyText1.color,
                            widget.section.items[2]),
                        _buildCaption(
                            context,
                            darkMode,
                            Theme.of(context).primaryColor,
                            widget.section.items[3]),
                      ],
                    ),
                    SizedBox(height: 16)
                  ],
                ),
              ),
              SizedBox(height: 16),
            ],
          );
  }

  Widget _buildCaption(
    BuildContext context,
    bool darkMode,
    Color color,
    PublisherAccountMediaVisionSectionItem item,
  ) {
    Widget _image(int index) {
      return Expanded(
          child: item.medias.length > index
              ? MediaCachedImage(
                  imageUrl: item.medias[index].previewUrl,
                  height: 80,
                )
              : Container(
                  color: AppColors.grey300.withOpacity(0.1),
                ));
    }

    return Expanded(
      child: GestureDetector(
        onTap: () => Navigator.push(
          context,
          MaterialPageRoute(
              builder: (context) => ProfileInsightsMediaAnalysisDetail(
                    widget.profile,
                    item.title,
                    sectionItem: item,
                  ),
              settings: AppAnalytics.instance
                  .getRouteSettings('profile/insights/ai/details')),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Card(
              margin: EdgeInsets.symmetric(horizontal: 4.0, vertical: 8.0),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(4.0),
                child: Container(
                  height: 80.0,
                  foregroundDecoration: BoxDecoration(
                    backgroundBlendMode: BlendMode.color,
                    color: color,
                  ),
                  child: Opacity(
                    opacity: 0.85,
                    child: Row(
                      children: <Widget>[
                        _image(0),
                        SizedBox(width: 1),
                        _image(1),
                        SizedBox(width: 1),
                        _image(2),
                      ],
                    ),
                  ),
                ),
              ),
            ),
            Padding(
              padding: EdgeInsets.only(top: 4.0, left: 8.0),
              child: Text(item.title),
            )
          ],
        ),
      ),
    );
  }
}
