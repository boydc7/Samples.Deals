import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/publisher_account_stats.dart';
import 'package:rydrworkspaces/models/publisher_account_stats_with.dart';

class PublisherAccountStatsResponse {
  final PublisherAccountStats accountStats;
  final DioError error;

  PublisherAccountStatsResponse(this.accountStats, this.error);

  PublisherAccountStatsResponse.fromResponse(Map<String, dynamic> json)
      : accountStats = PublisherAccountStats.fromJson(json['result']),
        error = null;

  PublisherAccountStatsResponse.withError(DioError error)
      : accountStats = null,
        error = error;
}

class PublisherAccountStatsWithResponse {
  final PublisherAccountStatsWith accountStatsWith;
  final DioError error;

  PublisherAccountStatsWithResponse(this.accountStatsWith, this.error);

  PublisherAccountStatsWithResponse.fromResponse(Map<String, dynamic> json)
      : accountStatsWith = PublisherAccountStatsWith.fromJson(json['result']),
        error = null;

  PublisherAccountStatsWithResponse.withError(DioError error)
      : accountStatsWith = null,
        error = error;
}
