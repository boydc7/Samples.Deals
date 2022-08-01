class RecordType {
  RecordOfType type;
  int id;

  RecordType(this.id, this.type);

  Map<String, dynamic> toJson() => {
        "type": this.type.toString().replaceAll('RecordOfType.', ''),
        "id": this.id
      };

  RecordType.fromJson(Map<String, dynamic> json) {
    this.type = recordOfTypeFromJson(json['type']);
    this.id = json['id'];
  }
}

enum RecordOfType {
  Unknown, // 0
  Contact, // 1
  File,
  FileText,
  PublisherApp,
  PublisherAccount, // 5
  Place,
  Deal,
  Hashtag,
  Message,
  Dialog, // 10
}
recordOfTypeFromJson(String type) {
  if (type == null) {
    return RecordOfType.Unknown;
  }

  switch (type.toLowerCase()) {
    case "contact":
      return RecordOfType.Contact;
      break;
    case "file":
      return RecordOfType.File;
      break;
    case "filetext":
      return RecordOfType.FileText;
      break;
    case "publisherapp":
      return RecordOfType.PublisherApp;
      break;
    case "publisheraccount":
      return RecordOfType.PublisherAccount;
      break;
    case "place":
      return RecordOfType.Place;
      break;
    case "deal":
      return RecordOfType.Deal;
      break;
    case "hashtag":
      return RecordOfType.Hashtag;
      break;
    case "message":
      return RecordOfType.Message;
      break;
    case "dialog":
      return RecordOfType.Dialog;
      break;
    default:
      return RecordOfType.Unknown;
  }
}

String recordOfTypeToString(RecordOfType type) {
  return type.toString().replaceFirst('RecordOfType.', '');
}
