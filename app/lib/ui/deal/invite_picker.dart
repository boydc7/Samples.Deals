import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';

import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/ui/deal/blocs/invite_picker.dart';
import 'package:rydr_app/ui/deal/widgets/shared/constants.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/app/theme.dart';

class InvitePickerPage extends StatefulWidget {
  final int dealId;
  final List<PublisherAccount> existingInvites;
  final Function handleInvite;

  InvitePickerPage(
    this.existingInvites, {
    this.dealId,
    this.handleInvite,
  });

  @override
  State<StatefulWidget> createState() => _InvitePickerPageState();
}

class _InvitePickerPageState extends State<InvitePickerPage>
    with AutomaticKeepAliveClientMixin {
  final _scrollController = ScrollController();
  final _scrollControllerSelected = ScrollController();
  final TextEditingController _controller = TextEditingController();
  final _searchOnChange = BehaviorSubject<String>();
  InvitePickerBloc _bloc;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();

    _scrollController.addListener(_onScroll);

    _bloc = InvitePickerBloc(
      existingUsers: widget.existingInvites,
      dealId: widget.dealId,
    );

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

  void _onScroll() {
    if (_scrollController.offset >=
            _scrollController.position.maxScrollExtent &&
        _bloc.hasMore &&
        !_bloc.isLoading) {
      _bloc.queryMore();
    }
  }

  bool _addUser(BuildContext context, PublisherAccount user, bool add) {
    if (add && _bloc.maxInvitesReached) {
      Scaffold.of(context).showSnackBar(SnackBar(
        content: Text("$dealMaxInvites invites is the maximum allowed"),
      ));

      return false;
    } else {
      if (add) {
        _bloc.addInvite(user);

        Future.delayed(
            Duration(milliseconds: 250),
            () => _scrollControllerSelected.animateTo(
                _scrollControllerSelected.position.maxScrollExtent,
                duration: Duration(milliseconds: 250),
                curve: Curves.easeOut));
      } else {
        _bloc.removeInvite(user);
      }

      if (widget.handleInvite != null) {
        widget.handleInvite(_bloc.pendingInvites.value);
      }

      return true;
    }
  }

  void _query(String query) => _searchOnChange.sink.add(query);

  void _clearQuery() {
    _controller.clear();
    _query("");
  }

  void _handleInvite() => Navigator.pop(context, _bloc.invitedUsers);

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return DefaultTabController(
      length: 2,
      child: Scaffold(
        extendBodyBehindAppBar: true,
        extendBody: true,
        appBar: _buildAppBar(dark),
        body: TabBarView(
          children: <Widget>[
            StreamBuilder<List<PublisherAccount>>(
              stream: _bloc.pendingInvites,
              builder: (context, snapshot) => ListRydr(
                scrollController: _scrollController,
                bloc: _bloc,
                existingInvites: snapshot.data ?? [],
                onTap: _addUser,
                onRefresh: _bloc.refresh,
              ),
            ),
            StreamBuilder<List<PublisherAccount>>(
              stream: _bloc.pendingInvites,
              builder: (context, snapshot) => ListInsta(
                _bloc,
                snapshot.data ?? [],
                _addUser,
              ),
            ),
          ],
        ),
        bottomNavigationBar: SizedBox(
          height: 94.0,
          child: _buildInvites(dark),
        ),
      ),
    );
  }

  Widget _buildAppBar(bool dark) => AppBar(
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
                  hintText: "Search",
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

        /// actions are only applicable to when
        /// we don't have a function passed to handle invites

        actions: widget.handleInvite == null
            ? <Widget>[
                StreamBuilder<List<PublisherAccount>>(
                  stream: _bloc.pendingInvites,
                  builder: (context, snapshot) {
                    bool hasInvites =
                        snapshot.data != null && snapshot.data.length > 0
                            ? true
                            : false;
                    return GestureDetector(
                      onTap: _handleInvite,
                      child: Padding(
                        padding:
                            EdgeInsets.only(top: 19.0, right: 16.0, left: 16),
                        child: Text(
                          hasInvites ? "Continue" : "Cancel",
                          style: TextStyle(
                              color: hasInvites
                                  ? AppColors.blue
                                  : Theme.of(context).textTheme.bodyText1.color,
                              fontWeight: FontWeight.w600,
                              fontSize: 16.0),
                        ),
                      ),
                    );
                  },
                ),
              ]
            : [Container()],
        bottom: TabBar(
          labelColor: Theme.of(context).tabBarTheme.labelColor,
          unselectedLabelColor:
              Theme.of(context).tabBarTheme.unselectedLabelColor,
          indicatorWeight: 1.4,
          indicatorColor: dark
              ? AppColors.white.withOpacity(0.87)
              : Theme.of(context).textTheme.bodyText2.color,
          tabs: <Widget>[
            Tab(text: "RYDR Creators"),
            Tab(text: "Instagram"),
          ],
        ),
      );

  Widget _buildInvites(bool dark) => StreamBuilder<List<PublisherAccount>>(
        stream: _bloc.pendingInvites,
        builder: (context, snapshot) {
          List<PublisherAccount> existing = snapshot.data ?? [];
          List<Widget> users = [];

          for (int x = 0; x < existing.length; x++) {
            final PublisherAccount user = existing[x];

            users.add(
              FadeInOpacityOnly(
                /// This pauses the first invitee to allow for the bottom bar to slide up,
                /// then there is no delay for all other invitees
                x == 0 ? 10 : 0,
                Container(
                  margin: EdgeInsets.only(left: 8.0),
                  child: Chip(
                    avatar: UserAvatar(
                      user,
                      hideBorder: true,
                    ),
                    shape: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(32),
                      borderSide: BorderSide(
                        color: user.isFromInstagram
                            ? Theme.of(context).primaryColor
                            : Utils.getRequestStatusColor(
                                DealRequestStatus.invited, dark),
                      ),
                    ),
                    backgroundColor: user.isFromInstagram
                        ? dark
                            ? Theme.of(context).primaryColor.withOpacity(0.1)
                            : Theme.of(context).appBarTheme.color
                        : dark
                            ? Utils.getRequestStatusColor(
                                    DealRequestStatus.invited, dark)
                                .withOpacity(0.1)
                            : Theme.of(context).appBarTheme.color,
                    padding: EdgeInsets.all(0),
                    label: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        Flexible(
                          fit: FlexFit.loose,
                          child: Text(
                            user.userName,
                            overflow: TextOverflow.ellipsis,
                            style: Theme.of(context).textTheme.bodyText1,
                          ),
                        ),
                        Visibility(
                          visible:
                              user.isVerified != null ? user.isVerified : false,
                          child: Padding(
                            padding: EdgeInsets.only(left: 4.0),
                            child: Icon(
                              AppIcons.badgeCheck,
                              color: Theme.of(context).primaryColor,
                              size: 12,
                            ),
                          ),
                        ),
                      ],
                    ),
                    onDeleted: () => _addUser(context, user, false),
                    deleteIconColor: user.isFromInstagram
                        ? Theme.of(context).primaryColor
                        : Utils.getRequestStatusColor(
                            DealRequestStatus.invited, dark),
                    elevation: 0,
                  ),
                ),
              ),
            );
          }

          final Widget bottomBar = Container(
            padding: EdgeInsets.symmetric(vertical: 8.0),
            color: Theme.of(context).appBarTheme.color,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Padding(
                  padding: EdgeInsets.only(left: 8.0),
                  child: Text(
                    "${_bloc.inviteCount} creators selected",
                    style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(color: Theme.of(context).hintColor),
                        ),
                  ),
                ),
                Expanded(
                  child: ListView(
                    controller: _scrollControllerSelected,
                    physics: AlwaysScrollableScrollPhysics(),
                    scrollDirection: Axis.horizontal,
                    padding: EdgeInsets.only(top: 0, bottom: 12.0, right: 8.0),
                    children: users,
                  ),
                )
              ],
            ),
          );

          return users.length == 0
              ? FadeOutOpacityOnly(0, bottomBar)
              : FadeInBottomTop(0, bottomBar, 350);
        },
      );
}

