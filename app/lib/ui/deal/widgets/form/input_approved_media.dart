import 'dart:async';
import 'dart:io';

import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/publisher_approved_media.dart';
import 'package:rydr_app/ui/deal/blocs/approved_media.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_field.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';

class DealInputApprovedMedia extends StatefulWidget {
  final int dealId;
  final List<PublisherApprovedMedia> existingMedia;
  final String placeName;
  final Function handleUpdate;

  DealInputApprovedMedia({
    @required this.dealId,
    @required this.existingMedia,
    @required this.placeName,
    @required this.handleUpdate,
  });

  @override
  _DealInputApprovedMediaState createState() => _DealInputApprovedMediaState();
}

class _DealInputApprovedMediaState extends State<DealInputApprovedMedia> {
  final DealApprovedMediaBloc _bloc = DealApprovedMediaBloc();
  StreamSubscription _sub;
  TextEditingController _textEditingController;

  @override
  void initState() {
    super.initState();

    /// TODO: ability to upload video

    /// listen for changes to the media response and then call the parent
    /// widget to update the list of approved media objects
    _sub = _bloc.mediaResponse.listen((res) {
      if (res.hasError == false) {
        widget.handleUpdate(res.models);
      }
    });

    _bloc.loadMedia(
      widget.dealId,
      widget.existingMedia,
      true,
    );
  }

  @override
  void dispose() {
    _textEditingController?.dispose();
    _sub?.cancel();

    super.dispose();
  }

  void _addMedia(bool video) async {
    /// TODO: we should limit the amount of publisher approved media
    /// one can add to a single deal, maybe 25?

    Permission permission;

    if (Platform.isIOS) {
      permission = Permission.photos;
    } else {
      permission = Permission.storage;
    }

    PermissionStatus permissionStatus = await permission.status;

    if (permissionStatus == PermissionStatus.restricted) {
      _showOpenAppSettingsDialog();

      permissionStatus = await permission.status;

      if (permissionStatus != PermissionStatus.granted) {
        //Only continue if permission granted
        return;
      }
    }

    if (permissionStatus == PermissionStatus.permanentlyDenied) {
      _showOpenAppSettingsDialog();

      permissionStatus = await permission.status;

      if (permissionStatus != PermissionStatus.granted) {
        //Only continue if permission granted
        return;
      }
    }

    if (permissionStatus == PermissionStatus.undetermined) {
      permissionStatus = await permission.request();

      if (permissionStatus != PermissionStatus.granted) {
        //Only continue if permission granted
        return;
      }
    }

    if (permissionStatus == PermissionStatus.denied) {
      if (Platform.isIOS) {
        _showOpenAppSettingsDialog();
      } else {
        permissionStatus = await permission.request();
      }

      if (permissionStatus != PermissionStatus.granted) {
        //Only continue if permission granted
        return;
      }
    }

    if (permissionStatus == PermissionStatus.granted) {
      if (video) {
        _getVideo();
      } else {
        _getImage();
      }
    }
  }

  Future<void> _showOpenAppSettingsDialog() =>
      showSharedModalAlert(context, Text("Permission Needed"),
          content: Text(
              "Photos permission is needed to select media from your device."),
          actions: [
            ModalAlertAction(
                isDefaultAction: true,
                label: "Open Settings",
                onPressed: openAppSettings),
            ModalAlertAction(
              label: "Cancel",
              onPressed: () => Navigator.of(context).pop(),
            )
          ]);

  Future _getImage() async {
    var image = await ImagePicker.pickImage(
      source: ImageSource.gallery,
      imageQuality: 50,
      maxWidth: 1080,
    );

    if (image != null) {
      showSharedLoadingLogo(context, content: "Uploading media");

      _bloc.upload(image).then((success) {
        Navigator.of(context).pop();

        if (!success) {
          showSharedModalError(context, title: "Unable to upload media");
        }
      });
    }
  }

  Future _getVideo() async {
    var image = await ImagePicker.pickVideo(
      source: ImageSource.gallery,
      maxDuration: const Duration(seconds: 60),
    );

    if (image != null) {
      showSharedLoadingLogo(context, content: "Uploading video");

      _bloc.upload(image).then((success) {
        Navigator.of(context).pop();

        if (!success) {
          showSharedModalError(context, title: "Unable to upload video");
        }
      });
    }
  }

  void _removeMedia(PublisherApprovedMedia media) {
    showSharedModalBottomActions(context, title: "Remove Artwork?", actions: [
      ModalBottomAction(
          isDefaultAction: true,
          isDestructiveAction: true,
          child: Text("Yes, Remove it"),
          onTap: () {
            _bloc.removeMedia(media);

            Navigator.of(context).pop();
          }),
    ]);
  }

