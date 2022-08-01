import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/range.dart';

class QueryCreatorStats {
  double dealValue;
  int maxApprovals;
  int targetReach;
  int targetImpressions;
  int targetEngagements;

  String search;
  double latitude;
  double longitude;
  double miles;
  LongRange minAgeRange;
  LongRange maxAgeRange;
  GenderType gender;
  LongRange followerRange;
  LongRange followingRange;
  DoubleRange storyEngagementRatingRange;
  LongRange storyImpressionsRange;
  LongRange storyReachRange;
  LongRange storyActionsRange;
  LongRange storiesRange;
  DoubleRange mediaEngagementRatingRange;
  DoubleRange mediaTrueEngagementRatingRange;
  LongRange mediaImpressionsRange;
  LongRange mediaReachRange;
  LongRange mediaActionsRange;
  LongRange mediasRange;
  LongRange avgStoryImpressionsRange;
  LongRange avgMediaImpressionsRange;
  LongRange avgStoryReachRange;
  LongRange avgMediaReachRange;
  LongRange avgStoryActionsRange;
  LongRange avgMediaActionsRange;
  LongRange follower7DayJitterRange;
  LongRange follower14DayJitterRange;
  LongRange follower30DayJitterRange;
  LongRange audienceUsaRange;
  LongRange audienceEnglishRange;
  LongRange audienceSpanishRange;
  LongRange audienceMaleRange;
  LongRange audienceFemaleRange;
  LongRange audienceAge1317Range;
  LongRange audienceAge18UpRange;
  LongRange audienceAge1824Range;
  LongRange audienceAge25UpRange;
  LongRange audienceAge2534Range;
  LongRange audienceAge3544Range;
  LongRange audienceAge4554Range;
  LongRange audienceAge5564Range;
  LongRange audienceAge65UpRange;
  LongRange imagesAvgAgeRange;
  DoubleRange suggestiveRatingRange;
  DoubleRange violenceRatingRange;
  DoubleRange rydr7DayActivityRatingRange;
  DoubleRange rydr14DayActivityRatingRange;
  DoubleRange rydr30DayActivityRatingRange;
  LongRange requestsRange;
  LongRange completedRequestsRange;
  DoubleRange avgCPMrRange;
  DoubleRange avgCPMiRange;
  DoubleRange avgCPERange;
  LongRange requestsRange1;
  LongRange completedRequestsRange1;
  DoubleRange avgCPMrRange1;
  DoubleRange avgCPMiRange1;
  DoubleRange avgCPERange1;
  LongRange requestsRange2;
  LongRange completedRequestsRange2;
  DoubleRange avgCPMrRange2;
  DoubleRange avgCPMiRange2;
  DoubleRange avgCPERange2;
  LongRange requestsRange3;
  LongRange completedRequestsRange3;
  DoubleRange avgCPMrRange3;
  DoubleRange avgCPMiRange3;
  DoubleRange avgCPERange3;
  LongRange requestsRange4;
  LongRange completedRequestsRange4;
  DoubleRange avgCPMrRange4;
  DoubleRange avgCPMiRange4;
  DoubleRange avgCPERange4;
  LongRange requestsRange5;
  LongRange completedRequestsRange5;
  DoubleRange avgCPMrRange5;
  DoubleRange avgCPMiRange5;
  DoubleRange avgCPERange5;
}

class CreatorStats {
  int creators;

  LongRange estimatedStoryImpressions;
  LongRange estimatedStoryReach;
  LongRange estimatedStoryEngagements;
  LongRange estimatedPostImpressions;
  LongRange estimatedPostReach;
  LongRange estimatedPostEngagements;

  LongRange storyApprovalsForTargetImpressions;
  LongRange storyApprovalsForTargetReach;
  LongRange storyApprovalsForTargetEngagements;
  LongRange postApprovalsForTargetImpressions;
  LongRange postApprovalsForTargetReach;
  LongRange postApprovalsForTargetEngagements;

