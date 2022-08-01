import 'package:flutter/material.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/deal/blocs/add_event.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_counter_picker.dart';

class DealAddEventPromotStart extends StatelessWidget {
  final AddEventBloc bloc;

  DealAddEventPromotStart(this.bloc);

  void _edit(BuildContext context) {
    showModalBottomSheet(
        context: context,
        builder: (BuildContext builder) => Theme(
              data: Theme.of(context).brightness == Brightness.light
                  ? AppTheme().buildTheme()
                  : AppTheme().buildDarkTheme(),
              child: DealInputCounterPicker(
                counterType: 'preEventDay',
                currentValue: bloc.preEventDays,
                continueLabel: "Set Day Count",
                enableContinue: true,
                onCancel: () => Navigator.of(context).pop(),
                onContinue: (int newValue) {
                  /// using existing if the user made no changes
                  final int value = newValue ?? bloc.preEventDays;

                  Navigator.of(context).pop();

                  bloc.setMediaStartDate(
                      bloc.startDate.value.subtract(Duration(days: value)));

                  /// Give the bottom sheet time to close before moving on
                  /// then, depending on the users' choice either move them to the posts-upload/manage
                  /// or bypass them straight to the post requirements if they don't want any posts prior
                  Future.delayed(
                      const Duration(milliseconds: 350),
                      () => bloc.setPage(value == null || value == 0
                          ? EventPage.PostRequirements
                          : EventPage.EventMedia));
                },
              ),
            ));
  }

  @override
  Widget build(BuildContext context) => Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          GestureDetector(
            onTap: () => _edit(context),
            child: Container(
              height: 80,
              width: 80,
              decoration: BoxDecoration(
                color: Theme.of(context).canvasColor,
                border: Border.all(
                    color: Theme.of(context).primaryColor, width: 1.5),
                borderRadius: BorderRadius.circular(4),
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Text(
                    bloc.preEventDays.toString(),
                    style: TextStyle(
                      fontSize: 28,
                      fontWeight: FontWeight.w300,
                      color: Theme.of(context).primaryColor,
                    ),
                  ),
                  Text(
                    "days",
                    style: TextStyle(
                      color: Theme.of(context).primaryColor,
                    ),
                  ),
                ],
              ),
            ),
          ),
          SizedBox(height: 32),
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 64),
            child: Text(
              "How many days before the event do you want them to start posting?",
              textAlign: TextAlign.center,
            ),
          ),
        ],
      );
}
