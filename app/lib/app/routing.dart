import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/models/enums/deal.dart';

import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/ui/connect/connect_facebook_modify.dart';
import 'package:rydr_app/ui/connect/authenticate.dart';
import 'package:rydr_app/ui/connect/connect_profile.dart';
import 'package:rydr_app/ui/deal/add.dart';
import 'package:rydr_app/ui/deal/add_event.dart';
import 'package:rydr_app/ui/deal/deal_insights.dart';
import 'package:rydr_app/ui/deal/deal_view.dart';
import 'package:rydr_app/ui/profile/about.dart';
import 'package:rydr_app/ui/profile/account.dart';
import 'package:rydr_app/ui/profile/debug.dart';
import 'package:rydr_app/ui/profile/delinquent.dart';

import 'package:rydr_app/ui/profile/main.dart';
import 'package:rydr_app/ui/connect/pages.dart';
import 'package:rydr_app/ui/connect/onboard.dart';
import 'package:rydr_app/ui/connect/choose_type.dart';
import 'package:rydr_app/ui/profile/notifications.dart';
import 'package:rydr_app/ui/profile/notifications_email.dart';
import 'package:rydr_app/ui/profile/places.dart';

import 'package:rydr_app/ui/profile/profile.dart';
import 'package:rydr_app/ui/deal/request.dart';
import 'package:rydr_app/ui/deal/request_dialog.dart';
import 'package:rydr_app/ui/deal/add_deal.dart';
import 'package:rydr_app/ui/deal/deal_edit.dart';
import 'package:rydr_app/ui/main/main.dart';
import 'package:rydr_app/ui/main/list_deals.dart';
import 'package:rydr_app/ui/main/list_requests.dart';
import 'package:rydr_app/ui/map/map.dart';
import 'package:rydr_app/ui/workspace/create.dart';
import 'package:rydr_app/ui/workspace/join.dart';

import 'package:rydr_app/ui/workspace/users.dart';
import 'package:rydr_app/ui/workspace/requests.dart';
import 'package:rydr_app/ui/workspace/settings.dart';

class AppRouting {
  /// routes which don't need to be constructured using additional params
  /// can be declared as static const here so we can also use them for comparision on route generation below

  /// INSTALL, ONBOARDING, CONNECT, PAGES
  static const String getAuthenticate = 'connect/authenticate';
  static const String getConnectProfile = 'connect/profile';
  static const String getConnectAddInstagram = 'connect/profile/instagram';
  static const String getConnectSwitchFacebook = 'connect/profile/facebook';
  static const String getConnectFacebookModify = 'connect/facebookmodify';
  static const String getConnectPages = 'connect/pages';
  static const String getConnectOnboard = 'connect/onboard';
  static const String getConnectChooseType = 'connect/onboard/choose';

  /// HOME
  static const String getHome = 'home';

  /// WORKSPACES
  static const String getWorkspaceSettings = 'workspace/settings';
  static const String getWorkspaceUsers = 'workspace/users';
  static const String getWorkspaceRequests = 'workspace/requests';
  static const String getWorkspaceJoin = 'workspace/join';
  static const String getWorkspaceCreate = 'workspace/create';

  /// DEALS
  static const String getDealsActive = 'deals';
  static const String getDealsArchived = 'deals/archived';
  static const String getDealsDeleted = 'deals/deleted';
  static const String getDealsMap = 'deals/map';

  static const String getDealAdd = 'deal/add';
  static const String getDealAddEvent = 'deal/addevent';
  static const String getDealAddDeal = 'deal/adddeal';
  static const String getDealAddVirtual = 'deal/addvirtual';

  static String getDealViewByLinkRoute(String dealLink) =>
      'deal/view/$dealLink';
  static String getDealViewByIdRoute(int dealId) => 'deal/view/$dealId';
  static String getDealEditRoute(int dealId) => 'deal/edit/$dealId';
  static String getDealInsightsRoute(int dealId) => 'deal/insights/$dealId';

