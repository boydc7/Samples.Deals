import 'package:rydrworkspaces/models/enums/deal.dart';

class ListPageArguments {
  int filterDealId;
  String filterDealName;
  int filterDealPublisherAccountId;
  int filterDealRequestPublisherAccountId;
  String filterDealPublisherAccountName;
  String filterDealRequestPublisherAccountName;
  List<DealStatus> filterDealStatus;
  List<DealRequestStatus> filterRequestStatus;
  ListPageLayout layoutType;

  DealSort sortDeals;

  bool isRequests;
  bool isCreatorHistory;

  /// this represents the index of the tabs if we're
  /// loading the list within the confines of the main scaffold
  /// we will set this from within the routing switch/case to then
  /// instruct the main page which tab to swith to when loading
  ///
  /// defaults to 0 which would be the first tab (e.g. the home tab)
  int mainScaffoldTab = 0;

  ListPageArguments({
    this.isRequests = true,
    this.layoutType = ListPageLayout.StandAlone,
    this.isCreatorHistory = false,
    this.filterDealId,
    this.filterDealName,
    this.filterDealPublisherAccountId,
    this.filterDealPublisherAccountName,
    this.filterDealRequestPublisherAccountId,
    this.filterDealRequestPublisherAccountName,
    this.filterDealStatus,
    this.filterRequestStatus,
    this.sortDeals,
  });
}

enum ListPageLayout { StandAlone, Integrated, Injected }
