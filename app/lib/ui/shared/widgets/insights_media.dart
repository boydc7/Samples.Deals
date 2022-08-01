import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/deal_metric.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class InsightsMedia extends StatefulWidget {
  final bool isLoading;
  final Widget overlay;
  final Deal deal;
  final DealCompletionMediaMetrics metrics;

  InsightsMedia({
    @required this.metrics,
    this.isLoading = false,
    this.overlay,
    this.deal,
  });

  @override
  _InsightsMediaState createState() => _InsightsMediaState();
}

class _InsightsMediaState extends State<InsightsMedia> {
  final GlobalKey key = GlobalKey();

  final Map<String, String> _pageContent = {
    "title": "Uploaded Posts",
    "subtitle": "Types of posts by Creators",
    "bottom_sheet_title": "Lifetime Post Insights",
    "story": "Story",
    "story_description":
        "An Instagram post of a photo or video that vanishes after 24 hours.",
    "image_or_video": "Image or Video",
    "image_or_video_description":
        "A single image or video Instagram post, posted to the Creator\'s feed.",
    "carousel": "Carousel",
    "carousel_description":
        "A swipeable Instagram post consisting of up to 10 photos and/or videos, posted to the Creator\'s feed.",
    "avg_posts": "Average Posts per RYDR",
    "avg_posts_description":
        "The total number of Stories and Posts by Creators, divided by your total number of RYDR requests.",
    "combined_posts": "Posts from RYDR",
    "stories": "Story",
    "images_videos_carousels": "Images, Videos, and Carousels",
    "images": "Single Image",
    "carousels": "Carousel",
    "videos": "Video",
  };

  @override
  void initState() {
    super.initState();

    _pageContent['bottom_sheet_subtitle'] = widget.deal != null
        ? 'This set of insights measures the types of posts that have been uploaded for this RYDR since its been active in the marketplace.'
        : 'This set of insights measures the types of posts that have been uploaded since your RYDR account was created.';
  }

  @override
  Widget build(BuildContext context) {
    return widget.isLoading ? _buildLoadingBody() : _buildResultsBody();
  }

  Widget _buildHeader() {
    return insightsSectionHeader(
      context: context,
      title: _pageContent['title'],
      subtitle: _pageContent['subtitle'],
      bottomSheetTitle: _pageContent['bottom_sheet_title'],
      bottomSheetSubtitle: _pageContent['bottom_sheet_subtitle'],
      bottomSheetWidget: insightsBottomSheet(
        context,
        [
          InsightsBottomSheetTile(
            _pageContent['story'],
            _pageContent['story_description'],
          ),
          InsightsBottomSheetTile(
            _pageContent['image_or_video'],
            _pageContent['image_or_video_description'],
          ),
          InsightsBottomSheetTile(
            _pageContent['carousel'],
            _pageContent['carousel_description'],
          ),
          InsightsBottomSheetTile(
            _pageContent['avg_posts'],
            _pageContent['avg_posts_description'],
          ),
        ],
      ),
    );
  }

  Widget _buildLoadingBody() {
    return Container(
      child: Column(
        key: key,
        children: <Widget>[
          _buildHeader(),
          LoadingStatsShimmer(),
        ],
      ),
    );
  }

  Widget _buildContent(bool noCompleted, int totalImpressions, int totalReach,
      double aspectRatio) {
    final NumberFormat f = NumberFormat.decimalPattern();
    final int totalMediaPosts = (widget.metrics.completedPostMedias ?? 0) +
        (widget.metrics.completedStoryMedias ?? 0);

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Padding(
          padding: EdgeInsets.only(top: 40, bottom: 32, left: 16, right: 16),
          child: insightsBigStat(
            context: context,
            value: totalMediaPosts.toDouble(),
            formatAsInt: true,
            label: _pageContent['combined_posts'],
          ),
        ),
        _buildListTile(_pageContent['stories'], Theme.of(context).primaryColor,
            f.format(widget.metrics?.stories), widget.metrics?.stories),
        SizedBox(height: 4),
        _buildListTile(_pageContent['images'], Colors.deepOrange,
            f.format(widget.metrics?.images), widget.metrics?.images),
        SizedBox(height: 4),
        _buildListTile(_pageContent['carousels'], AppColors.teal,
            f.format(widget.metrics?.carousels), widget.metrics?.carousels),
        SizedBox(height: 4),
        _buildListTile(_pageContent['videos'], AppColors.blue100,
            f.format(widget.metrics?.videos), widget.metrics?.videos),
        SizedBox(height: 16),
      ],
    );
  }

  Widget _buildListTile(String title, Color color, String value, int val) {
    final List<int> postQuantities = [
      widget.metrics?.stories,
      widget.metrics?.images,
      widget.metrics?.carousels,
      widget.metrics?.videos
    ];
    final int mostType =
        postQuantities.reduce((curr, next) => curr > next ? curr : next);
    final double percentage = mostType == 0 ? 0 : val.toDouble() / mostType;

    return Stack(
      children: <Widget>[
        Container(
          margin: EdgeInsets.only(right: 10),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.only(
              topRight: Radius.circular(16.0),
              bottomRight: Radius.circular(16.0),
            ),
            color: color.withOpacity(0.15),
          ),
          height: 32,
          width: MediaQuery.of(context).size.width * percentage,
        ),
        Container(
          padding: EdgeInsets.symmetric(horizontal: 16, vertical: 0),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              Container(
                height: 8.0,
                width: 8.0,
                margin: EdgeInsets.only(right: 16, top: 4),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(16.0),
                  color: color,
                ),
              ),
              Expanded(
                child: Padding(
                  padding: EdgeInsets.only(top: 4),
                  child: Text(title),
                ),
              ),
              Container(
                height: 24,
                margin: EdgeInsets.only(top: 4),
                padding: EdgeInsets.symmetric(horizontal: 8),
                decoration: BoxDecoration(
                    color: Theme.of(context).scaffoldBackgroundColor,
                    borderRadius: BorderRadius.circular(30)),
                child: Center(child: Text(value == "NaN%" ? "0%" : value)),
              )
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildResultsBody() {
    final double aspectRatio = 1.75;
    final int totalImpressions = (widget.metrics.postImpressions ?? 0) +
        (widget.metrics.storyImpressions ?? 0);
    final int totalReach =
        (widget.metrics.postReach ?? 0) + (widget.metrics.storyReach ?? 0);

    /// flag that'll indicate if we have any completed deals yet for this business
    /// if there aren't any yet we will fade out the insights sections and overlay info
    final bool noCompleted = (widget.metrics?.completedRequestDeals ?? 0) <= 0;

    return Column(
      mainAxisSize: MainAxisSize.min,
      key: key,
      children: <Widget>[
        _buildHeader(),
        _buildContent(noCompleted, totalImpressions, totalReach, aspectRatio),
        sectionDivider(context)
      ],
    );
  }
}
