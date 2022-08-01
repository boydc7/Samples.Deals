import 'package:rydr_app/models/enums/deal.dart';

class DealMetaData {
  final DealMetaType type;
  final String value;

  DealMetaData(this.type, this.value);

  DealMetaData.fromJson(Map<String, dynamic> json)
      : type = dealMetaTypeFromString(json['type']),
        value = json['value'];

  Map<String, dynamic> toJson() => {
        "type": dealMetaTypeToString(type),
        "value": value,
      };
}
