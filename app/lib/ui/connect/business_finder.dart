import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/ui/connect/blocs/business_finder.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class ConnectBusinessFinder extends StatefulWidget {
  ConnectBusinessFinder();

  @override
  State<StatefulWidget> createState() => _ConnectBusinessFinderState();
}

class _ConnectBusinessFinderState extends State<ConnectBusinessFinder> {
  final _scrollController = ScrollController();
  final TextEditingController _controller = TextEditingController();
  final _searchOnChange = BehaviorSubject<String>();
  final BusinessFinderBloc _bloc = BusinessFinderBloc();

  @override
  void initState() {
    super.initState();

    _controller.addListener(() => _bloc.setSearch(_controller.text));

    _searchOnChange
        .debounceTime(const Duration(milliseconds: 250))
        .listen((query) => _bloc.query(query));

    _query("");
  }

  @override
  void dispose() {
    _searchOnChange.close();
    _bloc.dispose();
    _scrollController.dispose();

    super.dispose();
  }

  void _query(String query) => _searchOnChange.sink.add(query);

  void _clearQuery() {
    _controller.clear();
    _query("");
  }

  void _linkBusiness(PublisherAccount profile) {
    showSharedModalAlert(context, Text("Are you sure?"), actions: [
      ModalAlertAction(
          label: "Cancel", onPressed: () => Navigator.of(context).pop()),
      ModalAlertAction(
          label: "Yes, Link it!",
          onPressed: () {
            Navigator.of(context).pop();

            showSharedLoadingLogo(context);

            _bloc.linkBusiness(profile.userName).then((success) {
              Navigator.of(context).pop();

              if (success) {
                Navigator.of(context).pushReplacementNamed(AppRouting.getHome);
              } else {
                showSharedModalError(
                  context,
                  title: "Unable to Link",
                  subTitle: "Please try again in a few moments",
                );
              }
            });
          }),
    ]);
  }

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      extendBodyBehindAppBar: true,
      extendBody: true,
      appBar: _buildAppBar(dark),
      body: StreamBuilder<List<PublisherAccount>>(
        stream: _bloc.resultsInsta,
        builder: (context, snapshot) => ListView.builder(
          itemCount: snapshot.data != null ? snapshot.data.length : 0,
          itemBuilder: (context, index) => _buildListItem(snapshot.data[index]),
        ),
      ),
    );
  }

  Widget _buildAppBar(bool dark) => AppBar(
        leading: AppBarCloseButton(context),
        titleSpacing: 4,
        automaticallyImplyLeading: false,
        title: Stack(
          alignment: Alignment.center,
          children: <Widget>[
            Container(
              height: 40,
              width: double.infinity,
              decoration: BoxDecoration(
                color: dark
                    ? Theme.of(context).cardColor.withOpacity(0.85)
                    : Theme.of(context).canvasColor,
                borderRadius: BorderRadius.circular(8),
              ),
            ),
            TextField(
              controller: _controller,
              onChanged: _query,
              enableSuggestions: true,
              cursorColor: Theme.of(context).textTheme.bodyText2.color,
              decoration: InputDecoration(
                  contentPadding: EdgeInsets.only(top: 0),
                  prefixIcon: Icon(AppIcons.searchReg,
                      size: 16, color: Theme.of(context).hintColor),
                  hintText: "Search Instagram profiles",
                  hintStyle: TextStyle(height: 1),
                  filled: false,
                  border: UnderlineInputBorder(
                    borderSide: BorderSide.none,
                  ),
                  suffix: StreamBuilder<String>(
                      stream: _bloc.search,
                      builder: (context, snapshot) => AnimatedOpacity(
                            duration: Duration(milliseconds: 250),
                            opacity: snapshot.data != null &&
                                    snapshot.data.isNotEmpty
                                ? 1
                                : 0,
                            child: GestureDetector(
                              onTap: _clearQuery,
                              child: Container(
                                color: Colors.transparent,
                                height: 40,
                                margin: EdgeInsets.only(right: 8),
                                child: Stack(
                                  alignment: Alignment.center,
                                  children: <Widget>[
                                    Transform.translate(
                                      offset: Offset(0, 4),
                                      child: Icon(Icons.cancel,
                                          color: Theme.of(context).hintColor,
                                          size:
                                              Theme.of(context).iconTheme.size),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          ))),
            ),
          ],
        ),
        actions: <Widget>[],
      );

  Widget _buildListItem(PublisherAccount profile) => Column(
        children: <Widget>[
          ListTile(
            leading: UserAvatar(
              profile,
              linkToIg: true,
            ),
            title: Row(
              children: <Widget>[
                Flexible(
                  fit: FlexFit.loose,
                  child: Text(
                    profile.userName,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                ),
                Visibility(
                  visible:
                      profile.isVerified != null ? profile.isVerified : false,
                  child: Padding(
                    padding: EdgeInsets.only(left: 4.0, bottom: 1.0),
                    child: Icon(
                      AppIcons.badgeCheck,
                      color: Theme.of(context).primaryColor,
                      size: 11.5,
                    ),
                  ),
                ),
              ],
            ),
            subtitle: Text(
              profile.nameDisplay,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(color: Theme.of(context).hintColor),
            ),
            trailing: Container(
              width: 90,
              child: PrimaryButton(
                context: context,
                label: "Link",
                onTap: () => _linkBusiness(profile),
                labelColor: Theme.of(context).scaffoldBackgroundColor,
                buttonColor: Theme.of(context).primaryColor,
              ),
            ),
          ),
        ],
      );
}
