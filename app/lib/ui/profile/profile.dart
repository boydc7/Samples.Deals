import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/publisher_account.dart';
import 'package:rydr_app/ui/main/list_requests.dart';
import 'package:rydr_app/ui/notifications/notifications.dart';
import 'package:rydr_app/ui/profile/blocs/profile.dart';
import 'package:rydr_app/ui/profile/settings.dart';
import 'package:rydr_app/ui/profile/widgets/header.dart';
import 'package:rydr_app/ui/profile/widgets/input_tags.dart';
import 'package:rydr_app/ui/profile/widgets/media.dart';
import 'package:rydr_app/ui/profile/widgets/summary_sections.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/badge.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';
import 'package:rydr_app/models/list_page_arguments.dart';

/// this is the profile view for either a user viewing their own profile
/// or for an influencer to look at a business
class ProfilePage extends StatefulWidget {
  final int profileId;
  final int initialTabIndex;

  ProfilePage(this.profileId, [this.initialTabIndex]);

  @override
  State<StatefulWidget> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage>
    with SingleTickerProviderStateMixin {
  final _bloc = ProfileBloc();

  TabController _tabController;
  ThemeData _theme;
  String _mapIconUrl;
  bool _darkMode;
  bool _isMe;
  bool _isCreator;

  @override
  void initState() {
    super.initState();

    final int _unreadNotifications = appState.currentProfile != null
        ? appState.currentProfile.unreadNotifications
        : 0;

    _isMe = widget.profileId != null &&
            widget.profileId == appState.currentProfile?.id ||
        widget.profileId == null;
    _isCreator = appState.currentProfile?.isCreator;

    _tabController = TabController(
      vsync: this,
      initialIndex: widget.initialTabIndex != null
          ? widget.initialTabIndex
          : widget.profileId == null
              ? _isCreator
                  ? _unreadNotifications > 0 ? 1 : 2
                  : _unreadNotifications > 0 ? 0 : 1
              : 0,
      length: widget.profileId == null ? _isCreator ? 3 : 2 : 1,
    );

    _tabController.addListener(() {
      if (!_tabController.indexIsChanging) {
        if (_isCreator) {
          AppAnalytics.instance.logScreen(_tabController.index == 0
              ? 'requests'
              : _tabController.index == 1 ? 'notifications' : 'profile/me');
        } else {
          AppAnalytics.instance.logScreen(
              _tabController.index == 0 ? 'notifications' : 'profile/me');
        }
      }
    });

    _bloc.loadProfile(widget.profileId);
  }

  @override
  void dispose() {
    _bloc.dispose();
    _tabController.dispose();

    super.dispose();
  }

  Future<void> _refresh() => _bloc.loadProfile(widget.profileId, true);

  void _goToSettings() => Navigator.push(
        context,
        MaterialPageRoute(
          builder: (context) => ProfileSettingsPage(),
          settings: AppAnalytics.instance.getRouteSettings('profile/settings'),
        ),
      );

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;

    _mapIconUrl = _darkMode
        ? 'assets/icons/map-dark-icon.svg'
        : 'assets/icons/map-icon.svg';

    return appState.currentProfile == null
        ? Container()
        : StreamBuilder<PublisherAccountResponse>(
            stream: _bloc.userResponse,
            builder: (context, snapshot) {
              if (snapshot.hasData) {
                if (snapshot.data.error != null) {
                  return _buildError(snapshot.data);
                }

                return _buildSuccess(snapshot.data);
              } else if (snapshot.hasError) {
                return _buildError(snapshot.error);
              } else {
                return _buildLoading();
              }
            },
          );
  }

  Widget _buildLoading() => Scaffold(
        appBar: AppBar(
          automaticallyImplyLeading: false,
          title: Text("Loading profile..."),
        ),
        body: ListView(children: [Container()]),
      );

