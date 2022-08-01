import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/range.dart';

class PublisherAccountMediaVision {
  final int totalPostsAnalyzed;
  final int totalStoriesAnalyzed;
  final int todayPostsAnalyzed;
  final int todayStoriesAnalyzed;
  final int postDailyLimit;
  final int storyDailyLimit;
  final PublisherAccountMediaVisionSection notable;
  final PublisherAccountMediaVisionSection stories;
  final PublisherAccountMediaVisionSection posts;
  final PublisherAccountMediaVisionSection captions;
  final List<PublisherMedia> recentPosts;
  final List<PublisherMedia> recentStories;

  PublisherAccountMediaVision.fromJson(Map<String, dynamic> json)
      : totalPostsAnalyzed = json['totalPostsAnalyzed'],
        totalStoriesAnalyzed = json['totalStoriesAnalyzed'],
        todayPostsAnalyzed = json['todayPostsAnalyzed'],
        todayStoriesAnalyzed = json['todayStoriesAnalyzed'],
        postDailyLimit = json['postDailyLimit'],
        storyDailyLimit = json['storyDailyLimit'],
        notable = json['notable'] != null
            ? PublisherAccountMediaVisionSection.fromJson(json['notable'])
            : null,
        stories = json['stories'] != null
            ? PublisherAccountMediaVisionSection.fromJson(json['stories'])
            : null,
        posts = json['posts'] != null
            ? PublisherAccountMediaVisionSection.fromJson(json['posts'])
            : null,
        captions = json['captions'] != null
            ? PublisherAccountMediaVisionSection.fromJson(json['captions'])
            : null,
        recentPosts = json['recentPosts'] != null
            ? List<PublisherMedia>.from(json['recentPosts']
                .map((media) => PublisherMedia.fromJson(media))
                .toList())
            : [],
        recentStories = json['recentStories'] != null
            ? List<PublisherMedia>.from(json['recentStories']
                .map((media) => PublisherMedia.fromJson(media))
                .toList())
            : [];

  List<PublisherMedia> recentForDisplay([int limit = 10]) {
    final int stories = recentStories.length;
    final int posts = recentPosts.length;
    final int half = limit ~/ 2;

    List<PublisherMedia> recent =
        stories > half ? recentStories.take(half).toList() : recentStories;

    if (recent.length < half) {
      recent.addAll(posts > limit - recent.length
          ? recentPosts.take(limit - recent.length).toList()
          : recentPosts);
    } else {
      recent
          .addAll(posts > half ? recentPosts.take(half).toList() : recentPosts);
    }

    return recent;
  }
}

class PublisherAccountMediaVisionSection {
  final String title;
  final int totalCount;
  final List<PublisherAccountMediaVisionSectionItem> items;

  PublisherAccountMediaVisionSection.fromJson(Map<String, dynamic> json)
      : title = json['title'],
        totalCount = json['totalCount'],
        items = json['items'] != null
            ? List<PublisherAccountMediaVisionSectionItem>.from(json['items']
                .map((item) =>
                    PublisherAccountMediaVisionSectionItem.fromJson(item))
                .toList())
            : [];
}

class PublisherAccountMediaVisionSectionItem {
  final String title;
  final String subTitle;
  final int count;
  final List<PublisherMedia> medias;
  final PublisherAccountMediaVisionSectionSearchDescriptor searchDescriptor;
  final List<String> searchTags;

  PublisherAccountMediaVisionSectionItem(
    this.title,
    this.subTitle,
    this.count,
    this.medias,
    this.searchDescriptor,
    this.searchTags,
  );

  PublisherAccountMediaVisionSectionItem.fromJson(Map<String, dynamic> json)
      : title = json['title'],
        subTitle = json['subTitle'],
        count = json['count'],
        medias = json['medias'] != null
            ? List<PublisherMedia>.from(json['medias']
                .map((media) => PublisherMedia.fromJson(media))
                .toList())
            : [],
        searchDescriptor =
            PublisherAccountMediaVisionSectionSearchDescriptor.fromJson(
                json['searchDescriptor']),
        searchTags = json['searchTags'] != null
            ? List<String>.from(json['searchTags'].map((tag) => tag).toList())
            : [];
}

class PublisherAccountMediaVisionSectionSearchDescriptor {
  final String query;
  final PublisherContentType contentType;
  final List<String> sentiments;
  final LongRange facesRange;
  final LongRange facesAvgAgeRange;
  final LongRange facesMalesRange;
  final LongRange facesFemalesRange;
  final LongRange facesSmilesRange;
  final LongRange facesBeardsRange;
  final LongRange facesMustachesRange;
  final LongRange facesEyeglassesRange;
  final LongRange facesSunglassesRange;
  final bool sortRecent;

  PublisherAccountMediaVisionSectionSearchDescriptor.fromJson(
      Map<String, dynamic> json)
      : query = json['query'],
        contentType = publisherContentTypeFromJson(json['contentType']),
        sentiments = json['sentiments'] != null
            ? List<String>.from(
                json['sentiments'].map((sentiment) => sentiment).toList())
            : [],
        facesRange = LongRange.fromJson(json['facesRange']),
        facesAvgAgeRange = LongRange.fromJson(json['facesAvgAgeRange']),
        facesMalesRange = LongRange.fromJson(json['facesMalesRange']),
        facesFemalesRange = LongRange.fromJson(json['facesFemalesRange']),
        facesSmilesRange = LongRange.fromJson(json['facesSmilesRange']),
        facesBeardsRange = LongRange.fromJson(json['facesBeardsRange']),
        facesMustachesRange = LongRange.fromJson(json['facesMustachesRange']),
        facesEyeglassesRange = LongRange.fromJson(json['facesEyeglassesRange']),
        facesSunglassesRange = LongRange.fromJson(json['facesSunglassesRange']),
        sortRecent = json['sortRecent'];
}
