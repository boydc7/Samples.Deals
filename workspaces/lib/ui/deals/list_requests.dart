import 'package:flutter/material.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/list_page_arguments.dart';
import 'package:rydrworkspaces/models/responses/deals.dart';
import 'package:rydrworkspaces/ui/deals/blocs/list_requests.dart';
import 'package:rydrworkspaces/ui/deals/request.dart';

class RequestsList extends StatefulWidget {
  @override
  _RequestsListState createState() => _RequestsListState();
}

class _RequestsListState extends State<RequestsList>
    with AutomaticKeepAliveClientMixin {
  final _scrollController = ScrollController();
  final ListBloc _bloc = ListBloc();

  ThemeData _theme;

  ListPageArguments args = ListPageArguments();

  @override
  bool wantKeepAlive = true;

  @override
  void initState() {
    super.initState();

    _scrollController.addListener(_onScroll);
    _bloc.loadList(args);
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.offset >=
            _scrollController.position.maxScrollExtent &&
        _bloc.hasMore &&
        !_bloc.isLoading) {
      _bloc.loadList(args);
    }
  }

  void _filterStatus(DealRequestStatus status) {
    args.filterRequestStatus = status == null ? null : [status];

    _bloc.loadList(args, true);
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);

    _theme = Theme.of(context);

    return Container(
      child: Column(
        children: <Widget>[
          _buildToolbar(),
          _buildListHeader(),
          Expanded(
            child: StreamBuilder<DealsResponse>(
                stream: _bloc.dealsResponse,
                builder: (context, snapshot) => snapshot.data == null
                    ? _buildLoading()
                    : snapshot.data != null && snapshot.data.error == null
                        ? snapshot.data.deals.isNotEmpty
                            ? _buildList(snapshot.data)
                            : _buildNoResults()
                        : _buildError(snapshot.data)),
          ),
        ],
      ),
    );
  }

  Widget _buildToolbar() => Container(
        height: kToolbarHeight,
        padding: EdgeInsets.only(left: 32, right: 32),
        child: StreamBuilder<ListPageArguments>(
            stream: _bloc.filterArgs,
            builder: (context, snapshot) {
              return Row(
                children: <Widget>[
                  DropdownButton(
                      value: args.filterRequestStatus != null
                          ? args.filterRequestStatus[0]
                          : null,
                      items: [
                        DropdownMenuItem(
                          child: Text("All"),
                          value: null,
                        ),
                        DropdownMenuItem(
                          child: Text("Pending"),
                          value: DealRequestStatus.requested,
                        ),
                        DropdownMenuItem(
                          child: Text("Invited"),
                          value: DealRequestStatus.invited,
                        ),
                        DropdownMenuItem(
                          child: Text("In Progress"),
                          value: DealRequestStatus.inProgress,
                        ),
                        DropdownMenuItem(
                          child: Text("Redeemed"),
                          value: DealRequestStatus.redeemed,
                        ),
                        DropdownMenuItem(
                          child: Text("Completed"),
                          value: DealRequestStatus.completed,
                        ),
                        DropdownMenuItem(
                          child: Text("Cancelled"),
                          value: DealRequestStatus.cancelled,
                        ),
                        DropdownMenuItem(
                          child: Text("Declined"),
                          value: DealRequestStatus.denied,
                        ),
                        DropdownMenuItem(
                          child: Text("Delinquent"),
                          value: DealRequestStatus.delinquent,
                        ),
                      ],
                      onChanged: _filterStatus),
                  Expanded(child: Text("test"))
                ],
              );
            }),
      );

  Widget _buildLoading() => Text("Loading");

  Widget _buildNoResults() => Text("No results");

  Widget _buildError(DealsResponse res) => Text(res.error.message);

  Widget _buildListHeader() => Container(
        height: kToolbarHeight,
        child: Row(
          children: <Widget>[
            Container(width: 100, child: Text("Requested By")),
            Expanded(child: Text("RYDR")),
            Container(
              width: 100,
              child: Text("Status"),
            ),
          ],
        ),
      );

  Widget _buildList(DealsResponse res) => RefreshIndicator(
        displacement: 0.0,
        backgroundColor: _theme.appBarTheme.color,
        color: _theme.textTheme.bodyText1.color,
        onRefresh: () => _bloc.loadList(args, true),
        child: ListView.builder(
            controller: _scrollController,
            padding: EdgeInsets.only(
                bottom: kToolbarHeight + MediaQuery.of(context).padding.bottom),
            itemCount: res.deals.length,
            itemBuilder: (BuildContext context, int index) =>
                _buildRow(res.deals[index])),
      );

  Widget _buildRow(Deal deal) {
    return GestureDetector(
        onTap: () => Navigator.push(
            context,
            MaterialPageRoute(
                fullscreenDialog: true,
                builder: (context) => Request(deal: deal))),
        child: Container(
            decoration: BoxDecoration(
                border: Border(
                    bottom: BorderSide(
              color: Colors.grey.withOpacity(0.2),
            ))),
            child: Row(
              children: <Widget>[
                Container(
                  width: 100,
                  height: 60,
                  decoration: BoxDecoration(
                      image: DecorationImage(
                          fit: BoxFit.cover,
                          image: NetworkImage(
                              deal.request.publisherAccount.profilePicture))),
                ),
                Expanded(
                    child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[Text(deal.title), Text(deal.description)],
                )),
                Container(
                  width: 100,
                  child: Text(
                      dealRequestStatusToStringDisplay(deal.request.status)),
                )
              ],
            )));
  }
}
