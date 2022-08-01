import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/dialog_message.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/notification.dart';
import 'package:rydr_app/ui/deal/blocs/request_dialog.dart';
import 'package:rydr_app/ui/deal/widgets/request/dialog_listitem.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class RequestDialogPage extends StatefulWidget {
  final Deal deal;
  final int dealId;
  final int publisherAccountId;
  final bool autofocus;

  RequestDialogPage({
    this.deal,
    this.dealId,
    this.publisherAccountId,
    this.autofocus = false,
  });

  @override
  State<StatefulWidget> createState() {
    return _RequestDialogPageState();
  }
}

class _RequestDialogPageState extends State<RequestDialogPage> {
  RequestDialogBloc _bloc;
  StreamSubscription _subNotifications;

  final TextEditingController _textEditingController = TextEditingController();
  final ScrollController _listScrollController = ScrollController();

  @override
  void initState() {
    super.initState();

    _bloc = RequestDialogBloc();
    _bloc.loadMessages(
      widget.deal,
      dealId: widget.dealId,
      publisherAccountId: widget.publisherAccountId,
    );

    _subNotifications = appState.messageStream.stream.listen((data) {
      _processMessage(data);
    });

    _listScrollController.addListener(_handleScrollChanged);
  }

  @override
  void dispose() {
    _subNotifications?.cancel();
    _bloc.dispose();

    _listScrollController.dispose();

    super.dispose();
  }

  void _sendMessage() async {
    String message = _textEditingController.text.trim();

    /// obviously only send a message if we have any input from the user at all
    if (message == "") {
      return;
    }

    _textEditingController.clear();

    _bloc.sendMessage(message);
  }

  void _handleScrollChanged() {
    if (_listScrollController.position.pixels ==
        _listScrollController.position.maxScrollExtent) {
      _bloc.loadPrevious();
    }
  }

