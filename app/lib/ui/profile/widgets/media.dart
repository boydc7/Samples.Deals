import 'dart:ui';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/ui/profile/blocs/media.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_detail_viewer.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/profile/widgets/header.dart';

class ProfileMediaPage extends StatefulWidget {
  final PublisherAccount user;

  ProfileMediaPage(this.user);

  @override
  State<StatefulWidget> createState() {
    return _ProfileMediaPageState();
  }
}

class _ProfileMediaPageState extends State<ProfileMediaPage>
    with AutomaticKeepAliveClientMixin {
  final ProfileMediaBloc _bloc = ProfileMediaBloc();

  /// keeps a reference to the currently visible overlay when viewing
  /// details of a given story or post media
  OverlayEntry overlayEntry;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();

    _bloc.loadMedia(widget.user?.id ?? appState.currentProfile.id);
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _showDetails(
    PublisherMedia post,
    PublisherAccount user,
    PublisherContentType type,
    List<PublisherMedia> posts,
    List<PublisherMedia> stories,
  ) {
    overlayEntry = OverlayEntry(
      builder: (BuildContext context) => Dismissible(
        key: UniqueKey(),
        direction: DismissDirection.vertical,
        onDismissed: (DismissDirection direction) {
          overlayEntry.remove();
        },
        child: MediaViewer(
            user,
            type == PublisherContentType.post ? posts : stories,
            post,
            overlayEntry,
            null),
      ),
      opaque: false,
      maintainState: true,
    );
    Navigator.of(context).overlay.insert(overlayEntry);
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);

    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return StreamBuilder<ProfileMediaBlocResponse>(
      stream: _bloc.mediaResponse,
      builder: (context, snapshot) {
        return snapshot.connectionState == ConnectionState.waiting
            ? Container(
                child: ListView(children: [LoadingListShimmer()]),
              )
            : snapshot.data.response.error != null
                ? Container(
                    child: RetryError(
                    error: snapshot.data.response.error,
                    onRetry: () {
                      _bloc.loadMedia(
                          widget.user?.id ?? appState.currentProfile.id);
                    },
                  ))
                : _buildMedia(
                    dark,
                    snapshot.data.posts,
                    snapshot.data.stories,
                  );
      },
    );
  }

  Widget _buildMedia(
    bool dark,
    List<PublisherMedia> posts,
    List<PublisherMedia> stories,
  ) {
    return Container(
      child: CustomScrollView(
        slivers: <Widget>[
          widget.user != null && appState.currentProfile.isCreator
              ? SliverToBoxAdapter(
                  child: Column(
                    children: <Widget>[
                      ProfileHeader(widget.user),
                      Divider(height: 1),
                    ],
                  ),
                )
              : SliverToBoxAdapter(child: Container()),
          _buildStoryMedia(
            dark,
            posts,
            stories,
          ),
          _buildPostHeader(
            dark,
            posts,
            stories,
          ),
          _buildPostMedia(
            dark,
            posts,
            stories,
          ),
          posts.length == 0
              ? SliverFillRemaining(
                  child: Column(
                    mainAxisSize: MainAxisSize.max,
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.center,
                    children: <Widget>[
                      Icon(
                        AppIcons.cameraRetro,
                        size: 40.0,
                      ),
                      SizedBox(
                        height: 16.0,
                      ),
                      Padding(
                        padding: EdgeInsets.all(16),
                        child: Text(
                            'We\'ve not synced your latest posts yet, or you haven\'t posted anything on your feed in a while...',
                            style: Theme.of(context).textTheme.headline6),
                      )
                    ],
                  ),
                )
              : SliverToBoxAdapter(
                  child: Container(),
                ),
        ],
      ),
    );
  }

  Widget _buildStoryMedia(
    bool dark,
    List<PublisherMedia> posts,
    List<PublisherMedia> stories,
  ) {
    List<Widget> media = [];

    stories.forEach((post) {
      media.add(
        GestureDetector(
          onTap: () => _showDetails(
            post,
            widget.user,
            PublisherContentType.story,
            posts,
            stories,
          ),
          child: Padding(
            padding: EdgeInsets.only(left: 16.0),
            child: Stack(
              alignment: Alignment.center,
              children: <Widget>[
                ClipRRect(
                  borderRadius: BorderRadius.circular(40.0),
                  child: SizedBox(
                    height: 72.0,
                    width: 72.0,
                    child: CachedNetworkImage(
                      imageUrl: post.previewUrl,
                      imageBuilder: (context, imageProvider) => Container(
                        decoration: BoxDecoration(
                          border: Border.all(
                            width: 1.0,
                            color: Color(0xd9d9d9),
                          ),
                          borderRadius: BorderRadius.circular(40.0),
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
                            'profile/widgets/media.dart > _buildStoryMedia',
                      ),
                    ),
                  ),
                ),
                Container(
                  width: 80.0,
                  height: 80.0,
                  decoration: BoxDecoration(
                      border: Border.all(color: Colors.grey.shade400, width: 1),
                      borderRadius: BorderRadius.circular(40.0)),
                )
              ],
            ),
          ),
        ),
      );
    });

    if (media.length > 0) {
      return SliverToBoxAdapter(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Padding(
              padding: EdgeInsets.only(left: 16.0, top: 16.0, bottom: 8.0),
              child: Row(
                children: <Widget>[
                  Text(
                    "Active Stories  ",
                    style:
                        TextStyle(fontSize: 16.0, fontWeight: FontWeight.w600),
                  ),
                  Text(
                    media.length.toString(),
                    style: TextStyle(
                        fontSize: 16.0,
                        fontWeight: FontWeight.w400,
                        color: AppColors.grey300),
                  ),
                ],
              ),
            ),
            Container(
              height: 80.0,
              child: ListView.builder(
                physics: AlwaysScrollableScrollPhysics(),
                scrollDirection: Axis.horizontal,
                itemCount: media.length,
                itemBuilder: (context, index) {
                  return media[index];
                },
              ),
            ),
            SizedBox(
              height: 16.0,
            )
          ],
        ),
      );
    } else {
      return SliverToBoxAdapter(
        child: Container(),
      );
    }
  }

  Widget _buildPostMedia(
    bool dark,
    List<PublisherMedia> posts,
    List<PublisherMedia> stories,
  ) {
    List<Widget> media = [];

    posts.forEach((post) {
      media.add(GestureDetector(
        onTap: () => _showDetails(
          post,
          widget.user,
          PublisherContentType.post,
          posts,
          stories,
        ),
        child: CachedNetworkImage(
          imageUrl: post.previewUrl,
          imageBuilder: (context, imageProvider) => Container(
            decoration: BoxDecoration(
              color:
                  dark ? Theme.of(context).appBarTheme.color : Colors.grey[200],
              image: DecorationImage(image: imageProvider, fit: BoxFit.cover),
            ),
          ),
          errorWidget: (context, url, error) => ImageError(
            logUrl: url,
            logParentName: 'profile/widgets/media.dart > _buildPostMedia',
          ),
        ),
      ));
    });

    return SliverGrid(
      gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        crossAxisSpacing: 1.0,
        mainAxisSpacing: 1.0,
      ),
      delegate: SliverChildListDelegate(
        media,
      ),
    );
  }

  Widget _buildPostHeader(
    bool dark,
    List<PublisherMedia> posts,
    List<PublisherMedia> stories,
  ) {
    if (posts.length > 0 && stories.length > 0) {
      return SliverList(
        delegate: SliverChildListDelegate([
          Padding(
            padding: EdgeInsets.only(left: 16.0, top: 16.0, bottom: 8.0),
            child: Row(
              children: <Widget>[
                Text(
                  "Latest Posts",
                  style: TextStyle(fontSize: 16.0, fontWeight: FontWeight.w600),
                ),
              ],
            ),
          ),
        ]),
      );
    } else {
      return SliverToBoxAdapter(
        child: Container(),
      );
    }
  }
}
