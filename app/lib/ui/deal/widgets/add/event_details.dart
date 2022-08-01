import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/blocs/add_event.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_date.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_description.dart';
import 'package:rydr_app/ui/deal/widgets/add/event_image.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_places.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_tags.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_title.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_toggle.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

class DealAddEventDetails extends StatefulWidget {
  final AddEventBloc bloc;
  final Deal dealToCopy;

  DealAddEventDetails({
    this.bloc,
    this.dealToCopy,
  });

  @override
  _DealAddEventDetailsState createState() => _DealAddEventDetailsState();
}

class _DealAddEventDetailsState extends State<DealAddEventDetails> {
  final GlobalKey _keyEnsureDescriptionVisible = GlobalKey();
  final _scrollController = ScrollController();

  final Map<String, String> _pageContent = {
    "ErrorTitle": "Title must be between 10 and 40 characters.",
    "ErrorDescription":
        "Description needs to be between 25 characters and 300 characters in length.",
    "ErrorStartDate": "Event must have a start date in the future.",
    "ErrorEndDate": "Event must have an end date after the start date.",
    "ErrorInvalidPlace": "RYDR is not yet available for this location.",
  };

  @override
  void initState() {
    super.initState();

    /// listen for when description or receive notes receive focus
    /// and then scroll so that even with the onscreen keyboard we can still see the
    /// icons for quick-fill and history is visible to the user
    widget.bloc.focusDescription.listen((val) {
      if (val == true) {
        Future.delayed(
            Duration(milliseconds: 250),
            () => Scrollable.ensureVisible(
                  _keyEnsureDescriptionVisible.currentContext,
                ));
      }
    });
  }

  @override
  void dispose() {
    _scrollController.dispose();

    super.dispose();
  }

  void _continue() {
    /// validate all required fields on this page
    final List<String> errors = [
      widget.bloc.validTitle ? null : _pageContent['ErrorTitle'],
      widget.bloc.validDescription ? null : _pageContent['ErrorDescription'],
      widget.bloc.validPlace ? null : _pageContent['ErrorInvalidPlace'],
      widget.bloc.validStartDate ? null : _pageContent['ErrorStartDate'],
      widget.bloc.validEndDate ? null : _pageContent['ErrorEndDate'],
    ].where((el) => el != null).toList();

    if (errors.isNotEmpty) {
      showSharedModalAlert(context, Text("Event needs tweaks"),
          content: Column(
              children: errors
                  .map((err) => Padding(
                      padding: EdgeInsets.only(top: 8), child: Text(err)))
                  .toList()),
          actions: [
            ModalAlertAction(
                label: "Okay",
                isDefaultAction: true,
                onPressed: () {
                  Navigator.of(context).pop();
                }),
          ]);
      return;
    }

    /// if no errors, then nagivate user to next step
    /// which could be different depending on:
    ///
    /// - if we're editing a draft and the user previously had set a mediaStartDate
    ///   then we can skip past the page where we ask them whether or not to post
    ///

    final EventPage nextPage = widget.bloc.deal.id != null &&
            widget.bloc.deal.id > 0 &&
            widget.bloc.deal.mediaStartDate != null
        ? EventPage.PromoteStartDate
        : EventPage.PromoteWithPosts;

    widget.bloc.setPage(nextPage);
  }

  @override
  Widget build(BuildContext context) => ListView(
        controller: _scrollController,
        children: <Widget>[
          _buildPlace(),
          _buildImage(),
          _buildDetails(),
          _buildContinue(),
        ],
      );