  Widget _buildAppBar(PublisherAccount profile) {
    List<Tab> tabs = [];

    if (_isMe) {
      if (_isCreator) {
        tabs.add(
          Tab(icon: Icon(AppIcons.megaphone)),
        );
      }

      tabs.add(
        Tab(
          icon: Stack(
            overflow: Overflow.visible,
            children: <Widget>[
              Icon(AppIcons.bell),
              StreamBuilder<int>(
                stream: appState.currentProfileUnreadNotifications,
                builder: (context, snapshot) {
                  final int count = snapshot.data ?? 0;
                  return count > 0
                      ? Positioned(
                          bottom: -1.0,
                          left: count > 9 ? -7.5 : -4.0,
                          child: FadeInScaleUp(
                            10,
                            Badge(
                              elevation: 0.0,
                              color: _theme.primaryColor,
                              value: count.toString(),
                            ),
                          ),
                        )
                      : Container(width: 0, height: 0);
                },
              ),
            ],
          ),
        ),
      );

      tabs.add(
        Tab(icon: Icon(AppIcons.userAlt)),
      );
    }

    return AppBar(
        backgroundColor: _theme.appBarTheme.color,
        elevation: 1.0,
        leading: _isMe && _isCreator
            ? GestureDetector(
                onTap: () => Navigator.of(context).pop(),
                child: Stack(
                  alignment: Alignment.center,
                  children: <Widget>[
                    Container(
                      width: 48.0,
                      height: 48.0,
                    ),
                    SvgPicture.asset(
                      _mapIconUrl,
                      width: 27.0,
                    )
                  ],
                ),
              )
            : IconButton(
                icon: _isMe ? Icon(AppIcons.arrowLeft) : backButtonIcon(),
                splashColor: Colors.transparent,
                highlightColor: Colors.transparent,
                onPressed: () {
                  /// go back if we can pop, otherwise send user to their respective home
                  if (Navigator.of(context).canPop()) {
                    Navigator.of(context).pop();
                  } else {
                    Navigator.of(context).pushNamed(AppRouting.getHome);
                  }
                },
              ),
        centerTitle: true,
        title: GestureDetector(
          onTap: _isMe
              ? () =>
                  Navigator.of(context).pushNamed(AppRouting.getConnectPages)
              : null,
          child: Container(
            width: double.infinity,
            height: 48.0,
            color: _theme.appBarTheme.color,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(profile.userName),
                _isMe
                    ? appState.currentWorkspace.hasLinkedPublishers
                        ? Icon(AppIcons.angleDownReg)
                        : Row(
                            children: <Widget>[
                              SizedBox(
                                width: 4.0,
                              ),
                              Icon(AppIcons.plus, size: 16)
                            ],
                          )
                    : Container(width: 0, height: 0)
              ],
            ),
          ),
        ),
        bottom: _isMe
            ? TabBar(
                labelColor: _theme.tabBarTheme.labelColor,
                unselectedLabelColor: _theme.tabBarTheme.unselectedLabelColor,
                controller: _tabController,
                indicatorWeight: 1.4,
                indicatorColor: _darkMode
                    ? AppColors.white.withOpacity(0.87)
                    : _theme.textTheme.bodyText2.color,
                tabs: tabs)
            : null,
        actions: _isMe
            ? <Widget>[
                GestureDetector(
                  child: Padding(
                    padding: EdgeInsets.only(right: 16.0),
                    child: Icon(
                      AppIcons.bars,
                      size: 24.0,
                    ),
                  ),
                  onTap: _goToSettings,
                )
              ]
            : <Widget>[
                Visibility(
                  visible: _isCreator && profile.isBusiness,
                  child: IconButton(
                    icon: Icon(AppIcons.instagram),
                    onPressed: () => Utils.launchUrl(
                      context,
                      "https://instagram.com/${profile.userName}",
                      trackingName: 'profile',
                    ),
                  ),
                ),
              ]);
  }

  Widget _buildSuccess(PublisherAccountResponse userResponse) {
    List<Widget> tabBarChildren = [];
    TabBarView tabBarView;

    if (_isMe) {
      if (_isCreator) {
        tabBarChildren = [
          ListRequests(
            arguments: ListPageArguments(
              layoutType: ListPageLayout.Injected,
              filterRequestStatus: [
                DealRequestStatus.requested,
                DealRequestStatus.invited,
                DealRequestStatus.inProgress,
                DealRequestStatus.redeemed,
                DealRequestStatus.completed,
              ],
            ),
          ),
          ListNotifications(),
          _buildProfilePage(userResponse.model),
        ];
      } else {
        tabBarChildren = [
          ListNotifications(),
          _buildProfilePage(userResponse.model),
        ];
      }
    } else {
      tabBarChildren = [
        ProfileMediaPage(userResponse.model),
      ];
    }

    tabBarView = TabBarView(
      controller: _tabController,
      children: tabBarChildren,
    );

    return Scaffold(appBar: _buildAppBar(userResponse.model), body: tabBarView);
  }

  Widget _buildProfilePage(PublisherAccount profile) => RefreshIndicator(
      displacement: 0.0,
      backgroundColor: Theme.of(context).appBarTheme.color,
      color: Theme.of(context).textTheme.bodyText2.color,
      onRefresh: _refresh,
      child: ListView(children: <Widget>[
        ProfileHeader(profile),

        /// hidden behind workspace feature, and only available
        /// for looking at a business profile
        profile.isBusiness
            ? ProfileInputTags(
                valueStream: _bloc.tags,
                handleUpdate: _bloc.updateTags,
              )
            : Container(),

        /// don't include additonal info for soft-linked profiles
        profile.isAccountSoft
            ? Container()
            : Column(
                children: <Widget>[
                  sectionDivider(context),
                  ProfileSummarySections(profile),
                ],
              )
      ]));

  Widget _buildError(PublisherAccountResponse response) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          title: Text("Unable to load this profile..."),
        ),
        body: RetryError(
            error: response.error,
            onRetry: () => _bloc.loadProfile(widget.profileId)),
      );
}
