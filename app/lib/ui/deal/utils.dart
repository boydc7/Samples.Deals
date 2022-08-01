import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/cupertino.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_counter_picker.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_date_picker.dart';
import 'package:share/share.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/services/deal.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/ui/deal/invite_picker.dart';
import 'package:rydr_app/ui/deal/widgets/shared/media_picker.dart';
import 'package:rydr_app/app/utils.dart';

void showDealInvitePicker(
  BuildContext context,
  bool editing, {
  int dealId,
  List<PublisherAccount> existingInvites,
  List<PublisherAccount> previousInvites,
  Function onClose,
}) {
  final Route route = MaterialPageRoute(
    fullscreenDialog: true,
    builder: (context) => InvitePickerPage(
      existingInvites,
      dealId: dealId,
    ),
    settings: AppAnalytics.instance
        .getRouteSettings("deal/${editing ? 'edit' : 'add'}/invitepicker"),
  );

  Navigator.push(context, route).then((result) => onClose(result));
}

void showDealCounterChoice({
  @required BuildContext context,
  @required String counterType,
  @required Function onContinue,
  @required Function onCancel,
  int currentValue,
  String continueLabel = 'Continue',
  bool enableContinue = false,
}) {
  showModalBottomSheet(
      context: context,
      builder: (BuildContext builder) => DealInputCounterPicker(
            counterType: counterType,
            currentValue: currentValue,
            continueLabel: continueLabel,
            onContinue: onContinue,
            onCancel: onCancel,
            enableContinue: enableContinue,
          ));
}

void showDealDatePicker({
  @required BuildContext context,
  @required Function onContinue,
  @required Function onCancel,
  @required bool reactivate,
  DateTime currentValue,
}) {
  if (reactivate) {
    showSharedModalBottomActions(context, title: "Options", actions: [
      ModalBottomAction(
          child: Text("Extend with No Expiration"),
          onTap: () {
            onContinue(maxDate);
          }),
      ModalBottomAction(
          child: Text("Set Expiration Date"),
          onTap: () async {
            Navigator.of(context).pop();
            Future.delayed(Duration(milliseconds: 500));
            showModalBottomSheet(
                context: context,
                builder: (BuildContext builder) => DealInputDatePicker(
                      title: "Expiration Date",
                      currentValue: currentValue,
                      reactivate: reactivate,
                      onContinue: onContinue,
                      onCancel: onCancel,
                    ));
          })
    ]);
  } else {
    showModalBottomSheet(
        context: context,
        builder: (BuildContext builder) => DealInputDatePicker(
              title: "Expiration Date",
              currentValue: currentValue,
              reactivate: reactivate,
              onContinue: onContinue,
              onCancel: onCancel,
            ));
  }
}

void showDealMinFollowersChoice({
  @required BuildContext context,
  @required Function onContinue,
  @required Function onCancel,
  int currentValue,
  String continueLabel = 'Continue',
  bool enableContinue = false,
}) {
  showModalBottomSheet(
      context: context,
      builder: (BuildContext builder) => DealInputCounterPicker(
            counterType: 'followerCount',
            currentValue: currentValue,
            continueLabel: continueLabel,
            onContinue: onContinue,
            onCancel: onCancel,
            enableContinue: enableContinue,
          ));
}

void showDealMediaPicker(
  BuildContext context,
  PublisherMedia existingMedia,
  Function onClose,
) {
  /// create picker only once, bottom sheet animation rebuilds the contents
  /// various times during the animatinon and has on "ondone" callback
  Widget picker =
      DealMediaPicker(onChoose: onClose, existingImage: existingMedia);

  showSharedModalBottomInfo(
    context,
    initialRatio: 0.75,
    hasCustomScrollView: true,
    child: picker,
  );
}

void showDealShare(BuildContext context, Deal deal) async {
  showSharedLoadingLogo(context);

  final StringIdResponse res = await DealService.getDealGuid(deal);

  Navigator.of(context).pop();

  if (res.error == null) {
    /// NOTE: shared links should not be deep/app links, just plain old web urls...
    var rydrUri = Uri.parse('https://go.getrydr.com/x/${res.id}');

    final String msg = '${deal.title} - get it at: $rydrUri - via @get_rydr';

    AppAnalytics.instance.logScreen('deal/shared');

    return Share.share(msg);
  } else {
    showSharedModalError(context);
  }

  return null;
}

void showRequestCompletedShare(
    BuildContext context, Deal deal, String shareUrl) async {
  AppAnalytics.instance.logScreen('deal/request/shared');

  return Share.share(
      '${deal.title} - ${deal.request.publisherAccount.userName} - View Completion Report: $shareUrl - via @get_rydr');
}
