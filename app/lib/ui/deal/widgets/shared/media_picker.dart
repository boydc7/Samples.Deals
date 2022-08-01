import 'package:flutter/material.dart';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/ui/deal/blocs/media_picker.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_field.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';

import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

class DealMediaPicker extends StatefulWidget {
  final Function onChoose;
  final PublisherMedia existingImage;
  final ScrollController controller;

  DealMediaPicker({
    @required this.onChoose,
    this.existingImage,
    this.controller,
  });

  @override
  State<StatefulWidget> createState() {
    return _DealMediaPickerState();
  }
}

class _DealMediaPickerState extends State<DealMediaPicker> {
  final DealMediaPickerBloc _bloc = DealMediaPickerBloc();
  final TextEditingController _controller = TextEditingController();

  @override
  void initState() {
    super.initState();

    _bloc.loadMedia();
  }

  @override
  void dispose() {
    _controller.dispose();
    _bloc.dispose();

    super.dispose();
  }

  void _useIgPostAsImage() {
    final String postUrl = _controller.text;

    /// do some rather basic validation on the url before sending it off
    if (!postUrl.startsWith('https://www.instagram.com/p/')) {
      showSharedModalError(context,
          title: "Invalid Post Url",
          subTitle: "Post url should start with 'https://www.instagram.com/p/");
    } else {
      showSharedLoadingLogo(context);

      _bloc.getImageFromPostUrl(postUrl).then((PublisherMedia media) {
        Navigator.of(context).pop();

        if (media != null) {
          widget.onChoose(media);
        } else {
          showSharedModalError(context,
              title: "Unable to get Post Image",
              subTitle:
                  "We were unable to process the requested Post image, please try again in a few moments or use a different post.");
        }
      });
    }
  }

  @override
  Widget build(BuildContext context) => StreamBuilder<PublisherMediasResponse>(
        stream: _bloc.mediaResponse,
        builder: (context, snapshot) =>
            snapshot.connectionState == ConnectionState.waiting
                ? _buildLoading()
                : snapshot.data.error != null
                    ? _buildError(snapshot.data)
                    : _buildSuccess(snapshot.data),
      );

  Widget _buildPostMedia(PublisherMediasResponse mediaResponse) {
    final int mediaId =
        widget.existingImage != null ? widget.existingImage.id : 0;
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    List<Widget> media = [];

    mediaResponse.models.forEach((post) {
      media.add(
        GestureDetector(
          onTap: () => widget.onChoose(post),
          child: CachedNetworkImage(
            imageUrl: post.previewUrl,
            imageBuilder: (context, imageProvider) => Container(
              child: post.id == mediaId
                  ? Container(
                      color: AppColors.blue.withOpacity(0.7),
                      child: Center(
                        child: Icon(
                          AppIcons.check,
                          color: AppColors.white,
                        ),
                      ),
                    )
                  : Container(),
              decoration: BoxDecoration(
                color: dark
                    ? Theme.of(context).appBarTheme.color
                    : Colors.grey[200],
                image: DecorationImage(image: imageProvider, fit: BoxFit.cover),
              ),
            ),
            errorWidget: (context, url, error) => ImageError(
              logUrl: url,
              logParentName:
                  'deal/widgets/shared/media_picker.dart > _buildPostMedia',
            ),
          ),
        ),
      );
    });

    return SliverGrid(
      gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: 3, crossAxisSpacing: 1.0, mainAxisSpacing: 1.0),
      delegate: SliverChildListDelegate(
        media,
      ),
    );
  }

  Widget _buildSuccess(PublisherMediasResponse mediaResponse) {
    return CustomScrollView(
      slivers: <Widget>[
        _buildPostMedia(mediaResponse),
        mediaResponse.models == null || mediaResponse.models.isEmpty == true
            ? SliverFillRemaining(
                child: Column(
                  mainAxisSize: MainAxisSize.max,
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: <Widget>[
                    Icon(
                      AppIcons.cameraRetro,
                      size: 40.0,
                    ),
                    SizedBox(
                      height: 16.0,
                    ),
                    Text('No Posts Yet',
                        style: Theme.of(context).textTheme.headline6)
                  ],
                ),
              )
            : SliverToBoxAdapter(
                child: Container(),
              ),

        /// we'll likely hide it for all but 'soft-linked' business accounts
        /// TODO: Brian need to style this
        appState.currentProfile.isAccountSoft
            ? SliverFillRemaining(
                child: Padding(
                  padding: EdgeInsets.only(left: 16, right: 16, bottom: 180),
                  child: Row(
                    children: <Widget>[
                      Expanded(
                          child: DealTextField(
                        controller: _controller,
                        hintText: "Paste Instagram Post url here...",
                        labelText: "Instagram Post Url",
                        maxLines: 1,
                        maxCharacters: 999,
                        minLines: 1,
                      )),
                      SizedBox(width: 8),
                      SecondaryButton(label: "Go", onTap: _useIgPostAsImage)
                    ],
                  ),
                ),
              )
            : SliverFillRemaining()
      ],
    );
  }

  Widget _buildError(PublisherMediasResponse mediaResponse) {
    return Container(
        color: Theme.of(context).scaffoldBackgroundColor,
        child: RetryError(
            error: mediaResponse.error, onRetry: () => _bloc.loadMedia()));
  }

  Widget _buildLoading() {
    return Container(
      color: Theme.of(context).appBarTheme.color,
      child: LoadingList(),
    );
  }
}
