import 'dart:async';

import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/strings.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/publisher_insights.dart';
import 'package:rydr_app/models/publisher_insights_age_gender.dart';
import 'package:rydr_app/models/publisher_insights_growth.dart';
import 'package:rydr_app/models/responses/publisher_insights_age_gender.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/publisher_insights_growth.dart';
import 'package:rydr_app/models/responses/publisher_insights_locations.dart';
import 'package:rydr_app/services/publisher_insights.dart';
import 'package:rydr_app/ui/profile/widgets/insights_chart.dart';

class InsightsFollowerBloc {
  final _dataAgeGender = BehaviorSubject<InsightsFollowersAgeData>();
  final _dataGrowth = BehaviorSubject<InsightsFollowersData>();
  final _dataLocations = BehaviorSubject<InsightsFollowersLocationsData>();

  AppStrings _strings;
  PublisherAccount _profile;
  Deal _deal;

  InsightsFollowerBloc(PublisherAccount profile, Deal deal) {
    _profile = profile;
    _deal = deal;
    _strings = AppStrings(profile);
  }

  dispose() {
    _dataAgeGender.close();
    _dataGrowth.close();
    _dataLocations.close();
  }

  PublisherAccount get profile => _profile;
  Deal get deal => _deal;
  AppStrings get strings => _strings;

  PublisherInsightsGrowthResponse resGrowth;
  PublisherInsightsLocationsResponse resLocations;

  BehaviorSubject<InsightsFollowersAgeData> get dataAgeGender =>
      _dataAgeGender.stream;

  BehaviorSubject<InsightsFollowersData> get dataGrowth => _dataGrowth.stream;

  BehaviorSubject<InsightsFollowersLocationsData> get dataLocations =>
      _dataLocations.stream;

  Future<void> load(bool forceRefresh) async {
    this.loadAgeGender(forceRefresh);
    this.loadGrowth(forceRefresh);
    this.loadlocations(forceRefresh);
  }

  Future<void> loadAgeGender(bool forceRefresh) async {
    List<BarChartGroupData> items = [];

    final PublisherInsightsAgeAndGenderResponse resAgeGender =
        await PublisherInsightsService.getAgeAndGender(
      _profile.id,
      forceRefresh: forceRefresh,
    );

    final PublisherInsightsAgeAndGenderResponseWithData resAgeGenderWithData =
        PublisherInsightsAgeAndGenderResponseWithData(
            resAgeGender.models, _profile);

    if (resAgeGenderWithData.hasResults) {
      items.add(_makeGroupData(0, "13-17", resAgeGenderWithData));
      items.add(_makeGroupData(1, "18-24", resAgeGenderWithData));
      items.add(_makeGroupData(2, "25-34", resAgeGenderWithData));
      items.add(_makeGroupData(3, "35-44", resAgeGenderWithData));
      items.add(_makeGroupData(4, "45-54", resAgeGenderWithData));
      items.add(_makeGroupData(5, "55-64", resAgeGenderWithData));
      items.add(_makeGroupData(6, "65+", resAgeGenderWithData));
    }

    _dataAgeGender.sink.add(InsightsFollowersAgeData(
      resAgeGender,
      resAgeGenderWithData,
      items,
    ));
  }

  Future<void> loadGrowth(bool forceRefresh) async {
    resGrowth = await PublisherInsightsService.getGrowth(
      _profile.id,
      forceRefresh: forceRefresh,
    );

    if (resGrowth.error == null) {
      setShowIndexGrowth(0);
    } else {
      _dataGrowth.sink.add(InsightsFollowersData(
          0,
          resGrowth,
          PublisherInsightsGrowthResponseWithData(resGrowth.models, _profile),
          null,
          null));
    }
  }

  Future<void> loadlocations(bool forceRefresh) async {
    resLocations = await PublisherInsightsService.getLocations(
      _profile.id,
      forceRefresh: forceRefresh,
    );

    setShowIndexLocations(0);
  }

