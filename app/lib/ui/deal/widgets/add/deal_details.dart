import 'package:flutter/material.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/blocs/add_deal.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_choose_threshold.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_choose_visibility.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_continue_button.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_date.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_description.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_invites.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_places.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_posts.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_receive_notes.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_section_insights.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_section_invite_only.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_section_thresholds.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_stories.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_tags.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_title.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_value.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_virtual.dart';

class DealAddDetails extends StatefulWidget {
  final DealAddBloc bloc;
  final Deal dealToCopy;
  final Function handleContinue;

  DealAddDetails({
    @required this.bloc,
    @required this.handleContinue,
    this.dealToCopy,
  });

  @override
  _DealAddDetailsState createState() => _DealAddDetailsState();
}

class _DealAddDetailsState extends State<DealAddDetails> {
  final ScrollController _scrollController = ScrollController();
  final GlobalKey _keyEnsureDescriptionVisible = GlobalKey();
  final GlobalKey _keyEnsureReceiveNotesVisible = GlobalKey();

  @override
  void initState() {
    super.initState();

    /// listen for when description or receive notes receive focus
    /// and then scroll so that even with the onscreen keyboard we can still see the
    /// icons for quick-fill and history is visible to the user
    widget.bloc.focusDescription.listen((val) {
      if (val) {
        Future.delayed(
            Duration(milliseconds: 250),
            () => Scrollable.ensureVisible(
                  _keyEnsureDescriptionVisible.currentContext,
                ));
      }
    });

    widget.bloc.focusReceiveNotes.listen((val) {
      if (val) {
        Future.delayed(
            Duration(milliseconds: 250),
            () => Scrollable.ensureVisible(
                _keyEnsureReceiveNotesVisible.currentContext));
      }
    });
  }

  @override
  void dispose() {
    _scrollController.dispose();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) => StreamBuilder<DealType>(
      stream: widget.bloc.dealType,
      builder: (context, dt) {
        final DealType dealType = dt.data ?? DealType.Deal;

        return ListView(
          controller: _scrollController,
          children: <Widget>[
            DealInputPlace(
              handlePlaceChange: widget.bloc.setPlace,
              valueStream: widget.bloc.place,
              dealType: dealType,
            ),
            DealInputVirtual(
              valueStream: widget.bloc.dealType,
              bloc: widget.bloc,
            ),
            DealInputTitle(
              valueStream: widget.bloc.title,
              handleUpdate: widget.bloc.setTitle,
              handleUpdateFocus: widget.bloc.setFocusTitle,
              focusStream: widget.bloc.focusTitle,
              charStream: widget.bloc.charTitle,
              mediaStream: widget.bloc.media,
              handleUpdateMedia: widget.bloc.setMedia,
              dealType: dealType,
            ),
            Container(
              key: _keyEnsureDescriptionVisible,
              child: SizedBox(height: 16.0),
            ),
            DealInputDescription(
              valueStream: widget.bloc.description,
              handleUpdate: widget.bloc.setDescription,
              handleUpdateFocus: widget.bloc.setFocusDescription,
              focusStream: widget.bloc.focusDescription,
              charStream: widget.bloc.charDescription,
              dealType: dealType,
            ),
            DealInputValue(
              valueStream: widget.bloc.value,
              focusStream: widget.bloc.focusCostOfGoods,
              handleUpdate: widget.bloc.setValue,
              handleUpdateFocus: widget.bloc.setFocusCostOfGoods,
              dealType: dealType,
            ),
            DealInputDate(
              labelText: "Expiration Date",
              emtpyText: widget.bloc.expirationDate.value == null
                  ? "Never Expires"
                  : "Choose an expiration date",
              value: widget.bloc.expirationDate,
              handleUpdate: widget.bloc.setExpirationDate,
            ),

            DealInputTags(
              handleUpdate: widget.bloc.setTags,
              valueStream: widget.bloc.tags,
            ),

            /// renders two boxes to allow for making a choice between
            /// "marketplace" and "invitation only" setup
            DealAddVisibilitySection(
              valueStream: widget.bloc.visibilityType,
              handleUpdate: widget.bloc.setVisibilityType,
              canUseInvites: widget.bloc.canUseInvites,
            ),

            /// This would show if we selected "Marketplace"
            /// and will let the user choose between "Followers & Engagement" or "Insights"
            ///
            /// Current: This only contains the "Choosing Creators" header
            DealAddThresholdSection(
              valueStream: widget.bloc.visibilityType,
              thresholdValueStream: widget.bloc.thresholdType,
              handleUpdate: widget.bloc.setVisibilityType,
              canUseInsights: widget.bloc.canUseInsights,
            ),

            /// this would show if we selected "Followers & Engagement"
            ///
            /// Current: This contains minimum follower count, min engagement rating,
            /// quantity, and toggle switches
            DealAddThresholdRestrictionsSection(widget.bloc),

            /// this would show if we selected "Insights"
            ///
            /// Current: Turned off completely
            DealAddThresholdInsightsSection(widget.bloc),

            /// only show invites by themselves (not part of invitesOnlySection)
            /// if we are in fact not currently on deal visibility invitesonly
            ///
            /// Current: This contains the invites section for BOTH options
            StreamBuilder<DealVisibilityType>(
                stream: widget.bloc.visibilityType,
                builder: (context, snapshot) => snapshot.data != null &&
                        snapshot.data == DealVisibilityType.Marketplace
                    ? DealAddInvitesSection(widget.bloc)
                    : snapshot.data != null
                        ? DealAddInviteOnlySection(widget.bloc)
                        : Container()),

            /// only show the exchange section (receive posts / story count & content guildelines)
            /// if we are able to (mainly once the user has made a 'visibility' choice between marketplace/invites)
            StreamBuilder<bool>(
              stream: widget.bloc.canShowExchangeSection,
              builder: (context, snapshot) => Visibility(
                visible: snapshot.data != null && snapshot.data == true,
                child: Column(
                  children: <Widget>[
                    SizedBox(height: 32),
                    Text(
                      'In exchange for...',
                      style: Theme.of(context).textTheme.bodyText1,
                      textAlign: TextAlign.center,
                    ),
                    SizedBox(height: 2.0),
                    Text(
                      'What do you want the Creators to post?',
                      style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                      textAlign: TextAlign.center,
                    ),
                    SizedBox(height: 24),
                    DealInputStories(
                      valueStream: widget.bloc.stories,
                      handleUpdate: widget.bloc.setStories,
                    ),
                    DealInputPosts(
                      valueStream: widget.bloc.posts,
                      handleUpdate: widget.bloc.setPosts,
                    ),

                    /// ensure we scroll to just above the content guidelines
                    /// once we receive focus in the field, that way the icons for history will show
                    Container(
                      key: _keyEnsureReceiveNotesVisible,
                      child: SizedBox(height: 0),
                    ),
                    DealInputReceiveNotes(
                      dealType: dealType,
                      valueStream: widget.bloc.receiveNotes,
                      handleUpdate: widget.bloc.setReceiveNotes,
                      handleUpdateFocus: widget.bloc.setFocusReceiveNotes,
                      focusStream: widget.bloc.focusReceiveNotes,
                      placeName: widget.bloc.place.value?.name,
                    ),
                    SizedBox(height: 4),
                  ],
                ),
              ),
            ),
            DealAddContinue(
              canPreviewStream: widget.bloc.canPreview,
              handleTap: widget.handleContinue,
              dealType: dealType,
            ),
          ],
        );
      });
}
