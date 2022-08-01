import 'dart:core';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show rootBundle;
import 'package:rydr_app/app/config.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/models/tag.dart';

/// Application-level global variable
TagsConfig tagsConfig = TagsConfig();

/// icon mapptng to category names
/// NOTE: add additional mappings as we add filters
final Map<String, dynamic> tagIcons = {
  "take out": AppIcons.shoppingBags,
  "drinks": AppIcons.glassMartiniAlt,
  "fitness": AppIcons.dumbbell,
  "coffee": AppIcons.coffee,
  "featured": AppIcons.star,
};

IconData getTagIcon(String tagValue) => tagIcons[tagValue.toLowerCase()];

class TagsConfig {
  final log = getLogger('MapConfig');
  static final TagsConfig _tagsConfig = TagsConfig._internal();

  List<Tag> tagsPublishers = [];
  List<Tag> tagsDeals = [];
  List<Tag> tagsDealsFilters = [];

  factory TagsConfig() {
    return _tagsConfig;
  }

  TagsConfig._internal();

  /// TO DO:
  /// we should guard against loading this twice...
  /// should also change this to lazy-load singleton
  initValues() async {
    log.i('initValues');

    Map<String, dynamic> tagsPublishersMap = json.decode(
        await rootBundle.loadString("assets/config/tags_publishers.json"));
    Map<String, dynamic> tagsDealsMap = json
        .decode(await rootBundle.loadString("assets/config/tags_deals.json"));
    Map<String, dynamic> tagsDealsFiltersMap = json.decode(
        await rootBundle.loadString("assets/config/tags_deals_filters.json"));

    tagsPublishersMap['tags']
        .forEach((tag) => tagsPublishers.add(Tag.fromJson(tag)));
    tagsDealsMap['tags'].forEach((tag) => tagsDeals.add(Tag.fromJson(tag)));
    tagsDealsFiltersMap['tags']
        .forEach((tag) => tagsDealsFilters.add(Tag.fromJson(tag)));

    var remoteConfig = await AppConfig.instance.remoteConfig;

    if (remoteConfig == null) {
      log.w('initValues | RemoteConfig unavailable - is it initialized?');

      return;
    }

    try {
      /// get tags configuration which includes defaults
      /// NOTE! these should be kept in synch between firebase remoteconfig and assets/config/tags* files
      tagsPublishersMap =
          json.decode(remoteConfig.getString('tags_publishers'));
      tagsDealsMap = json.decode(remoteConfig.getString('tags_deals'));
      tagsDealsFiltersMap =
          json.decode(remoteConfig.getString('tags_deals_filters'));

      if (tagsDealsMap != null) {
        tagsPublishers = [];
        tagsPublishersMap['tags']
            .forEach((tag) => tagsPublishers.add(Tag.fromJson(tag)));

        tagsDeals = [];
        tagsDealsMap['tags'].forEach((tag) => tagsDeals.add(Tag.fromJson(tag)));

        tagsDealsFilters = [];
        tagsDealsFiltersMap['tags']
            .forEach((tag) => tagsDealsFilters.add(Tag.fromJson(tag)));
      }

      log.i('initValues | remoteConfig fetch completed');
    } catch (exception) {
      log.e(
          'initValues | Unable to use remote config. Cached or defaults will be used',
          exception);
    }
  }
}
