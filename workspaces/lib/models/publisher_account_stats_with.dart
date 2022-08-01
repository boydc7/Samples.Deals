import 'package:rydrworkspaces/models/publisher_media_stat.dart';

/// represents summary of 'work history' between a creator and a business
class PublisherAccountStatsWith {
  int completedDealCount;
  int completionMediaCount;

  PublisherMediaStatValues stats;

  PublisherAccountStatsWith.fromJson(Map<String, dynamic> json) {
    completedDealCount = json['completedDealCount'];
    completionMediaCount = json['completionMediaCount'];

    /// convert to raw key/value map of publisher media stats
    final _stats = List<PublisherStatValue>.from(json['stats']
        .map((stat) => PublisherStatValue.fromJson(stat))
        .toList());

    /// convenience model that translates key/values
    /// into model with props that represent they key names
    stats = PublisherMediaStatValues(_stats);
  }
}