class ListRydr extends StatefulWidget {
  final ScrollController scrollController;
  final InvitePickerBloc bloc;
  final List<PublisherAccount> existingInvites;
  final Function onTap;
  final Function onRefresh;

  ListRydr({
    this.scrollController,
    this.bloc,
    this.existingInvites,
    this.onTap,
    this.onRefresh,
  });

  @override
  _ListRydrState createState() => _ListRydrState();
}

class _ListRydrState extends State<ListRydr>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);

    return StreamBuilder<List<PublisherAccount>>(
      stream: widget.bloc.resultsRydr,
      builder: (context, snapshot) {
        if (widget.bloc.isLoading) {
          return ListView(
            padding: EdgeInsets.only(
              top: 32,
              left: 16,
              right: 16,
            ),
            children: <Widget>[LoadingListShimmer()],
          );
        }

        /// show a message vs. a list if there are no additional creators
        /// available to invite
        if (snapshot.data == null || snapshot.data.length == 0) {
          return ListView(
            padding: EdgeInsets.only(
              top: MediaQuery.of(context).size.height / 2,
              left: 16,
              right: 16,
            ),
            children: <Widget>[
              Text(
                "No Creators",
                style: Theme.of(context).textTheme.headline6,
                textAlign: TextAlign.center,
              ),
              SizedBox(
                height: 8.0,
              ),
              Text(
                "There are no  creators available to invite",
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodyText2.merge(
                      TextStyle(color: AppColors.grey300),
                    ),
              ),
            ],
          );
        }

        return RefreshIndicator(
          displacement: 0.0,
          backgroundColor: Theme.of(context).appBarTheme.color,
          color: Theme.of(context).textTheme.bodyText2.color,
          onRefresh: widget.onRefresh,
          child: ListView.builder(
            controller: widget.scrollController,
            itemCount: snapshot.data.length,
            itemBuilder: (context, index) => InvitePickerListItem(
              snapshot.data[index],
              widget.existingInvites,
              widget.onTap,
            ),
          ),
        );
      },
    );
  }
}