  Widget _buildPlace() => Container(
        decoration: BoxDecoration(
          border:
              Border(left: BorderSide(color: Theme.of(context).dividerColor)),
        ),
        margin: EdgeInsets.only(left: 26, top: 16),
        padding: EdgeInsets.only(bottom: 0),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.start,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Stack(
              overflow: Overflow.visible,
              children: <Widget>[
                Padding(
                  padding: EdgeInsets.only(left: 16),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.start,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        "Event Location",
                        style: Theme.of(context).textTheme.bodyText1.merge(
                            TextStyle(color: Theme.of(context).primaryColor)),
                      ),
                      SizedBox(height: 4),
                      Text(
                        "Choose a saved location or add a new one",
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
                    AppIcons.mapMarkerAltSolid,
                    color: Theme.of(context).primaryColor,
                    size: 16,
                  ),
                ),
              ],
            ),
            DealInputPlace(
              handlePlaceChange: widget.bloc.setPlace,
              valueStream: widget.bloc.place,
              dealType: DealType.Event,
            ),
          ],
        ),
      );

  Widget _buildImage() => Container(
        decoration: BoxDecoration(
          border: Border(
            left: BorderSide(
              color: Theme.of(context).dividerColor,
            ),
          ),
        ),
        margin: EdgeInsets.only(left: 26),
        padding: EdgeInsets.only(bottom: 16, top: 16),
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
                        "Event Image",
                        style: Theme.of(context).textTheme.bodyText1.merge(
                            TextStyle(color: Theme.of(context).primaryColor)),
                      ),
                      SizedBox(height: 4),
                      Text(
                        "Choose a post from your Instagram feed",
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
                    AppIcons.solidImage,
                    color: Theme.of(context).primaryColor,
                    size: 16,
                  ),
                ),
              ],
            ),
            EventAddImage(
              existingMediaStream: widget.bloc.media,
              handleUpdate: widget.bloc.setMedia,
            ),
          ],
        ),
      );

  Widget _buildDetails() => Container(
        margin: EdgeInsets.only(left: 27),
        padding: EdgeInsets.only(bottom: 0),
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
                        "Event Details",
                        style: Theme.of(context).textTheme.bodyText1.merge(
                            TextStyle(color: Theme.of(context).primaryColor)),
                      ),
                      SizedBox(height: 4),
                      Text(
                        "Grab attention and outline details",
                        style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor)),
                      ),
                    ],
                  ),
                ),
                Positioned(
                  left: -10,
                  top: 0,
                  child: Icon(
                    AppIcons.ticketAltSolid,
                    color: Theme.of(context).primaryColor,
                    size: 16,
                  ),
                ),
              ],
            ),
            DealInputTitle(
              dealType: DealType.Event,
              valueStream: widget.bloc.title,
              handleUpdate: widget.bloc.setTitle,
              handleUpdateFocus: widget.bloc.setFocusTitle,
              focusStream: widget.bloc.focusTitle,
              charStream: widget.bloc.charTitle,
            ),
            Container(
              key: _keyEnsureDescriptionVisible,
              child: SizedBox(height: 16.0),
            ),
            DealInputDescription(
              dealType: DealType.Event,
              valueStream: widget.bloc.description,
              handleUpdate: widget.bloc.setDescription,
              handleUpdateFocus: widget.bloc.setFocusDescription,
              focusStream: widget.bloc.focusDescription,
              charStream: widget.bloc.charDescription,
            ),
            DealInputTags(
              handleUpdate: widget.bloc.setTags,
              valueStream: widget.bloc.tags,
            ),
            StreamBuilder<bool>(
              stream: widget.bloc.hasEndDate,
              builder: (context, snapshot) {
                final bool hasEndDate = snapshot.data == true;

                return Column(
                  children: <Widget>[
                    DealInputDate(
                      labelText: !hasEndDate
                          ? "Event Date/Time"
                          : "Event Start Date/Time",
                      emtpyText: "Choose a date and time...",
                      value: widget.bloc.startDate,
                      handleUpdate: widget.bloc.setStartDate,
                      supportsNoDate: false,
                    ),
                    Visibility(
                      visible: hasEndDate,
                      child: Padding(
                        padding: EdgeInsets.only(top: 16.0),
                        child: DealInputDate(
                          labelText: "Event End Date/Time",
                          emtpyText: "Choose a date and time...",
                          value: widget.bloc.endDate,
                          handleUpdate: widget.bloc.setEndDate,
                          supportsNoDate: false,
                        ),
                      ),
                    ),
                    Padding(
                      padding: EdgeInsets.only(left: 16, right: 16),
                      child: DealTextToggle(
                        labelText:
                            hasEndDate ? "Remove End Date" : "End Date/Time",
                        subtitleText: hasEndDate
                            ? ""
                            : "Add an end date if the event spans multiple days",
                        selected: hasEndDate,
                        onChange: (value) => widget.bloc.setHasEndDate(value),
                      ),
                    ),
                  ],
                );
              },
            ),
          ],
        ),
      );

  Widget _buildContinue() => SafeArea(
        bottom: true,
        child: Padding(
          padding: EdgeInsets.only(left: 16, right: 16, top: 32, bottom: 16),
          child: PrimaryButton(
            buttonColor: AppColors.blue,
            context: context,
            label: "Add Post Requirements",
            onTap: _continue,
          ),
        ),
      );
}
