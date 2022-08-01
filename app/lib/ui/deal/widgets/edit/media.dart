import 'package:flutter/material.dart';
import 'package:rydr_app/ui/deal/blocs/deal_edit.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_approved_media.dart';

class DealEditMedia extends StatelessWidget {
  final DealEditBloc bloc;

  DealEditMedia(this.bloc);

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 400,
      child: DealInputApprovedMedia(
        dealId: bloc.deal.id,
        existingMedia: bloc.artwork.value,
        placeName: bloc.deal.place?.name,
        handleUpdate: bloc.setArtwork,
      ),
    );
  }
}
