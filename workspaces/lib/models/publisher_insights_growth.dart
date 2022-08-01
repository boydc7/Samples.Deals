import 'package:rydrworkspaces/models/enums/publisher_insights.dart';
import 'package:rydrworkspaces/models/publisher_insights_day.dart';

class PublisherInsightsGrowth {
  DateTime date;
  int followers;
  int followerPriorDayGrowth;
  double followerPriorDayGrowthPercent;
  int following;
  int followingPriorDayGrowth;
  double followingPriorDayGrowthPercent;
  int onlineFollowers;
  int onlineFollowersPriorDayGrowth;
  double onlineFollowersPriorDayGrowthPercent;
  int impressions;
  int impressionsPriorDayGrowth;
  double impressionsPriorDayGrowthPercent;
  int reach;
  int reachPriorDayGrowth;
  double reachPriorDayGrowthPercent;
  int profileViews;
  int profileViewsPriorDayGrowth;
  double profileViewsPriorDayGrowthPercent;
  int websiteClicks;
  int websitePriorDayGrowth;
  double websitePriorDayGrowthPercent;
  int emailContacts;
  int emailPriorDayGrowth;
  double emailPriorDayGrowthPercent;
  int getDirectionClicks;
  int getDirectionClicksPriorDayGrowth;
  double getDirectionClicksPriorDayGrowthPercent;
  int phoneCallClicks;
  int phoneCallClicksPriorDayGrowth;
  double phoneCallClicksPriorDayGrowthPercent;
  int textMessageClicks;
  int textMessageClicksPriorDayGrowth;
  double textMessageClicksPriorDayGrowthPercent;

  PublisherInsightsGrowth.fromJson(Map<String, dynamic> json) {
    date = DateTime.parse(json['date']).toLocal();
    followers = toIntOrZero(json['followers']);
    followerPriorDayGrowth = toIntOrZero(json['followerPriorDayGrowth']);
    followerPriorDayGrowthPercent =
        toDoubleOrZero(json['followerPriorDayGrowthPercent']);
    following = toIntOrZero(json['following']);
    followingPriorDayGrowth = toIntOrZero(json['followingPriorDayGrowth']);
    followingPriorDayGrowthPercent =
        toDoubleOrZero(json['followingPriorDayGrowthPercent']);
    onlineFollowers = toIntOrZero(json['onlineFollowers']);
    onlineFollowersPriorDayGrowth =
        toIntOrZero(json['onlineFollowersPriorDayGrowth']);
    onlineFollowersPriorDayGrowthPercent =
        toDoubleOrZero(json['onlineFollowersPriorDayGrowthPercent']);
    impressions = toIntOrZero(json['impressions']);
    impressionsPriorDayGrowth = toIntOrZero(json['impressionsPriorDayGrowth']);
    impressionsPriorDayGrowthPercent =
        toDoubleOrZero(json['impressionsPriorDayGrowthPercent']);
    reach = toIntOrZero(json['reach']);
    reachPriorDayGrowth = toIntOrZero(json['reachPriorDayGrowth']);
    reachPriorDayGrowthPercent =
        toDoubleOrZero(json['reachPriorDayGrowthPercent']);
    profileViews = toIntOrZero(json['profileViews']);
    profileViewsPriorDayGrowth =
        toIntOrZero(json['profileViewsPriorDayGrowth']);
    profileViewsPriorDayGrowthPercent =
        toDoubleOrZero(json['profileViewsPriorDayGrowthPercent']);
    websiteClicks = toIntOrZero(json['websiteClicks']);
    websitePriorDayGrowth = toIntOrZero(json['websitePriorDayGrowth']);
    websitePriorDayGrowthPercent =
        toDoubleOrZero(json['websitePriorDayGrowthPercent']);
    emailContacts = toIntOrZero(json['emailContacts']);
    emailPriorDayGrowth = toIntOrZero(json['emailPriorDayGrowth']);
    emailPriorDayGrowthPercent =
        toDoubleOrZero(json['emailPriorDayGrowthPercent']);
    getDirectionClicks = toIntOrZero(json['getDirectionClicks']);
    getDirectionClicksPriorDayGrowth =
        toIntOrZero(json['getDirectionClicksPriorDayGrowth']);
    getDirectionClicksPriorDayGrowthPercent =
        toDoubleOrZero(json['getDirectionClicksPriorDayGrowthPercent']);
    phoneCallClicks = toIntOrZero(json['phoneCallClicks']);
    phoneCallClicksPriorDayGrowth =
        toIntOrZero(json['phoneCallClicksPriorDayGrowth']);
    phoneCallClicksPriorDayGrowthPercent =
        toDoubleOrZero(json['phoneCallClicksPriorDayGrowthPercent']);
    textMessageClicks = toIntOrZero(json['textMessageClicks']);
    textMessageClicksPriorDayGrowth =
        toIntOrZero(json['textMessageClicksPriorDayGrowth']);
    textMessageClicksPriorDayGrowthPercent =
        toDoubleOrZero(json['textMessageClicksPriorDayGrowthPercent']);
  }

