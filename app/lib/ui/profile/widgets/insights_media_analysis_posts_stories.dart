import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media_vision.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_detail.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_helpers.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

class ProfileInsightsMediaAnalysisPostsStories extends StatefulWidget {
  final PublisherAccount profile;
  final PublisherAccountMediaVisionSection stories;
  final PublisherAccountMediaVisionSection posts;

  ProfileInsightsMediaAnalysisPostsStories(
      this.profile, this.stories, this.posts);

  @override
  _ProfileInsightsMediaAnalysisPostsStoriesState createState() =>
      _ProfileInsightsMediaAnalysisPostsStoriesState();
}

class _ProfileInsightsMediaAnalysisPostsStoriesState
    extends State<ProfileInsightsMediaAnalysisPostsStories>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);

    final bool darkMode = Theme.of(context).brightness == Brightness.dark;
    final double albumWidth = (MediaQuery.of(context).size.width / 2.55) - 32;

    /// if we don't have story and post sections then return nothing
    /// then within each album we'll also guard against the album not having any media
    return (widget.stories == null || widget.stories?.items?.length == 0) &&
            (widget.posts == null || widget.posts?.items?.length == 0)
        ? Container(height: 0)
        : Column(
            children: [
              _buildAlbumGroup(
                context,
                darkMode,
                "Instagram Stories",
                true,
                widget.stories,
                albumWidth,
              ),
              widget.stories.items.length > 0
                  ? sectionDivider(context)
                  : Container(height: 0),
              _buildAlbumGroup(
                context,
                darkMode,
                "Instagram Posts",
                false,
                widget.posts,
                albumWidth,
              ),
            ],
          );
  }

  Widget _buildAlbumGroup(
      BuildContext context,
      bool darkMode,
      String sectionTitle,
      bool isStory,
      PublisherAccountMediaVisionSection section,
      double albumWidth) {
    final double aspectRatio = isStory ? 0.5625 : 1.0;
    final double captionHeight = 28.0;
    final double albumHeight = (albumWidth / aspectRatio) + captionHeight + 24;

    return section == null || section.items.isEmpty
        ? Container(height: 0)
        : Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Padding(
                padding: EdgeInsets.only(left: 16.0, top: 16.0, right: 16.0),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: <Widget>[
                    Text(
                      sectionTitle,
                      style: Theme.of(context).textTheme.bodyText2.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                    ),
                    Text(
                      section.totalCount.toString(),
                      style: Theme.of(context).textTheme.bodyText2.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                    ),
                  ],
                ),
              ),
              Container(
                height: albumHeight,
                padding: EdgeInsets.only(top: 8),
                width: MediaQuery.of(context).size.width,
                child: ListView(
                    padding: EdgeInsets.symmetric(horizontal: 8),
                    scrollDirection: Axis.horizontal,
                    children: section.items
                        .map(
                          (PublisherAccountMediaVisionSectionItem item) =>
                              _buildAlbum(context, darkMode, isStory, item,
                                  albumWidth, albumHeight, captionHeight),
                        )
                        .toList()),
              ),
              SizedBox(height: 12)
            ],
          );
  }

  Widget _buildAlbum(
      BuildContext context,
      bool darkMode,
      bool isStory,
      PublisherAccountMediaVisionSectionItem item,
      double albumWidth,
      double albumHeight,
      double captionHeight) {
    Widget getImage(int index) {
      return Expanded(
        child: item.medias.length > index
            ? MediaCachedImage(
                imageUrl: item.medias[index].previewUrl,
                width: albumWidth / 2,
                height: albumHeight / 2,
              )
            : Container(
                color: AppColors.grey300.withOpacity(0.1),
              ),
      );
    }

    return item.medias.isEmpty
        ? Container(
            height: 0,
          )
        : Container(
            padding: EdgeInsets.all(8.0),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.start,
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Expanded(
                  child: Card(
                    margin: EdgeInsets.all(0),
                    child: InkWell(
                      onTap: () => Navigator.push(
                        context,
                        MaterialPageRoute(
                            builder: (context) =>
                                ProfileInsightsMediaAnalysisDetail(
                                    widget.profile, item.title,
                                    subTitle: isStory
                                        ? "Instagram stories only"
                                        : "Instagram posts only",
                                    sectionItem: item,
                                    contentType: isStory
                                        ? PublisherContentType.story
                                        : PublisherContentType.post),
                            settings: AppAnalytics.instance.getRouteSettings(
                                'profile/insights/ai/details')),
                      ),
                      child: Container(
                        width: albumWidth,
                        height: albumHeight - 16,
                        child: ClipRRect(
                          borderRadius: BorderRadius.circular(4),
                          child: Column(
                            children: <Widget>[
                              Expanded(
                                child: Row(
                                  children: <Widget>[
                                    getImage(0),
                                    SizedBox(width: 2),
                                    getImage(1),
                                  ],
                                ),
                              ),
                              SizedBox(height: 2),
                              Expanded(
                                child: Row(
                                  children: <Widget>[
                                    getImage(2),
                                    SizedBox(width: 2),
                                    getImage(3),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
                Container(
                  height: captionHeight,
                  alignment: Alignment.bottomLeft,
                  child: Text(
                    item.title,
                    textAlign: TextAlign.left,
                  ),
                )
              ],
            ),
          );
  }
}
