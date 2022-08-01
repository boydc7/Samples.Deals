import 'dart:async';
import 'dart:ui' as ui;
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/ui/profile/blocs/insights_media_analysis_item.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:video_player/video_player.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/publisher_media_analysis.dart';
import 'package:rydr_app/models/publisher_media_stat.dart';
import 'package:rydr_app/models/responses/publisher_media_analysis.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class MediaViewer extends StatefulWidget {
  final List<PublisherMedia> media;
  final OverlayEntry overlayEntry;
  final PublisherMedia initialPost;
  final PublisherAccount profile;

  /// function to execute on the parent when user taps a image tag
  /// to then query for a new tag identified from the image
  final Function queryTag;

  MediaViewer(
    this.profile,
    this.media,
    this.initialPost,
    this.overlayEntry,
    this.queryTag,
  );

  @override
  _MediaViewerState createState() => _MediaViewerState();
}

class _MediaViewerState extends State<MediaViewer> {
  PageController _pageController;
  int _currentPage;

  @override
  void initState() {
    _currentPage = widget.initialPost == null
        ? 0
        : widget.media.indexWhere(
            (PublisherMedia m) => m.mediaId == widget.initialPost.mediaId);

    _pageController = PageController(
      initialPage: _currentPage,
      viewportFraction: 0.92,
    );

    super.initState();
  }

  @override
  void dispose() {
    _pageController.dispose();

    super.dispose();
  }

  void _onTagTap(String tag, List<String> tags) {
    widget.overlayEntry.remove();
    widget.queryTag(tag, tags);
  }

