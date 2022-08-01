enum ProfileGrowthType {
  Followers,
  Following,
  OnlineFollowers,
  Impressions,
  Reach,
  ProfileViews,
  WebsiteClicks,
  EmailContacts,
  PhoneCallClicks,
  TextMessageClicks,
}

enum LocationType {
  City,
  Country,
}
LocationType locationTypeFromString(String type) {
  if (type == null) {
    return null;
  }

  switch (type.toLowerCase()) {
    case "city":
      return LocationType.City;
    case "country":
      return LocationType.Country;
    default:
      return null;
  }
}

locationTypeToString(LocationType type) {
  return type.toString().replaceAll('locationType.', '');
}
