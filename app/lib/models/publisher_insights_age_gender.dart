class PublisherInsightsAgeAndGender {
  String ageRange;
  String gender;
  int value;

  PublisherInsightsAgeAndGender.fromJson(Map<String, dynamic> json) {
    ageRange = json['ageRange'];
    gender = json['gender'];
    value = json['value'];
  }
}
