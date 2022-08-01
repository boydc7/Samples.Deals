import 'dart:math';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/ui/deal/blocs/request_update.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_field.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

/// This is the request update page where we can either
/// a) cancel an existing request (this can be done by both the creator & business)
/// b) approve a pending request (this can only be done by the business
/// c) decline a request from a creator (this can only be done by the business)
/// d) decline an invite from a business (this can only be done by the creator)
class RequestUpdatePage extends StatefulWidget {
  final Deal deal;
  final DealRequestStatus requestStatusToUpdateTo;

  RequestUpdatePage(
    this.deal,
    this.requestStatusToUpdateTo,
  );

  @override
  State<StatefulWidget> createState() => _RequestUpdatePageState();
}

class _RequestUpdatePageState extends State<RequestUpdatePage> {
  RequestUpdateBloc _bloc;
  TextEditingController _notesController;

  @override
  void initState() {
    super.initState();

    _bloc = RequestUpdateBloc(
      widget.deal,
      widget.requestStatusToUpdateTo,
    );

    /// set notes field to exiting approval note if we're looking to
    /// approve a request, or empty string to avoid nulls when using other status
    _notesController = TextEditingController(
        text: widget.requestStatusToUpdateTo == DealRequestStatus.inProgress
            ? widget.deal.approvalNotes ?? ""
            : "");
  }

  @override
  void dispose() {
    _bloc.dispose();
    _notesController.dispose();

    super.dispose();
  }

  void _handleUpdateClick() async {
    showSharedLoadingLogo(
      context,
      content: "Updating RYDR",
    );

    final BaseResponse res =
        await _bloc.updateRequest(_notesController.text.trim());

    /// close the "updating alert"
    Navigator.of(context).pop();

    /// if the request update was successful then we simply reload the page
    /// with the id of the person the request belongs to
    if (res.error == null) {
      Navigator.of(context).pushNamedAndRemoveUntil(
          AppRouting.getRequestRoute(
              widget.deal.id,
              appState.currentProfile.isBusiness
                  ? widget.deal.request.publisherAccount.id
                  : null),
          (Route<dynamic> route) => false);
    } else {
      showSharedModalError(context);
    }
  }