  @override
  Widget build(BuildContext context) => FadeInOpacityOnly(
        0,
        BackdropFilter(
          filter: ui.ImageFilter.blur(sigmaX: 8.0, sigmaY: 8.0),
          child: Material(
            color: Colors.transparent,
            child: SafeArea(
              bottom: false,
              top: true,
              child: Stack(
                alignment: Alignment.bottomCenter,
                children: <Widget>[
                  PageView.builder(
                    onPageChanged: (page) {
                      setState(() => _currentPage = page);
                    },
                    controller: _pageController,
                    itemCount: widget.media.length,
                    itemBuilder: (BuildContext context, int index) {
                      final PublisherMedia m = widget.media[index];

                      return FadeInBottomTop(
                          0,
                          GestureDetector(
                            onTap: () => widget.overlayEntry.remove(),
                            onLongPress: () {
                              HapticFeedback.lightImpact();
                              Utils.launchUrl(
                                context,
                                m.publisherUrl,
                                trackingName: 'media',
                              );
                            },
                            child: Container(
                              margin: EdgeInsets.only(
                                  left: 2.0, right: 2.0, top: 16.0),
                              child: MediaViewerItem(
                                widget.profile,
                                m,

                                /// only do this if we have a queryTag function passed to the viewer
                                /// when used from the completed request page then we'd not have one
                                widget.queryTag != null ? _onTagTap : null,
                                index,
                                _currentPage,
                              ),
                            ),
                          ),
                          150);
                    },
                  ),
                  Container(
                    margin: EdgeInsets.only(bottom: 16.0),
                    padding:
                        EdgeInsets.symmetric(horizontal: 12.0, vertical: 8.0),
                    decoration: BoxDecoration(
                      color: Theme.of(context).canvasColor,
                      borderRadius: BorderRadius.circular(20.0),
                    ),
                    child: Text(
                      "Long press on media to open Instagram",
                      style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
        duration: 150,
      );
}

class MediaViewerItem extends StatefulWidget {
  final PublisherAccount profile;
  final PublisherMedia media;
  final Function onTagTap;
  final int pageIndex;
  final int currentPage;

  MediaViewerItem(
    this.profile,
    this.media,
    this.onTagTap,
    this.pageIndex,
    this.currentPage,
  );

  @override
  _MediaViewerItemState createState() => _MediaViewerItemState();
}

class _MediaViewerItemState extends State<MediaViewerItem> {
  final numberFormatter = NumberFormat("#,###");

  final Map<String, IconData> _iconMap = {
    PublisherMediaStatValueName.Actions: AppIcons.solidHeart,
    PublisherMediaStatValueName.Reach: AppIcons.usersSolid,
    PublisherMediaStatValueName.Saved: AppIcons.solidBookmark,
    PublisherMediaStatValueName.Comments: AppIcons.solidComment,
    PublisherMediaStatValueName.Impressions: AppIcons.solidEye,
    PublisherMediaStatValueName.Replies: AppIcons.paperPlaneSolid,
    PublisherMediaStatValueName.VideoViews: AppIcons.video,
  };

  final Map<String, String> _statNameMap = {
    PublisherMediaStatValueName.Replies: 'Replies',
    PublisherMediaStatValueName.TapsBack: 'Taps Back',
    PublisherMediaStatValueName.TapsForward: 'Taps Forward',
    PublisherMediaStatValueName.Impressions: 'Impressions',
    PublisherMediaStatValueName.Reach: 'Reach',
    PublisherMediaStatValueName.Exits: 'Exits',
    PublisherMediaStatValueName.Engagement: 'Engagement',
    PublisherMediaStatValueName.Comments: 'Comments',
    PublisherMediaStatValueName.Saved: 'Saved',
    PublisherMediaStatValueName.VideoViews: 'Video Views',
    PublisherMediaStatValueName.Actions: 'Actions',
  };

  InsightsMediaAnalysisItemBloc _bloc;
  VideoPlayerController _videoController;
  Timer _timer;
  bool _videoPlayerIsInitialized = false;

  @override
  void initState() {
    super.initState();

    _bloc = InsightsMediaAnalysisItemBloc(widget.profile);

    if (_timer != null) {
      _timer.cancel();
    }

    /// only load media analyzation if it's available on the media
    /// and if ai opted in is true, otherwise don't kick off the timer...
    if (widget.media.isAnalyzed && _bloc.aiEnabled) {
      _timer = Timer(
          Duration(seconds: 2), () => _bloc.loadMediaAnalysis(widget.media.id));
    }

    /// only applicable if media is a video hosted by us
    /// otherwise, don't initialize the video player
    if (widget.media.isMediaPlayable) {
      try {
        _videoController = VideoPlayerController.network(widget.media.mediaUrl)
          ..initialize().then((_) {
            setState(() {
              _videoPlayerIsInitialized = true;
              _videoController.setLooping(true);
            });
          });
      } catch (ex) {}
    }
  }

  @override
  void dispose() {
    _videoController?.dispose();

    _bloc.dispose();
    _timer?.cancel();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (widget.currentPage == widget.pageIndex &&
        _videoController != null &&
        _videoPlayerIsInitialized &&
        !_videoController.value.isPlaying) {
      _videoController.play();
      _videoController.setLooping(true);
    } else {
      if (widget.currentPage != widget.pageIndex &&
          _videoController != null &&
          _videoPlayerIsInitialized &&
          _videoController.value.isPlaying) {
        _videoController.pause();
      }
    }

    return ListView(
      padding: EdgeInsets.only(top: 16),
      children: <Widget>[
        Container(
          margin: EdgeInsets.only(left: 4, right: 4, bottom: 56.0),
          child: Card(
            shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(16.0)),
            margin: EdgeInsets.all(0.0),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                _buildProfileHeader(),
                _buildMedia(),
                _buildAnalysisResults(),
                _buildLifetimeStats(),
                _buildCaptionSentiment(),
                _buildCaption(),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildProfileHeader() => Padding(
        padding: EdgeInsets.symmetric(vertical: 8.0, horizontal: 12.0),
        child: Row(
          children: <Widget>[
            UserAvatar(
              widget.profile,
              width: 32,
            ),
            SizedBox(width: 8.0),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    widget.profile.userName,
                    style: TextStyle(fontWeight: FontWeight.w600),
                  ),
                  Text(DateFormat('MMMM d, y').format(widget.media.createdAt),
                      style: Theme.of(context).textTheme.caption),
                ],
              ),
            ),
          ],
        ),
      );

  Widget _buildMedia() {
    /// actual image with overlay of analyzed image labels if available
    /// use a video player for videos hosted by us, otherwise preload the image to get dimensions
    /// and create proper aspect ratio for display of it
    if (widget.media.isMediaPlayable) {
      return AspectRatio(
        aspectRatio: _videoController.value.aspectRatio,
        child: Container(
          color: Theme.of(context).scaffoldBackgroundColor,
          child: Stack(
            children: <Widget>[
              VideoPlayer(_videoController),
              Positioned(
                top: 16.0,
                left: 16.0,
                child: AnimatedOpacity(
                    duration: Duration(milliseconds: 350),
                    opacity: _videoController.value.isPlaying ? 0.0 : 1.0,
                    child: _getMediaTypeIcon(widget.media)),
              ),
            ],
          ),
        ),
      );
    } else {
      final Image _image = Image.network(widget.media.previewUrl);
      final Completer<ui.Image> completer = Completer<ui.Image>();
      _image.image.resolve(ImageConfiguration()).addListener(
        ImageStreamListener(
          (ImageInfo image, bool _) {
            completer.complete(image.image);
          },
        ),
      );

      return FutureBuilder<ui.Image>(
        future: completer.future,
        builder: (BuildContext context, AsyncSnapshot<ui.Image> snapshot) {
          final double aspectRatio = snapshot.hasData
              ? snapshot.data.width / snapshot.data.height
              : 1.0;

          return AspectRatio(
            aspectRatio: aspectRatio,
            child: Stack(
              children: <Widget>[
                CachedNetworkImage(
                  imageUrl: widget.media.previewUrl,
                  imageBuilder: (context, imageProvider) => Container(
                    decoration: BoxDecoration(
                      color: Theme.of(context).scaffoldBackgroundColor,
                      image: DecorationImage(
                          image: imageProvider,
                          fit: BoxFit.cover,
                          alignment: Alignment.center),
                    ),
                  ),
                  errorWidget: (context, url, error) => ImageError(
                    logUrl: url,
                    logParentName:
                        'profile/widgets/insights_media_analysis_details_viewer.dart > _buildMedia',
                  ),
                ),
                Positioned(
                  top: 16.0,
                  left: 16.0,
                  child: _getMediaTypeIcon(widget.media),
                ),
              ],
            ),
          );
        },
      );
    }
  }

  /// hide if we don't have a function to tap
  Widget _buildTags() => widget.onTagTap == null
      ? Container()
      : StreamBuilder<bool>(
          stream: _bloc.mediaLoading,
          builder: (context, snapshot) => snapshot.data == null ||
                  snapshot.data == true
              ? Container(height: 0)
              : StreamBuilder<PublisherMediaAnalysisResponse>(
                  stream: _bloc.mediaAnalysisResponse,
                  builder: (context, snapshot) {
                    if (snapshot.data != null &&
                        snapshot.data.error == null &&
                        snapshot.data.model != null &&
                        snapshot.data.model.imageLabels != null &&
                        snapshot.data.model.imageLabels.isNotEmpty) {
                      final PublisherMediaAnalysis res = snapshot.data.model;

                      final List<String> tags = res.imageLabels
                          .map((tag) => tag.value)
                          .toSet()
                          .toList();

                      return Column(
                        mainAxisAlignment: MainAxisAlignment.end,
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Container(
                            height: 40,
                            alignment: Alignment.bottomLeft,
                            margin: EdgeInsets.only(bottom: 16),
                            child: ListView(
                              padding: EdgeInsets.symmetric(
                                  horizontal: 8, vertical: 0),
                              scrollDirection: Axis.horizontal,
                              children: tags
                                  .map(
                                    (label) => Padding(
                                      padding:
                                          EdgeInsets.symmetric(horizontal: 4),
                                      child: ActionChip(
                                        backgroundColor: Theme.of(context)
                                            .scaffoldBackgroundColor,
                                        shape: OutlineInputBorder(
                                          borderRadius:
                                              BorderRadius.circular(40),
                                          borderSide: BorderSide(
                                            color: Theme.of(context)
                                                .textTheme
                                                .bodyText1
                                                .color,
                                          ),
                                        ),
                                        onPressed: () =>
                                            widget.onTagTap(label, tags),
                                        label: Text(label),
                                        labelStyle: TextStyle(
                                            color: Theme.of(context)
                                                .textTheme
                                                .bodyText1
                                                .color),
                                      ),
                                    ),
                                  )
                                  .toList(),
                            ),
                          ),
                        ],
                      );
                    } else {
                      return Container(height: 0);
                    }
                  },
                ),
        );

  Widget _buildLifetimeStats() => widget.media.lifetimeStats != null
      ? Column(
          children: <Widget>[
            Padding(
              padding: EdgeInsets.symmetric(vertical: 12.0, horizontal: 4.0),
              child: Row(
                children: widget.media.lifetimeStats.stats
                    .map(
                      (PublisherStatValue stat) =>
                          _buildStat(widget.media, stat),
                    )
                    .toList(),
              ),
            ),
            widget.media.contentType == PublisherContentType.story
                ? Padding(
                    padding: EdgeInsets.only(
                        left: 16.0, right: 16.0, bottom: 16.0, top: 8.0),
                    child: Column(
                      children: widget.media.lifetimeStats.stats.map(
                        (PublisherStatValue stat) {
                          return Padding(
                            padding: EdgeInsets.symmetric(vertical: 8.0),
                            child: Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: <Widget>[
                                Text(_statNameMap[stat.name]),
                                Text(numberFormatter.format(stat.value)),
                              ],
                            ),
                          );
                        },
                      ).toList(),
                    ),
                  )
                : Container(height: 0),
          ],
        )
      : _buildInsightsUnavailable(widget.media);

  Widget _buildCaptionSentiment() {
    final NumberFormat f = NumberFormat.decimalPattern();

    return widget.media.contentType == PublisherContentType.post
        ? ListTile(
            dense: true,
            title: RichText(
              text: TextSpan(
                style: Theme.of(context).textTheme.bodyText2,
                children: <TextSpan>[
                  TextSpan(text: 'Liked by '),
                  TextSpan(
                      text: '${f.format(widget.media.actionCount)} users',
                      style: TextStyle(fontWeight: FontWeight.w600)),
                  TextSpan(
                      text:
                          ' · ${f.format(widget.media.commentCount)} comments'),
                ],
              ),
            ),
          )
        : Container(
            height: 0,
          );
  }

  Widget _buildCaption() => widget.media.caption != null &&
          widget.media.caption.length > 0
      ? Column(
          children: <Widget>[
            StreamBuilder<bool>(
              stream: _bloc.mediaCaptionExpanded,
              builder: (context, snapshot) {
                final bool expanded =
                    snapshot.data == true || widget.media.caption.length < 100;

                if (widget.media.contentType == PublisherContentType.story) {
                  final String foundWordInStory = widget.media.caption ?? "";
                  final List<String> wordInStory = foundWordInStory
                      .split(" ")
                      .map((String word) => word.trim())
                      .where((String word) => word.length > 2)
                      .toList();

                  return widget.onTagTap != null
                      ? Column(
                          children: <Widget>[
                            Padding(
                              padding:
                                  EdgeInsets.only(left: 16, right: 8, top: 8),
                              child: Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceBetween,
                                children: <Widget>[
                                  Text(
                                    "Found Text",
                                    style:
                                        Theme.of(context).textTheme.bodyText1,
                                  ),
                                  Text(
                                    "Tap to search",
                                    style: Theme.of(context)
                                        .textTheme
                                        .caption
                                        .merge(
                                          TextStyle(
                                            color: Theme.of(context).hintColor,
                                          ),
                                        ),
                                  ),
                                ],
                              ),
                            ),
                            Container(
                              height: 40,
                              alignment: Alignment.bottomLeft,
                              margin: EdgeInsets.only(bottom: 16, top: 12),
                              child: ListView(
                                padding: EdgeInsets.symmetric(
                                    horizontal: 8, vertical: 0),
                                scrollDirection: Axis.horizontal,
                                children: wordInStory.map((w) {
                                  return Padding(
                                    padding:
                                        EdgeInsets.symmetric(horizontal: 4),
                                    child: ActionChip(
                                      backgroundColor: Theme.of(context)
                                          .scaffoldBackgroundColor,
                                      shape: OutlineInputBorder(
                                        borderRadius: BorderRadius.circular(40),
                                        borderSide: BorderSide(
                                          color: Theme.of(context)
                                              .textTheme
                                              .bodyText1
                                              .color,
                                        ),
                                      ),
                                      onPressed: () => widget.onTagTap(
                                          w.trim(), wordInStory),
                                      label: Text(w),
                                    ),
                                  );
                                }).toList(),
                              ),
                            ),
                          ],
                        )
                      : Container();
                } else {
                  return Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Expanded(
                        child: Container(
                          padding: EdgeInsets.only(
                            left: 16,
                            right: 16,
                            bottom: 20,
                          ),
                          width: double.infinity,
                          child: Text(
                            widget.media.caption,
                            maxLines: expanded ? 200 : 2,
                            overflow: TextOverflow.ellipsis,
                            textAlign: TextAlign.left,
                          ),
                        ),
                      ),
                      Visibility(
                        visible: !expanded,
                        child: Padding(
                          padding: EdgeInsets.only(right: 8.0),
                          child: IconButton(
                            icon: Icon(AppIcons.chevronDown,
                                color: Theme.of(context).hintColor),
                            onPressed: () =>
                                _bloc.setMediaCaptionExpanded(true),
                          ),
                        ),
                      )
                    ],
                  );
                }
              },
            ),
          ],
        )
      : Container(height: 0);

  Widget _buildAnalysisResults() => StreamBuilder<bool>(
        stream: _bloc.mediaLoading,
        builder: (context, snapshot) {
          return snapshot.data == null
              ? Container()
              : snapshot.data == true
                  ? Column(
                      children: <Widget>[
                        Container(
                          padding: EdgeInsets.all(16),
                          child: Text(
                            "Loading Selfie Vision™...",
                            style: TextStyle(
                              color: Theme.of(context).hintColor,
                            ),
                          ),
                        ),
                        Divider(height: 1),
                      ],
                    )
                  : Column(
                      children: <Widget>[
                        Center(
                          child: Padding(
                            padding: EdgeInsets.only(
                                left: 16, right: 16, top: 16, bottom: 16),
                            child: Text(
                              "Selfie Vision™ Results",
                              style: Theme.of(context).textTheme.bodyText1,
                            ),
                          ),
                        ),
                        _buildTags(),
                        StreamBuilder<PublisherMediaAnalysisResponse>(
                          stream: _bloc.mediaAnalysisResponse,
                          builder: (context, snapshot) {
                            if (snapshot.data != null &&
                                snapshot.data.error == null) {
                              final PublisherMediaAnalysis res =
                                  snapshot.data.model;

                              List<Widget> imageStats = [];

                              Widget tile(String label, double count,
                                  {String value = ""}) {
                                NumberFormat f = NumberFormat.decimalPattern();
                                return Padding(
                                  padding: EdgeInsets.only(bottom: 16),
                                  child: Row(
                                    children: <Widget>[
                                      Expanded(child: Text(label)),
                                      Text(count == null
                                          ? value
                                          : f.format(count)),
                                    ],
                                  ),
                                );
                              }

                              imageStats.add(res.imageFacesCount > 0
                                  ? tile(
                                      res.imageFacesSmiles ==
                                              res.imageFacesCount
                                          ? "Total Smiling Faces"
                                          : "Total Faces",
                                      res.imageFacesCount.toDouble())
                                  : null);

                              imageStats.add(res.imageFacesSmiles > 0 &&
                                      res.imageFacesSmiles !=
                                          res.imageFacesCount
                                  ? tile("Smiling Faces",
                                      res.imageFacesSmiles.toDouble())
                                  : null);

                              imageStats.add(res.imageFacesFemales > 0
                                  ? tile("Female",
                                      res.imageFacesFemales.toDouble())
                                  : null);

                              imageStats.add(res.imageFacesMales > 0
                                  ? tile("Male", res.imageFacesMales.toDouble())
                                  : null);

                              imageStats.add(res.imageFacesEmotions.length > 0
                                  ? Column(
                                      children: res.imageFacesEmotions.keys
                                          .map((k) => tile(
                                              "Emotion: " +
                                                  k[0].toUpperCase() +
                                                  k.substring(1).toLowerCase(),
                                              res.imageFacesEmotions[k]
                                                  .toDouble()))
                                          .toList())
                                  : null);

                              imageStats.add(res.imageFacesAvgAge > 0
                                  ? tile("Average Age", res.imageFacesAvgAge)
                                  : null);

                              imageStats.add(res.imageFacesBeards > 0
                                  ? tile(
                                      "Beards", res.imageFacesBeards.toDouble())
                                  : null);

                              imageStats.add(res.imageFacesEyeglasses > 0 ||
                                      res.imageFacesSunglasses > 0
                                  ? tile(
                                      "Glasses",
                                      (res.imageFacesEyeglasses +
                                              res.imageFacesSunglasses)
                                          .toDouble())
                                  : null);

                              imageStats.add(res.imageFacesMustaches > 0
                                  ? tile("Mustaches",
                                      res.imageFacesMustaches.toDouble())
                                  : null);

                              return Container(
                                  padding: EdgeInsets.symmetric(horizontal: 16),
                                  width: double.infinity,
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: <Widget>[
                                      Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: imageStats
                                                    .where(
                                                        (stat) => stat != null)
                                                    .length >
                                                0
                                            ? imageStats
                                                .map((label) => label)
                                                .where((stat) => stat != null)
                                                .toList()
                                            : [Container(height: 0)],
                                      ),
                                      StreamBuilder<
                                          PublisherMediaAnalysisResponse>(
                                        stream: _bloc.mediaAnalysisResponse,
                                        builder: (context, snapshot) {
                                          if (snapshot.data != null &&
                                              snapshot.data.error == null) {
                                            final PublisherMediaAnalysis res =
                                                snapshot.data.model;
                                            final String sentiment =
                                                res.isPositiveSentiment
                                                    ? "Positive"
                                                    : res.isNegativeSentiment
                                                        ? "Negative"
                                                        : "Mixed/Neutral";

                                            return tile(
                                                'Caption Sentiment', null,
                                                value: sentiment);
                                          } else {
                                            return tile(
                                                'Caption Sentiment', null,
                                                value: "Analyzing...");
                                          }
                                        },
                                      ),
                                    ],
                                  ));
                            } else {
                              return Container(height: 0);
                            }
                          },
                        ),
                        Divider(height: 1),
                      ],
                    );
        },
      );

