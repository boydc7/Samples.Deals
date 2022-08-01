import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/deal/utils.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';

class DealMedia extends StatefulWidget {
  final PublisherMedia existingMedia;
  final DealStatus currentDealStatus;
  final Function onChoose;
  final bool big;
  final bool expired;

  DealMedia({
    this.existingMedia,
    @required this.currentDealStatus,
    @required this.onChoose,
    this.big = false,
    this.expired = false,
  });

  @override
  State<StatefulWidget> createState() => _DealMediaState();
}

class _DealMediaState extends State<DealMedia> {
  PublisherMedia media;

  @override
  void initState() {
    super.initState();

    media = widget.existingMedia;
  }

  void editMedia(BuildContext context) => showDealMediaPicker(
        context,
        widget.existingMedia,
        (PublisherMedia m) {
          /// close the image picker
          Navigator.of(context).pop();

          /// update local state to show the image in the widget
          /// only if we've actually chosen an image, then also callback to the parent
          if (m != null) {
            setState(() => media = m);

            widget.onChoose(m);
          }
        },
      );

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final bool canEdit = widget.currentDealStatus != DealStatus.completed &&
        widget.currentDealStatus != DealStatus.deleted &&
        !widget.expired;

    return GestureDetector(
      onTap: () => canEdit ? editMedia(context) : null,
      child: media != null
          ? Container(
              decoration: BoxDecoration(
                boxShadow: widget.big
                    ? AppShadows.elevation[3]
                    : <BoxShadow>[
                        BoxShadow(
                            offset: Offset(0.0, 0.0),
                            blurRadius: 0.0,
                            spreadRadius: 0.0,
                            color: Colors.transparent)
                      ],
                borderRadius: BorderRadius.circular(widget.big ? 16.0 : 4.0),
              ),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(widget.big ? 16.0 : 4.0),
                child: CachedNetworkImage(
                  imageUrl: media.previewUrl,
                  imageBuilder: (context, imageProvider) => Container(
                    height: 56.0,
                    width: 58.0,
                    color: Colors.grey.shade300,
                    foregroundDecoration: BoxDecoration(
                      borderRadius:
                          BorderRadius.circular(widget.big ? 16.0 : 4.0),
                      border: Border.all(
                          color: widget.big
                              ? Colors.transparent
                              : AppColors.grey300),
                      image: DecorationImage(
                        fit: BoxFit.cover,
                        colorFilter: ColorFilter.mode(
                            widget.currentDealStatus == DealStatus.paused ||
                                    !canEdit ||
                                    widget.expired
                                ? Colors.white
                                : Colors.transparent,
                            BlendMode.saturation),
                        image: imageProvider,
                      ),
                    ),
                  ),
                  errorWidget: (context, url, error) => ImageError(
                    logUrl: url,
                    logParentName: 'deal/widgets/shared/text_media.dart',
                  ),
                ),
              ),
            )
          : Container(
              height: 59.0,
              width: 58.0,
              decoration: BoxDecoration(
                color: dark
                    ? Theme.of(context).appBarTheme.color
                    : AppColors.white20,
                border: Border.all(
                  color: Theme.of(context).hintColor,
                ),
                borderRadius: BorderRadius.circular(4.0),
              ),
              child: Center(
                child: Stack(
                  alignment: Alignment.bottomRight,
                  overflow: Overflow.visible,
                  children: <Widget>[
                    Icon(AppIcons.image, color: Theme.of(context).hintColor),
                    Positioned(
                      bottom: -4.5,
                      right: -8,
                      child: Stack(
                        alignment: Alignment.center,
                        children: <Widget>[
                          ClipRRect(
                            borderRadius: BorderRadius.circular(40.0),
                            child: Container(
                              color: dark
                                  ? Theme.of(context).appBarTheme.color
                                  : AppColors.white20,
                              height: 18.0,
                              width: 18.0,
                            ),
                          ),
                          Icon(
                            AppIcons.plusCircleReg,
                            color: Theme.of(context).hintColor,
                            size: 15.0,
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
    );
  }
}
