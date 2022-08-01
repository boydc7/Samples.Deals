class WorkspaceFeatures {
  static int none = 0;
  static int teams = 1;
  static int businessFinder = 2;
  static int dealTags = 4;
  static int businessTags = 8;

  static bool hasFeature(int features, int compare) {
    return features == null ? false : features & compare == compare;
  }

  static bool hasTeams(int features) => hasFeature(features, teams);

  static bool hasBusinessFinder(int features) =>
      hasFeature(features, businessFinder);

  static bool hasDealTags(int features) => hasFeature(features, dealTags);
  static bool hasBusinessTags(int features) =>
      hasFeature(features, businessTags);
}
