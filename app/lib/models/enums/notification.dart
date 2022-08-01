enum AppNotificationType {
  unspecified,
  message,
  dialog,
  dealMatched,
  dealRequested,
  dealInvited,
  dealRequestApproved,
  dealRequestDenied,
  dealRequestCancelled,
  dealRequestRedeemed,
  dealRequestCompleted,
  dealRequestDelinquent,
  dealCompletionMediaDetected,
  accountAttention,

  /// Not yet implemented
  workspaceEvent,
  emailReminders,
  emailProductAnnouncements,
  emailFeedback,
  emailInvitations,
  emailDealMatch,
  emailMonthlySummary,
  all,
}

const _appNotificationTypeMap = <String, AppNotificationType>{
  "unspecified": AppNotificationType.unspecified,
  "message": AppNotificationType.message,
  "dialog": AppNotificationType.dialog,
  "dealmatched": AppNotificationType.dealMatched,
  "dealrequested": AppNotificationType.dealRequested,
  "dealinvited": AppNotificationType.dealInvited,
  "dealrequestapproved": AppNotificationType.dealRequestApproved,
  "dealrequestdenied": AppNotificationType.dealRequestDenied,
  "dealrequestcancelled": AppNotificationType.dealRequestCancelled,
  "dealrequestredeemed": AppNotificationType.dealRequestRedeemed,
  "dealrequestcompleted": AppNotificationType.dealRequestCompleted,
  "dealrequestdelinquent": AppNotificationType.dealRequestDelinquent,
  "dealcompletionmediadetected":
      AppNotificationType.dealCompletionMediaDetected,
  "accountattention": AppNotificationType.accountAttention,
  "workspaceevent": AppNotificationType.workspaceEvent,
  "emailreminders": AppNotificationType.emailReminders,
  "emailproductannouncements": AppNotificationType.emailProductAnnouncements,
  "emailfeedback": AppNotificationType.emailFeedback,
  "emailinvitations": AppNotificationType.emailInvitations,
  "emaildealmatch": AppNotificationType.dealMatched,
  "emailmonthlysummary": AppNotificationType.emailMonthlySummary,
  "all": AppNotificationType.all,
};

appNotificationTypeToString(AppNotificationType type) =>
    type.toString().replaceAll('AppNotificationType.', '');

appNotificationTypeFromString(String type) => type == null
    ? AppNotificationType.unspecified
    : _appNotificationTypeMap[type.toLowerCase()];
