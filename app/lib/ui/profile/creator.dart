import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/ui/profile/blocs/creator.dart';
import 'package:rydr_app/ui/profile/widgets/creator_history.dart';
import 'package:rydr_app/ui/profile/widgets/summary_sections.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:shimmer/shimmer.dart';

class ProfileCreatorPage extends StatefulWidget {
  final Deal deal;

  ProfileCreatorPage(this.deal);

  @override
  State<StatefulWidget> createState() => _ProfileCreatorPageState();
}

class _ProfileCreatorPageState extends State<ProfileCreatorPage> {
  final CreatorBloc _bloc = CreatorBloc();
  final ScrollController _scrollController = ScrollController();

  ThemeData _theme;
  PublisherAccount _creator;
  bool _darkMode;
  double _expandedHeight;

  @override
  void initState() {
    super.initState();

    _bloc.loadProfile(widget.deal.request.publisherAccount.id);

    _expandedHeight = 220;
    _bloc.setScrollOffset(_expandedHeight);
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _bloc.dispose();
    _scrollController.dispose();

    super.dispose();
  }

  void _onScroll() =>
      _bloc.setScrollOffset(_expandedHeight - _scrollController.offset);

  void _options() => showSharedModalBottomActions(
        context,
        title: 'Available Profile Options',
        subtitle: _creator.userName,
        actions: <ModalBottomAction>[
          ModalBottomAction(
            child: Text("View Instagram Profile"),
            icon: AppIcons.times,
            onTap: () => Utils.launchUrl(
              context,
              "https://instagram.com/${_creator.userName}",
              trackingName: 'profile',
            ),
          ),
        ],
      );

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;
    _creator = widget.deal.request.publisherAccount;

