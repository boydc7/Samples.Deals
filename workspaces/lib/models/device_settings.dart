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
