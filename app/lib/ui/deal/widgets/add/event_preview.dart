import 'dart:async';

import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/deal/blocs/add_event.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_age.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_auto_approve.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_engagement_rating.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_follower_count.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_toggle.dart';
import 'package:rydr_app/ui/deal/widgets/shared/threshold_info.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class DealAddEventPreview extends StatefulWidget {
  final AddEventBloc bloc;

  DealAddEventPreview(this.bloc);

  @override
  _DealAddEventPreviewState createState() => _DealAddEventPreviewState();
}

class _DealAddEventPreviewState extends State<DealAddEventPreview> {
  final GlobalKey _keyEnsureThresholdsVisible = GlobalKey();

  void _toggleVisibility(bool val) {
    widget.bloc.setVisibilityType(val == true
        ? DealVisibilityType.Marketplace
        : DealVisibilityType.InviteOnly);

    if (val) {
      Future.delayed(
          Duration(milliseconds: 250),
          () => Scrollable.ensureVisible(
                _keyEnsureThresholdsVisible.currentContext,
              ));
    }
  }

  void _save([bool publish = true]) {
    showSharedLoadingLogo(context);

    widget.bloc.save(publish).then((success) {
      Navigator.of(context).pop();

      if (!success) {
        showSharedModalError(
          context,
          title: "Unable to save this event",
          subTitle: "Please try again in a few moments",
        );
      } else if (!publish) {
        showSharedModalAlert(context, Text("Event Draft Saved"), actions: [
          ModalAlertAction(
            label: "Okay!",
            onPressed: () => Navigator.of(context).pushNamedAndRemoveUntil(
                AppRouting.getDealsActive, (Route<dynamic> route) => false),
          )
        ]);
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: ListView(
        children: <Widget>[
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 16),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.start,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                _buildDetails(),
                _buildArtwork(),
                _buildPostRequirements(),
              ],
            ),
          ),
          _buildInvites(),
          _buildPublicEventSettings(),
        ],
      ),
      bottomNavigationBar: Container(
        height: 84,
        padding: EdgeInsets.only(
            bottom: MediaQuery.of(context).padding.bottom,
            left: 16,
            right: 16,
            top: 16),
        decoration: BoxDecoration(color: Theme.of(context).appBarTheme.color),
        child: Row(
          children: <Widget>[
            Expanded(
              child: PrimaryButton(
                buttonColor: Theme.of(context).primaryColor,
                context: context,
                label: "Publish & Send",
                onTap: _save,
              ),
            ),
            SizedBox(width: 8),
            SecondaryButton(
              context: context,
              label: "Save Draft",
              onTap: () => _save(false),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildDetails() => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: CachedNetworkImage(
              imageUrl: widget.bloc.media.value != null
                  ? widget.bloc.media.value.previewUrl
                  : "",
              imageBuilder: (context, imageProvider) => Container(
                height: 200,
                width: double.infinity,
                decoration: BoxDecoration(
                    image: DecorationImage(
                        fit: BoxFit.cover, image: imageProvider)),
              ),
              errorWidget: (context, url, error) => ImageError(
                logUrl: url,
                logParentName:
                    'deal/widgets/add/event_preview.dart > event image',
              ),
            ),
          ),
          SizedBox(height: 16.0),
          Text(
            widget.bloc.title.value,
            style: Theme.of(context).textTheme.bodyText2.merge(
                  TextStyle(
                      fontWeight: FontWeight.w500,
                      fontSize: 24.0,
                      color: Theme.of(context).textTheme.bodyText2.color),
                ),
          ),
          SizedBox(height: 6.0),
          Text(widget.bloc.description.value,
              style: Theme.of(context).textTheme.bodyText2),
          SizedBox(height: 8),
          Row(
            children: <Widget>[
              Chip(
                avatar: CachedNetworkImage(
                  imageUrl: appState.currentProfile.profilePicture,
                  imageBuilder: (context, imageProvider) => CircleAvatar(
                    backgroundColor: Theme.of(context).canvasColor,
                    backgroundImage: imageProvider,
                  ),
                  errorWidget: (context, url, error) => ImageError(
                    logUrl: url,
                    logParentName:
                        'deal/widgets/add/event_preview.dart > profile avatar',
                  ),
                ),
                backgroundColor: Theme.of(context).scaffoldBackgroundColor,
                shape: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(40),
                  borderSide: BorderSide(
                    width: 1.0,
                    color: Theme.of(context).primaryColor,
                  ),
                ),
                labelStyle: Theme.of(context).textTheme.bodyText1.merge(
                      TextStyle(
                        color: Theme.of(context).primaryColor,
                      ),
                    ),
                label: Text(appState.currentProfile.userName),
              ),
              SizedBox(width: 8),
              Flexible(
                child: Chip(
                    avatar: Icon(
                      AppIcons.mapMarkerAltSolid,
                      color: Theme.of(context).primaryColor,
                      size: 18,
                    ),
                    backgroundColor: Theme.of(context).scaffoldBackgroundColor,
                    shape: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(40),
                      borderSide: BorderSide(
                        width: 1.0,
                        color: Theme.of(context).primaryColor,
                      ),
                    ),
                    labelStyle: Theme.of(context).textTheme.bodyText1.merge(
                          TextStyle(color: Theme.of(context).primaryColor),
                        ),
                    label: Text(
                        widget.bloc.place.value.name ?? "No location name",
                        overflow: TextOverflow.ellipsis)),
              ),
            ],
          ),
          SizedBox(height: 4),
          Divider(height: 16),
        ],
      );

  Widget _buildArtwork() => StreamBuilder<List<PublisherApprovedMedia>>(
        stream: widget.bloc.artwork,
        builder: (context, snapshot) =>
            snapshot.data == null || snapshot.data.isEmpty
                ? Container()
                : Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Padding(
                        padding: EdgeInsets.only(bottom: 8.0, top: 8),
                        child: Text(
                          "Pre-event Artwork",
                          style: Theme.of(context).textTheme.bodyText1,
                        ),
                      ),
                      Container(
                          height: 120,
                          child: ListView(
                              scrollDirection: Axis.horizontal,
                              children: snapshot.data
                                  .map((PublisherApprovedMedia media) =>
                                      _buildArtworkMedia(media))
                                  .toList()))
                    ],
                  ),
      );

  Widget _buildArtworkMedia(PublisherApprovedMedia media) => Padding(
        padding: EdgeInsets.only(right: 8),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(8),
          child: CachedNetworkImage(
            imageUrl: media.previewUrl,
            imageBuilder: (context, imageProvider) => Container(
              height: 120,
              width: 120 * 0.5625,
              decoration: BoxDecoration(
                image: DecorationImage(
                  fit: BoxFit.cover,
                  image: imageProvider,
                ),
              ),
            ),
            errorWidget: (context, url, error) => ImageError(
              logUrl: url,
              logParentName:
                  'deal/widgets/add/event_preview.dart > _buildArtworkMedia',
            ),
          ),
        ),
      );

  Widget _buildPostRequirements() => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Padding(
            padding: EdgeInsets.only(bottom: 8.0, top: 16),
            child: Text(
              "Event Post Requirements",
              style: Theme.of(context).textTheme.bodyText1,
            ),
          ),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              _buildPostCountBox(
                  'Instagram\nStories',
                  widget.bloc.stories.value != null
                      ? widget.bloc.stories.value.toDouble()
                      : 0.0),
              SizedBox(width: 8),
              _buildPostCountBox(
                  'Instagram\nPosts',
                  widget.bloc.posts.value != null
                      ? widget.bloc.posts.value.toDouble()
                      : 0.0)
            ],
          ),
          Text(widget.bloc.receiveNotes.value ?? ""),
          SizedBox(height: 4),
          Divider(height: 24),
        ],
      );

  Widget _buildPostCountBox(String title, double value) => Expanded(
          child: Container(
        margin: EdgeInsets.only(bottom: 8),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(8),
          color: Theme.of(context).brightness == Brightness.dark
              ? Theme.of(context).appBarTheme.color
              : Colors.grey.shade200,
        ),
        padding: EdgeInsets.all(16),
        child: insightsBigStat(
            context: context,
            value: value,
            formatAsInt: true,
            labelColor: Theme.of(context).textTheme.bodyText1.color,
            label: title),
      ));

  Widget _buildInvites() => widget.bloc.invites.value != null &&
          widget.bloc.invites.value.isNotEmpty
      ? Column(
          children: <Widget>[
            Container(
              width: double.infinity,
              padding: EdgeInsets.only(left: 16, bottom: 16, top: 8, right: 8),
              child: Text(
                "RSVP Invites",
                style: Theme.of(context).textTheme.bodyText1,
              ),
            ),
            Column(
              children: widget.bloc.invites.value
                  .map((PublisherAccount u) => ListTile(
                        leading: UserAvatar(u),
                        title: Text(u.userName,
                            style: Theme.of(context).textTheme.bodyText2.merge(
                                TextStyle(
                                    color: Theme.of(context)
                                        .textTheme
                                        .bodyText2
                                        .color))),
                        subtitle: Text(u.nameDisplay,
                            style: Theme.of(context).textTheme.caption.merge(
                                TextStyle(color: Theme.of(context).hintColor))),
                      ))
                  .toList(),
            ),
            SizedBox(height: 16)
          ],
        )
      : Container();

  Widget _buildPublicEventSettings() => Column(
        children: <Widget>[
          StreamBuilder<DealVisibilityType>(
              stream: widget.bloc.visibilityType,
              builder: (context, visibilityType) {
                return Column(
                  children: <Widget>[
                    Padding(
                      padding: EdgeInsets.only(left: 16, right: 16),
                      child:

                          /// if we don't have any invites then this must be a public deal
                          /// so there will be no option for the user to change it
                          widget.bloc.invites.value == null ||
                                  widget.bloc.invites.value.isEmpty
                              ? Row(
                                  mainAxisAlignment: MainAxisAlignment.start,
                                  children: <Widget>[
                                    Expanded(
                                      child: Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: <Widget>[
                                          Text("Public Event",
                                              style: Theme.of(context)
                                                  .textTheme
                                                  .bodyText1),
                                          SizedBox(height: 4),
                                          Text(
                                              "Thresholds for who can request this event",
                                              style: Theme.of(context)
                                                  .textTheme
                                                  .caption
                                                  .merge(TextStyle(
                                                      color: Theme.of(context)
                                                          .hintColor))),
                                        ],
                                      ),
                                    ),
                                  ],
                                )
                              : DealTextToggle(
                                  labelText: "Public Event",
                                  subtitleText:
                                      "Discoverable by Creators on RYDR",
                                  selected: visibilityType.data ==
                                      DealVisibilityType.Marketplace,
                                  onChange: _toggleVisibility,
                                ),
                    ),
                    visibilityType.data == DealVisibilityType.Marketplace
                        ? Column(
                            children: <Widget>[
                              SizedBox(height: 24),
                              DealInputFollowerCount(
                                valueStream: widget.bloc.followerCount,
                                handleUpdate: widget.bloc.setFollowerCount,
                              ),
                              DealInputEngagementRating(
                                valueStream: widget.bloc.engagementRating,
                                handleUpdate: widget.bloc.setEngagementRating,
                              ),
                              DealThresholdInfo(
                                dealType: DealType.Event,
                                engagementRatingStream:
                                    widget.bloc.engagementRating,
                                followerCountStream: widget.bloc.followerCount,
                              ),
                              DealInputAutoApprove(
                                valueStream: widget.bloc.autoApprove,
                                quantityStream: null,
                                handleUpdate: widget.bloc.setAutoApprove,
                                dealType: DealType.Event,
                                canApproveUnlimited: true,
                              ),
                              DealInputAge(
                                valueStream: widget.bloc.age,
                                handleUpdate: widget.bloc.setAge,
                              ),
                            ],
                          )
                        : Container(),
                  ],
                );
              }),
          SizedBox(
            height: 16,
            key: _keyEnsureThresholdsVisible,
          ),
        ],
      );
}
