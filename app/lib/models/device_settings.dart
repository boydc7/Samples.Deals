class IOsPushNotificationSettings {
  bool alert;
  bool sound;
  bool badge;
  DateTime lastChecked;
  bool dismissed;

  IOsPushNotificationSettings({
    this.alert,
    this.sound,
    this.badge,
    this.lastChecked,
    this.dismissed = false,
  });

  Map<String, dynamic> toJson() => {
        "alert": this.alert,
        "sound": this.sound,
        "badge": this.badge,
        "lastChecked": this.lastChecked.toString(),
        "dismissed": this.dismissed,
      };

  IOsPushNotificationSettings.fromJson(Map<String, dynamic> json) {
    this.alert = json['alert'];
    this.sound = json['sound'];
    this.badge = json['badge'];
    this.lastChecked = DateTime.parse(json['lastChecked']);
    this.dismissed = json['dismissed'];
  }

  @override
  String toString() {
    return """alert: $alert, sound: $sound, badge: $badge, lastChecked: $lastChecked, dismissed: $dismissed""";
  }
}

class OnboardSettings {
  bool doneAsCreator;
  bool doneAsBusiness;
  bool askedLocation;
  bool askedNotifications;
  bool creatorSawAutoApprove;

  OnboardSettings({
    this.doneAsBusiness = false,
    this.doneAsCreator = false,
    this.askedLocation = false,
    this.askedNotifications = false,
    this.creatorSawAutoApprove = false,
  });

  Map<String, dynamic> toJson() => {
        "doneAsCreator": this.doneAsCreator,
        "doneAsBusiness": this.doneAsBusiness,
        "askedLocation": this.askedLocation,
        "askedNotifications": this.askedNotifications,
        "creatorSawAutoApprove": this.creatorSawAutoApprove,
      };

  OnboardSettings.fromJson(Map<String, dynamic> json) {
    this.doneAsBusiness = json['doneAsBusiness'];
    this.doneAsCreator = json['doneAsCreator'];
    this.askedNotifications = json['askedNotifications'];
    this.askedLocation = json['askedLocation'];

    /// any new setting we'll have to default otherwise already saved and would be null
    this.creatorSawAutoApprove = json['creatorSawAutoApprove'] ?? false;
  }

  @override
  String toString() =>
      """doneAsBusiness: $doneAsBusiness, doneAsCreator: $doneAsCreator, askedLocation: $askedLocation, askedNotifications: $askedNotifications, creatorSawAutoApprove: $creatorSawAutoApprove""";
}

class DeviceInfo {
  int activeWorkspaceId;
  int activeProfileId;

  DeviceInfo();

  DeviceInfo.fromJson(Map<String, dynamic> json) {
    this.activeWorkspaceId = json['activeWorkspaceId'];
    this.activeProfileId = json['activeProfileId'];
  }

  Map<String, dynamic> toJson() => {
        "activeWorkspaceId": this.activeWorkspaceId,
        "activeProfileId": this.activeProfileId,
      };

  @override
  String toString() {
    return """activeWorkspaceId: $activeWorkspaceId, activeProfileId: $activeProfileId""";
  }
}

class UsageInfo {
  int opened;
  int elapsedSecondsSinceLastOpen;
  DateTime lastOpen;

  UsageInfo({
    this.opened = 0,
    this.elapsedSecondsSinceLastOpen = 0,
    this.lastOpen,
  });

  Map<String, dynamic> toJson() {
    /// track seconds since last open
    final DateTime now = DateTime.now();
    final int elapsedSeconds =
        this.lastOpen != null ? now.difference(this.lastOpen).inSeconds : 0;

    return {
      "opened": this.opened,
      "elapsedSecondsSinceLastOpen": elapsedSeconds,
      "lastOpen": now.toString(),
    };
  }

  UsageInfo.fromJson(Map<String, dynamic> json) {
    this.opened = json['opened'];
    this.elapsedSecondsSinceLastOpen = json['elapsedSecondsSinceLastOpen'];
    this.lastOpen = DateTime.parse(json['lastOpen']);
  }

  @override
  String toString() {
    return """opened: $opened, elapsedSecondsSinceLastOpen: $elapsedSecondsSinceLastOpen, lastOpen: $lastOpen""";
  }
}