  Widget _buildInsightsUnavailable(PublisherMedia m) => Container();

  Widget _getMediaTypeIcon(PublisherMedia m) => m.type == MediaType.video
      ? Icon(AppIcons.video, color: Colors.white, size: 24.0)
      : m.type == MediaType.carouselAlbum
          ? Icon(AppIcons.clone, color: Colors.white, size: 24.0)
          : Icon(AppIcons.camera, color: Colors.white, size: 24.0);

  Widget _getStatsIcon(PublisherStatValue stat) =>
      Icon(_iconMap[stat.name] ?? AppIcons.question,
          size: 16.0, color: Theme.of(context).iconTheme.color);

  Widget _buildStat(PublisherMedia m, PublisherStatValue stat) {
    final numberFormatter = NumberFormat("#,###");

    if (m.contentType == PublisherContentType.story) {
      if (stat.name == PublisherMediaStatValueName.Reach ||
          stat.name == PublisherMediaStatValueName.Replies ||
          stat.name == PublisherMediaStatValueName.Impressions &&
              m.contentType == PublisherContentType.story) {
        return Expanded(
          child: Padding(
            padding: EdgeInsets.symmetric(vertical: 12.0, horizontal: 4.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.center,
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Tooltip(
                  message: stat.name.toUpperCase(),
                  child: Container(
                    width: 32,
                    child: _getStatsIcon(stat),
                    margin: EdgeInsets.only(bottom: 8.0),
                  ),
                ),
                Text(
                  numberFormatter.format(stat.value),
                ),
              ],
            ),
          ),
        );
      } else {
        return Container();
      }
    } else {
      if (stat.name == PublisherMediaStatValueName.Reach ||
          stat.name == PublisherMediaStatValueName.Comments ||
          stat.name == PublisherMediaStatValueName.Impressions ||
          stat.name == PublisherMediaStatValueName.Actions ||
          stat.name == PublisherMediaStatValueName.Saved &&
              m.type != MediaType.video) {
        return Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Tooltip(
                preferBelow: false,
                message: stat.name.toUpperCase(),
                child: Container(
                  width: 32,
                  child: _getStatsIcon(stat),
                  margin: EdgeInsets.only(bottom: 8.0),
                ),
              ),
              Text(
                numberFormatter.format(stat.value),
              ),
            ],
          ),
        );
      }
      if (stat.name == PublisherMediaStatValueName.Reach ||
          stat.name == PublisherMediaStatValueName.Comments ||
          stat.name == PublisherMediaStatValueName.VideoViews ||
          stat.name == PublisherMediaStatValueName.Impressions ||
          stat.name == PublisherMediaStatValueName.Actions &&
              m.type == MediaType.video) {
        return Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Tooltip(
                message: stat.name.toUpperCase(),
                child: Container(
                  width: 32,
                  child: _getStatsIcon(stat),
                  margin: EdgeInsets.only(bottom: 8.0),
                ),
              ),
              Text(
                numberFormatter.format(stat.value),
              ),
            ],
          ),
        );
      } else {
        return Container();
      }
    }
  }
}