  /// REQUESTS
  static const String getRequestsPending = 'requests/pending';
  static const String getRequestsInProgress = 'requests/inprogress';
  static const String getRequestsRedeemed = 'requests/redeemed';
  static const String getRequestsInvited = 'requests/invited';
  static const String getRequestsCompleted = 'requests/completed';
  static const String getRequestsCancelled = 'requests/cancelled';
  static const String getRequestsDenied = 'requests/denied';
  static const String getRequestsDelinquent = 'requests/delinquent';
  static String getRequestRoute(int dealId, [int publisherAccountId]) =>
      publisherAccountId != null
          ? 'request/view/$dealId/$publisherAccountId'
          : 'request/view/$dealId';
  static String getRequestDialogRoute(int dealId, [int publisherAccountId]) =>
      publisherAccountId != null
          ? 'request/messages/$dealId/$publisherAccountId'
          : 'request/messages/$dealId';
  static String getRequestRedeemRoute(int dealId, [int publisherAccountId]) =>
      publisherAccountId != null
          ? 'request/redeem/$dealId/$publisherAccountId'
          : 'request/redeem/$dealId';
  static String getRequestCompleteRoute(int dealId, [int publisherAccountId]) =>
      publisherAccountId != null
          ? 'request/complete/$dealId/$publisherAccountId'
          : 'request/complete/$dealId';

  static String getRequestDeclineRoute(int dealId, [int publisherAccountId]) =>
      publisherAccountId != null
          ? 'request/decline/$dealId/$publisherAccountId'
          : 'request/decline/$dealId';
  static String getRequestCancelRoute(int dealId, [int publisherAccountId]) =>
      publisherAccountId != null
          ? 'request/cancel/$dealId/$publisherAccountId'
          : 'request/cancel/$dealId';
  static String getRequestApproveRoute(int dealId, [int publisherAccountId]) =>
      publisherAccountId != null
          ? 'request/approve/$dealId/$publisherAccountId'
          : 'request/approve/$dealId';

  /// PROFILE
  static const String getProfileDelinquent = 'profile/delinquent';
  static const String getProfileMeRoute = 'profile/me';
  static const String getProfileRequestsRoute = 'profile/requests';
  static const String getProfilePlacesRoute = 'profile/settings/places';
  static const String getProfileNotificationsRoute =
      'profile/settings/notifications';
  static const String getProfileEmailNotificationsRoute =
      'profile/settings/emailnotifications';
  static const String getProfileAccountRoute = 'profile/settings/account';
  static const String getProfileAboutRoute = 'profile/settings/about';
  static const String getProfileDebugRoute = 'profile/settings/debug';
  static String getProfileRoute(int publisherAccountId) =>
      'profile/view/$publisherAccountId';

