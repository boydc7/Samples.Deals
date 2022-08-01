enum DealType {
  Unknown,
  Deal,
  Event,
  Virtual,
}

DealType dealTypeFromJson(String dealType) {
  if (dealType == null) {
    return DealType.Deal;
  }

  switch (dealType.toLowerCase()) {
    case "deal":
      return DealType.Deal;
      break;
    case "event":
      return DealType.Event;
      break;
    case "virtual":
      return DealType.Virtual;
      break;
    default:
      return DealType.Unknown;
      break;
  }
}

String dealTypeToString(DealType dealType) =>
    dealType.toString().replaceFirst('DealType.', '');

enum DealStatus {
  draft,
  published,
  completed,
  deleted,
  paused,
}

DealStatus dealStatusFromJson(String status) {
  if (status == null) {
    return null;
  }

  switch (status.toLowerCase()) {
    case "published":
      return DealStatus.published;
      break;
    case "completed":
      return DealStatus.completed;
      break;
    case "paused":
      return DealStatus.paused;
      break;
    case "deleted":
      return DealStatus.deleted;
      break;
    default:
      return DealStatus.draft;
      break;
  }
}

String dealStatusToString(DealStatus status) =>
    status.toString().replaceFirst('DealStatus.', '');

String dealStatusToStringDisplay(DealStatus status) {
  /// map requests & deal statuses to display friendly names
  final Map<String, String> names = {
    "draft": "Draft",
    "published": "Active",
    "completed": "Archived",
    "deleted": "Deleted",
    "paused": "Paused",
  };

  return names[dealStatusToString(status)] ?? '';
}

enum DealSort { newest, followerValue, expiring, closest }

dealSortToString(DealSort sort) => sort.toString().replaceAll('DealSort.', '');

enum DealVisibilityType { Marketplace, InviteOnly }

enum DealThresholdType { Restrictions, Insights }

enum DealMetricType {
  Unknown,
  Impressed,
  Clicked,
  XClicked,
  Created,
  Updated,
  Requested,
  RequestApproved,
  RequestDenied,
  RequestCompleted,
  RequestCancelled,
}

dealMetricTypeToString(DealMetricType type) =>
    type.toString().replaceAll('DealMetricType.', '');

enum DealRequestStatus {
  unknown,
  requested,
  invited,
  denied,
  inProgress,
  redeemed,
  completed,
  cancelled,
  delinquent
}
DealRequestStatus dealRequestStatusFromJson(String status) {
  if (status == null) {
    return null;
  }

  switch (status.toLowerCase()) {
    case "requested":
      return DealRequestStatus.requested;
      break;
    case "invited":
      return DealRequestStatus.invited;
      break;
    case "denied":
      return DealRequestStatus.denied;
      break;
    case "inprogress":
      return DealRequestStatus.inProgress;
      break;
    case "redeemed":
      return DealRequestStatus.redeemed;
      break;
    case "completed":
      return DealRequestStatus.completed;
      break;
    case "cancelled":
      return DealRequestStatus.cancelled;
      break;
    case "delinquent":
      return DealRequestStatus.delinquent;
      break;
    default:
      return DealRequestStatus.unknown;
  }
}

String dealRequestStatusToString(DealRequestStatus status) =>
    status.toString().replaceFirst('DealRequestStatus.', '');

String dealRequestStatusToStringDisplay(DealRequestStatus status) {
  /// map requests & deal statuses to display friendly names
  final Map<String, String> names = {
    "requested": "Requests",
    "invited": "Invites",
    "denied": "Declined",
    "inProgress": "In-Progress",
    "redeemed": "Redeemed",
    "completed": "Completed",
    "cancelled": "Cancelled",
    "delinquent": "Delinquent"
  };

  return names[dealRequestStatusToString(status)] ?? '';
}

enum DealRestrictionType {
  unknown,
  minFollowerCount,
  minEngagementRating,
  minAge,
}

dealRestrictionTypeFromString(String type) {
  switch (type.toLowerCase()) {
    case "minfollowercount":
      return DealRestrictionType.minFollowerCount;
      break;
    case "minengagementrating":
      return DealRestrictionType.minEngagementRating;
      break;
    case "minage":
      return DealRestrictionType.minAge;
      break;
    default:
      return DealRestrictionType.unknown;
      break;
  }
}

dealRestrictionTypeToString(DealRestrictionType type) =>
    type.toString().replaceAll('DealRestrictionType.', '');

enum DealStatType {
  unknown,
  totalRequests,
  totalApproved,
  totalDenied,
  totalRedeemed,
  totalCompleted,
  totalCancelled,
  currentRequested,
  currentApproved,
  currentDenied,
  currentRedeemed,
  currentCompleted,
  currentCancelled,
  totalInvites,
  currentInvites,
  publishedDeals,
  completedThisWeek,
  completedLastWeek,
  totalDelinquent,
  currentDelinquent,
}

const _dealStatTypeMap = <String, DealStatType>{
  "Unknown": DealStatType.unknown,
  "TotalRequests": DealStatType.totalRequests,
  "TotalApproved": DealStatType.totalApproved,
  "TotalDenied": DealStatType.totalDenied,
  "TotalRedeemed": DealStatType.totalRedeemed,
  "TotalCompleted": DealStatType.totalCompleted,
  "TotalCancelled": DealStatType.totalCancelled,
  "CurrentRequested": DealStatType.currentRequested,
  "CurrentApproved": DealStatType.currentApproved,
  "CurrentDenied": DealStatType.currentDenied,
  "CurrentRedeemed": DealStatType.currentRedeemed,
  "CurrentCompleted": DealStatType.currentCompleted,
  "CurrentCancelled": DealStatType.currentCancelled,
  "TotalInvites": DealStatType.totalInvites,
  "CurrentInvites": DealStatType.currentInvites,
  "PublishedDeals": DealStatType.publishedDeals,
  "CompletedThisWeek": DealStatType.completedThisWeek,
  "CompletedLastWeek": DealStatType.completedLastWeek,
  "TotalDelinquent": DealStatType.totalDelinquent,
  "CurrentDelinquent": DealStatType.currentDelinquent,
};

dealStatTypeFromString(String type) =>
    type == null ? DealStatType.unknown : _dealStatTypeMap[type];

enum DealMetaType {
  Unknown,
  StartDate,
  EndDate,
  MediaStartDate,
}

const _dealMetaTypeMap = <String, DealMetaType>{
  "Unknown": DealMetaType.Unknown,
  "StartDate": DealMetaType.StartDate,
  "EndDate": DealMetaType.EndDate,
  "MediaStartDate": DealMetaType.MediaStartDate,
};

dealMetaTypeToString(DealMetaType type) =>
    type.toString().replaceAll('DealMetaType.', '');

dealMetaTypeFromString(String type) =>
    type == null ? DealMetaType.Unknown : _dealMetaTypeMap[type];
