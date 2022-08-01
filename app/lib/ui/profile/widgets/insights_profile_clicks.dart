import 'package:flutter/material.dart';
import 'package:rydr_app/app/strings.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/publisher_insights.dart';
import 'package:rydr_app/models/responses/publisher_insights_growth.dart';
import 'package:rydr_app/ui/profile/blocs/insights_profile.dart';

import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_insights_growth.dart';

import 'package:rydr_app/ui/profile/widgets/insights_chart.dart';

class ProfileInsightsProfileClicks extends StatefulWidget {
  final bool loading;
  final PublisherAccount profile;
  final PublisherInsightsGrowthResponse growthResponse;
  final InsightsProfileBloc bloc;

  ProfileInsightsProfileClicks({
    @required this.loading,
    @required this.profile,
    @required this.growthResponse,
    @required this.bloc,
  });

  @override
  _ProfileInsightsProfileClicksState createState() =>
      _ProfileInsightsProfileClicksState();
}

class _ProfileInsightsProfileClicksState
    extends State<ProfileInsightsProfileClicks> {
  AppStrings _strings;
  @override
  initState() {
    super.initState();

    _strings = AppStrings(widget.profile);
  }

  @override
  Widget build(BuildContext context) {
    /// not available for a business looking at a creator
    if (appState.currentProfile.isBusiness && widget.profile.isCreator) {
      return Container();
    }

    return Column(
      children: <Widget>[
        /// header of the data / graph widget will show always,
        /// e.g. while loading, after load and regardles of success or error
        Visibility(
          visible: widget.loading,
          child: insightsSectionHeader(
            context: context,
            icon: AppIcons.chartLine,
            title: _strings.profileInteractionsTapsTitle,
            subtitle: _strings.profileInteractionsSubtitle,
          ),
        ),

        /// body of the widget will show different states depending on loading (shimmer)
        /// completed with error (retry widget), or success (actual data/graph)
        widget.loading
            ? insightsLoadingBody()
            : widget.growthResponse.models == null
                ? insightsNoResults(
                    context, _strings.profileProfileInteractionsNoResults, null)
                : StreamBuilder<int>(
                    stream: widget.bloc.showIndex,
                    builder: (context, snapshot) {
                      return _buildResultsBody(snapshot.data ?? 0);
                    },
                  ),
      ],
    );
  }

  /// header of widget which includes title, subtitle, and bottom sheet
  /// this will be rendered while and after loading is completed
  Widget _buildHeader(int showIndex, bool hasWebsiteData, bool hasEmailData,
      bool hasPhoneData, bool hasTxtData) {
    final List<bool> tapData = [
      hasWebsiteData,
      hasEmailData,
      hasPhoneData,
      hasTxtData
    ];
    var tapDataTrue = tapData.where((d) => d == true);
    final List<InsightsBottomSheetTile> allBottomTiles = [
      hasWebsiteData
          ? InsightsBottomSheetTile(
              _strings.websiteTaps,
              _strings.profileInteractionsWebsiteTapsDescription,
            )
          : null,
      hasEmailData
          ? InsightsBottomSheetTile(
              _strings.emailTaps,
              _strings.profileInteractionsEmailTapsDescription,
            )
          : null,
      hasPhoneData
          ? InsightsBottomSheetTile(
              _strings.phoneTaps,
              _strings.profileInteractionsPhoneTapsDescription,
            )
          : null,
      hasTxtData
          ? InsightsBottomSheetTile(
              _strings.textTaps,
              _strings.profileInteractionsTextTapsDescription,
            )
          : null,
    ];
    var nonNullBottomTiles = allBottomTiles.where((d) => d != null);
    final List<InsightsBottomSheetTile> actualBottomTiles =
        List.from(nonNullBottomTiles);

    return insightsSectionHeader(
      context: context,
      icon: AppIcons.chartLine,
      title: _strings.profileInteractionsTapsTitle,
      subtitle: _strings.profileInteractionsSubtitle,
      bottomSheetTitle: tapDataTrue.length != 0
          ? _strings.profileInteractionsTapSheetTitle
          : null,
      bottomSheetSubtitle: _strings.profileInteractionsSubtitle,
      bottomSheetWidget: Visibility(
        visible: tapDataTrue.length > 0,
        child: insightsBottomSheet(context, actualBottomTiles),
      ),
      initialRatio: tapDataTrue.length == 1
          ? 0.3
          : tapDataTrue.length == 2
              ? 0.37
              : tapDataTrue.length == 3
                  ? 0.44
                  : tapDataTrue.length == 4 ? 0.52 : 0.65,
    );
  }

  Widget _buildResultsBody(int showIndex) {
    final PublisherInsightsGrowthSummary summaryWebsite =
        PublisherInsightsGrowthSummary(
      widget.growthResponse.models,
      ProfileGrowthType.WebsiteClicks,
      showIndex == 0 ? 7 : 30,
    );

    final PublisherInsightsGrowthSummary summaryEmail =
        PublisherInsightsGrowthSummary(
      widget.growthResponse.models,
      ProfileGrowthType.EmailContacts,
      showIndex == 0 ? 7 : 30,
    );

    final PublisherInsightsGrowthSummary summaryPhone =
        PublisherInsightsGrowthSummary(
      widget.growthResponse.models,
      ProfileGrowthType.PhoneCallClicks,
      showIndex == 0 ? 7 : 30,
    );

    final PublisherInsightsGrowthSummary summaryTxt =
        PublisherInsightsGrowthSummary(
      widget.growthResponse.models,
      ProfileGrowthType.TextMessageClicks,
      showIndex == 0 ? 7 : 30,
    );

    /// indicator of what data we have
    final bool hasWebsiteData = summaryWebsite.max.total > 0;
    final bool hasEmailData = summaryEmail.max.total > 0;
    final bool hasPhoneData = summaryPhone.max.total > 0;
    final bool hasTxtData = summaryTxt.max.total > 0;

    /// only add datasets for anything that has actually some data
    final List<ChartData> data = [
      hasWebsiteData
          ? ChartData(
              dataColor: chartDataColor.blue,
              data: summaryWebsite.flSpots,
              maxY: summaryWebsite.max.total,
              minY: summaryWebsite.min.total,
            )
          : null,
      hasEmailData
          ? ChartData(
              dataColor: chartDataColor.green,
              data: summaryEmail.flSpots,
              maxY: summaryEmail.max.total,
              minY: summaryEmail.min.total,
            )
          : null,
      hasPhoneData
          ? ChartData(
              dataColor: chartDataColor.red,
              data: summaryPhone.flSpots,
              maxY: summaryPhone.max.total,
              minY: summaryPhone.min.total,
            )
          : null,
      hasTxtData
          ? ChartData(
              dataColor: chartDataColor.teal,
              data: summaryTxt.flSpots,
              maxY: summaryTxt.max.total,
              minY: summaryTxt.min.total,
            )
          : null
    ].where((d) => d != null).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        _buildHeader(
            showIndex, hasWebsiteData, hasEmailData, hasPhoneData, hasTxtData),
        hasEmailData || hasPhoneData || hasWebsiteData || hasTxtData
            ? ProfileInsightsChart(
                dates: summaryWebsite.dates,
                data: data,
              )
            : insightsNoGraph(context),
        insightsToggleButtons(
          context,
          [
            _strings.sevenDays,
            _strings.thirtyDays,
          ],
          showIndex,
          widget.bloc.setShowIndex,
        ),
        SizedBox(height: 12),
        Divider(height: 1, indent: 16.0, endIndent: 16.0),
        SizedBox(height: 4),
        hasWebsiteData
            ? insightsGrowthMinMax(
                context,
                summary: summaryWebsite,
                title: _strings.totalWebsiteTaps,
                color: Theme.of(context).primaryColor,
              )
            : Container(),
        hasPhoneData
            ? insightsGrowthMinMax(
                context,
                summary: summaryPhone,
                title: _strings.totalPhoneTaps,
                color: AppColors.errorRed,
              )
            : Container(),
        hasEmailData
            ? insightsGrowthMinMax(
                context,
                summary: summaryEmail,
                title: _strings.totalEmailTaps,
                color: AppColors.successGreen,
              )
            : Container(),
        hasTxtData
            ? insightsGrowthMinMax(
                context,
                summary: summaryTxt,
                title: _strings.totalTextTaps,
                color: AppColors.teal,
              )
            : Container(),
      ],
    );
  }
}
