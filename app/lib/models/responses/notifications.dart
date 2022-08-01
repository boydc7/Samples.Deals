import 'package:rydr_app/models/notification.dart';
import 'package:rydr_app/models/notification_settings.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class NotificationsResponse extends BaseResponses<AppNotification> {
  NotificationsResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => AppNotification.fromJson(d))
                    .cast<AppNotification>()
                    .toList()
                : []);

  NotificationsResponse.fromModels(List<AppNotification> models)
      : super.fromModels(
          models,
        );
}

class NotificationSubscriptionResponse
    extends BaseResponses<NotificationSetting> {
  NotificationSubscriptionResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j
                .map((dynamic d) => NotificationSetting.fromJson(d))
                .cast<NotificationSetting>()
                .toList());
}
