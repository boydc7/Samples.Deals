import 'package:flutter/material.dart';
import 'package:rydrworkspaces/services/navigation.dart';
import 'package:rydrworkspaces/ui/deals/list_deals.dart';
import 'package:rydrworkspaces/ui/deals/list_requests.dart';
import 'package:rydrworkspaces/ui/insights/home.dart';
import 'package:rydrworkspaces/ui/main_bloc.dart';

class MainPage extends StatefulWidget {
  MainPage({Key key}) : super(key: key);

  @override
  _MainPageState createState() => _MainPageState();
}

class _MainPageState extends State<MainPage>
    with SingleTickerProviderStateMixin {
  final MainBloc _bloc = MainBloc();
  TabController _tabController;

  @override
  void initState() {
    super.initState();

    _tabController = TabController(
      length: 3,
      vsync: this,
      initialIndex: 2,
    );

    _tabController.addListener(() {
      if (!_tabController.indexIsChanging) {
        _bloc.setTab(_tabController.index);
      }
    });
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _goToTab(int index) => _tabController.animateTo(index);

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      initialIndex: 0,
      length: 3,
      child: Scaffold(
        appBar: AppBar(
          automaticallyImplyLeading: false,
          backgroundColor: Theme.of(context).scaffoldBackgroundColor,
          elevation: 0,
          title: StreamBuilder<int>(
            stream: _bloc.tabIndex,
            builder: (context, snapshot) {
              final tab = snapshot.data ?? 0;

              return Row(
                children: <Widget>[
                  FlatButton(
                    onPressed: () => _goToTab(0),
                    child: Text("Insights",
                        style: TextStyle(
                            fontWeight: tab == 0
                                ? FontWeight.bold
                                : FontWeight.normal)),
                  ),
                  FlatButton(
                    onPressed: () => _goToTab(1),
                    child: Text("Marketplace",
                        style: TextStyle(
                            fontWeight: tab == 1
                                ? FontWeight.bold
                                : FontWeight.normal)),
                  ),
                  FlatButton(
                    onPressed: () => _goToTab(2),
                    child: Text("RYDRs",
                        style: TextStyle(
                            fontWeight: tab == 2
                                ? FontWeight.bold
                                : FontWeight.normal)),
                  ),
                  FlatButton(
                    onPressed: () => NavigationService.instance
                        .navigateTo('/xrequest', queryParams: {
                      'id': 'Rv8CTiC2j8QZ5lGI7A-XUMZpSe9pGLs-ZiB2iudOshk~'
                    }),
                    child: Text("External",
                        style: TextStyle(
                            fontWeight: tab == 2
                                ? FontWeight.bold
                                : FontWeight.normal)),
                  ),
                ],
              );
            },
          ),
          actions: <Widget>[
            IconButton(
              icon: Icon(
                Icons.person,
                color: Theme.of(context).accentColor,
              ),
              onPressed: () => null,
            )
          ],
        ),
        body: Container(
          decoration: BoxDecoration(
              border:
                  Border(top: BorderSide(color: Colors.grey.withOpacity(0.2)))),
          child: TabBarView(
            controller: _tabController,
            children: <Widget>[
              Home(),
              DealsList(),
              RequestsList(),
            ],
          ),
        ),
      ),
    );
  }
}
