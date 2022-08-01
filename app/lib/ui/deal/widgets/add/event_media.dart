import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/deal/blocs/add_event.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_approved_media.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class DealAddEventMedia extends StatelessWidget {
  final AddEventBloc bloc;

  DealAddEventMedia(this.bloc);

  @override
  Widget build(BuildContext context) => Scaffold(
        body: Column(
          children: <Widget>[
            Expanded(
              child: DealInputApprovedMedia(
                dealId: bloc.deal.id,
                existingMedia: bloc.artwork.value,
                placeName: bloc.place.value?.name,
                handleUpdate: bloc.setArtwork,
              ),
            ),
            _buildContinueWithout(context),
            _buildEventDate(context),
          ],
        ),
        bottomNavigationBar: _buildBottomNavigationBar(context),
      );

  Widget _buildContinueWithout(BuildContext context) =>
      StreamBuilder<List<PublisherApprovedMedia>>(
          stream: bloc.artwork,
          builder: (context, snapshot) =>
              snapshot.data != null && snapshot.data.isEmpty
                  ? Padding(
                      padding: EdgeInsets.only(bottom: 32),
                      child: TextButton(
                        label: "Continue without artwork",
                        color: Theme.of(context).primaryColor,
                        onTap: () => bloc.setPage(EventPage.PostRequirements),
                      ))
                  : Container());

  Widget _buildEventDate(BuildContext context) {
    final DateTime eventDate = bloc.startDate.value;
    final DateTime eventDateLocal = eventDate.toLocal();

    final int daysBeforeEvent = bloc.preEventDays;
    final DateFormat simpleDay = DateFormat('EEE, MMMM d');
    final DateTime startPostingDate =
        eventDateLocal.subtract(Duration(days: daysBeforeEvent));
    final String simplestartPostingDate = simpleDay.format(startPostingDate);

    return Stack(
      overflow: Overflow.visible,
      alignment: Alignment.topCenter,
      children: <Widget>[
        GestureDetector(
          onTap: () => bloc.setPage(EventPage.PromoteStartDate),
          child: Container(
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
                  simplestartPostingDate,
                  style:
                      Theme.of(context).appBarTheme.textTheme.headline6.merge(
                            TextStyle(
                              color: Theme.of(context).primaryColor,
                            ),
                          ),
                ),
                SizedBox(height: 4),
                Text(
                  'Tap to edit',
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(
                          color: Theme.of(context).hintColor,
                        ),
                      ),
                ),
              ],
            ),
          ),
        ),
        Align(
          alignment: Alignment.centerRight,
          child: Container(
            height: 4,
            width: MediaQuery.of(context).size.width / 2,
            decoration: BoxDecoration(
              gradient: LinearGradient(
                stops: [0, 1],
                begin: Alignment.centerLeft,
                end: Alignment.centerRight,
                colors: [
                  Theme.of(context).primaryColor,
                  Color(0xFF657CD6),
                ],
              ),
            ),
          ),
        ),
        Positioned(
          top: -16,
          child: Stack(
            children: <Widget>[
              Transform.translate(
                offset: Offset(3, -2),
                child: Transform.rotate(
                  angle: 0.1,
                  child: Container(
                    width: 32 * 0.5625,
                    height: 32,
                    decoration: BoxDecoration(
                      color: Theme.of(context).appBarTheme.color,
                      border: Border.all(
                        width: 3,
                        color: Theme.of(context).primaryColor,
                      ),
                      borderRadius: BorderRadius.circular(3),
                    ),
                  ),
                ),
              ),
              Transform.translate(
                offset: Offset(-3, 2),
                child: Transform.rotate(
                  angle: -0.1,
                  child: Container(
                    width: 32 * 0.5625,
                    height: 32,
                    decoration: BoxDecoration(
                      color: Theme.of(context).appBarTheme.color,
                      border: Border.all(
                        width: 3,
                        color: Theme.of(context).primaryColor,
                      ),
                      borderRadius: BorderRadius.circular(3),
                    ),
                  ),
                ),
              ),
            ],
          ),
        )
      ],
    );
  }

  Widget _buildBottomNavigationBar(BuildContext context) =>
      StreamBuilder<List<PublisherApprovedMedia>>(
          stream: bloc.artwork,
          builder: (context, snapshot) => snapshot.data != null &&
                  snapshot.data.isNotEmpty
              ? Container(
                  height: 68,
                  padding: EdgeInsets.only(
                      bottom: MediaQuery.of(context).padding.bottom),
                  decoration:
                      BoxDecoration(color: Theme.of(context).appBarTheme.color),
                  child: Padding(
                    padding: EdgeInsets.symmetric(horizontal: 16.0),
                    child: PrimaryButton(
                      buttonColor: Theme.of(context).primaryColor,
                      context: context,
                      label: "Continue",
                      onTap: () => bloc.setPage(EventPage.PostRequirements),
                    ),
                  ),
                )
              : Container(height: 0));
}
