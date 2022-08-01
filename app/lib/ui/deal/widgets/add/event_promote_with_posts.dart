import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/ui/deal/blocs/add_event.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class DealAddEventPromotWithPosts extends StatelessWidget {
  final AddEventBloc bloc;

  DealAddEventPromotWithPosts(this.bloc);

  @override
  Widget build(BuildContext context) => Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          Stack(
            children: <Widget>[
              Transform.rotate(
                angle: -0.105,
                child: Container(
                  height: 120,
                  width: 120 * 0.5625,
                  decoration: BoxDecoration(
                    color: Theme.of(context).canvasColor,
                    border: Border.all(
                        color: Theme.of(context).hintColor, width: 1.5),
                    borderRadius: BorderRadius.circular(4),
                  ),
                ),
              ),
              Transform.rotate(
                angle: 0.065,
                child: Container(
                  height: 120,
                  width: 120 * 0.5625,
                  decoration: BoxDecoration(
                    color: Theme.of(context).canvasColor,
                    border: Border.all(
                        color: Theme.of(context).hintColor, width: 1.5),
                    borderRadius: BorderRadius.circular(4),
                  ),
                  child: Center(
                    child: Transform.translate(
                      offset: Offset(-2.5, 0),
                      child: Icon(
                        AppIcons.users,
                        size: 28,
                        color: Theme.of(context).hintColor,
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
          SizedBox(height: 32),
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 64),
            child: Text(
              "Do you want Creators to promote\nyour event with posts?",
              textAlign: TextAlign.center,
            ),
          ),
          SizedBox(height: 4),
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 64),
            child: Text(
              "This will happen before the event.",
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(
                      color: Theme.of(context).hintColor,
                    ),
                  ),
            ),
          ),
          SizedBox(height: 32),
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 32),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Expanded(
                  child: SecondaryButton(
                    fullWidth: true,
                    context: context,
                    label: "No thanks",
                    onTap: () {
                      bloc.setMediaStartDate(null);
                      bloc.setPage(EventPage.PostRequirements);
                    },
                  ),
                ),
                SizedBox(width: 8),
                Expanded(
                  child: SecondaryButton(
                    fullWidth: true,
                    context: context,
                    label: "Yes I do",
                    onTap: () {
                      bloc.setPage(EventPage.PromoteStartDate);
                    },
                  ),
                ),
              ],
            ),
          ),
          SizedBox(height: kToolbarHeight),
        ],
      );
}
