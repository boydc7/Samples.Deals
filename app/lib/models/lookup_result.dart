import 'package:rydr_app/models/record_type.dart';

class LookupResult {
  int recordId;
  RecordOfType type;
  bool isDeleted;
  String name;

  LookupResult.fromJson(Map<String, dynamic> json) {
    this.recordId = json['recordId'];
    this.type = recordOfTypeFromJson(json['recordType']);
    this.isDeleted = json['isDeleted'];
    this.name = json['name'];
  }
}