  CreatorStat followers;
  CreatorStat storyEngagementRating;
  CreatorStat storyImpressions;
  CreatorStat storyReach;
  CreatorStat storyActions;
  CreatorStat stories;
  CreatorStat mediaEngagementRating;
  CreatorStat mediaTrueEngagementRating;
  CreatorStat mediaImpressions;
  CreatorStat mediaReach;
  CreatorStat mediaActions;
  CreatorStat medias;
  CreatorStat avgStoryImpressions;
  CreatorStat avgMediaImpressions;
  CreatorStat avgStoryReach;
  CreatorStat avgMediaReach;
  CreatorStat avgStoryActions;
  CreatorStat avgMediaActions;
  CreatorStat follower7DayJitter;
  CreatorStat follower14DayJitter;
  CreatorStat follower30DayJitter;
  CreatorStat audienceUsa;
  CreatorStat audienceEnglish;
  CreatorStat audienceSpanish;
  CreatorStat audienceMale;
  CreatorStat audienceFemale;
  CreatorStat audienceAge1317;
  CreatorStat audienceAge18Up;
  CreatorStat audienceAge1824;
  CreatorStat audienceAge25Up;
  CreatorStat audienceAge2534;
  CreatorStat audienceAge3544;
  CreatorStat audienceAge4554;
  CreatorStat audienceAge5564;
  CreatorStat audienceAge65Up;
  CreatorStat imagesAvgAge;
  CreatorStat suggestiveRating;
  CreatorStat violenceRating;
  CreatorStat rydr7DayActivityRating;
  CreatorStat rydr14DayActivityRating;
  CreatorStat rydr30DayActivityRating;
  CreatorStat requests;
  CreatorStat completedRequests;
  CreatorStat avgCPMr;
  CreatorStat avgCPMi;
  CreatorStat avgCPE;
  int creators1;
  CreatorStat requests1;
  CreatorStat completedRequests1;
  CreatorStat avgCPMr1;
  CreatorStat avgCPMi1;
  CreatorStat avgCPE1;
  int creators2;
  CreatorStat requests2;
  CreatorStat completedRequests2;
  CreatorStat avgCPMr2;
  CreatorStat avgCPMi2;
  CreatorStat avgCPE2;
  int creators3;
  CreatorStat requests3;
  CreatorStat completedRequests3;
  CreatorStat avgCPMr3;
  CreatorStat avgCPMi3;
  CreatorStat avgCPE3;
  int creators4;
  CreatorStat requests4;
  CreatorStat completedRequests4;
  CreatorStat avgCPMr4;
  CreatorStat avgCPMi4;
  CreatorStat avgCPE4;
  int creators5;
  CreatorStat requests5;
  CreatorStat completedRequests5;
  CreatorStat avgCPMr5;
  CreatorStat avgCPMi5;
  CreatorStat avgCPE5;