  void setShowIndexGrowth(int index) {
    final PublisherInsightsGrowthResponseWithData resGrowthWithData =
        PublisherInsightsGrowthResponseWithData(resGrowth.models, _profile);

    var sum = PublisherInsightsGrowthSummary(
      resGrowth.models,
      ProfileGrowthType.Followers,
      index == 0 ? 7 : 30,
    );

    var chart = [
      ChartData(
        dataColor: chartDataColor.blue,
        data: sum.flSpots,
        maxY: sum.max.total,
        minY: sum.min.total,
      )
    ];

    _dataGrowth.sink.add(
        InsightsFollowersData(index, resGrowth, resGrowthWithData, sum, chart));
  }

  void setShowIndexLocations(int index) =>
      _dataLocations.sink.add(InsightsFollowersLocationsData(
          index,
          resLocations,
          PublisherInsightsLocationsResponseWithData(
              resLocations.models, _profile)));

  BarChartGroupData _makeGroupData(
    int x,
    String ageRange,
    PublisherInsightsAgeAndGenderResponseWithData response,
  ) {
    final double width = 8.0;
    final double scaleFactor = response.scaleFactor;
    final double y1 = response.males
        .firstWhere(
            (PublisherInsightsAgeAndGender ag) => ag.ageRange == ageRange,
            orElse: () {
          return PublisherInsightsAgeAndGender.fromJson({"value": 0});
        })
        .value
        .toDouble();
    final double y2 = response.females
        .firstWhere(
            (PublisherInsightsAgeAndGender ag) => ag.ageRange == ageRange,
            orElse: () {
          return PublisherInsightsAgeAndGender.fromJson({"value": 0});
        })
        .value
        .toDouble();
    final double y3 = response.unknown
        .firstWhere(
            (PublisherInsightsAgeAndGender ag) => ag.ageRange == ageRange,
            orElse: () {
          return PublisherInsightsAgeAndGender.fromJson({"value": 0});
        })
        .value
        .toDouble();

    final Color leftBarColor = y1 * scaleFactor <= 0.15
        ? AppColors.blue.withOpacity(0.5)
        : y1 * scaleFactor < 0.65 && y1 * scaleFactor > 0.15
            ? AppColors.blue.withOpacity(0.75)
            : AppColors.blue;
    final Color rightBarColor = y2 * scaleFactor <= 0.15
        ? AppColors.teal.withOpacity(0.5)
        : y2 * scaleFactor < 0.65 && y2 * scaleFactor > 0.15
            ? AppColors.teal.withOpacity(0.75)
            : AppColors.teal;
    final Color unknownBarColor = y3 * scaleFactor <= 0.15
        ? AppColors.grey300.withOpacity(0.5)
        : y3 * scaleFactor < 0.65 && y3 * scaleFactor > 0.15
            ? AppColors.grey300.withOpacity(0.75)
            : AppColors.grey300;

    return BarChartGroupData(barsSpace: 4, x: x, barRods: [
      BarChartRodData(
        y: y1,
        color: leftBarColor,
        width: width,
      ),
      BarChartRodData(
        y: y2,
        color: rightBarColor,
        width: width,
      ),
      BarChartRodData(
        y: y3,
        color: unknownBarColor,
        width: width,
      ),
    ]);
  }
}

class InsightsFollowersAgeData {
  final PublisherInsightsAgeAndGenderResponse ageAndGenderResponse;
  final PublisherInsightsAgeAndGenderResponseWithData
      ageAndGenderResponseWithData;
  final List<BarChartGroupData> ageGroups;

  InsightsFollowersAgeData(
    this.ageAndGenderResponse,
    this.ageAndGenderResponseWithData,
    this.ageGroups,
  );
}

class InsightsFollowersData {
  final int index;
  final PublisherInsightsGrowthResponse growthResponse;
  final PublisherInsightsGrowthResponseWithData growthResponseWithData;
  final PublisherInsightsGrowthSummary summary;
  final List<ChartData> data;

  InsightsFollowersData(
    this.index,
    this.growthResponse,
    this.growthResponseWithData,
    this.summary,
    this.data,
  );
}

class InsightsFollowersLocationsData {
  final int index;
  final PublisherInsightsLocationsResponse locationsResponse;
  final PublisherInsightsLocationsResponseWithData locationsResponseWithData;

  InsightsFollowersLocationsData(
    this.index,
    this.locationsResponse,
    this.locationsResponseWithData,
  );
}
