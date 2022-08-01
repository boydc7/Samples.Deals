import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';
import 'package:rydrworkspaces/models/publisher_insights_age_gender.dart';

class PublisherInsightsAgeAndGenderResponse {
  List<PublisherInsightsAgeAndGender> ageAndGender;
  PublisherAccount profile;
  DioError error;

  List<PublisherInsightsAgeAndGender> males;
  List<PublisherInsightsAgeAndGender> females;
  List<PublisherInsightsAgeAndGender> unknown;
  int followedBy;
  int totalFollowedByGenders;
  int maxValueMale;
  int maxValueFemale;
  int maxValueUnknown;
  int maxValue;
  int totalFollowersMale;
  int totalFollowersFemale;
  int totalFollowersUnknown;
  String totalFollowersMalePercent;
  String totalFollowersFemalePercent;
  String totalFollowersUnknownPercent;

  double topAmount;
  double scaleFactor;

  bool hasResults = false;

  PublisherInsightsAgeAndGenderResponse(
      this.profile, this.ageAndGender, this.error);

  PublisherInsightsAgeAndGenderResponse.fromResponse(
      PublisherAccount profile, Map<String, dynamic> json) {
    ageAndGender = json['results'] != null
        ? json['results']
            .map((dynamic d) => PublisherInsightsAgeAndGender.fromJson(d))
            .cast<PublisherInsightsAgeAndGender>()
            .toList()
        : [];

    if (ageAndGender.isEmpty == false) {
      followedBy = profile.publisherMetrics.followedBy.toInt();

      males = ageAndGender
          .where((PublisherInsightsAgeAndGender ag) => ag.gender == "M")
          .toList();
      females = ageAndGender
          .where((PublisherInsightsAgeAndGender ag) => ag.gender == "F")
          .toList();
      unknown = ageAndGender
          .where((PublisherInsightsAgeAndGender ag) => ag.gender == "U")
          .toList();

      totalFollowersMale = males.isEmpty
          ? 0
          : males
              .map((v) => int.parse(v.value.toString()))
              .reduce((a, b) => a + b);
      totalFollowersFemale = females.isEmpty
          ? 0
          : females
              .map((v) => int.parse(v.value.toString()))
              .reduce((a, b) => a + b);
      totalFollowersUnknown = unknown.isEmpty
          ? 0
          : unknown
              .map((v) => int.parse(v.value.toString()))
              .reduce((a, b) => a + b);

      totalFollowedByGenders =
          totalFollowersMale + totalFollowersFemale + totalFollowersUnknown;

      totalFollowersMalePercent =
          ((totalFollowersMale / totalFollowedByGenders) * 100)
              .toStringAsFixed(1);
      totalFollowersFemalePercent =
          ((totalFollowersFemale / totalFollowedByGenders) * 100)
              .toStringAsFixed(1);
      totalFollowersUnknownPercent =
          ((totalFollowersUnknown / totalFollowedByGenders) * 100)
              .toStringAsFixed(1);

      maxValueMale = males.isEmpty
          ? 0
          : males.length > 1
              ? males
                  .reduce((curr, next) => curr.value > next.value ? curr : next)
                  .value
              : males[0].value;

      maxValueFemale = females.isEmpty
          ? 0
          : females.length > 1
              ? females
                  .reduce((curr, next) => curr.value > next.value ? curr : next)
                  .value
              : females[0].value;

      maxValueUnknown = unknown.isEmpty
          ? 0
          : unknown.length > 1
              ? unknown
                  .reduce((curr, next) => curr.value > next.value ? curr : next)
                  .value
              : unknown[0].value;

      maxValue = [maxValueMale, maxValueFemale, maxValueUnknown]
          .reduce((curr, next) => curr > next ? curr : next);
      topAmount = maxValue.toDouble();
      scaleFactor = 1.0 / topAmount;

      hasResults = true;
    }
  }

  PublisherInsightsAgeAndGenderResponse.withError(DioError error)
      : profile = null,
        ageAndGender = null,
        error = error;
}