  @override
  Widget build(BuildContext context) {
    final String statusString =
        dealRequestStatusToString(widget.requestStatusToUpdateTo);
    final Size size = MediaQuery.of(context).size;
    final bool isBusiness = appState.currentProfile.isBusiness;
    final bool isCancelling =
        widget.requestStatusToUpdateTo == DealRequestStatus.cancelled ||
            widget.requestStatusToUpdateTo == DealRequestStatus.denied;

    final _random = Random();
    final String toUsername = appState.currentProfile.isBusiness
        ? widget.deal.request.publisherAccount.userName
        : widget.deal.publisherAccount.userName;

    final approvalExamples = [
      'Example: Come in and ask to speak with Thuan. Tell him you\'re here on a RYDR and he\'ll take care of you! Enjoy!',
      'Example: Stephanie will be at the front desk. Show her your approved RYDR and she\'ll show you to your seats!',
      'Example: Once you place your order, give them promo code: RYDR1AX2. That\'s all you need, we hope you enjoy!',
      'Example: Knock on the door and Megan will let you in. Let her know you\'re here with RYDR, then grab a cold brew, relax, and enjoy our space!'
    ];

    final statusColor = {
      "cancelled": AppColors.grey300,
      "denied": AppColors.grey300,
      "inProgress": isBusiness
          ? Theme.of(context).primaryColor
          : Theme.of(context).accentColor,
    };

    final title = {
      "cancelled": "Cancelling RYDR",
      "denied": "Decline Confirmation",
      "inProgress": "Confirm How to Redeem",
    };

    final subTitle = {
      "cancelled":
          "There can be many reasons for cancelling a RYDR. We want to make sure that both parties understand your reason for cancelling, so be as concise as possible.",
      "denied": isBusiness
          ? "Let $toUsername know why you're delining their RYDR request"
          : "Let $toUsername know why you're declining their RYDR invite",
      "inProgress": widget.deal.approvalNotes ?? "",
    };

    final labelText = {
      "cancelled": "Tell $toUsername why you\'re cancelling",
      "denied": "E.g. Thank you, however at this time...",
      "inProgress": "How to redeem this RYDR",
    };

    final hintText = {
      "cancelled": "Be as clear and concise as possible...",
      "denied": "E.g. Thank you, however at this time...",
      "inProgress": approvalExamples[_random.nextInt(approvalExamples.length)],
    };

    final buttonTitle = {
      "cancelled": "Cancel RYDR",
      "denied": "Decline RYDR",
      "inProgress": "Approve Request",
    };

    return Scaffold(
      appBar: AppBar(
        leading: AppBarCloseButton(context),
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
        title: Text(title[statusString]),
        centerTitle: true,
        elevation: 0,
      ),
      body: SafeArea(
        child: Container(
          height: size.height,
          width: size.width,
          padding: EdgeInsets.symmetric(horizontal: 16.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              StreamBuilder<bool>(
                  stream: _bloc.showingTextField,
                  builder: (context, snapshot) {
                    final bool showingTextField = snapshot.data == true;
                    return Expanded(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: <Widget>[
                          GestureDetector(
                            onTap: () =>
                                _bloc.setShowingTextField(!showingTextField),
                            child: Icon(
                              subTitle[statusString] == ""
                                  ? AppIcons.plusCircle
                                  : AppIcons.commentAltLines,
                              size: 40.0,
                              color:
                                  Theme.of(context).textTheme.bodyText2.color,
                            ),
                          ),
                          SizedBox(height: 20.0),
                          Visibility(
                            visible: subTitle[statusString] != "",
                            child: Column(
                              children: <Widget>[
                                showingTextField
                                    ? _buildNotesField(
                                        hintText[statusString],
                                        labelText[statusString],
                                      )
                                    : GestureDetector(
                                        onTap: () => _bloc.setShowingTextField(
                                            !showingTextField),
                                        child: Container(
                                          decoration: BoxDecoration(
                                            borderRadius:
                                                BorderRadius.circular(4.0),
                                            border: Border.all(
                                                color: Theme.of(context)
                                                    .textTheme
                                                    .bodyText2
                                                    .color),
                                            color: Theme.of(context)
                                                .scaffoldBackgroundColor,
                                          ),
                                          margin: EdgeInsets.only(
                                              left: 16.0, right: 16.0),
                                          padding: EdgeInsets.symmetric(
                                              horizontal: 16.0, vertical: 16.0),
                                          child: Text(subTitle[statusString],
                                              textAlign: TextAlign.center,
                                              style: Theme.of(context)
                                                  .textTheme
                                                  .bodyText2),
                                        ),
                                      ),
                                SizedBox(height: 4),
                                Text(
                                  !showingTextField
                                      ? 'Tap to edit'
                                      : 'This will be sent as a direct message',
                                  style: Theme.of(context)
                                      .textTheme
                                      .caption
                                      .merge(
                                        TextStyle(
                                            color: Theme.of(context).hintColor),
                                      ),
                                ),
                              ],
                            ),
                          ),
                          Visibility(
                            visible: subTitle[statusString] == "",
                            child: showingTextField
                                ? _buildNotesField(
                                    hintText[statusString],
                                    labelText[statusString],
                                  )
                                : GestureDetector(
                                    onTap: () => _bloc
                                        .setShowingTextField(!showingTextField),
                                    child: Container(
                                      color: Theme.of(context)
                                          .scaffoldBackgroundColor,
                                      padding: EdgeInsets.symmetric(
                                          horizontal: 16.0),
                                      child: RichText(
                                        textAlign: TextAlign.center,
                                        text: TextSpan(
                                            style: Theme.of(context)
                                                .textTheme
                                                .bodyText2,
                                            children: [
                                              TextSpan(
                                                  style: Theme.of(context)
                                                      .textTheme
                                                      .bodyText1
                                                      .merge(
                                                        TextStyle(
                                                          fontSize: 16.0,
                                                        ),
                                                      ),
                                                  text: "Redeeming this RYDR"),
                                              TextSpan(
                                                text:
                                                    "\nTap here to add details to ${widget.deal.request.publisherAccount.userName} so they know what to do once approved.",
                                              )
                                            ]),
                                      ),
                                    ),
                                  ),
                          ),
                        ],
                      ),
                    );
                  }),
              PrimaryButton(
                buttonColor: statusColor[statusString],
                label: buttonTitle[statusString],
                onTap: _handleUpdateClick,
              ),
              SizedBox(height: 8.0),
              Visibility(
                visible: isCancelling,
                child: Text(
                  subTitle[statusString],
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.bodyText2.merge(
                        TextStyle(color: Theme.of(context).hintColor),
                      ),
                ),
              ),
              Visibility(
                visible: !isCancelling,
                child: RichText(
                  textAlign: TextAlign.center,
                  text: TextSpan(
                      style: Theme.of(context)
                          .textTheme
                          .bodyText2
                          .merge(TextStyle(color: Theme.of(context).hintColor)),
                      children: [
                        TextSpan(text: 'Note: Make sure '),
                        TextSpan(
                          text: toUsername,
                          style: TextStyle(fontWeight: FontWeight.w600),
                        ),
                        TextSpan(
                            text:
                                ' knows exactly how to obtain this RYDR. This will be sent as the first direct message between both parties '),
                      ]),
                ),
              ),
              SizedBox(
                height: 16.0,
              )
            ],
          ),
        ),
        bottom: true,
      ),
    );
  }

  Widget _buildNotesField(String hintText, String labelText) {
    return Padding(
      padding: EdgeInsets.only(left: 16.0, right: 16.0),
      child: DealTextField(
        labelText: labelText,
        maxCharacters: 500,
        autoFocus: true,
        hintText: hintText,
        minLines: 3,
        maxLines: 3,
        controller: _notesController,
      ),
    );
  }
}