  void _processMessage(AppNotification message) {
    _bloc.processMessage(message);
  }

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return StreamBuilder<Deal>(
      stream: _bloc.deal,
      builder: (context, snapshot) {
        return snapshot.connectionState == ConnectionState.waiting ||
                snapshot.data == null
            ? _buildLoadingBody(dark)
            : _buildSuccessBody(dark, snapshot.data);
      },
    );
  }

  Widget _buildLoadingBody(bool dark) => Scaffold(
        appBar: AppBar(
          title: Text("Loading messages"),
          backgroundColor: dark
              ? Theme.of(context).scaffoldBackgroundColor
              : AppColors.white,
          leading: AppBarBackButton(context),
        ),
        backgroundColor: dark
            ? Theme.of(context).scaffoldBackgroundColor
            : AppColors.white50,
        body: ListView(
          padding: EdgeInsets.all(16),
          children: <Widget>[
            LoadingListShimmer(),
          ],
        ),
      );

  Widget _buildSuccessBody(bool dark, Deal deal) {
    return deal.request.lastMessage == null
        ? Scaffold(
            appBar: _buildAppBar(dark, deal),
            body: Container(
              width: double.infinity,
              child: Column(
                children: [
                  Expanded(
                    child: _buildFirstMessageSplash(deal),
                  ),
                  _buildInput(deal),
                ],
              ),
            ),
          )
        : Scaffold(
            appBar: _buildAppBar(dark, deal),
            body: Container(
              width: double.infinity,
              child: Column(
                children: [
                  Expanded(
                      child: StreamBuilder<List<DialogMessage>>(
                    stream: _bloc.messages,
                    builder: (context, snapshot) {
                      List<DialogMessage> messages = snapshot.data ?? [];

                      return ListView.builder(
                        padding: EdgeInsets.only(bottom: 16),
                        physics: AlwaysScrollableScrollPhysics(),
                        reverse: true,
                        shrinkWrap: true,
                        itemBuilder: (BuildContext context, int index) =>
                            DialogListItem(
                          deal: deal,
                          lastMessage: index + 1 < messages.length
                              ? messages[index + 1]
                              : null,
                          message: messages[index],
                        ),
                        itemCount: messages.length,
                        controller: _listScrollController,
                      );
                    },
                  )),
                  _buildInput(deal)
                ],
              ),
            ),
          );
  }

  Widget _buildFirstMessageSplash(Deal deal) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Icon(
          AppIcons.commentAltLines,
          size: 40.0,
          color: Theme.of(context).textTheme.bodyText2.color,
        ),
        SizedBox(
          height: 20.0,
        ),
        Text('Direct Message', style: Theme.of(context).textTheme.headline5),
        SizedBox(
          height: 8.0,
        ),
        RichText(
          text: TextSpan(
            style: Theme.of(context)
                .textTheme
                .bodyText2
                .merge(TextStyle(color: Theme.of(context).hintColor)),
            children: <TextSpan>[
              TextSpan(text: 'Send a message to '),
              TextSpan(
                  text: '${deal.request.publisherAccount.userName}',
                  style: TextStyle(fontWeight: FontWeight.w600)),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildInput(Deal deal) {
    return SafeArea(
      bottom: true,
      child: Container(
        child: deal.request.canSendMessages
            ? Row(
                children: <Widget>[
                  Expanded(
                    child: Container(
                      margin: EdgeInsets.only(
                        left: 16,
                        right: 8,
                        top: 8.0,
                        bottom: 8.0,
                      ),
                      child: TextField(
                        autofocus: widget.autofocus,
                        textInputAction: TextInputAction.send,
                        keyboardAppearance: Theme.of(context).brightness,
                        textCapitalization: TextCapitalization.sentences,
                        onSubmitted: (value) {
                          _sendMessage();
                        },
                        maxLines: null,
                        controller: _textEditingController,
                        decoration: InputDecoration.collapsed(
                          hintText: 'Type your message...',
                        ),
                      ),
                    ),
                  ),
                  TextButton(
                    label: 'Send',
                    color: Theme.of(context).primaryColor,
                    onTap: () {
                      _sendMessage();
                    },
                  )
                ],
              )
            : Container(
                alignment: Alignment.center,
                height: 42.0,
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Text('Message History'),
                    Text(
                      'Messages can be sent up to 24h after a RYDR is completed',
                      style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                    ),
                  ],
                ),
              ),
        width: double.infinity,
        decoration: BoxDecoration(
          border: Border(
            top: BorderSide(color: Theme.of(context).dividerColor),
          ),
        ),
      ),
    );
  }

  /// AppBar for when we've successfully loaded/rendered the dialog messages
  Widget _buildAppBar(bool dark, Deal deal) {
    final PublisherAccount chatPartner = appState.currentProfile.isBusiness
        ? deal.request.publisherAccount
        : deal.publisherAccount;

    return AppBar(
      leading: AppBarBackButton(context),
      title: GestureDetector(
        onTap: () {
          Navigator.of(context).pushNamed(
            AppRouting.getProfileRoute(chatPartner.id),
            arguments: appState.currentProfile.isBusiness ? deal : null,
          );
        },
        child: Container(
          width: double.infinity,
          child: Row(
            children: <Widget>[
              UserAvatar(
                chatPartner,
                width: 32.0,
              ),
              SizedBox(width: 12.0),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Text(chatPartner.userName),
                    Text(deal.title,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.caption),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
      centerTitle: true,

      /// if we were passed a deal from the previous page
      /// then the back button would take us back to the deal request
      /// if not, then we can add an action button that would load the deal request
      actions: widget.deal == null
          ? <Widget>[
              Container(
                width: 60.0,
                child: IconButton(
                  onPressed: () {
                    Navigator.of(context).pushNamed(AppRouting.getRequestRoute(
                      deal.id,
                      deal.request.publisherAccount.id,
                    ));
                  },
                  icon: Icon(AppIcons.megaphone),
                ),
              )
            ]
          : [],
    );
  }
}
