import 'package:flutter/material.dart';
import 'package:percent_indicator/linear_percent_indicator.dart';
import 'package:rydr_app/models/responses/publisher_insights_locations.dart';
import 'package:rydr_app/ui/profile/blocs/insights_followers.dart';

import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/models/publisher_insights_location.dart';

class ProfileInsightsLocations extends StatelessWidget {
  final InsightsFollowerBloc bloc;

  final Map<String, String> _pageContent = {
    "cities": "Cities",
    "countries": "Countries",
  };

  ProfileInsightsLocations(this.bloc);

  @override
  Widget build(BuildContext context) {
    final bool isMe = appState.currentProfile.id == bloc.profile.id;
    final String profileUsername = bloc.profile.userName;

    return Column(
      children: <Widget>[
        /// header of the data / graph widget will show always,
        /// e.g. while loading, after load and regardles of success or error
        _buildHeader(context, isMe, profileUsername),

        StreamBuilder<InsightsFollowersLocationsData>(
          stream: bloc.dataLocations,
          builder: (context, snapshot) {
            return snapshot.connectionState == ConnectionState.waiting
                ? insightsLoadingBody()
                : snapshot.data.locationsResponse.error != null
                    ? insightsErrorBody(snapshot.data.locationsResponse.error,
                        () {
                        bloc.loadlocations(true);
                      })
                    : snapshot.data.locationsResponseWithData.cities.length ==
                                0 ||
                            snapshot.data.locationsResponseWithData.countries
                                    .length ==
                                0
                        ? insightsNoResults(
                            context, bloc.strings.followerGrowthNoResults, null)
                        : _buildResultsBody(context, snapshot.data);
          },
        ),
      ],
    );
  }

