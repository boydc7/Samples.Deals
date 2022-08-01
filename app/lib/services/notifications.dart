import 'dart:async';
import 'package:firebase_messaging/firebase_messaging.dart';

import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/models/enums/notification.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/notifications.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/services/device_storage.dart';

class NotificationService {
  static final _log = getLogger('NotificationService');

  /// get these on first start/connect of app, save them
  /// to re-use across other functions that need access to them
  static String _firebaseToken;
  static String _messagingTokenHash;

  /// Subscribe this device (and implicit user) to notifications
  /// This is called when the app starts, or when we want to refresh workspaces
  static Future<Null> subscribe() async {
    /// get a messaging token from firebase, will get cached version if
    /// we've previously gotten a valid token from firebase
    /// store this in static variable so we have access to it in other functions
    _firebaseToken = await FirebaseMessaging().getToken();

    /// if we for some reason were not able to get a token then there's nothing to do
    /// we'll log a severe error but otherwise we don't subscribe anyone/anything
    if (_firebaseToken == null) {
      _log.e(
        'FirebaseMessaging().getToken | unable to retrieve firebase messaging token',
      );

      return;
    }

    /// if we've previously gotten a token, then we've also gotten back a hashed
    /// version of it from the server and stored it in local preference - we use this going forward
    /// to manage notification specific calls vs. storing and sendig the full token each time
    if (_messagingTokenHash == null || _messagingTokenHash.isEmpty) {
      _messagingTokenHash = await DeviceStorage.getMessagingTokenHash();
    }

    // Send the token and existing hash (if any) to the server, it'll do the rest
    Map<String, dynamic> payload = {
      "token": _firebaseToken,
    };

    if (_messagingTokenHash != null && _messagingTokenHash.isNotEmpty) {
      payload['oldTokenHash'] = _messagingTokenHash;
    }

    final ApiResponse apiResponse = await AppApi.instance.post(
      'notifications/subscribe',
      body: payload,
    );

    // If successful and we receive a tokenHash back, store it now
    if (apiResponse.hasData) {
      _messagingTokenHash = apiResponse.response.data['result']['id'];

      await DeviceStorage.setMessagingTokenHash(_messagingTokenHash);
    }
  }

  /// Unsubscribe this device from notifications
  /// This is called when the user signs out of their master facebook account
  static Future<Null> unsubscribe() async {
    if (_messagingTokenHash == null || _messagingTokenHash.isEmpty) {
      _messagingTokenHash ??= await DeviceStorage.getMessagingTokenHash();
    }

    if (_messagingTokenHash == null || _messagingTokenHash.isEmpty) {
      return;
    }

    // Unsubscribe this token from notifications on the server
    await AppApi.instance.delete(
      'notifications/subscribe/$_messagingTokenHash',
    );

    // Clear the in-memory values, but do not clear the stored messaging token hash...it'll get reset/resent on
    // a new subscribe attempt if one is made, and the server will handle ignoring it if appropriate, or using it
    _firebaseToken = null;
    _messagingTokenHash = null;
  }

  /// gets list of subscription for a given user
  /// all topics that they're currently subscribed to
  static Future<NotificationSubscriptionResponse> getSubscriptions(
      [bool forceRefresh = false]) async {
    final String path = 'notifications/subscriptions';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return NotificationSubscriptionResponse.fromApiResponse(apiResponse);
  }

  // Subscribe to a given notification type
  static Future<Null> setSubscriptionTo({
    AppNotificationType type,
  }) async {
    await AppApi.instance.put(
      'notifications/subscriptions',
      body: {
        'notificationType': appNotificationTypeToString(type),
      },
    );

    /// refresh cache
    getSubscriptions(true);
  }

  // Unsubscribe to a given notification type
  static Future<Null> deleteSubscriptionTo({
    AppNotificationType type,
  }) async {
    await AppApi.instance.delete(
      'notifications/subscriptions/${appNotificationTypeToString(type)}',
    );

    /// refresh cache
    getSubscriptions(true);
  }

  static Future<NotificationsResponse> queryNotifications({
    int skip = 0,
    int take = 25,
    bool forceRefresh = false,
  }) async {
    Map<String, dynamic> params = {
      "skip": skip.toString(),
      "take": take.toString(),
    };

    final ApiResponse apiResponse = await AppApi.instance.get(
      'notifications',
      queryParams: params,
    );

    return NotificationsResponse.fromApiResponse(apiResponse);
  }

  /// marks a single notification, or all for a given user as read
  ///
  /// no need for this to be async, we can fire and forget
  static void markAsRead([String notificationId]) {
    AppApi.instance.delete(
      notificationId != null
          ? 'notifications/$notificationId'
          : 'notifications',
    );
  }
}