  /// opens a bottom sheet to add/edit caption for a given media object
  Future<void> _showAddCaption(PublisherApprovedMedia artwork) {
    _textEditingController = TextEditingController(text: artwork.caption);

    return showSharedModalBottomInfo(context,
        initialRatio: 0.8,
        title: "Post caption",
        child: Padding(
            padding: EdgeInsets.only(left: 16, right: 16),
            child: Column(
              children: <Widget>[
                DealTextField(
                  controller: _textEditingController,
                  minLines: 4,
                  maxLines: 999,
                  maxCharacters: null,
                  labelText: null,
                  autoFocus: true,
                  hintText:
                      "Add notes about what caption, #hashtag(s), @mention(s) to post with this media",
                ),
                SizedBox(height: 16),
                Container(
                  height: 40,
                  child: ListView(
                    scrollDirection: Axis.horizontal,
                    padding: EdgeInsets.only(left: 16, right: 16),
                    children: <Widget>[
                      _buildActionChip('@${appState.currentProfile.userName}'),
                      SizedBox(width: 8),
                      widget.placeName != null
                          ? _buildActionChip(widget.placeName)
                          : Container(),
                    ],
                  ),
                ),
                SizedBox(height: 16),
                PrimaryButton(
                  context: context,
                  label: "Update",
                  onTap: () {
                    _bloc
                        .updateMedia(
                            artwork..caption = _textEditingController.text)
                        .then((success) {
                      Navigator.of(context).pop();

                      if (!success) {
                        showSharedModalError(
                          context,
                          title: "Caption Update Error",
                          subTitle:
                              "Please try updating your caption again in a few moments",
                        );
                      }
                    });
                  },
                ),
                SizedBox(height: 16),
              ],
            )));
  }

  void _insertText(String text) {
    final String currentText = _textEditingController.text;
    final int cursorPosition = _textEditingController.selection.base.offset;
    final int newCursorPosition = cursorPosition + text.length + 1;
    final String leadingSpace = cursorPosition == 0
        ? ''
        : currentText.substring(cursorPosition - 1, cursorPosition) == ' '
            ? ''
            : ' ';
    final String trailingSpace = cursorPosition >= currentText.length
        ? ' '
        : currentText.substring(cursorPosition, cursorPosition + 1) == ' '
            ? ''
            : ' ';

    final String textToInsert = '$leadingSpace$text$trailingSpace';

    _textEditingController.value = _textEditingController.value.copyWith(
        text:
            Utils.addCharAtPosition(currentText, textToInsert, cursorPosition),
        selection: TextSelection(
          baseOffset: newCursorPosition,
          extentOffset: newCursorPosition,
        ));
  }

  @override
  Widget build(BuildContext context) =>
      StreamBuilder<PublisherApprovedMediasResponse>(
          stream: _bloc.mediaResponse,
          builder: (context, snapshot) {
            if (widget.dealId != null &&
                snapshot.connectionState == ConnectionState.waiting) {
              return _buildLoading();
            }

            final bool hasMedia = snapshot.data != null &&
                snapshot.data.models != null &&
                snapshot.data.models.isNotEmpty;

            return !hasMedia
                ? _buildChoose()
                : _buildMedias(snapshot.data.models);
          });

