import 'package:rydr_app/models/publisher_insights_age_gender.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PublisherInsightsAgeAndGenderResponse
    extends BaseResponses<PublisherInsightsAgeAndGender> {
  PublisherInsightsAgeAndGenderResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) =>
                        PublisherInsightsAgeAndGender.fromJson(d))
                    .cast<PublisherInsightsAgeAndGender>()
                    .toList()
                : []);
}

class PublisherInsightsAgeAndGenderResponseWithData {
  List<PublisherInsightsAgeAndGender> ageAndGender;
  PublisherAccount profile;

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

  PublisherInsightsAgeAndGenderResponseWithData(
    this.ageAndGender,
    this.profile,
  ) {
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
}
