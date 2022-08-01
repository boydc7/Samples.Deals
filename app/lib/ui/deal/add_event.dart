import 'package:flutter/material.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/ui/deal/blocs/add_event.dart';
import 'package:rydr_app/ui/deal/invite_picker.dart';
import 'package:rydr_app/ui/deal/widgets/add/event_details.dart';
import 'package:rydr_app/ui/deal/widgets/add/event_done.dart';
import 'package:rydr_app/ui/deal/widgets/add/event_media.dart';
import 'package:rydr_app/ui/deal/widgets/add/event_post_requirements.dart';
import 'package:rydr_app/ui/deal/widgets/add/event_preview.dart';
import 'package:rydr_app/ui/deal/widgets/add/event_promote_start.dart';
import 'package:rydr_app/ui/deal/widgets/add/event_promote_with_posts.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class DealAddEvent extends StatefulWidget {
  final Deal deal;

  DealAddEvent(this.deal);

  @override
  _DealAddEventState createState() => _DealAddEventState();
}

class _DealAddEventState extends State<DealAddEvent> {
  AddEventBloc _bloc;

  /// these will be configured using _generateWidgets, which is based on
  /// what page/step of the event add wizard we're currently on
  Widget _appBarTitle;
  Widget _appBarLeading;
  Widget _appBarTrailing;
  Widget _body;

  @override
  void initState() {
    _bloc = AddEventBloc(widget.deal);

    super.initState();
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _goToPage(EventPage page) => _bloc.setPage(page);

  /// configure appbar and body widget to use depending
  /// on what page we're currently on in the event wizard
  void _generateWidgets(EventPage page) {
    if (page == EventPage.Details) {
      _appBarTitle = Text("Create RYDR Event");
      _appBarLeading = AppBarCloseButton(context);
      _appBarTrailing = null;
      _body = DealAddEventDetails(
        bloc: _bloc,
        dealToCopy: widget.deal,
      );
    }

    if (page == EventPage.PromoteWithPosts) {
      _appBarTitle =
          _buildTitleWithSubtitle("Post Requirements", "Before the Event");
      _appBarLeading = AppBarBackButton(context,
          onPressed: () => _goToPage(EventPage.Details));
      _appBarTrailing = null;
      _body = DealAddEventPromotWithPosts(_bloc);
    }

    if (page == EventPage.PromoteStartDate) {
      _appBarTitle =
          _buildTitleWithSubtitle("Post Requirements", "Start Posting");
      _appBarLeading = AppBarBackButton(context,
          onPressed: () => _goToPage(EventPage.PromoteWithPosts));
      _appBarTrailing = null;
      _body = DealAddEventPromotStart(_bloc);
    }

    if (page == EventPage.EventMedia) {
      _appBarTitle =
          _buildTitleWithSubtitle("Post Media", "Add Branded Content");
      _appBarLeading = AppBarBackButton(context,
          onPressed: () => _goToPage(EventPage.PromoteStartDate));
      _appBarTrailing = null;
      _body = DealAddEventMedia(_bloc);
    }

    if (page == EventPage.PostRequirements) {
      _appBarTitle = _buildTitleWithSubtitle(
          "Post Requirements", "What to post @ the Event");

      /// check if we want to post before or not, if not skip past media page
      /// and send user straight back to event details
      _appBarLeading = AppBarBackButton(context,
          onPressed: () => _goToPage(_bloc.preEventDays > 0
              ? EventPage.EventMedia
              : EventPage.Details));
      _appBarTrailing = null;
      _body = DealAddEventPostRequirements(_bloc);
    }

    if (page == EventPage.InvitePicker) {
      _appBarTitle = _buildTitleWithSubtitle("Invite Creators", "RSVP Invites");
      _appBarLeading = AppBarBackButton(context,
          onPressed: () => _goToPage(EventPage.PostRequirements));
      _appBarTrailing = StreamBuilder<List<PublisherAccount>>(
        stream: _bloc.invites,
        builder: (context, snapshot) {
          final bool hasInvites =
              snapshot.data != null && snapshot.data.isNotEmpty;

          return FlatButton(
            onPressed: () => _bloc.setPage(EventPage.Preview),
            child: Text(hasInvites ? "Continue" : "Skip"),
          );
        },
      );
      _body = InvitePickerPage(
        _bloc.invites.value,
        handleInvite: _bloc.setInvites,
      );
    }

    if (page == EventPage.Preview) {
      _appBarTitle = Text("Preview Event");
      _appBarLeading = AppBarBackButton(context,
          onPressed: () => _goToPage(EventPage.InvitePicker));
      _appBarTrailing = null;
      _body = DealAddEventPreview(_bloc);
    }

    if (page == EventPage.Done) {
      _appBarTitle = null;
      _appBarLeading = null;
      _body = DealAddEventDone(_bloc);
    }
  }

  @override
  Widget build(BuildContext context) => StreamBuilder<EventPage>(
        stream: _bloc.page,
        builder: (context, snapshot) {
          final EventPage page = snapshot.data ?? EventPage.Details;

          _generateWidgets(page);

          return Scaffold(
            appBar: _appBarTitle == null
                ? null
                : AppBar(
                    backgroundColor: Theme.of(context).scaffoldBackgroundColor,
                    elevation: 0,
                    leading: _appBarLeading,
                    title: _appBarTitle,
                    actions: <Widget>[_appBarTrailing ?? Container()],
                  ),
            body: _body,
          );
        },
      );

  /// simple helper to create a appbar title with a title and subtitle
  /// vs. just a simple appbar title Text()
  Widget _buildTitleWithSubtitle(String title, String subTitle) => Column(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Text(title),
          Text(
            subTitle,
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(
                    color: Theme.of(context).hintColor,
                  ),
                ),
          ),
        ],
      );
}