  /// TODO: Brian, style this loading screen, this would only show for when
  /// we're loading a draft event or editing an existing deal/event
  Widget _buildLoading() => Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[Text("Loading existing artwork")],
      );

  /// Initial screen for the user to select whether or not to upload artwork
  /// here they can 'skip' artwork complete and move onto the next screen
  Widget _buildChoose() => Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: Column(
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
                              color: Utils.getRequestStatusColor(
                                  DealRequestStatus.invited,
                                  Theme.of(context).brightness ==
                                      Brightness.dark),
                              width: 1.5),
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
                              color: Theme.of(context).primaryColor,
                              width: 1.5),
                          borderRadius: BorderRadius.circular(4),
                        ),
                        child: Center(
                          child: Icon(
                            AppIcons.plusCircle,
                            size: 28,
                            color: Theme.of(context).primaryColor,
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
                SizedBox(height: 32),
                Text("Upload Promotional Artwork"),
                SizedBox(height: 4),
                Padding(
                  padding: EdgeInsets.symmetric(horizontal: 64),
                  child: Text(
                    "Select Instagram artwork for Creators to post before the event.",
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
                  padding: EdgeInsets.symmetric(horizontal: 64),
                  child: Row(
                    children: <Widget>[
                      Expanded(
                        child: PrimaryButton(
                          buttonColor: Theme.of(context).appBarTheme.color,
                          labelColor:
                              Theme.of(context).textTheme.bodyText2.color,
                          hasIcon: true,
                          icon: AppIcons.videoLight,
                          onTap: () => _addMedia(true),
                          label: "Video",
                        ),
                      ),
                      SizedBox(width: 8),
                      Expanded(
                        child: PrimaryButton(
                          buttonColor: Theme.of(context).appBarTheme.color,
                          labelColor:
                              Theme.of(context).textTheme.bodyText2.color,
                          hasIcon: true,
                          icon: AppIcons.image,
                          onTap: () => _addMedia(false),
                          label: "Image",
                        ),
                      ),
                    ],
                  ),
                ),
                SizedBox(height: kToolbarHeight),
              ],
            ),
          ),
        ],
      );

  /// list of currently uploaded medias
  Widget _buildMedias(List<PublisherApprovedMedia> medias) => PageView(
        controller: PageController(viewportFraction: 0.8),
        children: medias
            .map((PublisherApprovedMedia media) => _buildMedia(media))
            .toList()
              ..add(_buildAdd()),
      );

  Widget _buildMedia(PublisherApprovedMedia artwork) => Padding(
        padding: EdgeInsets.only(top: 16, bottom: 32, left: 8, right: 8),
        child: Column(
          mainAxisSize: MainAxisSize.max,
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Expanded(
              child: Center(
                child: Stack(
                  overflow: Overflow.visible,
                  alignment: Alignment.center,
                  children: <Widget>[
                    Container(
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(8),
                        boxShadow: AppShadows.elevation[0],
                      ),
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(8),
                        child: CachedNetworkImage(
                          imageUrl: artwork.previewUrl,
                          errorWidget: (context, url, error) => ImageError(
                            logUrl: url,
                            logParentName:
                                'deal/widgets/form/input_approved_media.dart > _buildMedia',
                          ),
                        ),
                      ),
                    ),
                    artwork.caption != null && artwork.caption.trim() != ""
                        ? GestureDetector(
                            onTap: () => _showAddCaption(artwork),
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              children: <Widget>[
                                Container(
                                  width: double.infinity,
                                  padding: EdgeInsets.symmetric(
                                      horizontal: 16, vertical: 16),
                                  margin: EdgeInsets.symmetric(horizontal: 16),
                                  decoration: BoxDecoration(
                                    color: Theme.of(context)
                                        .canvasColor
                                        .withOpacity(0.9),
                                    border: Border.all(
                                        color: Theme.of(context).canvasColor,
                                        width: 1),
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Center(
                                    child: Text(artwork.caption,
                                        textAlign: TextAlign.left,
                                        style: Theme.of(context)
                                            .textTheme
                                            .caption
                                            .merge(TextStyle(
                                                color: Theme.of(context)
                                                    .hintColor))),
                                  ),
                                ),
                              ],
                            ),
                          )
                        : GestureDetector(
                            onTap: () => _showAddCaption(artwork),
                            child: Container(
                              height: 140,
                              width: 140,
                              decoration: BoxDecoration(
                                color: Theme.of(context)
                                    .canvasColor
                                    .withOpacity(0.9),
                                border: Border.all(
                                    color: Theme.of(context).canvasColor,
                                    width: 1),
                                borderRadius: BorderRadius.circular(70),
                              ),
                              padding: EdgeInsets.symmetric(horizontal: 8),
                              child: Center(
                                child: Text(
                                    "Tap to add a caption or guidelines",
                                    textAlign: TextAlign.center,
                                    style: Theme.of(context)
                                        .textTheme
                                        .caption
                                        .merge(TextStyle(
                                            color:
                                                Theme.of(context).hintColor))),
                              ),
                            ),
                          ),
                    Positioned(
                      bottom: -16,
                      child: GestureDetector(
                        onTap: () => _removeMedia(artwork),
                        child: Container(
                          height: 60,
                          width: 60,
                          decoration: BoxDecoration(
                            color: Theme.of(context).scaffoldBackgroundColor,
                            borderRadius: BorderRadius.circular(32),
                          ),
                          child: Icon(
                            AppIcons.trashAlt,
                            size: 18,
                          ),
                        ),
                      ),
                    )
                  ],
                ),
              ),
            ),
          ],
        ),
      );

  Widget _buildAdd() => Padding(
        padding: EdgeInsets.only(top: 64, bottom: 64),
        child: Container(
          margin: EdgeInsets.symmetric(horizontal: 16),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(8),
            color: Theme.of(context).canvasColor.withOpacity(0.5),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              Icon(
                AppIcons.plusCircle,
                size: 28,
                color: Theme.of(context).hintColor,
              ),
              SizedBox(height: 16),
              Text("Add More Promo Artwork"),
              SizedBox(height: 4),
              Padding(
                padding: EdgeInsets.symmetric(horizontal: 64),
                child: Text(
                  "Tap to choose more",
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(
                          color: Theme.of(context).hintColor,
                        ),
                      ),
                ),
              ),

              /// TODO: Brian style two buttons as the picker has to call separate functions
              /// to open gallery for images vs. videos
              Row(
                children: <Widget>[
                  Expanded(
                    child: SecondaryButton(
                        onTap: () => _addMedia(true), label: "Video"),
                  ),
                  SizedBox(width: 8),
                  Expanded(
                    child: SecondaryButton(
                      onTap: () => _addMedia(false),
                      label: "Image",
                    ),
                  ),
                ],
              )
            ],
          ),
        ),
      );

  Widget _buildActionChip(String label) => ActionChip(
        pressElevation: 1.0,
        onPressed: () => _insertText(label),
        avatar: Icon(AppIcons.plus, size: 16),
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
        shape: OutlineInputBorder(
          borderRadius: BorderRadius.circular(40),
          borderSide: BorderSide(
            width: 1.0,
            color: Theme.of(context).iconTheme.color,
          ),
        ),
        labelStyle: Theme.of(context).textTheme.bodyText1.merge(
              TextStyle(
                color: Theme.of(context).iconTheme.color,
              ),
            ),
        label: Text(label),
      );
}
