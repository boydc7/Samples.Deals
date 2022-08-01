import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/blocs/add_event.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_posts.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_receive_notes.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_stories.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

class DealAddEventPostRequirements extends StatefulWidget {
  final AddEventBloc bloc;

  DealAddEventPostRequirements(this.bloc);

  @override
  _DealAddEventPostRequirementsState createState() =>
      _DealAddEventPostRequirementsState();
}

class _DealAddEventPostRequirementsState
    extends State<DealAddEventPostRequirements> {
  String _simpleEventDate;
  String _dayOfEvent;

  void _continue([bool skipInvites = false]) {
    /// validate that we have at least one story or post
    if (widget.bloc.deal.requestedPosts == 0 &&
        widget.bloc.deal.requestedStories == 0) {
      showSharedModalError(
        context,
        title: "Your Event needs a tweak",
        subTitle:
            "Choose number of stories and/or posts required by the Creator",
      );

      return;
    }

    widget.bloc
        .setPage(skipInvites ? EventPage.Preview : EventPage.InvitePicker);
  }

  @override
  Widget build(BuildContext context) {
    final DateTime eventDateLocal = widget.bloc.startDate.value.toLocal();

    _simpleEventDate = DateFormat('EEE, MMMM d @ ha').format(eventDateLocal);
    _dayOfEvent = DateFormat('d').format(eventDateLocal);

    return Scaffold(
      body: Column(
        children: <Widget>[
          Expanded(
            child: ListView(
              padding: EdgeInsets.only(top: 16),
              children: <Widget>[
                _buildReceiveTypes(),
                _buildReceiveNotes(),
              ],
            ),
          ),
          _buildFooter(),
        ],
      ),
      bottomNavigationBar: Container(
        height: 68,
        padding: EdgeInsets.only(bottom: MediaQuery.of(context).padding.bottom),
        decoration: BoxDecoration(color: Theme.of(context).appBarTheme.color),
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: 16.0),
          child: Row(
            children: <Widget>[
              Expanded(
                child: PrimaryButton(
                  buttonColor: Utils.getRequestStatusColor(
                      DealRequestStatus.invited,
                      Theme.of(context).brightness == Brightness.dark),
                  context: context,
                  label: "Invite Creators",
                  onTap: _continue,
                ),
              ),
              SizedBox(width: 8),
              SecondaryButton(
                context: context,
                label: "Skip",
                onTap: () => _continue(true),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildReceiveTypes() => Container(
        decoration: BoxDecoration(
          border: Border(
            left: BorderSide(
              color: Theme.of(context).dividerColor,
            ),
          ),
        ),
        margin: EdgeInsets.only(left: 26),
        padding: EdgeInsets.only(bottom: 16),
        child: Column(
          children: <Widget>[
            Stack(
              overflow: Overflow.visible,
              children: <Widget>[
                Container(
                  width: double.infinity,
                  padding: EdgeInsets.only(left: 16, bottom: 16),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.start,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        "Minimum Post Requirements from Creators",
                        style: Theme.of(context).textTheme.bodyText1.merge(
                            TextStyle(color: Theme.of(context).primaryColor)),
                      ),
                      SizedBox(height: 4),
                      Text(
                        "Posts required from the event",
                        style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor)),
                      ),
                    ],
                  ),
                ),
                Positioned(
                  left: -8,
                  top: 0,
                  child: Icon(
                    AppIcons.portraitSolid,
                    color: Theme.of(context).primaryColor,
                    size: 16,
                  ),
                ),
              ],
            ),
            Column(
              children: <Widget>[
                DealInputStories(
                  valueStream: widget.bloc.stories,
                  handleUpdate: widget.bloc.setStories,
                ),
                DealInputPosts(
                  valueStream: widget.bloc.posts,
                  handleUpdate: widget.bloc.setPosts,
                ),
                SizedBox(height: 16.0),
              ],
            ),
          ],
        ),
      );

  Widget _buildReceiveNotes() => Container(
        margin: EdgeInsets.only(left: 26),
        padding: EdgeInsets.only(bottom: 16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Stack(
              overflow: Overflow.visible,
              children: <Widget>[
                Container(
                  width: double.infinity,
                  padding: EdgeInsets.only(left: 16, bottom: 16),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.start,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        "Additional Notes",
                        style: Theme.of(context).textTheme.bodyText1.merge(
                            TextStyle(color: Theme.of(context).primaryColor)),
                      ),
                      SizedBox(height: 4),
                      Text(
                        "Details or suggestions on what Creators should post",
                        style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor)),
                      ),
                    ],
                  ),
                ),
                Positioned(
                  left: -8,
                  top: 0,
                  child: Icon(
                    AppIcons.solidFileAlt,
                    color: Theme.of(context).primaryColor,
                    size: 16,
                  ),
                ),
              ],
            ),
            DealInputReceiveNotes(
              valueStream: widget.bloc.receiveNotes,
              handleUpdate: widget.bloc.setReceiveNotes,
              handleUpdateFocus: widget.bloc.setFocusReceiveNotes,
              focusStream: widget.bloc.focusReceiveNotes,
              placeName: widget.bloc.place.value?.name,
              dealType: DealType.Event,
            ),
          ],
        ),
      );

  Widget _buildFooter() => Stack(
        overflow: Overflow.visible,
        alignment: Alignment.topCenter,
        children: <Widget>[
          Container(
            height: 100,
            width: double.infinity,
            decoration: BoxDecoration(
                border: Border(
                    top: BorderSide(color: Theme.of(context).dividerColor)),
                color: Theme.of(context).appBarTheme.color),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(
                  _simpleEventDate,
                  style: Theme.of(context)
                      .appBarTheme
                      .textTheme
                      .headline6
                      .merge(
                        TextStyle(
                          color: Utils.getRequestStatusColor(
                              DealRequestStatus.invited,
                              Theme.of(context).brightness == Brightness.dark),
                        ),
                      ),
                ),
                SizedBox(height: 4),
                Text(
                  'Day of Event',
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(
                          color: Theme.of(context).hintColor,
                        ),
                      ),
                ),
              ],
            ),
          ),
          Align(
            alignment: Alignment.centerLeft,
            child: widget.bloc.preEventDays == 0
                ? Container()
                : Container(
                    height: 4,
                    width: MediaQuery.of(context).size.width / 2,
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        stops: [0, 1],
                        begin: Alignment.centerLeft,
                        end: Alignment.centerRight,
                        colors: [
                          Color(0xFF657CD6),
                          Utils.getRequestStatusColor(DealRequestStatus.invited,
                              Theme.of(context).brightness == Brightness.dark),
                        ],
                      ),
                    ),
                  ),
          ),
          Positioned(
            top: -24,
            child: Stack(
              alignment: Alignment.bottomCenter,
              children: <Widget>[
                Container(
                  height: 28,
                  width: 28,
                  color: Theme.of(context).appBarTheme.color,
                ),
                Icon(
                  AppIcons.calendar,
                  size: 40,
                  color: Utils.getRequestStatusColor(DealRequestStatus.invited,
                      Theme.of(context).brightness == Brightness.dark),
                ),
                Padding(
                  padding: EdgeInsets.only(bottom: 7.0),
                  child: Text(
                    _dayOfEvent,
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      color: Utils.getRequestStatusColor(
                          DealRequestStatus.invited,
                          Theme.of(context).brightness == Brightness.dark),
                    ),
                  ),
                )
              ],
            ),
          )
        ],
      );
}