  CreatorStats.fromJson(Map<String, dynamic> json) {
    creators = json['creators'];

    estimatedStoryImpressions =
        LongRange.fromJson(json['estimatedStoryImpressions']);
    estimatedStoryReach = LongRange.fromJson(json['estimatedStoryReach']);
    estimatedStoryEngagements =
        LongRange.fromJson(json['estimatedStoryEngagements']);
    estimatedPostImpressions =
        LongRange.fromJson(json['estimatedPostImpressions']);
    estimatedPostReach = LongRange.fromJson(json['estimatedPostReach']);
    estimatedPostEngagements =
        LongRange.fromJson(json['estimatedPostEngagements']);

    storyApprovalsForTargetImpressions =
        LongRange.fromJson(json['storyApprovalsForTargetImpressions']);
    storyApprovalsForTargetReach =
        LongRange.fromJson(json['storyApprovalsForTargetReach']);
    storyApprovalsForTargetEngagements =
        LongRange.fromJson(json['storyApprovalsForTargetEngagements']);
    postApprovalsForTargetImpressions =
        LongRange.fromJson(json['postApprovalsForTargetImpressions']);
    postApprovalsForTargetReach =
        LongRange.fromJson(json['postApprovalsForTargetReach']);
    postApprovalsForTargetEngagements =
        LongRange.fromJson(json['postApprovalsForTargetEngagements']);

    followers = CreatorStat.fromJson(json['followers']);
    storyEngagementRating = CreatorStat.fromJson(json['storyEngagementRating']);
    storyImpressions = CreatorStat.fromJson(json['storyImpressions']);
    storyReach = CreatorStat.fromJson(json['storyReach']);
    storyActions = CreatorStat.fromJson(json['storyActions']);
    stories = CreatorStat.fromJson(json['stories']);
    mediaEngagementRating = CreatorStat.fromJson(json['mediaEngagementRating']);
    mediaTrueEngagementRating =
        CreatorStat.fromJson(json['mediaTrueEngagementRating']);
    mediaImpressions = CreatorStat.fromJson(json['mediaImpressions']);
    mediaReach = CreatorStat.fromJson(json['mediaReach']);
    mediaActions = CreatorStat.fromJson(json['mediaActions']);
    medias = CreatorStat.fromJson(json['medias']);
    avgStoryImpressions = CreatorStat.fromJson(json['avgStoryImpressions']);
    avgMediaImpressions = CreatorStat.fromJson(json['avgMediaImpressions']);
    avgStoryReach = CreatorStat.fromJson(json['avgStoryReach']);
    avgMediaReach = CreatorStat.fromJson(json['avgMediaReach']);
    avgStoryActions = CreatorStat.fromJson(json['avgStoryActions']);
    avgMediaActions = CreatorStat.fromJson(json['avgMediaActions']);
    follower7DayJitter = CreatorStat.fromJson(json['follower7DayJitter']);
    follower14DayJitter = CreatorStat.fromJson(json['follower14DayJitter']);
    follower30DayJitter = CreatorStat.fromJson(json['follower30DayJitter']);
    audienceUsa = CreatorStat.fromJson(json['audienceUsa']);
    audienceEnglish = CreatorStat.fromJson(json['audienceEnglish']);
    audienceSpanish = CreatorStat.fromJson(json['audienceSpanish']);
    audienceMale = CreatorStat.fromJson(json['audienceMale']);
    audienceFemale = CreatorStat.fromJson(json['audienceFemale']);
    audienceAge1317 = CreatorStat.fromJson(json['audienceAge1317']);
    audienceAge18Up = CreatorStat.fromJson(json['audienceAge18Up']);
    audienceAge1824 = CreatorStat.fromJson(json['audienceAge1824']);
    audienceAge25Up = CreatorStat.fromJson(json['audienceAge25Up']);
    audienceAge2534 = CreatorStat.fromJson(json['audienceAge2534']);
    audienceAge3544 = CreatorStat.fromJson(json['audienceAge3544']);
    audienceAge4554 = CreatorStat.fromJson(json['audienceAge4554']);
    audienceAge5564 = CreatorStat.fromJson(json['audienceAge5564']);
    audienceAge65Up = CreatorStat.fromJson(json['audienceAge65Up']);
    imagesAvgAge = CreatorStat.fromJson(json['imagesAvgAge']);
    suggestiveRating = CreatorStat.fromJson(json['suggestiveRating']);
    violenceRating = CreatorStat.fromJson(json['violenceRating']);
    rydr7DayActivityRating =
        CreatorStat.fromJson(json['rydr7DayActivityRating']);
    rydr14DayActivityRating =
        CreatorStat.fromJson(json['rydr14DayActivityRating']);
    rydr30DayActivityRating =
        CreatorStat.fromJson(json['rydr30DayActivityRating']);
    requests = CreatorStat.fromJson(json['requests']);
    completedRequests = CreatorStat.fromJson(json['completedRequests']);
    avgCPMr = CreatorStat.fromJson(json['avgCPMr']);
    avgCPMi = CreatorStat.fromJson(json['avgCPMi']);
    avgCPE = CreatorStat.fromJson(json['avgCPE']);
    creators1 = json['creators1'];
    requests1 = CreatorStat.fromJson(json['requests1']);
    completedRequests1 = CreatorStat.fromJson(json['completedRequests1']);
    avgCPMr1 = CreatorStat.fromJson(json['avgCPMr1']);
    avgCPMi1 = CreatorStat.fromJson(json['avgCPMi1']);
    avgCPE1 = CreatorStat.fromJson(json['avgCPE1']);
    creators2 = json['creators2'];
    requests2 = CreatorStat.fromJson(json['requests2']);
    completedRequests2 = CreatorStat.fromJson(json['completedRequests2']);
    avgCPMr2 = CreatorStat.fromJson(json['avgCPMr2']);
    avgCPMi2 = CreatorStat.fromJson(json['avgCPMi2']);
    avgCPE2 = CreatorStat.fromJson(json['avgCPE2']);
    creators3 = json['creators3'];
    requests3 = CreatorStat.fromJson(json['requests3']);
    completedRequests3 = CreatorStat.fromJson(json['completedRequests3']);
    avgCPMr3 = CreatorStat.fromJson(json['avgCPMr3']);
    avgCPMi3 = CreatorStat.fromJson(json['avgCPMi3']);
    avgCPE3 = CreatorStat.fromJson(json['avgCPE3']);
    creators4 = json['creators4'];
    requests4 = CreatorStat.fromJson(json['requests4']);
    completedRequests4 = CreatorStat.fromJson(json['completedRequests4']);
    avgCPMr4 = CreatorStat.fromJson(json['avgCPMr4']);
    avgCPMi4 = CreatorStat.fromJson(json['avgCPMi4']);
    avgCPE4 = CreatorStat.fromJson(json['avgCPE4']);
    creators5 = json['creators5'];
    requests5 = CreatorStat.fromJson(json['requests5']);
    completedRequests5 = CreatorStat.fromJson(json['completedRequests5']);
    avgCPMr5 = CreatorStat.fromJson(json['avgCPMr5']);
    avgCPMi5 = CreatorStat.fromJson(json['avgCPMi5']);
    avgCPE5 = CreatorStat.fromJson(json['avgCPE5']);
  }
}

class CreatorStat {
  double min;
  double max;
  double avg;
  double sum;
  double stdDev;
  int count;

  CreatorStat.fromJson(Map<String, dynamic> json) {
    min = json['min']?.toDouble();
    max = json['max']?.toDouble();
    avg = json['max']?.toDouble();
    sum = json['sum']?.toDouble();
    stdDev = json['stdDev']?.toDouble();
    count = json['count']?.toInt();
  }
}
