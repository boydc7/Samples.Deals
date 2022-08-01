import 'package:flutter/material.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/blocs/add_deal.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_approval_notes.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class DealAddPreview extends StatelessWidget {
  final DealAddBloc bloc;
  final Function save;

  DealAddPreview({
    @required this.bloc,
    @required this.save,
  });

  @override
  Widget build(BuildContext context) => StreamBuilder<DealType>(
        stream: bloc.dealType,
        builder: (context, dealType) {
          return ListView(
            children: <Widget>[
              ListTile(
                leading: UserAvatar(appState.currentProfile),
                title: Text(
                  bloc.title.value,
                  style: TextStyle(
                    fontWeight: FontWeight.w600,
                    color: Theme.of(context).textTheme.bodyText2.color,
                  ),
                ),
                subtitle: RichText(
                  overflow: TextOverflow.ellipsis,
                  text: TextSpan(
                    style: Theme.of(context).textTheme.subtitle2,
                    children: <TextSpan>[
                      TextSpan(text: bloc.place.value.name),
                      TextSpan(
                          text: ' · ${bloc.place.value.address.name}',
                          style: TextStyle(
                              fontWeight: FontWeight.normal,
                              color: AppColors.grey400))
                    ],
                  ),
                ),
              ),
              Divider(height: 16),
              Padding(
                padding:
                    EdgeInsets.only(top: 8, bottom: 16, left: 16, right: 16),
                child: Text(
                    'Let the Creator know how to obtain this RYDR. We will show this message to approved Creators only.',
                    style: Theme.of(context).textTheme.bodyText2),
              ),
              DealInputApprovalNotes(
                valueStream: bloc.approvalNotes,
                handleUpdate: bloc.setApprovalNotes,
                handleUpdateFocus: bloc.setFocusApprovalNotes,
                focusStream: bloc.focusApprovalNotes,
                isVirtual: dealType.data != null
                    ? dealType.data == DealType.Virtual
                    : false,
              ),
              Padding(
                padding: EdgeInsets.only(left: 16.0, right: 16, top: 16),
                child: PrimaryButton(
                  buttonColor: dealType.data == DealType.Virtual
                      ? Colors.deepOrange
                      : Theme.of(context).primaryColor,
                  context: context,
                  label: 'Publish this RYDR',
                  onTap: () => save(true),
                ),
              ),
            ],
          );
        },
      );
}
