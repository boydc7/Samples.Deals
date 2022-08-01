import 'package:rydrworkspaces/models/enums/deal.dart';

class DealRestriction {
  DealRestrictionType type;
  String value;

  DealRestriction(this.type, this.value);

  DealRestriction.fromJson(Map<String, dynamic> json) {
    this.type = dealRestrictionTypeFromString(json['type']);
    this.value = json['value'];
  }

  Map<String, dynamic> toJson() => {
        "type": dealRestrictionTypeToString(type),
        "value": value,
      };
}
