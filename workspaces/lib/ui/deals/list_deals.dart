import 'package:flutter/material.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/list_page_arguments.dart';
import 'package:rydrworkspaces/models/responses/deals.dart';
import 'package:rydrworkspaces/ui/deals/blocs/list_deals.dart';

class DealsList extends StatefulWidget {
  @override
  _DealsListState createState() => _DealsListState();
}

class _DealsListState extends State<DealsList> {
  final ListBloc _bloc = ListBloc();

  ThemeData _theme;

  ListPageArguments args = ListPageArguments();

  @override
  void initState() {
    super.initState();

    _bloc.loadList(args);
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);

    return Container(
      child: Column(
        children: <Widget>[
          Text("here's the deals bar"),
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

  Widget _buildLoading() => Text("Loading");

  Widget _buildNoResults() => Text("No results");

  Widget _buildError(DealsResponse res) => Text("Error");

  Widget _buildListHeader() => Container(
        height: kToolbarHeight,
        child: Row(
          children: <Widget>[
            Container(width: 100),
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
            padding: EdgeInsets.only(
                bottom: kToolbarHeight + MediaQuery.of(context).padding.bottom),
            physics: AlwaysScrollableScrollPhysics(),
            itemCount: res.deals.length,
            itemBuilder: (BuildContext context, int index) {
              return _buildRow(res.deals[index]);
            }),
      );

  Widget _buildRow(Deal deal) {
    return Container(
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
                      image: NetworkImage(deal.publisherMedias[0].previewUrl))),
            ),
            Expanded(
                child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[Text(deal.title), Text(deal.description)],
            )),
            Container(
              width: 100,
              child: Text(dealStatusToStringDisplay(deal.status)),
            )
          ],
        ));
  }
}
