import 'package:flutter/material.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/blocs/deal_edit.dart';
import 'package:rydr_app/ui/deal/widgets/edit/media.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_age.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_approval_notes.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_date.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_follower_count.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_tags.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_value.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_engagement_rating.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_toggle.dart';
import 'package:rydr_app/ui/deal/widgets/shared/threshold_info.dart';

class DealEditFields extends StatelessWidget {
  final DealEditBloc bloc;
  final bool canEdit;

  DealEditFields(this.bloc, this.canEdit);

  @override
  Widget build(BuildContext context) {
    final Deal deal = bloc.deal;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Padding(
          padding: EdgeInsets.only(left: 16, right: 16, bottom: 24, top: 16),
          child: Text(!canEdit ? 'Threshold Details' : 'Editable Details',
              style: Theme.of(context).textTheme.bodyText1),
        ),

        DealInputTags(
          handleUpdate: bloc.setTags,
          valueStream: bloc.tags,
        ),

        /// expiration date is not applicable to events, instead we'll have start/end dates
        deal.dealType == DealType.Event
            ? StreamBuilder<bool>(
                stream: bloc.hasEndDate,
                builder: (context, snapshot) {
                  final bool hasEndDate = snapshot.data == true;

                  return Column(
                    children: <Widget>[
                      DealInputDate(
                        labelText: !hasEndDate
                            ? "Event Date/Time"
                            : "Event Start Date/Time",
                        emtpyText: "Choose a date and time...",
                        value: bloc.startDate,
                        handleUpdate: bloc.setStartDate,
                        supportsNoDate: false,
                      ),
                      Visibility(
                        visible: hasEndDate,
                        child: DealInputDate(
                          labelText: "Event End Date/Time",
                          emtpyText: "Choose a date and time...",
                          value: bloc.endDate,
                          handleUpdate: bloc.setEndDate,
                          supportsNoDate: false,
                        ),
                      ),
                      Padding(
                        padding:
                            EdgeInsets.only(left: 16, right: 16, bottom: 16),
                        child: DealTextToggle(
                          labelText:
                              hasEndDate ? "Remove End Date" : "End Date/Time",
                          subtitleText: hasEndDate
                              ? ""
                              : "Add an end date if the event spans multiple days",
                          selected: hasEndDate,
                          onChange: bloc.setHasEndDate,
                        ),
                      ),
                    ],
                  );
                })
            : DealInputDate(
                labelText: "Expiration Date",
                emtpyText: "Never Expires",
                value: bloc.expirationDate,
                handleUpdate: bloc.setExpirationDate,
              ),

        /// restrictions are only visible when this is not an invite-only deal
        !deal.isPrivateDeal
            ? Column(
                children: <Widget>[
                  DealInputFollowerCount(
                    valueStream: bloc.followerCount,
                    handleUpdate: bloc.setFollowerCount,
                    isExpired: !canEdit,
                  ),
                  DealInputEngagementRating(
                    valueStream: bloc.engagementRating,
                    handleUpdate: bloc.setEngagementRating,
                    isExpired: !canEdit,
                  ),
                  canEdit
                      ? DealThresholdInfo(
                          engagementRatingStream: bloc.engagementRating,
                          followerCountStream: bloc.followerCount,
                        )
                      : Container(),
                ],
              )
            : Container(),

        DealInputValue(
          valueStream: bloc.value,
          focusStream: bloc.focusCostOfGoods,
          isExpired: !canEdit,
          handleUpdate: bloc.setValue,
          handleUpdateFocus: bloc.setFocusCostOfGoods,
          dealType: deal.dealType,
        ),
        DealInputApprovalNotes(
          valueStream: bloc.approvalNotes,
          handleUpdate: bloc.setApprovalNotes,
          handleUpdateFocus: bloc.setFocusApprovalNotes,
          focusStream: bloc.focusApprovalNotes,
          isExpired: !canEdit,
          isVirtual: deal.dealType == DealType.Virtual,
        ),
        DealInputAge(
          valueStream: bloc.age,
          handleUpdate: bloc.setAge,
          isExpired: !canEdit,
        ),

        /// only include media component on events for now
        deal.dealType == DealType.Event ? DealEditMedia(bloc) : Container(),
      ],
    );
  }
}
