import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/ui/deal/blocs/use_recent.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class UseRecent extends StatelessWidget {
  final _bloc = UseRecentBloc();

  final TextEditingController controller;
  final String title;
  final String fieldToUse;

  UseRecent({
    @required this.controller,
    @required this.title,
    @required this.fieldToUse,
  });

  void _showHistory(BuildContext context) async {
    /// header widget that displays while loading and after
    final Widget header = Container(
      width: double.infinity,
      padding: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(title, style: Theme.of(context).textTheme.headline6),
          SizedBox(height: 4),
          Text(
            "Tap to select",
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(color: Theme.of(context).hintColor),
                ),
          ),
          SizedBox(height: 8),
        ],
      ),
    );

    /// loading state can apply to future builder and stream builder
    /// so we'll use the same one either way, hence define it here
    final Widget loading =
        Padding(child: LoadingListShimmer(), padding: EdgeInsets.all(16));

    final Widget noResults = Container(
        padding: EdgeInsets.all(16),
        child: Text("You don't have any ${title.toLowerCase()}"));

    showSharedModalBottomInfo(
      context,
      child: FutureBuilder(
        future: _bloc.loadRecentDeals(),
        builder: (context, snapshot) =>
            snapshot.connectionState == ConnectionState.waiting
                ? loading
                : StreamBuilder<List<Deal>>(
                    stream: _bloc.recentDeals,
                    builder: (context, snapshot) => snapshot.data != null &&
                            snapshot.data.isNotEmpty &&
                            _bloc.recentDealsContent(fieldToUse).isNotEmpty
                        ? Column(
                            children: <Widget>[
                              header,
                              Divider(height: 1),
                              Column(
                                children: _bloc
                                    .recentDealsContent(fieldToUse)
                                    .map((String value) => Column(
                                          children: <Widget>[
                                            InkWell(
                                              onTap: () {
                                                _useValue(controller, value);
                                                Navigator.of(context).pop();
                                              },
                                              child: Container(
                                                width: double.infinity,
                                                color: Theme.of(context)
                                                    .appBarTheme
                                                    .color,
                                                padding: EdgeInsets.all(16),
                                                child: Text(value),
                                              ),
                                            ),
                                            Divider(height: 1),
                                          ],
                                        ))
                                    .toList(),
                              )
                            ],
                          )
                        : noResults,
                  ),
      ),
      initialRatio: 0.4,
    );
  }

  void _useLast(BuildContext context) async {
    String value;
    await _bloc.loadRecentDeals();

    if (_bloc.recentDeals.value != null && _bloc.recentDeals.value.isNotEmpty) {
      final Deal lastDeal = _bloc.recentDeals.value[0];

      value = fieldToUse == 'receiveNotes'
          ? lastDeal.receiveNotes
          : fieldToUse == 'approvalNotes'
              ? lastDeal.approvalNotes
              : lastDeal.description;
    }

    /// might not have any deals yet, or last deal might not have any value for the given field,
    /// as only 'description' is required, so receiveNotes or approvalNotes could be null
    if (value == null || value.trim().isEmpty) {
      Scaffold.of(context).showSnackBar(SnackBar(
        content: Text("No recent data to paste"),
      ));
    } else {
      _useValue(controller, value);
    }
  }

  void _useValue(TextEditingController controller, String value) =>
      controller.text = value;

  @override
  Widget build(BuildContext context) => Row(
        children: <Widget>[
          IconButton(
              icon: Icon(AppIcons.bolt), onPressed: () => _useLast(context)),
          IconButton(
            icon: Icon(AppIcons.history),
            onPressed: () => _showHistory(context),
          ),
        ],
      );
}