  static Route<dynamic> getRoute(RouteSettings settings) {
    final log = getLogger('AppRouting');

    log.i('getRoute | $settings');

    /// this tracks a 'sanitized' route (e.g. removing id's and such) with analytics
    /// then builds and returnes the intended route back to the caller
    RouteSettings _trackAndBuildRoute(path) {
      AppAnalytics.instance.logScreen(path);

      return RouteSettings(name: settings.name);
    }

    /// NOTE! that we pass along route settings which is observed by the analytics observer on the app scaffold
    /// which is then tracked by analtyics as the 'path'
    switch (settings.name) {
      case getHome:
        return PageTransitionNone(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => MainPage());
      case getWorkspaceSettings:
        return MaterialPageRoute(
            fullscreenDialog: true,
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => WorkspaceSettings());
      case getWorkspaceJoin:
        return MaterialPageRoute(
            fullscreenDialog: true,
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => WorkspaceJoinPage());
      case getWorkspaceCreate:
        return MaterialPageRoute(
            fullscreenDialog: true,
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => WorkspaceCreatePage());
      case getWorkspaceRequests:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => WorkspaceRequests());
      case getWorkspaceUsers:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => WorkspaceUsers());
      case getDealsArchived:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ListDeals(
                arguments: settings.arguments ??
                    ListPageArguments(
                      layoutType: ListPageLayout.StandAlone,
                      isRequests: false,
                      filterDealStatus: [
                        DealStatus.completed,
                      ],
                    )));
      case getDealsDeleted:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ListDeals(
                arguments: settings.arguments ??
                    ListPageArguments(
                      layoutType: ListPageLayout.StandAlone,
                      isRequests: false,
                      filterDealStatus: [
                        DealStatus.deleted,
                      ],
                    )));
      case getDealsActive:
        {
          ListPageArguments args = settings.arguments ??
              ListPageArguments(
                isRequests: false,
                layoutType: ListPageLayout.Integrated,
                filterDealStatus: [DealStatus.published],
              );

          /// set tab to 1 which is only relevant if we're
          /// not loading list in stand alone and would trigger main.dart
          /// to load the second tab (e.g. active marketplace deals)
          return MaterialPageRoute(
              settings: _trackAndBuildRoute(settings.name),
              builder: (context) => args.layoutType == ListPageLayout.StandAlone
                  ? ListDeals(arguments: args)
                  : MainPage(arguments: args..mainScaffoldTab = 1));
        }
      case getRequestsPending:
        {
          ListPageArguments args = settings.arguments ??
              ListPageArguments(
                  filterRequestStatus: [DealRequestStatus.requested]);

          /// set tab to 2 which is only relevant if we're
          /// not loading list in stand alone and would trigger main.dart
          /// to load the third tab (e.g. pending requests)
          return MaterialPageRoute(
              settings: _trackAndBuildRoute(settings.name),
              builder: (context) => args.layoutType == ListPageLayout.StandAlone
                  ? ListRequests(arguments: args)
                  : MainPage(arguments: args..mainScaffoldTab = 2));
        }
      case getRequestsInProgress:
        {
          ListPageArguments args = settings.arguments ??
              ListPageArguments(filterRequestStatus: [
                DealRequestStatus.inProgress,
              ]);

          /// set tab to 3 which is only relevant if we're
          /// not loading list in stand alone and would trigger main.dart
          /// to load the fourth tab (e.g. requests in progress)
          return MaterialPageRoute(
              settings: _trackAndBuildRoute(settings.name),
              builder: (context) => args.layoutType == ListPageLayout.StandAlone
                  ? ListRequests(arguments: args)
                  : MainPage(arguments: args..mainScaffoldTab = 2));
        }
      case getRequestsRedeemed:
        {
          ListPageArguments args = settings.arguments ??
              ListPageArguments(filterRequestStatus: [
                DealRequestStatus.redeemed,
              ]);

          /// set tab to 3 which is only relevant if we're
          /// not loading list in stand alone and would trigger main.dart
          /// to load the fourth tab (e.g. requests in progress)
          return MaterialPageRoute(
              settings: _trackAndBuildRoute(settings.name),
              builder: (context) => args.layoutType == ListPageLayout.StandAlone
                  ? ListRequests(arguments: args)
                  : MainPage(arguments: args..mainScaffoldTab = 2));
        }
      case getRequestsCompleted:
        {
          ListPageArguments args = settings.arguments ??
              ListPageArguments(filterRequestStatus: [
                DealRequestStatus.completed,
              ]);

          /// set tab to 4 which is only relevant if we're
          /// not loading list in stand alone and would trigger main.dart
          /// to load the fifth tab (e.g. completed requests)
          return MaterialPageRoute(
              settings: _trackAndBuildRoute(settings.name),
              builder: (context) => args.layoutType == ListPageLayout.StandAlone
                  ? ListRequests(arguments: args)
                  : MainPage(arguments: args..mainScaffoldTab = 2));
        }
      case getRequestsDelinquent:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ListRequests(
                arguments: settings.arguments ??
                    ListPageArguments(
                        layoutType: ListPageLayout.StandAlone,
                        filterRequestStatus: [DealRequestStatus.delinquent])));
      case getRequestsCancelled:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ListRequests(
                arguments: settings.arguments ??
                    ListPageArguments(
                        layoutType: ListPageLayout.StandAlone,
                        filterRequestStatus: [DealRequestStatus.cancelled])));
      case getRequestsInvited:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ListRequests(
                arguments: settings.arguments ??
                    ListPageArguments(
                        layoutType: ListPageLayout.StandAlone,
                        filterRequestStatus: [DealRequestStatus.invited])));
      case getRequestsDenied:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ListRequests(
                arguments: settings.arguments ??
                    ListPageArguments(
                        layoutType: ListPageLayout.StandAlone,
                        filterRequestStatus: [DealRequestStatus.denied])));
      case getDealsMap:
        return PageTransitionNone(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => MapPage());

      case getAuthenticate:
        return PageTransitionNone(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => AuthenticatePage());

      case getConnectProfile:
        return PageTransitionNone(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ConnectProfilePage());

      case getConnectSwitchFacebook:
        return PageTransitionNone(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) =>
                ConnectProfilePage(switchFacebookAcount: true));

      case getConnectAddInstagram:
        return PageTransitionNone(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ConnectProfilePage(addInstagramBasic: true));

      case getConnectFacebookModify:
        return MaterialPageRoute(
            fullscreenDialog: true,
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ConnectFacebookModifyPage());
      case getConnectPages:
        return PageTransitionNone(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ConnectPagesPage());
      case getConnectOnboard:
        return PageTransitionNone(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ConnectOnboardPage());
      case getConnectChooseType:
        return PageTransitionNone(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ConnectChooseTypePage(settings.arguments));
      case getProfileDelinquent:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ProfileDelinquent());
      case getProfileMeRoute:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ProfilePage(null));
      case getProfileRequestsRoute:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ProfilePage(null, 0));
      case getProfilePlacesRoute:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ProfilePlacesPage());
      case getProfileNotificationsRoute:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ProfileSettingsNotificationsPage());
      case getProfileEmailNotificationsRoute:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ProfileSettingsNotificationsEmailPage());
      case getProfileAccountRoute:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ProfileSettingsAccountPage());
      case getProfileAboutRoute:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ProfileSettingsAboutPage());
      case getProfileDebugRoute:
        return MaterialPageRoute(
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => ProfileSettingsDebugPage());
      case getDealAdd:
        return MaterialPageRoute(
            fullscreenDialog: true,
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => DealAdd());
      case getDealAddDeal:
        return MaterialPageRoute(
            fullscreenDialog: true,
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) =>
                DealAddDeal(deal: settings.arguments ?? null));
      case getDealAddVirtual:
        return MaterialPageRoute(
            fullscreenDialog: true,
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => DealAddDeal(
                  deal: settings.arguments ?? null,
                  dealType: DealType.Virtual,
                ));
      case getDealAddEvent:
        return MaterialPageRoute(
            fullscreenDialog: true,
            settings: _trackAndBuildRoute(settings.name),
            builder: (context) => DealAddEvent(settings.arguments ?? null));
      default:
        {
          // split the route name which will help us identify details / ids
          // of where we're looking to navigate users to
          final List<String> pathElements = settings.name.split('/');

          if (pathElements[0] == 'profile') {
            /// profile/view/$publisherAccountId
            ///
            /// Currently, just having a route with profile id but likely will extend to add
            /// individual pages (like settings, notifications) within a users' profile
            return MaterialPageRoute(
              settings: _trackAndBuildRoute('profile/view'),
              builder: (context) => MainProfile(
                profileId: int.parse(pathElements[2]),
                deal: settings.arguments,
              ),
            );
          }

          if (pathElements[0] == 'request') {
            if (pathElements[1] == 'messages') {
              return MaterialPageRoute(
                  settings: _trackAndBuildRoute('request/messages'),
                  builder: (context) => RequestDialogPage(
                        deal: settings.arguments,
                        dealId: int.parse(pathElements[2]),
                        publisherAccountId: int.parse(pathElements[3]),
                      ));
            } else if (pathElements[1] == 'decline') {
              return MaterialPageRoute(
                  fullscreenDialog: true,
                  settings: _trackAndBuildRoute('request/decline'),
                  builder: (context) => RequestPage(
                        deal: settings.arguments,
                        dealId: int.parse(pathElements[2]),
                        publisherAccountId: int.parse(pathElements[3]),
                        sendToRequestStatus: DealRequestStatus.denied,
                      ));
            } else if (pathElements[1] == 'cancel') {
              return MaterialPageRoute(
                  fullscreenDialog: true,
                  settings: _trackAndBuildRoute('request/cancel'),
                  builder: (context) => RequestPage(
                        deal: settings.arguments,
                        dealId: int.parse(pathElements[2]),
                        publisherAccountId: int.parse(pathElements[3]),
                        sendToRequestStatus: DealRequestStatus.cancelled,
                      ));
            } else if (pathElements[1] == 'complete') {
              return MaterialPageRoute(
                  fullscreenDialog: true,
                  settings: _trackAndBuildRoute('request/complete'),
                  builder: (context) => RequestPage(
                        deal: settings.arguments,
                        dealId: int.parse(pathElements[2]),
                        publisherAccountId: int.parse(pathElements[3]),
                        sendToRequestStatus: DealRequestStatus.completed,
                      ));
            } else if (pathElements[1] == 'redeem') {
              return MaterialPageRoute(
                  fullscreenDialog: true,
                  settings: _trackAndBuildRoute('request/redeem'),
                  builder: (context) => RequestPage(
                        deal: settings.arguments,
                        dealId: int.parse(pathElements[2]),
                        publisherAccountId: int.parse(pathElements[3]),
                        sendToRequestStatus: DealRequestStatus.redeemed,
                      ));
            } else if (pathElements[1] == 'approve') {
              return MaterialPageRoute(
                  fullscreenDialog: true,
                  settings: _trackAndBuildRoute('request/approve'),
                  builder: (context) => RequestPage(
                        deal: settings.arguments,
                        dealId: int.parse(pathElements[2]),
                        publisherAccountId: int.parse(pathElements[3]),
                        sendToRequestStatus: DealRequestStatus.inProgress,
                      ));
            }

            /// we get the deal id no matter what
            final int dealId = int.parse(pathElements[2]);

            /// optionally we may have the publisher account id as another path param
            final int publisherAccountId =
                pathElements.length > 3 ? int.parse(pathElements[3]) : null;

            return MaterialPageRoute(
                settings: _trackAndBuildRoute('request/view'),
                builder: (context) => RequestPage(
                      dealId: dealId,
                      publisherAccountId: publisherAccountId,

                      /// if we have arguments then that'd be the deal
                      /// passed as arguments to the route, so we'll pass the deal along
                      deal: settings.arguments,
                    ));
          }

          if (pathElements[0] == 'deal') {
            /// dealinsights/$dealId
            /// deal/edit/$dealId
            /// deal/view/$dealLink
            /// deal/view/$dealId

            if (pathElements[1] == 'view') {
              /// attempt to parse the 3rd element in the array, and if it succeeds we're viewing
              /// deal page via id, otherwise we have a deal link
              final int dealId = int.tryParse(pathElements[2]);
              final String dealLink = dealId == null ? pathElements[2] : null;

              return MaterialPageRoute(
                  fullscreenDialog: true,
                  settings: _trackAndBuildRoute('deal/view'),
                  builder: (context) => DealPage(dealId, dealLink));
            }

            if (pathElements[1] == 'edit') {
              return MaterialPageRoute(
                  settings: _trackAndBuildRoute('deal/edit'),
                  builder: (context) =>
                      DealEditPage(int.parse(pathElements[2])));
            }

            if (pathElements[1] == 'insights') {
              return MaterialPageRoute(
                settings: _trackAndBuildRoute('deal/insights'),
                builder: (context) =>
                    DealInsightsPage(deal: settings.arguments),
              );
            }
          }
        }
    }

    return null;
  }
}

class PageTransitionNone extends MaterialPageRoute {
  PageTransitionNone({WidgetBuilder builder, RouteSettings settings})
      : super(builder: builder, settings: settings);

  @override
  Widget buildTransitions(BuildContext context, Animation<double> animation,
      Animation<double> secondaryAnimation, Widget child) {
    Animation<Offset> custom =
        Tween<Offset>(begin: Offset(0.0, 0.0), end: Offset(0.0, 0.0))
            .animate(animation);

    return SlideTransition(position: custom, child: child);
  }
}
