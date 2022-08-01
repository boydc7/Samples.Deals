import 'dart:ui';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/publisher_media_vision.dart';
import 'package:rydr_app/models/responses/publisher_media_analysis.dart';
import 'package:rydr_app/ui/profile/blocs/insights_media_analysis.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_detail_viewer.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_helpers.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

class ProfileInsightsMediaAnalysisDetail extends StatefulWidget {
  final PublisherAccount profile;
  final PublisherAccountMediaVisionSectionItem sectionItem;
  final List<PublisherMedia> media;
  final PublisherContentType contentType;
  final String title;
  final String subTitle;

  ProfileInsightsMediaAnalysisDetail(
    this.profile,
    this.title, {
    this.sectionItem,
    this.media,
    this.contentType,
    this.subTitle,
  });

  @override
  _ProfileInsightsMediaAnalysisDetailState createState() =>
      _ProfileInsightsMediaAnalysisDetailState();
}

class _ProfileInsightsMediaAnalysisDetailState
    extends State<ProfileInsightsMediaAnalysisDetail> {
  final DateFormat formatter = DateFormat('MMMd');

  OverlayEntry _overlayEntry;
  InsightsMediaAnalysisBloc _bloc;
  List<PublisherMedia> _existingMedia;
  String _title;
  String _subTitle;
  List<String> _tags;
  PublisherContentType _contentType;

  @override
  void initState() {
    super.initState();

    _bloc = InsightsMediaAnalysisBloc(widget.profile);
    _existingMedia = widget.media;
    _title = widget.title;
    _subTitle = widget.subTitle;
    _tags = widget.sectionItem?.searchTags;
    _contentType = widget.contentType;

    /// if we have a sectionItem then run the query on it
    if (widget.sectionItem != null) {
      _querySection();
    }
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _querySection() {
    _title = widget.title;
    _subTitle = widget.subTitle;
    _tags = widget.sectionItem?.searchTags;
    _contentType = widget.contentType;

    _bloc.querySection(widget.sectionItem.searchDescriptor, _contentType);
  }

  void _queryTag(String tag) {
    _contentType = null;
    _title = tag;
    _subTitle = widget.title;
    _bloc.queryTag(tag, _contentType);
  }

  void _queryFromViewer(String tag, List<String> tags) {
    _existingMedia = null;
    _contentType = null;
    _title = tag;
    _subTitle = "Instagram Stories & Posts";
    _tags = tags;

    /// Dummy to trigger rebuild
    _bloc.setMediaLoading(false);

    _bloc.queryTag(tag);
  }

  void _showDetails(PublisherMedia post) {
    _overlayEntry = OverlayEntry(
      builder: (BuildContext context) {
        return Dismissible(
            key: UniqueKey(),
            direction: DismissDirection.vertical,
            onDismissed: (DismissDirection direction) => _overlayEntry.remove(),
            child: MediaViewer(
              widget.profile,
              _existingMedia != null ? _existingMedia : _bloc.mediaLoaded,
              post,
              _overlayEntry,
              _queryFromViewer,
            ));
      },
      opaque: false,
      maintainState: true,
    );
    Navigator.of(context).overlay.insert(_overlayEntry);
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        body: StreamBuilder<bool>(
          stream: _bloc.mediaLoading,
          builder: (context, snapshot) {
            return _existingMedia != null
                ? _buildResults(_existingMedia)
                : StreamBuilder<PublisherMediaAnalysisQueryResponse>(
                    stream: _bloc.mediaQueryResponse,
                    builder: (context, snapshot) {
                      final List<PublisherMedia> items =
                          snapshot.data != null ? snapshot.data.models : [];

                      return snapshot.connectionState ==
                                  ConnectionState.waiting ||
                              snapshot.data == null
                          ? _buildLoading()
                          : snapshot.data.error != null
                              ? _buildError(snapshot.data)
                              : items.isEmpty
                                  ? _buildNoResults()
                                  : _buildResults(items);
                    },
                  );
          },
        ),
        bottomNavigationBar: Container(
          height: kToolbarHeight + MediaQuery.of(context).padding.bottom,
          padding:
              EdgeInsets.only(bottom: MediaQuery.of(context).padding.bottom),
          color: Theme.of(context).primaryColor,
          child: Center(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: <Widget>[
                Padding(
                  padding: EdgeInsets.only(left: 16, right: 16, top: 2),
                  child: Icon(
                    AppIcons.flask,
                    color: Colors.white,
                    size: 22,
                  ),
                ),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      Text("Experimental Results",
                          style: Theme.of(context)
                              .textTheme
                              .bodyText1
                              .merge(TextStyle(color: Colors.white))),
                      Text(
                          "This feature is in beta and results may be inconsistent.",
                          style: Theme.of(context).textTheme.caption.merge(
                              TextStyle(fontSize: 10, color: Colors.white)))
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      );

  Widget _buildAppBar() => SliverAppBar(
        forceElevated: true,
        floating: true,
        elevation: 1.0,
        pinned: true,
        snap: true,
        leading: AppBarBackButton(context),
        centerTitle: true,
        actions: <Widget>[Container(width: kMinInteractiveDimension)],
        title: Column(
          children: <Widget>[
            Text(_title),
            _subTitle != null
                ? Text(_subTitle,
                    style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(color: Theme.of(context).hintColor),
                        ))
                : Container(),
          ],
        ),
        expandedHeight: widget.title == "Recently Analyzed" || _tags.length == 0
            ? 0.0
            : 120,
        flexibleSpace: FlexibleSpaceBar(
          collapseMode: CollapseMode.parallax,
          background: SafeArea(
            top: true,
            child: Padding(
              padding: EdgeInsets.only(top: kToolbarHeight),
              child: Center(
                child: _buildFilters(),
              ),
            ),
          ),
        ),
      );

  Widget _buildLoading() => CustomScrollView(
        slivers: <Widget>[
          _buildAppBar(),
          LoadingSliverGridShimmer(),
        ],
      );

  Widget _buildError(PublisherMediaAnalysisQueryResponse res) =>
      CustomScrollView(
        slivers: <Widget>[
          _buildAppBar(),
          SliverFillRemaining(
            child: Center(
              child: RetryError(
                error: res.error,
                onRetry: _querySection,
              ),
            ),
          ),
        ],
      );

  Widget _buildNoResults() => CustomScrollView(
        slivers: <Widget>[
          _buildAppBar(),
          SliverFillRemaining(child: Center(child: Text("No Results"))),
        ],
      );

  Widget _buildHeader(String title, int length) {
    if (length > 0) {
      return SliverToBoxAdapter(
        child: Container(
          height: 40.0,
          color: Theme.of(context).scaffoldBackgroundColor,
          alignment: Alignment.centerLeft,
          child: Padding(
            padding: EdgeInsets.symmetric(horizontal: 16.0),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: <Widget>[Text(title), Text("$length")],
            ),
          ),
        ),
      );
    } else {
      return SliverToBoxAdapter(
        child: Container(height: 1),
      );
    }
  }

  Widget _buildResults(List<PublisherMedia> items) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    /// divide items into stories and posts if we don't have a contenttype specific
    /// and render a sliver layout that includes a list of stories at the top and grid of posts below
    if (_contentType == null) {
      final List<PublisherMedia> stories = items
          .where(
              (PublisherMedia m) => m.contentType == PublisherContentType.story)
          .toList()
            ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
      final List<PublisherMedia> posts = items
          .where(
              (PublisherMedia m) => m.contentType == PublisherContentType.post)
          .toList()
            ..sort((a, b) => b.createdAt.compareTo(a.createdAt));

      return CustomScrollView(
        controller: PrimaryScrollController.of(context),
        slivers: <Widget>[
          _buildAppBar(),
          _buildHeader("Stories", stories.length),
          _buildStories(dark, stories),
          _buildHeader("Posts", posts.length),
          _buildGrid(dark, posts, PublisherContentType.post),
        ],
      );
    } else {
      return CustomScrollView(
        controller: PrimaryScrollController.of(context),
        slivers: <Widget>[
          _buildAppBar(),
          _buildGrid(
            dark,
            items..sort((a, b) => b.createdAt.compareTo(a.createdAt)),
            _contentType,
          )
        ],
      );
    }
  }

  Widget _buildStories(bool dark, List<PublisherMedia> stories) {
    List<Widget> media = [];

    stories.forEach(
      (post) {
        media.add(
          GestureDetector(
            onTap: () => _showDetails(post),
            child: Stack(
              alignment: Alignment.bottomCenter,
              overflow: Overflow.visible,
              children: <Widget>[
                CachedNetworkImage(
                  imageUrl: post.previewUrl,
                  imageBuilder: (context, imageProvider) => Container(
                    width: 146.25,
                    height: 260,
                    margin: EdgeInsets.only(right: 1.0),
                    decoration: BoxDecoration(
                      color: dark
                          ? Theme.of(context).appBarTheme.color
                          : Colors.grey[200],
                      image: DecorationImage(
                          image: imageProvider, fit: BoxFit.cover),
                    ),
                  ),
                  errorWidget: (context, url, error) => ImageError(
                    logUrl: url,
                    logParentName:
                        'profile/widgets/insights_media_analysis_details.dart > _buildStories',
                  ),
                ),
                Positioned(
                  bottom: 8.0,
                  child: Container(
                    decoration: BoxDecoration(
                        color: dark
                            ? Theme.of(context)
                                .appBarTheme
                                .color
                                .withOpacity(0.8)
                            : Colors.white.withOpacity(0.8),
                        borderRadius: BorderRadius.circular(4)),
                    padding: EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    child: Text(
                      formatter.format(post.createdAt),
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );

    if (stories.length > 0) {
      return SliverToBoxAdapter(
        child: Container(
          height: 250.0,
          child: ListView.builder(
            physics: AlwaysScrollableScrollPhysics(),
            scrollDirection: Axis.horizontal,
            itemCount: stories.length,
            itemBuilder: (context, index) {
              return media[index];
            },
          ),
        ),
      );
    } else {
      return SliverToBoxAdapter(
        child: Container(),
      );
    }
  }

  Widget _buildGrid(
    bool dark,
    List<PublisherMedia> posts,
    PublisherContentType contentType,
  ) {
    return posts.length == 0
        ? SliverPadding(
            padding: EdgeInsets.all(16),
          )
        : SliverGrid(
            gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: posts.length >= 3 ? 3 : posts.length,
              crossAxisSpacing: 1.0,
              mainAxisSpacing: 1.0,
              childAspectRatio:
                  contentType == PublisherContentType.post ? 1.0 : 0.5625,
            ),
            delegate: SliverChildListDelegate(posts
                .map((post) => GestureDetector(
                      onTap: () => _showDetails(post),
                      child: Stack(
                        alignment: Alignment.bottomCenter,
                        children: <Widget>[
                          MediaCachedImage(
                            imageUrl: post.previewUrl,
                          ),
                          Positioned(
                              bottom: 8.0,
                              child: Container(
                                  decoration: BoxDecoration(
                                      color: dark
                                          ? Theme.of(context)
                                              .appBarTheme
                                              .color
                                              .withOpacity(0.8)
                                          : Colors.white.withOpacity(0.8),
                                      borderRadius: BorderRadius.circular(4)),
                                  padding: EdgeInsets.symmetric(
                                      horizontal: 8, vertical: 4),
                                  child:
                                      Text(formatter.format(post.createdAt)))),
                          Positioned(
                            top: 10,
                            right: 10,
                            child: post.type == MediaType.image
                                ? Container()
                                : post.type == MediaType.carouselAlbum
                                    ? Icon(AppIcons.clone,
                                        color: Colors.white, size: 18.0)
                                    : Icon(AppIcons.video,
                                        color: Colors.white, size: 18.0),
                          ),
                        ],
                      ),
                    ))
                .toList()),
          );
  }

  Widget _buildFilters() {
    return _tags != null && _tags.isNotEmpty
        ? Container(
            height: 72.0,
            child: StreamBuilder<String>(
              stream: _bloc.query,
              builder: (context, snapshot) {
                final String currentTag = snapshot.data ?? "";

                return ListView(
                  padding: EdgeInsets.symmetric(horizontal: 12),
                  physics: AlwaysScrollableScrollPhysics(),
                  scrollDirection: Axis.horizontal,
                  children: _tags.map((String tag) {
                    if (tag.length != 1) {
                      return Padding(
                        padding: EdgeInsets.symmetric(horizontal: 4.0),
                        child: GestureDetector(
                          onTap: () => currentTag == tag
                              ? _querySection()
                              : _queryTag(tag),
                          child: Chip(
                            backgroundColor:
                                Theme.of(context).appBarTheme.color,
                            shape: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(40),
                              borderSide: BorderSide(
                                color: currentTag == tag
                                    ? Theme.of(context)
                                        .textTheme
                                        .bodyText1
                                        .color
                                    : Theme.of(context).hintColor,
                              ),
                            ),
                            label: Text(tag),
                            labelStyle: TextStyle(
                              color: currentTag == tag
                                  ? Theme.of(context).textTheme.bodyText1.color
                                  : Theme.of(context).hintColor,
                            ),
                          ),
                        ),
                      );
                    } else {
                      return Container();
                    }
                  }).toList(),
                );
              },
            ),
          )
        : Container(height: 0);
  }
}
