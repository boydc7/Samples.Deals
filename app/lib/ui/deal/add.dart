import 'package:flutter/material.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/ui/deal/blocs/add.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class DealAdd extends StatefulWidget {
  @override
  _DealAddState createState() => _DealAddState();
}

class _DealAddState extends State<DealAdd> {
  final AddBloc _bloc = AddBloc();

  void initState() {
    super.initState();

    _bloc.loadDrafts(true);
  }

  void dispose() {
    super.dispose();

    _bloc.dispose();
  }

  void _addDeal() => Navigator.of(context).pushNamed(AppRouting.getDealAddDeal);
  void _addVirtual() =>
      Navigator.of(context).pushNamed(AppRouting.getDealAddVirtual);
  void _addEvent() =>
      Navigator.of(context).pushNamed(AppRouting.getDealAddEvent);

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: AppBarCloseButton(context),
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
        elevation: 0,
      ),
      body: Column(
        children: <Widget>[
          FlatButton(child: Text("Add In-Person"), onPressed: _addDeal),
          FlatButton(child: Text("Add Virtual"), onPressed: _addVirtual),
          appState.isBusinessPro
              ? FlatButton(child: Text("Add Event"), onPressed: _addEvent)
              : Container(),

          /// and pre-set it to filter by 'draft' (NOTE i added a 'draft' filter to the main list)
        ],
      ),
    );
  }
}