  Widget _buildPercentContainer(
    BuildContext context,
    int index,
    PublisherInsightsLocationsResponseWithData locationsResponse,
    bool dark,
    String label,
    String dealCity,
    int value,
  ) {
    /// Not the most stable comparison, but if the city matches the label
    /// we're passing then we found a match for a given deal (if we have one)
    /// againts a city that this user has followers in and we can highlight it in the list
    final bool isDealCity = dealCity != null ? dealCity == label : false;

    /// calculate the scale factor of each city compared to the users followers
    final double scaleFactorCity = 1.0 / locationsResponse.topCity;
    final double scaleFactorCountry = 1.0 / locationsResponse.topCountry;
    final double valuePercent = value / locationsResponse.followedBy;
    final double scaledPercentageCity = valuePercent * scaleFactorCity > 1.0
        ? 1.0
        : valuePercent * scaleFactorCity;
    final double scaledPercentageCountry =
        valuePercent * scaleFactorCountry > 1.0
            ? 1.0
            : valuePercent * scaleFactorCountry;
    final double gradientOpacityCity = scaledPercentageCity > 0.8
        ? 1.0
        : scaledPercentageCity < 0.3 ? 0.25 : scaledPercentageCity;
    final double gradientOpacityCountry = scaledPercentageCountry > 0.8
        ? 1.0
        : scaledPercentageCountry < 0.3 ? 0.25 : scaledPercentageCountry;

    return Padding(
      padding: EdgeInsets.only(bottom: 16.0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          Container(
            width: 110,
            margin: EdgeInsets.only(right: 4.0),
            child: Text(
              label,
              textAlign: TextAlign.left,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(
                        color: isDealCity
                            ? AppColors.successGreen
                            : Theme.of(context).textTheme.bodyText2.color,
                        height: 1.0,
                        fontWeight:
                            isDealCity ? FontWeight.w600 : FontWeight.normal),
                  ),
            ),
          ),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(top: 1.0),
              child: LinearPercentIndicator(
                animation: true,
                animationDuration: 550,
                lineHeight: 8.0,
                percent:
                    index == 0 ? scaledPercentageCity : scaledPercentageCountry,
                linearStrokeCap: LinearStrokeCap.roundAll,
                linearGradient: LinearGradient(
                    colors: isDealCity
                        ? [AppColors.successGreen, AppColors.successGreen]
                        : [
                            Theme.of(context).primaryColor.withOpacity(
                                  index == 0
                                      ? gradientOpacityCity
                                      : gradientOpacityCountry,
                                ),
                            Theme.of(context).primaryColor.withOpacity(
                                  index == 0
                                      ? gradientOpacityCity
                                      : gradientOpacityCountry,
                                )
                          ],
                    stops: [0.0, 1.0],
                    begin: Alignment.centerLeft,
                    end: Alignment.centerRight),
                backgroundColor: dark
                    ? Theme.of(context).appBarTheme.color
                    : Colors.grey.shade200,
              ),
            ),
          ),
          Container(
            width: 36.0,
            margin: EdgeInsets.only(left: 4.0),
            child: Text(
              Utils.formatDoubleForDisplay(value.toDouble()),
              textAlign: TextAlign.left,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(
                        color: isDealCity
                            ? AppColors.successGreen
                            : AppColors.grey300,
                        height: 1.0,
                        fontWeight:
                            isDealCity ? FontWeight.w600 : FontWeight.normal),
                  ),
            ),
          )
        ],
      ),
    );
  }

  /// header of widget which includes title and subtitle
  /// we'll re-use this to render the lo
  Widget _buildHeader(BuildContext context, bool isMe, String profileUsername) {
    return insightsSectionHeader(
      context: context,
      icon: AppIcons.chartBar,
      title: bloc.strings.followLocationTitle,
      subtitle: bloc.strings.followLocationsSubtitle,
    );
  }

  Widget _buildTopLocations(
    BuildContext context,
    int index,
    PublisherInsightsLocationsResponseWithData locationsResponse,
    List<PublisherInsightsLocation> locations,
    String dealCity,
  ) {
    return Row(
      children: <Widget>[
        Expanded(
          child: insightsBigStat(
            context: context,
            countColor: dealCity != null
                ? dealCity == locations[0].nameCleaned
                    ? AppColors.successGreen
                    : AppColors.grey800
                : AppColors.grey800,
            value: index == 0
                ? locations[0].percentage(locationsResponse.followedBy)
                : locationsResponse.countries[0]
                    .percentage(locationsResponse.followedBy),
            formatAsPercentage: true,
            label: index == 0
                ? locations[0].nameCleaned
                : locationsResponse.countries[0].nameCleaned,
          ),
        ),
        locations.length > 1
            ? Expanded(
                child: insightsBigStat(
                  context: context,
                  countColor: dealCity != null
                      ? dealCity == locations[1].nameCleaned
                          ? AppColors.successGreen
                          : AppColors.grey800
                      : AppColors.grey800,
                  value: index == 0
                      ? locations[1].percentage(locationsResponse.followedBy)
                      : locationsResponse.countries[1]
                          .percentage(locationsResponse.followedBy),
                  formatAsPercentage: true,
                  label: index == 0
                      ? locations[1].nameCleaned
                      : locationsResponse.countries[1].nameCleaned,
                ),
              )
            : Container(),
      ],
    );
  }

  Widget _buildResultsBody(
    BuildContext context,
    InsightsFollowersLocationsData data,
  ) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final String dealCity =
        bloc.deal != null ? bloc.deal.place.address.city : null;
    final List<PublisherInsightsLocation> locations = data.index == 0
        ? data.locationsResponseWithData.cities
        : data.locationsResponseWithData.countries;

    return Column(
      children: <Widget>[
        Padding(
            padding: EdgeInsets.only(
                left: 16.0, top: 40.0, right: 16.0, bottom: 24.0),
            child: AnimatedCrossFade(
              duration: Duration(milliseconds: 250),
              crossFadeState: data.index == 0
                  ? CrossFadeState.showFirst
                  : CrossFadeState.showSecond,
              firstChild: _buildTopLocations(
                context,
                data.index,
                data.locationsResponseWithData,
                data.locationsResponseWithData.cities,
                dealCity,
              ),
              secondChild: _buildTopLocations(
                context,
                data.index,
                data.locationsResponseWithData,
                data.locationsResponseWithData.cities,
                dealCity,
              ),
            )),
        AnimatedSwitcher(
            switchOutCurve: Interval(
              0.6,
              1.0,
              curve: Curves.fastOutSlowIn,
            ),
            switchInCurve: Interval(
              0.6,
              1.0,
              curve: Curves.fastOutSlowIn,
            ),
            duration: Duration(milliseconds: 550),
            child: Container(
              padding: EdgeInsets.symmetric(horizontal: 16.0, vertical: 32.0),
              child: Column(
                children: locations.map((e) {
                  return _buildPercentContainer(
                    context,
                    data.index,
                    data.locationsResponseWithData,
                    dark,
                    e.nameCleaned,
                    dealCity,
                    e.value,
                  );
                }).toList(),
              ),
            )),
        insightsToggleButtons(
          context,
          [
            _pageContent['cities'],
            _pageContent['countries'],
          ],
          data.index,
          bloc.setShowIndexLocations,
        ),
        SizedBox(height: 16)
      ],
    );
  }
}