  toIntOrZero(dynamic json) {
    return json == null ? 0 : json.toInt();
  }

  toDoubleOrZero(dynamic json) {
    return json == null ? 0.0 : json.toDouble();
  }
}

class PublisherInsightsGrowthSummary {
  final int maxDays = 30;

  int total;
  PublisherInsightsDay max;
  PublisherInsightsDay min;
  double avg;
  double growthRate;
  int diff;
  List<PublisherInsightsGrowth> items;
  // List<FlSpot> flSpots = [];
  List<DateTime> dates;

  PublisherInsightsGrowthSummary(
    List<PublisherInsightsGrowth> growth,
    ProfileGrowthType type,
    int days,
  ) {
    /// first, we sort the data on date
    List<PublisherInsightsGrowth> _sorted = growth
      ..sort((a, b) => a.date.compareTo(b.date));

    /// if we have more than maxDays days of data then trim to maxDays only
    if (_sorted.length > maxDays) {
      _sorted = _sorted.skip(_sorted.length - maxDays).take(maxDays).toList();
    }

    PublisherInsightsGrowth _min;
    PublisherInsightsGrowth _max;

    /// days we want to range by can't exceed max length of sorted items we have
    /// we also don't want to exceed maximum of 30 days so cap it at that
    days = days > _sorted.length ? _sorted.length : days;

    /// get array of items and dates matching the number of days we're asking for
    items = _sorted.skip(_sorted.length - days).take(days).toList();
    dates = items.map((m) => m.date).toList();

    switch (type) {
      case ProfileGrowthType.Followers:
        {
          _min = items.reduce((c, n) => c.followers < n.followers ? c : n);
          _max = items.reduce((c, n) => c.followers > n.followers ? c : n);

          total = _max.followers;
          avg = total / days;

          min = PublisherInsightsDay(_min.followers, _min.date, null);
          max = PublisherInsightsDay(_max.followers, _max.date, null);
          diff = items[0].followers - items.last.followers;

          growthRate = items[0].followers > 0
              ? (items.last.followers - items[0].followers) /
                  items[0].followers *
                  100
              : 0;

          for (int x = 0; x < items.length; x++) {
            // flSpots.add(
            //   FlSpot(
            //     x.toDouble(),
            //     items[x].followers.toDouble(),
            //   ),
            // );
          }

          break;
        }
      case ProfileGrowthType.Following:
        {
          _min = items.reduce((c, n) => c.following < n.following ? c : n);
          _max = items.reduce((c, n) => c.following > n.following ? c : n);

          total = _max.following;
          avg = total / days;

          min = PublisherInsightsDay(_min.following, _min.date, null);
          max = PublisherInsightsDay(_max.following, _max.date, null);
          diff = items[0].following - items.last.following;

          growthRate = items[0].following > 0
              ? (items.last.following - items[0].following) /
                  items[0].following *
                  100
              : 0;

          for (int x = 0; x < items.length; x++) {
            // flSpots.add(
            //   FlSpot(
            //     x.toDouble(),
            //     items[x].following.toDouble(),
            //   ),
            // );
          }

          break;
        }
      case ProfileGrowthType.OnlineFollowers:
        {
          _min = items
              .reduce((c, n) => c.onlineFollowers < n.onlineFollowers ? c : n);
          _max = items
              .reduce((c, n) => c.onlineFollowers > n.onlineFollowers ? c : n);

          total = _max.onlineFollowers;
          avg = total / days;

          min = PublisherInsightsDay(_min.onlineFollowers, _min.date, null);
          max = PublisherInsightsDay(_max.onlineFollowers, _max.date, null);
          diff = items[0].onlineFollowers - items.last.onlineFollowers;

          growthRate = items[0].onlineFollowers > 0
              ? (items.last.onlineFollowers - items[0].onlineFollowers) /
                  items[0].onlineFollowers *
                  100
              : 0;

          for (int x = 0; x < items.length; x++) {
            // flSpots.add(
            //   FlSpot(
            //     x.toDouble(),
            //     items[x].onlineFollowers.toDouble(),
            //   ),
            // );
          }

          break;
        }
      case ProfileGrowthType.Impressions:
        {
          _min = items.reduce((c, n) => c.impressions < n.impressions ? c : n);
          _max = items.reduce((c, n) => c.impressions > n.impressions ? c : n);

          total = items.map<int>((m) => m.impressions).reduce((a, b) => a + b);
          avg = total / days;

          min = PublisherInsightsDay(_min.impressions, _min.date, null);
          max = PublisherInsightsDay(_max.impressions, _max.date, null);

          diff = items[0].impressions - items.last.impressions;

          growthRate = items[0].impressions > 0
              ? (items.last.impressions - items[0].impressions) /
                  items[0].impressions *
                  100
              : 0;

          for (int x = 0; x < items.length; x++) {
            // flSpots.add(
            //   FlSpot(
            //     x.toDouble(),
            //     items[x].impressions.toDouble(),
            //   ),
            // );
          }

          break;
        }
      case ProfileGrowthType.Reach:
        {
          _min = items.reduce((c, n) => c.reach < n.reach ? c : n);
          _max = items.reduce((c, n) => c.reach > n.reach ? c : n);

          total = items.map<int>((m) => m.reach).reduce((a, b) => a + b);
          avg = total / days;

          min = PublisherInsightsDay(_min.reach, _min.date, null);
          max = PublisherInsightsDay(_max.reach, _max.date, null);

          diff = items[0].reach - items.last.reach;

          growthRate = items[0].reach > 0
              ? (items.last.reach - items[0].reach) / items[0].reach * 100
              : 0;

          for (int x = 0; x < items.length; x++) {
            // flSpots.add(
            //   FlSpot(
            //     x.toDouble(),
            //     items[x].reach.toDouble(),
            //   ),
            // );
          }

          break;
        }
      case ProfileGrowthType.ProfileViews:
        {
          _min =
              items.reduce((c, n) => c.profileViews < n.profileViews ? c : n);
          _max =
              items.reduce((c, n) => c.profileViews > n.profileViews ? c : n);

          total = items.map<int>((m) => m.profileViews).reduce((a, b) => a + b);
          avg = total / days;

          min = PublisherInsightsDay(_min.profileViews, _min.date, null);
          max = PublisherInsightsDay(_max.profileViews, _max.date, null);

          diff = items[0].profileViews - items.last.profileViews;

          growthRate = items[0].profileViews > 0
              ? (items.last.profileViews - items[0].profileViews) /
                  items[0].profileViews *
                  100
              : 0;

          for (int x = 0; x < items.length; x++) {
            // flSpots.add(
            //   FlSpot(
            //     x.toDouble(),
            //     items[x].profileViews.toDouble(),
            //   ),
            // );
          }

          break;
        }
      case ProfileGrowthType.WebsiteClicks:
        {
          _min =
              items.reduce((c, n) => c.websiteClicks < n.websiteClicks ? c : n);
          _max =
              items.reduce((c, n) => c.websiteClicks > n.websiteClicks ? c : n);

          total =
              items.map<int>((m) => m.websiteClicks).reduce((a, b) => a + b);
          avg = total / days;

          min = PublisherInsightsDay(_min.websiteClicks, _min.date, null);
          max = PublisherInsightsDay(_max.websiteClicks, _max.date, null);

          diff = items[0].websiteClicks - items.last.websiteClicks;

          growthRate = items[0].websiteClicks > 0
              ? (items.last.websiteClicks - items[0].websiteClicks) /
                  items[0].websiteClicks *
                  100
              : 0;

          for (int x = 0; x < items.length; x++) {
            // flSpots.add(
            //   FlSpot(
            //     x.toDouble(),
            //     items[x].websiteClicks.toDouble(),
            //   ),
            // );
          }

          break;
        }
      case ProfileGrowthType.EmailContacts:
        {
          _min =
              items.reduce((c, n) => c.emailContacts < n.emailContacts ? c : n);
          _max =
              items.reduce((c, n) => c.emailContacts > n.emailContacts ? c : n);

          total =
              items.map<int>((m) => m.emailContacts).reduce((a, b) => a + b);
          avg = total / days;

          min = PublisherInsightsDay(_min.emailContacts, _min.date, null);
          max = PublisherInsightsDay(_max.emailContacts, _max.date, null);

          diff = items[0].emailContacts - items.last.emailContacts;

          growthRate = items[0].emailContacts > 0
              ? (items.last.emailContacts - items[0].emailContacts) /
                  items[0].emailContacts *
                  100
              : 0;

          for (int x = 0; x < items.length; x++) {
            // flSpots.add(
            //   FlSpot(
            //     x.toDouble(),
            //     items[x].emailContacts.toDouble(),
            //   ),
            // );
          }

          break;
        }
      case ProfileGrowthType.PhoneCallClicks:
        {
          _min = items
              .reduce((c, n) => c.phoneCallClicks < n.phoneCallClicks ? c : n);
          _max = items
              .reduce((c, n) => c.phoneCallClicks > n.phoneCallClicks ? c : n);

          total =
              items.map<int>((m) => m.phoneCallClicks).reduce((a, b) => a + b);
          avg = total / days;

          min = PublisherInsightsDay(_min.phoneCallClicks, _min.date, null);
          max = PublisherInsightsDay(_max.phoneCallClicks, _max.date, null);

          diff = items[0].phoneCallClicks - items.last.phoneCallClicks;

          growthRate = items[0].phoneCallClicks > 0
              ? (items.last.phoneCallClicks - items[0].phoneCallClicks) /
                  items[0].phoneCallClicks *
                  100
              : 0;

          for (int x = 0; x < items.length; x++) {
            // flSpots.add(
            //   FlSpot(
            //     x.toDouble(),
            //     items[x].phoneCallClicks.toDouble(),
            //   ),
            // );
          }

          break;
        }
      case ProfileGrowthType.TextMessageClicks:
        {
          _min = items.reduce(
              (c, n) => c.textMessageClicks < n.textMessageClicks ? c : n);
          _max = items.reduce(
              (c, n) => c.textMessageClicks > n.textMessageClicks ? c : n);

          total = items
              .map<int>((m) => m.textMessageClicks)
              .reduce((a, b) => a + b);
          avg = total / days;

          min = PublisherInsightsDay(_min.textMessageClicks, _min.date, null);
          max = PublisherInsightsDay(_max.textMessageClicks, _max.date, null);

          diff = items[0].textMessageClicks - items.last.textMessageClicks;

          growthRate = items[0].textMessageClicks > 0
              ? (items.last.textMessageClicks - items[0].textMessageClicks) /
                  items[0].textMessageClicks *
                  100
              : 0;

          for (int x = 0; x < items.length; x++) {
            // flSpots.add(
            //   FlSpot(
            //     x.toDouble(),
            //     items[x].textMessageClicks.toDouble(),
            //   ),
            // );
          }

          break;
        }
      default:
        break;
    }
  }
}
