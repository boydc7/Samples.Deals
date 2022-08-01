import 'package:rydr_app/models/enums/deal.dart';

class DealStat {
  DealStatType type;
  String value;

  DealStat(this.type, this.value);

  DealStat.fromJson(Map<String, dynamic> json) {
    this.type = dealStatTypeFromString(json['type']);
    this.value = json['value'];
  }
}
