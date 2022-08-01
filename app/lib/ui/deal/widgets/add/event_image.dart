import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_media.dart';

class EventAddImage extends StatelessWidget {
  final Stream<PublisherMedia> existingMediaStream;
  final Function handleUpdate;

  EventAddImage({
    @required this.existingMediaStream,
    @required this.handleUpdate,
  });

  @override
  Widget build(BuildContext context) => Container(
        width: double.infinity,
        height: 160,
        padding: EdgeInsets.only(left: 16.0, right: 16.0),
        margin: EdgeInsets.only(bottom: 16.0),
        child: StreamBuilder<PublisherMedia>(
          stream: existingMediaStream,
          builder: (context, snapshot) {
            final PublisherMedia existingMedia = snapshot.data;

            /// if we have no media, or the streams' last value is still null
            /// then purposefully return back the same widget, but wrap in container
            /// to force a full re-build of a different widget if/when there is another value on the stream
            return existingMedia == null
                ? Container(
                    child: DealMedia(
                      existingMedia: null,
                      currentDealStatus: DealStatus.draft,
                      onChoose: handleUpdate,
                    ),
                  )
                : DealMedia(
                    existingMedia: existingMedia,
                    currentDealStatus: DealStatus.draft,
                    onChoose: handleUpdate,
                  );
          },
        ),
      );
}
