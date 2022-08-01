class WorkspaceFeatures {
  static int none = 0;
  static int teams = 1;

  static bool hasFeature(int features, int compare) {
    return features == null ? false : features & compare == compare;
  }

  static bool hasTeams(int features) {
    return hasFeature(features, teams);
  }
}
