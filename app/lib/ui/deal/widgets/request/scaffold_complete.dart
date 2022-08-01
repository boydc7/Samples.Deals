import 'dart:async';

import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/ui/deal/blocs/request_complete.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold_complete_confirm.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/circle_progress_bar.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/icons.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

class RequestCompletePage extends StatefulWidget {
  final Deal deal;

  RequestCompletePage(this.deal);

  @override
  State<StatefulWidget> createState() => _RequestCompletePageState();
}

class _RequestCompletePageState extends State<RequestCompletePage>
    with AutomaticKeepAliveClientMixin {
  final ScrollController _scrollController = ScrollController();

  RequestCompleteBloc _bloc;
  StreamSubscription _subErrorAdd;
  ThemeData _theme;

  Deal _deal;
  int _requestedPosts;
  int _requestedStories;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();

    _deal = widget.deal;
    _requestedPosts = _deal.requestedPosts;
    _requestedStories = _deal.requestedStories;

    _bloc = RequestCompleteBloc();
    _bloc.loadMedia();

    _subErrorAdd = _bloc.errorAddMedia.listen(_onErrorAdd);
  }

  @override
  void dispose() {
    _subErrorAdd?.cancel();
    _bloc.dispose();
    _scrollController.dispose();

    super.dispose();
  }

  void _onErrorAdd(bool val) {
    if (val) {
      showSharedModalError(
        context,
        title: 'Unable to use this post',
        subTitle:
            'This was posted before you converted your Instagram profile to a professional profile.',
      );
    }
  }

  void _showIncompleteAlert(int posts, int stories) => showSharedModalAlert(
        context,
        Text("RYDR Incomplete"),
        content: Text(
            "You're completing this RYDR without the minimum requested posts. Please give an explanation to ${_deal.publisherAccount.userName} why the minimum wasn't met, or select the missing posts."),
        actions: [
          ModalAlertAction(
            isDestructiveAction: true,
            label: "Cancel",
            onPressed: () => Navigator.of(context).pop(),
          ),
          ModalAlertAction(
            isDefaultAction: true,
            label: "Complete",
            onPressed: () {
              Navigator.of(context).pop();
              _goToConfirm(posts, stories);
            },
          ),
        ],
      );

  void _goToConfirm(int posts, int stories) => Navigator.of(context).push(
        MaterialPageRoute(
            builder: (BuildContext context) => RequestCompleteConfirmPage(
                  deal: _deal,
                  completionMedia: _bloc.selectedMedia.value,
                  posts: posts,
                  stories: stories,
                ),
            settings: AppAnalytics.instance
                .getRouteSettings('request/completeconfirm')),
      );

  @override
  Widget build(BuildContext context) {
    super.build(context);

    _theme = Theme.of(context);

    return StreamBuilder<PublisherMediasResponse>(
      stream: _bloc.mediaResponse,
      builder: (context, snapshot) {
        return snapshot.connectionState == ConnectionState.waiting
            ? _buildLoadingBody()
            : snapshot.data.error != null
                ? _buildErrorBody(snapshot.data)
                : _buildSuccessBody(snapshot.data);
      },
    );
  }

  Widget _buildLoadingBody() => Scaffold(
      appBar: AppBar(
        leading: AppBarCloseButton(context),
        title: Text("Loading your recent posts..."),
      ),
      body: ListView(children: [LoadingGridShimmer()]));

  Widget _buildErrorBody(PublisherMediasResponse res) => Scaffold(
        appBar: AppBar(
          leading: AppBarCloseButton(context),
          title: Text("Unable to load recent posts.."),
        ),
        body: RetryError(
          onRetry: _bloc.loadMedia,
          error: res.error ?? res.error,
        ),
      );

  /// TODO: upload screenshots for storis
  Widget _buildSuccessBody(PublisherMediasResponse res) => Scaffold(
        appBar: _buildAppBar(),
        body: Column(
          children: <Widget>[
            Expanded(
              child: CustomScrollView(
                controller: _scrollController,
                slivers: <Widget>[
                  /// don't include stories for non-full publishers
                  !widget.deal.request.publisherAccount.isAccountFull
                      ? DealRequestCompleteStories(_bloc)
                      : DealRequestUploadStories(),
                  SliverToBoxAdapter(
                    child: Container(height: 1.0),
                  ),
                  DealRequestCompletePosts(_bloc),
                ].where((el) => el != null).toList(),
              ),
            ),
          ],
        ),
      );

  Widget _buildAppBar() {
    final double bottomHeight =
        _requestedPosts > 0 || _requestedStories > 0 ? 120.0 : 0.0;
    final double bottomPreferredHeight = bottomHeight + 48.0;
    final double appBarHeight = bottomPreferredHeight + kToolbarHeight;

    final Widget checkSingle = SizedBox(
      width: 36.0,
      height: 36.0,
      child: Center(
        child: Icon(
          AppIcons.check,
          size: 30.0,
          color: AppColors.successGreen,
        ),
      ),
    );

    final Widget checkDouble = SizedBox(
      width: 36.0,
      height: 36.0,
      child: Center(
        child: Icon(
          AppIcons.checkDouble,
          size: 30.0,
          color: AppColors.successGreen,
        ),
      ),
    );

    return PreferredSize(
      child: StreamBuilder<RequestCompleteSelectedCount>(
          stream: _bloc.selectedCount,
          builder: (context, snapshot) {
            final RequestCompleteSelectedCount selectedCount = snapshot.data;
            final selectedStories = selectedCount?.stories ?? 0;
            final selectedPosts = selectedCount?.posts ?? 0;

            /// if no stories or posts are requested, or the user already selected the required amount
            /// then we can mark canComplete for stories and/or posts as true
            ///
            /// TODO:Andre, we'll need to alter these to ignore stories for private
            bool canCompleteStories =
                _requestedStories == 0 || selectedStories >= _requestedStories;
            bool canCompletePosts =
                _requestedPosts == 0 || selectedPosts >= _requestedPosts;

            /// we'll determine if we should be showing in incomplete warning when the user
            /// taps to continue completing this request...
            bool incompleteWarning = false;

            /// if the user did not select the right amount of stories, but the request
            /// calls for more than 3 of them, then we'll let them get by with selecting one less
            /// but we'll display the incopmlete warning to them
            if (!canCompleteStories &&
                _requestedStories > 3 &&
                selectedStories == _requestedStories - 1) {
              canCompleteStories = true;
              incompleteWarning = true;
            }

            if (!canCompletePosts &&
                _requestedPosts > 3 &&
                selectedPosts >= (_requestedPosts / 2).floor()) {
              canCompletePosts = true;
              incompleteWarning = selectedPosts < _requestedPosts;
            }

            if (_requestedPosts == 0 && selectedPosts > 0) {
              canCompletePosts = true;
              canCompleteStories = true;
              incompleteWarning = false;
            }

            return AppBar(
              leading: AppBarCloseButton(context),
              elevation: 1.0,
              title: Text("Choose Posts"),
              actions: <Widget>[
                AnimatedOpacity(
                  opacity: canCompleteStories && canCompletePosts ? 1.0 : 0.0,
                  duration: Duration(milliseconds: 300),
                  child: TextButton(
                      label: 'Continue',
                      color: _theme.primaryColor,
                      onTap: canCompleteStories && canCompletePosts
                          ? incompleteWarning
                              ? () => _showIncompleteAlert(
                                  selectedPosts, selectedStories)
                              : () =>
                                  _goToConfirm(selectedPosts, selectedStories)
                          : null),
                ),
              ],
              bottom: PreferredSize(
                preferredSize: Size(double.infinity, bottomPreferredHeight),
                child: Column(
                  children: <Widget>[
                    Container(
                      width: double.infinity,
                      height: bottomHeight,
                      alignment: Alignment.bottomCenter,

                      /// show basic for non-full publishers
                      child: !widget.deal.request.publisherAccount.isAccountFull
                          ? Column(
                              crossAxisAlignment: CrossAxisAlignment.center,
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: <Widget>[
                                SizedBox(
                                  height: 88.0,
                                  width: 88.0,
                                  child: Stack(
                                    alignment: Alignment.center,
                                    children: <Widget>[
                                      CircleProgressBar(
                                        backgroundColor: _theme.hintColor,
                                        foregroundColor: _requestedPosts == 0 &&
                                                selectedPosts == 0
                                            ? _theme.primaryColor
                                            : selectedPosts >= _requestedPosts
                                                ? AppColors.successGreen
                                                : _theme.primaryColor,
                                        value: _requestedPosts == 0 &&
                                                selectedPosts == 0
                                            ? 0
                                            : _requestedPosts > 0
                                                ? (selectedPosts /
                                                    _requestedPosts)
                                                : 1,
                                      ),
                                      AnimatedCrossFade(
                                        alignment: Alignment.center,
                                        crossFadeState: _requestedPosts == 0 &&
                                                selectedPosts == 0
                                            ? CrossFadeState.showFirst
                                            : selectedPosts >= _requestedPosts
                                                ? CrossFadeState.showSecond
                                                : CrossFadeState.showFirst,
                                        duration: Duration(milliseconds: 500),
                                        firstChild: SizedBox(
                                          width: 36.0,
                                          height: 36.0,
                                          child: Center(
                                            child: Text(
                                              '$selectedPosts/$_requestedPosts',
                                              style: TextStyle(
                                                  fontSize: 18.0,
                                                  color: _theme.hintColor),
                                            ),
                                          ),
                                        ),
                                        secondChild: AnimatedCrossFade(
                                          alignment: Alignment.center,
                                          crossFadeState:
                                              selectedPosts > _requestedPosts
                                                  ? CrossFadeState.showSecond
                                                  : CrossFadeState.showFirst,
                                          duration: Duration(milliseconds: 500),
                                          firstChild: checkSingle,
                                          secondChild: checkDouble,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                SizedBox(height: 4.0),
                                Text(
                                  "Posts",
                                  style: TextStyle(
                                      fontSize: 16.0,
                                      fontWeight: FontWeight.w600),
                                ),
                              ],
                            )
                          : Row(
                              crossAxisAlignment: CrossAxisAlignment.center,
                              children: <Widget>[
                                SizedBox(
                                  width: 32.0,
                                ),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.center,
                                    mainAxisAlignment: MainAxisAlignment.center,
                                    children: <Widget>[
                                      SizedBox(
                                        height: 88.0,
                                        width: 88.0,
                                        child: Stack(
                                          alignment: Alignment.center,
                                          children: <Widget>[
                                            CircleProgressBar(
                                              backgroundColor: _theme.hintColor,
                                              foregroundColor:
                                                  _requestedStories == 0 &&
                                                          selectedStories == 0
                                                      ? _theme.primaryColor
                                                      : selectedStories >=
                                                              _requestedStories
                                                          ? AppColors
                                                              .successGreen
                                                          : _theme.primaryColor,
                                              value: _requestedStories == 0 &&
                                                      selectedStories == 0
                                                  ? 0
                                                  : _requestedStories > 0
                                                      ? (selectedStories /
                                                          _requestedStories)
                                                      : 1,
                                            ),
                                            AnimatedCrossFade(
                                              alignment: Alignment.center,
                                              crossFadeState:
                                                  _requestedStories == 0 &&
                                                          selectedStories == 0
                                                      ? CrossFadeState.showFirst
                                                      : selectedStories >=
                                                              _requestedStories
                                                          ? CrossFadeState
                                                              .showSecond
                                                          : CrossFadeState
                                                              .showFirst,
                                              duration:
                                                  Duration(milliseconds: 500),
                                              firstChild: SizedBox(
                                                width: 36.0,
                                                height: 36.0,
                                                child: Center(
                                                  child: Text(
                                                    '$selectedStories/$_requestedStories',
                                                    style: TextStyle(
                                                        fontSize: 18.0,
                                                        color:
                                                            _theme.hintColor),
                                                  ),
                                                ),
                                              ),
                                              secondChild: AnimatedCrossFade(
                                                alignment: Alignment.center,
                                                crossFadeState:
                                                    selectedStories >
                                                            _requestedStories
                                                        ? CrossFadeState
                                                            .showSecond
                                                        : CrossFadeState
                                                            .showFirst,
                                                duration:
                                                    Duration(milliseconds: 500),
                                                firstChild: checkSingle,
                                                secondChild: checkDouble,
                                              ),
                                            ),
                                          ],
                                        ),
                                      ),
                                      SizedBox(height: 4.0),
                                      Text(
                                        "Stories",
                                        style: TextStyle(
                                            fontSize: 16.0,
                                            fontWeight: FontWeight.w600),
                                      ),
                                    ],
                                  ),
                                ),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.center,
                                    mainAxisAlignment: MainAxisAlignment.center,
                                    children: <Widget>[
                                      SizedBox(
                                        height: 88.0,
                                        width: 88.0,
                                        child: Stack(
                                          alignment: Alignment.center,
                                          children: <Widget>[
                                            CircleProgressBar(
                                              backgroundColor: _theme.hintColor,
                                              foregroundColor:
                                                  _requestedPosts == 0 &&
                                                          selectedPosts == 0
                                                      ? _theme.primaryColor
                                                      : selectedPosts >=
                                                              _requestedPosts
                                                          ? AppColors
                                                              .successGreen
                                                          : _theme.primaryColor,
                                              value: _requestedPosts == 0 &&
                                                      selectedPosts == 0
                                                  ? 0
                                                  : _requestedPosts > 0
                                                      ? (selectedPosts /
                                                          _requestedPosts)
                                                      : 1,
                                            ),
                                            AnimatedCrossFade(
                                              alignment: Alignment.center,
                                              crossFadeState:
                                                  _requestedPosts == 0 &&
                                                          selectedPosts == 0
                                                      ? CrossFadeState.showFirst
                                                      : selectedPosts >=
                                                              _requestedPosts
                                                          ? CrossFadeState
                                                              .showSecond
                                                          : CrossFadeState
                                                              .showFirst,
                                              duration:
                                                  Duration(milliseconds: 500),
                                              firstChild: SizedBox(
                                                width: 36.0,
                                                height: 36.0,
                                                child: Center(
                                                  child: Text(
                                                    '$selectedPosts/$_requestedPosts',
                                                    style: TextStyle(
                                                        fontSize: 18.0,
                                                        color:
                                                            _theme.hintColor),
                                                  ),
                                                ),
                                              ),
                                              secondChild: AnimatedCrossFade(
                                                alignment: Alignment.center,
                                                crossFadeState: selectedPosts >
                                                        _requestedPosts
                                                    ? CrossFadeState.showSecond
                                                    : CrossFadeState.showFirst,
                                                duration:
                                                    Duration(milliseconds: 500),
                                                firstChild: checkSingle,
                                                secondChild: checkDouble,
                                              ),
                                            ),
                                          ],
                                        ),
                                      ),
                                      SizedBox(height: 4.0),
                                      Text(
                                        "Posts",
                                        style: TextStyle(
                                            fontSize: 16.0,
                                            fontWeight: FontWeight.w600),
                                      ),
                                    ],
                                  ),
                                ),
                                SizedBox(width: 32.0),
                              ],
                            ),
                    ),
                    Container(
                      height: 48.0,
                      child: Align(
                        alignment: Alignment.topCenter,
                        child: Padding(
                          padding: EdgeInsets.only(
                              top: 8.0, left: 32.0, right: 32.0),
                          child: RichText(
                            textAlign: TextAlign.center,
                            text: TextSpan(
                                style: _theme.textTheme.caption.merge(
                                  TextStyle(color: AppColors.grey300),
                                ),
                                children: [
                                  TextSpan(
                                      text: 'Select all posts mentioning '),
                                  TextSpan(
                                    text: _deal.publisherAccount.userName,
                                    style:
                                        TextStyle(fontWeight: FontWeight.w600),
                                  ),
                                  TextSpan(text: ' or '),
                                  TextSpan(
                                    text: _deal.place.name,
                                    style:
                                        TextStyle(fontWeight: FontWeight.w600),
                                  ),
                                ]),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            );
          }),
      preferredSize: Size.fromHeight(appBarHeight),
    );
  }
}

class DealRequestUploadStories extends StatelessWidget {
  List<PublisherMedia> uploadedMedia = [];

  @override
  Widget build(BuildContext context) {
    int itemCount = 1;

    if (uploadedMedia.isEmpty) {
      return SliverToBoxAdapter(
        child: Container(
          height: 250.0,
          decoration: BoxDecoration(
            color: Theme.of(context).appBarTheme.color,
          ),
          child: Stack(
            alignment: Alignment.center,
            children: <Widget>[
              Padding(
                padding: EdgeInsets.all(4),
                child: Row(
                  children: <Widget>[
                    Expanded(
                      child: Container(
                        height: 260,
                        margin: EdgeInsets.all(4),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(4),
                          color: Theme.of(context).canvasColor.withOpacity(0.2),
                        ),
                      ),
                    ),
                    Expanded(
                      child: Container(
                        height: 260,
                        margin: EdgeInsets.all(4),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(4),
                          color: Theme.of(context).canvasColor.withOpacity(0.2),
                        ),
                      ),
                    ),
                    Expanded(
                      child: Container(
                        height: 260,
                        margin: EdgeInsets.all(4),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(4),
                          color: Theme.of(context).canvasColor.withOpacity(0.2),
                        ),
                      ),
                    )
                  ],
                ),
              ),
              Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.center,
                children: <Widget>[
                  Container(
                    height: 40,
                    width: 40,
                    decoration: BoxDecoration(
                      image: DecorationImage(
                        fit: BoxFit.cover,
                        image: AssetImage(
                          'assets/icons/instagram-story-icon.png',
                        ),
                      ),
                    ),
                  ),
                  Padding(
                    padding: EdgeInsets.only(top: 8.0),
                    child: Text("Tap to Upload Story Screenshots"),
                  ),
                  Text(
                    "Non-professional accounts must screenshot their stories",
                    style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(
                            color: Theme.of(context).hintColor,
                          ),
                        ),
                  ),
                ],
              ),
            ],
          ),
        ),
      );
    } else {
      return SliverToBoxAdapter(
        child: Container(
          height: 250.0,
          child: ListView.builder(
            physics: AlwaysScrollableScrollPhysics(),
            scrollDirection: Axis.horizontal,
            itemCount: itemCount + 1,
            itemBuilder: (context, index) {
              if (index == (itemCount - 1)) {
                return CachedNetworkImage(
                  imageUrl:
                      "https://i.pinimg.com/originals/63/c8/8b/63c88b0039f0066e9c50a055ff681f30.jpg",
                  imageBuilder: (context, imageProvider) => Container(
                    width: 146.25,
                    height: 260,
                    margin: EdgeInsets.only(right: 1.0),
                    decoration: BoxDecoration(
                      image: DecorationImage(
                          image: imageProvider, fit: BoxFit.cover),
                    ),
                    child: Container(
                      color: AppColors.blue.withOpacity(0.7),
                      child: Center(
                        child: Icon(
                          AppIcons.check,
                          color: AppColors.white,
                        ),
                      ),
                    ),
                  ),
                  errorWidget: (context, url, error) => ImageError(
                    logUrl: url,
                    logParentName:
                        'deal/widgets/request/scaffold_complete.dart',
                  ),
                );
              } else {
                return Container(
                  width: 146.25,
                  height: 260,
                  margin: EdgeInsets.only(right: 1.0),
                  color: Theme.of(context).canvasColor.withOpacity(0.2),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.center,
                    children: <Widget>[
                      Container(
                        height: 32,
                        width: 32,
                        decoration: BoxDecoration(
                          image: DecorationImage(
                            fit: BoxFit.cover,
                            image: AssetImage(
                              'assets/icons/instagram-story-icon.png',
                            ),
                          ),
                        ),
                      ),
                      Padding(
                        padding: EdgeInsets.only(top: 8.0),
                        child: Text("Add Screenshot"),
                      ),
                      Padding(
                        padding: EdgeInsets.only(top: 2.0),
                        child: Text(
                          "Instagram Story",
                          style: Theme.of(context).textTheme.caption.merge(
                                TextStyle(
                                  color: Theme.of(context).hintColor,
                                ),
                              ),
                        ),
                      ),
                    ],
                  ),
                );
              }
            },
          ),
        ),
      );
    }
  }
}

class DealRequestCompleteStories extends StatelessWidget {
  final RequestCompleteBloc bloc;

  DealRequestCompleteStories(this.bloc);

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<List<PublisherMedia>>(
      stream: bloc.userStories,
      builder: (context, snapshot) {
        final List<PublisherMedia> stories = snapshot.data ?? [];
        final DateFormat formatter = DateFormat('MMMd');

        List<Widget> media = [];
        bool dark = Theme.of(context).brightness == Brightness.dark;

        stories.forEach((post) {
          media.add(
            GestureDetector(
              onTap: () => bloc.setMedia(post),
              child: Stack(
                alignment: Alignment.bottomCenter,
                overflow: Overflow.visible,
                children: <Widget>[
                  Opacity(
                    /// adjust opacity on media that was created before they were a business account
                    opacity: post.isPreBizAccountConversionMedia ? 0.5 : 1,
                    child: CachedNetworkImage(
                      imageUrl: post.previewUrl,
                      imageBuilder: (context, imageProvider) => Container(
                        width: 146.25,
                        height: 260,
                        margin: EdgeInsets.only(right: 1.0),
                        decoration: BoxDecoration(
                          image: DecorationImage(
                              image: imageProvider, fit: BoxFit.cover),
                        ),
                        child: post.selected
                            ? Container(
                                color: AppColors.blue.withOpacity(0.7),
                                child: Center(
                                  child: Icon(
                                    AppIcons.check,
                                    color: AppColors.white,
                                  ),
                                ),
                              )
                            : Container(),
                      ),
                      errorWidget: (context, url, error) => ImageError(
                        logUrl: url,
                        logParentName:
                            'deal/widgets/request/scaffold_complete.dart',
                      ),
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
        });

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
      },
    );
  }
}

class DealRequestCompletePosts extends StatelessWidget {
  final RequestCompleteBloc bloc;

  DealRequestCompletePosts(this.bloc);

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return StreamBuilder<List<PublisherMedia>>(
      stream: bloc.userPosts,
      builder: (context, snapshot) {
        final List<PublisherMedia> posts = snapshot.data ?? [];
        final DateFormat formatter = DateFormat('MMMd');
        List<Widget> media = [];

        posts.forEach((post) {
          media.add(GestureDetector(
            onTap: () => bloc.setMedia(post),
            child: Stack(
              alignment: Alignment.bottomCenter,
              children: <Widget>[
                Opacity(
                  /// adjust opacity on media that was created before they were a business account
                  opacity: post.isPreBizAccountConversionMedia ? 0.5 : 1,
                  child: CachedNetworkImage(
                    imageUrl: post.previewUrl,
                    imageBuilder: (context, imageProvider) => Container(
                      decoration: BoxDecoration(
                        color: dark
                            ? Theme.of(context).appBarTheme.color
                            : Colors.grey[200],
                        image: DecorationImage(
                          image: imageProvider,
                          fit: BoxFit.cover,
                        ),
                      ),
                      child: post.selected
                          ? Container(
                              color: AppColors.blue.withOpacity(0.7),
                              child: Center(
                                child: Icon(
                                  AppIcons.check,
                                  color: AppColors.white,
                                ),
                              ),
                            )
                          : Container(),
                    ),
                    errorWidget: (context, url, error) => ImageError(
                      logUrl: url,
                      logParentName:
                          'deal/widgets/request/scaffold_complete.dart > DealRequestCompletePosts',
                    ),
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
                        padding:
                            EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        child: Text(formatter.format(post.createdAt)))),
                Positioned(
                  top: 12,
                  left: 12,
                  child: post.type == MediaType.image
                      ? Icon(AppIcons.camera,
                          color: AppColors.white.withOpacity(0.7), size: 18.0)
                      : post.type == MediaType.carouselAlbum
                          ? Icon(AppIcons.clone,
                              color: AppColors.white.withOpacity(0.7),
                              size: 18.0)
                          : Icon(AppIcons.video,
                              color: AppColors.white.withOpacity(0.7),
                              size: 18.0),
                ),
              ],
            ),
          ));
        });

        return SliverGrid(
          gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 3, crossAxisSpacing: 1.0, mainAxisSpacing: 1.0),
          delegate: SliverChildListDelegate(
            media,
          ),
        );
      },
    );
  }
}
