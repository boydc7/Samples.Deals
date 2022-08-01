import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/publisher_media_vision.dart';
import 'package:rydr_app/ui/profile/blocs/insights_media_analysis.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_captions.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_notable.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_posts_stories.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_recent.dart';
import 'package:rydr_app/ui/shared/widgets/badge.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

class ProfileInsightsMediaAnalysis extends StatefulWidget {
  final PublisherAccount profile;

  ProfileInsightsMediaAnalysis(this.profile);

  @override
  _ProfileInsightsMediaAnalysisState createState() =>
      _ProfileInsightsMediaAnalysisState();
}

class _ProfileInsightsMediaAnalysisState
    extends State<ProfileInsightsMediaAnalysis>
    with AutomaticKeepAliveClientMixin {
  ThemeData _theme;
  bool _darkMode;
  InsightsMediaAnalysisBloc _bloc;

  final Map<String, String> _pageContent = {
    "title": "Selfie Vision™",
    "subtitle": "Instagram Post & Story Analysis",
    "no_results_message":
        "We are still sorting, gathering and analyzing.\nCheck back in 5 minutes.",
    "no_results_subtitle": "Pull to refresh",
    "opt_in_error":
        "Unable to opt-in to Selfie Vision™, please try again in a few moments",
    "continue": "Continue",
    "not_opted_in": "This account has not yet opted into Selfie Vision™",
  };

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();

    _pageContent["legal"] = appState.currentProfile.isCreator
        ? "By continuing, you are agreeing to allow qualifying businesses to view these results for approving RYDR requests. Note: these results are based on an algorithmic interpretation of your Instagram media and captions and are constantly being improved."
        : "By continuing, you are agreeing to allow RYDR to analyze your recent posts and stories. Note: these results are based on an algorithmic interpretation of your Instagram media and captions and are constantly being improved.";

    _bloc = InsightsMediaAnalysisBloc(widget.profile);
    _bloc.loadData();
  }

  @override
  void dispose() {
    _bloc.dispose();
    super.dispose();
  }

  void _showMoreInfo() {
    showSharedModalBottomInfo(context,
        hideTitleOnAndroid: false,
        title: _pageContent['title'],
        topWidget: Badge(
          elevation: 0,
          color: Theme.of(context).primaryColor,
          value: "Beta",
        ),
        subtitle: _pageContent['subtitle'],
        child: insightsBottomSheet(context, [
          InsightsBottomSheetTile(
            "What is Selfie Vision?",
            "This is a proprietary artififcal intelligence engine that runs multiple levels of analysis across your Instagram posts, stories, and post captions.",
          ),
          InsightsBottomSheetTile(
            "Image Analysis and Labeling",
            "Using the latest artificial intelligence technology, our image labeling system identifies thousands of objects and scenes within your posts.\n\nExample: \"Show me every post that has a dog in it.\"",
          ),
          InsightsBottomSheetTile(
            "Emotion and Face Detection",
            "We identify and detect faces, and their emotions, appearing in your posts and surface attributes such as gender, age range, eyes open, glasses, and facial hair for each post.\n\nExample: \"Show me every post that has people with beards.\"",
          ),
          InsightsBottomSheetTile(
            "Optical Character Recognition",
            "We detect and index text that appears within your posts. This includes things like Story tags, quotes, memes, etc.\n\nExample: \"Show me every story that I tagged @handstandman.\"",
          ),
        ]),
        initialRatio: 0.8);
  }

  void _enableAnalysis() async {
    showSharedLoadingLogo(context);

    /// opt in to AI for the current profile
    final bool enabled = await _bloc.setAcceptedTerms();

    Navigator.of(context).pop();

    /// if for some reason we were unable to complete the opt-in call
    /// show an error to the user asking them to try again in a few moments
    if (!enabled) {
      showSharedModalError(context, subTitle: _pageContent["otp_in_error"]);
    }
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);

    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;

    /// if the user him/herself is viewing their own Ai then check the stream flag
    /// on their own/current profile, otherwise check appstate function to see if
    /// this business has access to the creators ai based on biz subscription and creator setting
    if (widget.profile.id == appState.currentProfile.id) {
      return StreamBuilder<bool>(
        stream: appState.currentProfileOptInToAi,
        builder: (context, snapshot) =>
            snapshot.data == true ? _buildAccepted() : _buildNotAccepted(),
      );
    } else {
      return appState.isAiAvailable(widget.profile)
          ? _buildAccepted()
          : _buildNotAccepted();
    }
  }

  Widget _buildNotAccepted() {
    final Color color = _theme.scaffoldBackgroundColor;
    final String brainIconUrl = _darkMode
        ? 'assets/icons/brain-icon-big-dark.svg'
        : 'assets/icons/brain-icon-big-light.svg';

    /// need to "check" for who is accessing this profile
    /// is it the user him/herself or is it a business viewing another creator
    /// and then show a different splash screen for a business (probably just a simple one since they should not actully get here...)
    return Scaffold(
      backgroundColor: color,
      appBar: AppBar(
        backgroundColor: color,
        elevation: 0.0,
        leading: AppBarBackButton(context),
      ),
      body: SafeArea(
        child: Column(
          children: <Widget>[
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Padding(
                    padding: EdgeInsets.only(bottom: 16.0),
                    child: SvgPicture.asset(
                      brainIconUrl,
                      width: 130.0,
                    ),
                  ),
                  Text(
                    _pageContent["title"],
                    style: _theme.textTheme.headline6.merge(
                      TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 28.0,
                      ),
                    ),
                  ),
                  SizedBox(height: 8),
                  Padding(
                    padding: EdgeInsets.symmetric(horizontal: 40.0),
                    child: Text(
                      _pageContent["subtitle"],
                      textAlign: TextAlign.center,
                    ),
                  ),
                  SizedBox(height: 16),
                  SecondaryButton(
                    label: "Learn More",
                    onTap: () => _showMoreInfo(),
                  )
                ],
              ),
            ),
            appState.currentProfile.id == widget.profile.id
                ? Padding(
                    padding:
                        EdgeInsets.symmetric(vertical: 8.0, horizontal: 16.0),
                    child: Column(
                      children: <Widget>[
                        PrimaryButton(
                          onTap: _enableAnalysis,
                          label: _pageContent["continue"],
                          hasIcon: true,
                          hasShadow: true,
                          icon: AppIcons.arrowRight,
                        ),
                        SizedBox(height: 8.0),
                        Text(
                          _pageContent["legal"],
                          style: _theme.textTheme.caption.merge(
                            TextStyle(color: _theme.hintColor),
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ],
                    ),
                  )
                : Padding(
                    padding:
                        EdgeInsets.symmetric(vertical: 8.0, horizontal: 16.0),
                    child: Text(_pageContent["not_opted_in"])),
          ],
        ),
      ),
    );
  }

  Widget _buildAccepted() => StreamBuilder<PublisherAccountMediaVisionResponse>(
      stream: _bloc.analysisResponse,
      builder: (context, snapshot) => snapshot.connectionState ==
              ConnectionState.waiting
          ? _buildLoadingScaffold()
          : snapshot.data.error != null
              ? _buildErrorScaffold(snapshot.data)
              : snapshot.data.model == null ||
                      (snapshot.data.model.totalPostsAnalyzed ??
                              0 + snapshot.data.model.todayStoriesAnalyzed ??
                              0) ==
                          0
                  ? _buildNoResultsScaffold()
                  : _buildResultsScaffold(snapshot.data));

  Widget _buildLoadingScaffold() => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          backgroundColor: _theme.scaffoldBackgroundColor,
          elevation: 0,
        ),
        body: insightsLoadingBody(),
      );

  Widget _buildErrorScaffold(PublisherAccountMediaVisionResponse data) =>
      Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          backgroundColor: _theme.scaffoldBackgroundColor,
          title: Text(_pageContent['title']),
        ),
        body: Container(
            child: RetryError(
          error: data.error,
          fullSize: true,
          onRetry: _bloc.loadData,
        )),
      );

  Widget _buildNoResultsScaffold() => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          backgroundColor: _theme.scaffoldBackgroundColor,
          elevation: 0,
        ),
        body: RefreshIndicator(
          displacement: 0.0,
          backgroundColor: _theme.appBarTheme.color,
          color: _theme.textTheme.bodyText2.color,
          onRefresh: () => _bloc.loadData(true),
          child: ListView(
            physics: AlwaysScrollableScrollPhysics(),
            children: <Widget>[
              SafeArea(
                child: Container(
                  height:
                      MediaQuery.of(context).size.height - kToolbarHeight * 2,
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      Expanded(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: <Widget>[
                            Text(
                              _pageContent['title'],
                              textAlign: TextAlign.center,
                              style: _theme.textTheme.bodyText1.merge(
                                TextStyle(
                                  fontSize: 20,
                                ),
                              ),
                            ),
                            SizedBox(height: 4),
                            Text(
                              _pageContent['no_results_message'],
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                color: _theme.hintColor,
                              ),
                            ),
                          ],
                        ),
                      ),
                      Text(
                        _pageContent['no_results_subtitle'],
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          color: _theme.hintColor,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      );

  Widget _buildResultsScaffold(PublisherAccountMediaVisionResponse data) {
    final List<PublisherMedia> recentForDisplay = data.model.recentForDisplay();

    return Scaffold(
      body: RefreshIndicator(
        displacement: 0.0,
        backgroundColor: _theme.appBarTheme.color,
        color: _theme.textTheme.bodyText2.color,
        onRefresh: () => _bloc.loadData(true),
        child: CustomScrollView(
          slivers: <Widget>[
            SliverAppBar(
              forceElevated: true,
              elevation: 1.0,
              pinned: true,
              leading: AppBarBackButton(context),
              centerTitle: true,
              title: Text(_pageContent['title']),
              actions: <Widget>[
                IconButton(
                  icon: Icon(
                    AppIcons.flask,
                    size: 22,
                    color: Theme.of(context).primaryColor,
                  ),
                  onPressed: () => _showMoreInfo(),
                )
              ],
              expandedHeight: recentForDisplay.length == 0 ||
                      appState.currentProfile.id != widget.profile.id
                  ? 0
                  : 168,
              flexibleSpace: recentForDisplay.length == 0 ||
                      appState.currentProfile.id != widget.profile.id
                  ? null
                  : FlexibleSpaceBar(
                      collapseMode: CollapseMode.parallax,
                      background: Column(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: <Widget>[
                          ProfileInsightsMediaAnalysisRecent(
                            widget.profile,
                            data.model.recentForDisplay(),
                            data.model.recentPosts,
                            data.model.recentStories,
                          ),
                        ],
                      ),
                    ),
            ),
            SliverList(
              delegate: SliverChildListDelegate(
                [
                  ProfileInsightsMediaAnalysisNotable(
                    widget.profile,
                    data.model.notable,
                  ),
                  ProfileInsightsMediaAnalysisPostsStories(
                    widget.profile,
                    data.model.stories,
                    data.model.posts,
                  ),
                  ProfileInsightsMediaAnalysisCaptions(
                    widget.profile,
                    data.model.captions,
                  ),
                ],
              ),
            ),
            SliverToBoxAdapter(
              child: SizedBox(
                  height: kToolbarHeight * 1.5 +
                      MediaQuery.of(context).padding.bottom),
            )
          ],
        ),
      ),
      extendBody: true,
      bottomNavigationBar: _buildBottomBar(data),
    );
  }

  Widget _buildBottomBar(PublisherAccountMediaVisionResponse data) {
    final int postLimit = data.model.postDailyLimit;
    final int storyLimit = data.model.storyDailyLimit;
    final int postsQueue = data.model.todayPostsAnalyzed;
    final int storyQueue = data.model.todayStoriesAnalyzed;
    final bool exhausted = postsQueue >= postLimit && storyQueue >= storyLimit;

    Widget _badge(int index, int queue) => Expanded(
          child: Container(
            margin: EdgeInsets.only(right: 2),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(4.0),
              child: Container(
                height: 6.0,
                color: index < queue ? _theme.primaryColor : _theme.hintColor,
              ),
            ),
          ),
        );

    return SafeArea(
      top: true,
      bottom: false,
      child: SizedBox.expand(
        child: DraggableScrollableSheet(
          initialChildSize: 0.16,
          minChildSize: 0.1,
          maxChildSize: 0.18,
          builder: (BuildContext context, ScrollController scrollController) {
            final Size size = MediaQuery.of(context).size;
            final double topOpacity = scrollController?.hasClients == false
                ? 1.0
                : (size.height /
                            scrollController?.position?.viewportDimension) <
                        1.12
                    ? 0.0
                    : 1.0;
            final double bottomOpacity = scrollController?.hasClients == false
                ? 1.0
                : (size.height /
                            scrollController?.position?.viewportDimension) >
                        7
                    ? 0.0
                    : 1.0;
            final int remainingPosts =
                ((data.model.postDailyLimit) - (postsQueue)).clamp(
                    0, data.model.storyDailyLimit + data.model.postDailyLimit);
            final int remainingStories =
                ((data.model.storyDailyLimit) - (storyQueue)).clamp(
                    0, data.model.storyDailyLimit + data.model.storyDailyLimit);

            return Container(
              decoration: BoxDecoration(
                color:
                    _darkMode ? _theme.canvasColor : _theme.appBarTheme.color,
                borderRadius: BorderRadius.only(
                  topLeft: Radius.circular(16),
                  topRight: Radius.circular(16),
                ),
                boxShadow: AppShadows.elevation[1],
              ),
              child: ListView(
                padding: EdgeInsets.only(top: 12),
                controller: scrollController,
                children: <Widget>[
                  AnimatedOpacity(
                    duration: Duration(milliseconds: 250),
                    opacity: topOpacity,
                    child: Container(
                      margin: EdgeInsets.only(bottom: 8),
                      alignment: Alignment.center,
                      child: SizedBox(
                        height: 4.0,
                        width: 40.0,
                        child: Container(
                          decoration: BoxDecoration(
                            color: _darkMode
                                ? _theme.hintColor
                                : _theme.canvasColor,
                            borderRadius: BorderRadius.circular(8.0),
                          ),
                        ),
                      ),
                    ),
                  ),
                  GestureDetector(
                    onTap: () => null,
                    //onTap: () => Navigator.of(context).push(MaterialPageRoute(
                    //builder: (context) => InsightsMediaAnalysisQueuePage(),
                    //settings: AppAnalytics.instance
                    //  .getRouteSettings('profile/insights/ai/queue'))),
                    child: Padding(
                      padding: EdgeInsets.only(left: 16.0, right: 16.0),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          AnimatedCrossFade(
                            duration: Duration(milliseconds: 250),
                            crossFadeState: bottomOpacity == 0
                                ? CrossFadeState.showSecond
                                : CrossFadeState.showFirst,
                            firstChild: Text(
                              (data.model.storyDailyLimit +
                                          data.model.postDailyLimit) ==
                                      remainingPosts
                                  ? "You haven't posted yet today"
                                  : !exhausted
                                      ? "Daily AI Limit"
                                      : "Daily AI Limit Exhausted",
                              style: _theme.textTheme.bodyText1.merge(
                                TextStyle(
                                  color: Theme.of(context).primaryColor,
                                ),
                              ),
                            ),
                            secondChild: Text(
                              remainingPosts == 1 && remainingStories == 0
                                  ? "$remainingPosts post remaining today"
                                  : remainingPosts == 0 && remainingStories == 0
                                      ? "Post Limited Reached for Today"
                                      : remainingPosts == 1 &&
                                              remainingStories > 0
                                          ? "$remainingPosts post and $remainingStories stories remaining today"
                                          : "",
                              style: _theme.textTheme.bodyText1,
                            ),
                          ),
                          Text(
                            "New posts start analyzing after midnight UTC.",
                            style: _theme.textTheme.caption.merge(
                              TextStyle(color: _theme.hintColor),
                            ),
                          ),
                          SizedBox(height: 16.0),
                          Visibility(
                            visible: !exhausted,
                            child: AnimatedOpacity(
                              duration: Duration(milliseconds: 250),
                              opacity: bottomOpacity,
                              child: Row(
                                children: <Widget>[
                                  Expanded(
                                    child: Column(
                                      children: <Widget>[
                                        Row(
                                          children: List.generate(
                                            data.model.postDailyLimit,
                                            (index) =>
                                                _badge(index, postsQueue),
                                          ),
                                        ),
                                        Padding(
                                          padding: EdgeInsets.only(top: 6.0),
                                          child: Text(
                                            postLimit > 1
                                                ? "$postLimit posts"
                                                : "1 post",
                                            style:
                                                _theme.textTheme.caption.merge(
                                              TextStyle(
                                                  color: _theme.hintColor),
                                            ),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                  SizedBox(width: 16.0),
                                  Expanded(
                                    flex: 5,
                                    child: Column(
                                      children: <Widget>[
                                        Row(
                                          children: List.generate(
                                            data.model.storyDailyLimit,
                                            (index) =>
                                                _badge(index, storyQueue),
                                          ),
                                        ),
                                        Padding(
                                          padding: EdgeInsets.only(top: 6.0),
                                          child: Text(
                                            data.model.storyDailyLimit >
                                                    storyQueue
                                                ? storyLimit > 1
                                                    ? "$storyLimit stories"
                                                    : "1 story"
                                                : "Story Limit Reached",
                                            style:
                                                _theme.textTheme.caption.merge(
                                              TextStyle(
                                                  color: _theme.hintColor),
                                            ),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                          SizedBox(height: !exhausted ? 12.0 : 0),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}