    return StreamBuilder<PublisherMediasResponse>(
      stream: _bloc.mediaResponse,
      builder: (context, AsyncSnapshot<PublisherMediasResponse> snapshot) {
        if (snapshot.hasData) {
          if (snapshot.data.error != null) {
            return _buildError(snapshot.data);
          }
          // return _buildLoading(context);
          return _buildSuccess(snapshot.data.models);
        } else if (snapshot.hasError) {
          return _buildError(snapshot.error);
        } else {
          return _buildLoading(context);
        }
      },
    );
  }

  Widget _buildLoading(BuildContext context) => Scaffold(
        extendBody: true,
        floatingActionButtonLocation: FloatingActionButtonLocation.centerDocked,
        floatingActionButton: FloatingActionButton(
          elevation: 2,
          onPressed: () {},
          child: Icon(
            AppIcons.ellipsisV,
            color: _theme.hintColor,
          ),
          backgroundColor: _theme.appBarTheme.color,
        ),
        bottomNavigationBar: BottomAppBar(
          elevation: 2.0,
          color: _darkMode ? Color(0xFF232323) : _theme.appBarTheme.color,
          notchMargin: 4,
          shape: AutomaticNotchedShape(
              RoundedRectangleBorder(), StadiumBorder(side: BorderSide())),
          child: Container(
            height: kToolbarHeight,
          ),
        ),
      );

  Widget _buildError(PublisherMediasResponse response) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          title: Text("Unable to load this profile..."),
        ),
        body: RetryError(
            error: response.error,
            onRetry: () =>
                _bloc.loadProfile(widget.deal.request.publisherAccount.id)),
      );

  Widget _buildImgPlaceholder(
    BuildContext context,
    bool error, [
    bool loading = true,
  ]) =>
      error || !loading
          ? Expanded(
              child: Container(
                decoration: BoxDecoration(
                  color: _theme.scaffoldBackgroundColor,
                ),
              ),
            )
          : Expanded(
              child: Shimmer.fromColors(
                baseColor: _darkMode ? Color(0xFF121212) : AppColors.white100,
                highlightColor: _darkMode ? Colors.black : AppColors.white50,
                child: Container(
                  decoration: BoxDecoration(
                    color: _theme.appBarTheme.color,
                  ),
                ),
              ),
            );

  Widget _buildImg(BuildContext context, PublisherMedia post, int index) =>
      Expanded(
        child: FadeInOpacityOnly(
          (1.5 * index).toDouble(),
          CachedNetworkImage(
            imageUrl: post.previewUrl,
            imageBuilder: (context, imageProvider) => Container(
              width: (MediaQuery.of(context).size.width / 2) - 22.0,
              decoration: BoxDecoration(
                color: _theme.appBarTheme.color,
                image: DecorationImage(
                  alignment: Alignment.center,
                  fit: BoxFit.cover,
                  image: imageProvider,
                ),
              ),
            ),
            errorWidget: (context, url, error) => ImageError(
              logUrl: url,
              logParentName: 'profile/creator.dart > _buildImg',
              logPublisherAccountId: widget.deal.request.publisherAccount.id,
            ),
          ),
        ),
      );

  Widget _buildHeaderPhotos(List<PublisherMedia> media) => Container(
        foregroundDecoration: BoxDecoration(
          gradient: LinearGradient(
            end: Alignment.center,
            begin: Alignment.bottomCenter,
            stops: [0.0, 0.5, 1.0],
            colors: [
              Colors.black.withOpacity(0.65),
              Colors.black.withOpacity(0.35),
              Colors.black.withOpacity(0.0)
            ],
          ),
        ),
        child: Column(
          children: <Widget>[
            Expanded(
              child: Row(
                children: <Widget>[
                  media.length > 0
                      ? _buildImg(context, media[0], 0)
                      : _buildImgPlaceholder(context, false, false),
                  Container(
                    width: 1,
                    height: double.infinity,
                    color: _theme.appBarTheme.color,
                  ),
                  media.length > 1
                      ? _buildImg(context, media[1], 1)
                      : _buildImgPlaceholder(context, false, false),
                  Container(
                    width: 1,
                    height: double.infinity,
                    color: _theme.appBarTheme.color,
                  ),
                  media.length > 2
                      ? _buildImg(context, media[2], 2)
                      : _buildImgPlaceholder(context, false, false),
                  Container(
                    width: 1,
                    height: double.infinity,
                    color: _theme.appBarTheme.color,
                  ),
                  media.length > 3
                      ? _buildImg(context, media[3], 3)
                      : _buildImgPlaceholder(context, false, false),
                ],
              ),
            ),
            Container(
              height: 1,
              width: double.infinity,
              color: _theme.appBarTheme.color,
            ),
            Expanded(
              child: Row(
                children: <Widget>[
                  media.length > 4
                      ? _buildImg(context, media[4], 4)
                      : _buildImgPlaceholder(context, false, false),
                  Container(
                    width: 1,
                    height: double.infinity,
                    color: _theme.appBarTheme.color,
                  ),
                  media.length > 5
                      ? _buildImg(context, media[5], 5)
                      : _buildImgPlaceholder(context, false, false),
                  Container(
                    width: 1,
                    height: double.infinity,
                    color: _theme.appBarTheme.color,
                  ),
                  media.length > 6
                      ? _buildImg(context, media[6], 6)
                      : _buildImgPlaceholder(context, false, false),
                  Container(
                    width: 1,
                    height: double.infinity,
                    color: _theme.appBarTheme.color,
                  ),
                  media.length > 7
                      ? _buildImg(context, media[7], 7)
                      : _buildImgPlaceholder(context, false, false),
                ],
              ),
            ),
          ],
        ),
      );

  Widget _buildBasicStats() {
    final NumberFormat f = NumberFormat.decimalPattern();
    final TextStyle style = _theme.textTheme.bodyText1.merge(
      TextStyle(
        fontSize: 16,
        fontWeight: FontWeight.w700,
      ),
    );

    return Padding(
      padding: EdgeInsets.only(top: 24.0),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(f.format(_creator.publisherMetrics.media), style: style),
                Text("Posts"),
              ],
            ),
          ),
          Expanded(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(_creator.publisherMetrics.followedByDisplay, style: style),
                Text("Followers"),
              ],
            ),
          ),
          Expanded(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(_creator.publisherMetrics.followsDisplay, style: style),
                Text("Following"),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSuccess(List<PublisherMedia> media) {
    final titleStyle = _theme.textTheme.bodyText2.merge(
      TextStyle(
        fontWeight: FontWeight.w500,
        fontSize: 24.0,
        color: _theme.textTheme.bodyText2.color,
      ),
    );

    return Scaffold(
      extendBody: true,
      body: Stack(
        overflow: Overflow.visible,
        children: [
          NestedScrollView(
            controller: _scrollController,
            physics: AlwaysScrollableScrollPhysics(),
            headerSliverBuilder: (context, value) => [
              SliverAppBar(
                automaticallyImplyLeading: false,
                expandedHeight: _expandedHeight,
                primary: false,
                backgroundColor: _theme.scaffoldBackgroundColor,
                flexibleSpace: FlexibleSpaceBar(
                  background: _buildHeaderPhotos(media),
                ),
              ),
            ],
            body: ListView(
              padding: EdgeInsets.only(top: 0),
              children: <Widget>[
                FadeInBottomTop(
                  0,
                  Padding(
                    padding: EdgeInsets.only(
                        top: 8, bottom: 16, left: 32, right: 32),
                    child: Container(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.center,
                        mainAxisSize: MainAxisSize.max,
                        children: <Widget>[
                          Container(
                            height: 4.0,
                            child: Center(
                              child: Container(
                                width: 28.0,
                                decoration: BoxDecoration(
                                    color: _darkMode
                                        ? Colors.white24
                                        : _theme.canvasColor,
                                    borderRadius: BorderRadius.circular(8.0)),
                              ),
                            ),
                          ),
                          SizedBox(height: 16),
                          Text(_creator.userName,
                              textAlign: TextAlign.center, style: titleStyle),
                          _creator.description != null
                              ? Column(
                                  children: <Widget>[
                                    SizedBox(height: 6.0),
                                    Text(
                                      _creator.description != null
                                          ? _creator.description
                                          : "",
                                      textAlign: TextAlign.center,
                                      style: _theme.textTheme.bodyText2,
                                    ),
                                  ],
                                )
                              : Container(height: 0),
                          _creator.description == null &&
                                  _creator.nameDisplay != null
                              ? Column(
                                  children: <Widget>[
                                    SizedBox(height: 6.0),
                                    Text(
                                      _creator.nameDisplay != null
                                          ? _creator.nameDisplay
                                          : "",
                                      textAlign: TextAlign.center,
                                      style: _theme.textTheme.bodyText2,
                                    ),
                                  ],
                                )
                              : Container(height: 0),
                          _buildBasicStats()
                        ],
                      ),
                    ),
                  ),
                  350,
                ),
                FadeInBottomTop(
                  5,
                  CreatorWorkHistorySection(
                    _bloc,
                    widget.deal.request.publisherAccount,
                  ),
                  350,
                ),
                FadeInBottomTop(
                  7,
                  sectionDivider(context),
                  350,
                ),
                FadeInBottomTop(
                  10,
                  ProfileSummarySections(
                    widget.deal.request.publisherAccount,
                    widget.deal,
                  ),
                  350,
                ),
                SizedBox(height: kToolbarHeight * 1.5),
              ],
              physics: NeverScrollableScrollPhysics(),
            ),
          ),
          StreamBuilder<double>(
            stream: _bloc.scrollOffset,
            builder: (context, snapshot) => Positioned(
              top: snapshot.data == null ? _expandedHeight : snapshot.data - 16,
              width: MediaQuery.of(context).size.width,
              child: Container(
                height: 16,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.only(
                    topRight: Radius.circular(16),
                    topLeft: Radius.circular(16),
                  ),
                  color: _theme.scaffoldBackgroundColor,
                ),
              ),
            ),
          ),
        ],
      ),
      floatingActionButtonLocation: FloatingActionButtonLocation.centerDocked,
      floatingActionButton: GestureDetector(
        onLongPress: () => _options(),
        child: FloatingActionButton(
          elevation: 2,
          hoverElevation: 2,
          focusElevation: 2,
          highlightElevation: 2,
          splashColor: _theme.textTheme.bodyText2.color.withOpacity(0.4),
          backgroundColor: _theme.appBarTheme.color,
          child: UserAvatar(
            _creator,
            width: 56.0,
            hideBorder: true,
          ),
          onPressed: () => Utils.launchUrl(
            context,
            "https://instagram.com/${_creator.userName}",
            trackingName: 'profile',
          ),
        ),
      ),
      bottomNavigationBar: BottomAppBar(
        elevation: 2.0,
        color: _darkMode ? Color(0xFF232323) : _theme.appBarTheme.color,
        notchMargin: 4,
        shape: AutomaticNotchedShape(
            RoundedRectangleBorder(), StadiumBorder(side: BorderSide())),
        child: Padding(
          padding: EdgeInsets.symmetric(vertical: 4, horizontal: 8),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              AppBarBackButton(
                context,
                color: _theme.textTheme.bodyText2.color,
              ),
              Padding(
                padding: EdgeInsets.only(bottom: 4.0),
                child: Text(
                  _creator.userName,
                  style: _theme.textTheme.caption.merge(
                    TextStyle(
                      fontWeight: FontWeight.w500,
                      color: _theme.textTheme.bodyText1.color,
                    ),
                  ),
                ),
              ),
              IconButton(
                icon: Icon(
                  AppIcons.ellipsisV,
                  color: _theme.textTheme.bodyText2.color,
                ),
                onPressed: () => _options(),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