class ListInsta extends StatefulWidget {
  final InvitePickerBloc bloc;
  final List<PublisherAccount> existingInvites;
  final Function onTap;

  ListInsta(
    this.bloc,
    this.existingInvites,
    this.onTap,
  );

  @override
  _ListInstaState createState() => _ListInstaState();
}

class _ListInstaState extends State<ListInsta>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);

    return StreamBuilder<List<PublisherAccount>>(
      stream: widget.bloc.resultsInsta,
      builder: (context, snapshot) => ListView.builder(
        itemCount: snapshot.data != null ? snapshot.data.length : 0,
        itemBuilder: (context, index) => InvitePickerListItem(
            snapshot.data[index], widget.existingInvites, widget.onTap),
      ),
    );
  }
}

class InvitePickerListItem extends StatefulWidget {
  final PublisherAccount user;
  final List<PublisherAccount> existingInvites;
  final Function onTap;

  InvitePickerListItem(
    this.user,
    this.existingInvites,
    this.onTap,
  );

  @override
  _InvitePickerListItemState createState() => _InvitePickerListItemState();
}

class _InvitePickerListItemState extends State<InvitePickerListItem> {
  bool selected;

  @override
  void initState() {
    super.initState();
  }

  void _onTap(BuildContext context) {
    if (widget.onTap(context, widget.user, !selected)) {
      setState(() => selected = !selected);
    }
  }

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    selected = widget.existingInvites.indexWhere((PublisherAccount existing) =>
            existing.userName == widget.user.userName) >
        -1;

    return Column(
      children: <Widget>[
        ListTile(
          leading: UserAvatar(
            widget.user,
            linkToIg: true,
          ),
          title: Row(
            children: <Widget>[
              Flexible(
                fit: FlexFit.loose,
                child: Text(
                  widget.user.userName,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodyText1,
                ),
              ),
              Visibility(
                visible: widget.user.isVerified != null
                    ? widget.user.isVerified
                    : false,
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
            widget.user.nameDisplay,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(color: Theme.of(context).hintColor),
          ),
          trailing: Container(
            width: 90,
            child: selected
                ? SecondaryButton(
                    context: context,
                    buttonColor: widget.user.isFromInstagram
                        ? Theme.of(context).primaryColor
                        : Utils.getRequestStatusColor(
                            DealRequestStatus.invited, dark),
                    label: "Remove",
                    onTap: () => _onTap(context),
                  )
                : PrimaryButton(
                    context: context,
                    label: widget.user.isFromInstagram ? "Add" : "Invite",
                    onTap: () => _onTap(context),
                    labelColor: Theme.of(context).scaffoldBackgroundColor,
                    buttonColor: widget.user.isFromInstagram
                        ? Theme.of(context).primaryColor
                        : Utils.getRequestStatusColor(
                            DealRequestStatus.invited, dark),
                  ),
          ),
        ),
      ],
    );
  }
}
