import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';

class PublisherAccountStats {
  Map<DealStatType, int> dealRequestStats;

  PublisherAccountStats.fromJson(Map<String, dynamic> json) {
    this.dealRequestStats = json['dealRequestStats'] == null
        ? null
        : Map.fromIterable(
            Deal.jsonToStats(json['dealRequestStats']),
            key: (s) => s.type,
            value: (s) => int.tryParse(s.value) ?? 0,
          );
  }

  int tryGetDealStatValue(DealStatType forDealStatType) =>
      dealRequestStats.containsKey(forDealStatType)
          ? dealRequestStats[forDealStatType]
          : 0;
}
