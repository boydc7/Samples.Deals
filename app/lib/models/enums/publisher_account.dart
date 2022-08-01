enum RydrAccountType {
  business,
  influencer,
  businessAndInfluencer,
  rydrAccount,
  admin,
  unknown,
}

rydrAccountTypeFromInt(int type) {
  if (type == null) {
    return RydrAccountType.unknown;
  }

  switch (type) {
    case 1:
      return RydrAccountType.business;
      break;
    case 2:
      return RydrAccountType.influencer;
      break;
    case 3:
      return RydrAccountType.businessAndInfluencer;
      break;
    case 4:
      return RydrAccountType.rydrAccount;
      break;
    case 8:
      return RydrAccountType.admin;
      break;
    default:
      return RydrAccountType.unknown;
      break;
  }
}

rydrAccountTypeToInt(RydrAccountType type) {
  if (type == null) {
    return null;
  }

  switch (type) {
    case RydrAccountType.business:
      return 1;
      break;
    case RydrAccountType.influencer:
      return 2;
      break;
    case RydrAccountType.businessAndInfluencer:
      return 3;
      break;
    case RydrAccountType.rydrAccount:
      return 4;
      break;
    case RydrAccountType.admin:
      return 8;
      break;
    default:
      return 0;
      break;
  }
}

rydrAccountTypeToString(RydrAccountType type) {
  switch (type) {
    case RydrAccountType.business:
      return 'Business';
      break;
    case RydrAccountType.influencer:
      return 'Creator';
      break;
    case RydrAccountType.businessAndInfluencer:
      return 'Business+Influencers';
      break;
    case RydrAccountType.rydrAccount:
      return 'Rydr Account';
      break;
    case RydrAccountType.admin:
      return 'Admin';
      break;
    default:
      return 'Unknown';
      break;
  }
}

enum PublisherType {
  unknown,
  facebook,
  instagram,
}

publisherTypeFromString(String type) {
  if (type == null) {
    return null;
  }

  switch (type.toLowerCase()) {
    case "facebook":
      return PublisherType.facebook;
      break;
    case "instagram":
      return PublisherType.instagram;
      break;
    default:
      return PublisherType.unknown;
      break;
  }
}

publisherTypeToString(PublisherType type) =>
    type.toString().replaceAll('PublisherType.', '');

enum PublisherAccountType { unknown, user, fbIgUser, page }

publisherAccountTypeFromString(String type) {
  if (type == null) {
    return null;
  }

  switch (type.toLowerCase()) {
    case "user":
      return PublisherAccountType.user;
      break;
    case "fbiguser":
      return PublisherAccountType.fbIgUser;
      break;
    case "page":
      return PublisherAccountType.page;
      break;
    default:
      return PublisherAccountType.unknown;
      break;
  }
}

publisherAccountTypeToString(PublisherAccountType type) =>
    type.toString().replaceAll('PublisherAccountType.', '');

enum PublisherLinkType { None, Basic, Full }
PublisherLinkType publisherLinkTypeFromString(String type) {
  if (type == null) {
    return null;
  }

  switch (type.toLowerCase()) {
    case "basic":
      return PublisherLinkType.Basic;
    case "full":
      return PublisherLinkType.Full;
    default:
      return PublisherLinkType.None;
  }
}

String publisherLinkTypeToString(PublisherLinkType type) =>
    type.toString().replaceAll('PublisherLinkType.', '');

enum SubscriptionType {
  None,
  PayPerBusiness,
  Unlimited,
  Trial,
}

subscriptionTypeFromString(String type) {
  if (type == null) {
    return SubscriptionType.None;
  }

  switch (type.toLowerCase()) {
    case "trial":
      return SubscriptionType.Trial;
      break;
    case "payperbusiness":
      return SubscriptionType.PayPerBusiness;
      break;
    case "unlimited":
      return SubscriptionType.Unlimited;
      break;
    default:
      return SubscriptionType.None;
      break;
  }
}

subscriptionTypeToString(SubscriptionType type) =>
    type.toString().replaceAll('SubscriptionType.', '');

enum PublisherAccountConnectionType {
  unspecified,
  contacted,
  contactedBy,
  dealtWith,
}

publisherAccountConnectionTypeToString(PublisherAccountConnectionType type) {
  return type.toString().replaceAll('PublisherAccountConnectionType.', '');
}

enum GenderType { Unknown, Male, Female, Other }
genderTypeToString(GenderType type) {
  return type.toString().replaceAll('GenderType.', '');
}

genderTypeFromString(String type) {
  if (type == null) {
    return null;
  }

  switch (type.toLowerCase()) {
    case "male":
      return GenderType.Male;
    case "female":
      return GenderType.Female;
    case "other":
      return GenderType.Other;
    default:
      return GenderType.Unknown;
  }
}

/// type of firebase authentication used for the 'master' rydr user
enum AuthType { Apple, Google }
authTypeFromString(String type) {
  return type == null
      ? null
      : type == "google.com"
          ? AuthType.Google
          : type == "apple.com" ? AuthType.Apple : null;
}
