import 'package:rydrworkspaces/models/enums/publisher_media.dart';

class PublisherMediaLineItem {
  PublisherContentType type;
  int quantity;

  PublisherMediaLineItem({
    this.type,
    this.quantity,
  });

  PublisherMediaLineItem.fromJson(Map<String, dynamic> json) {
    this.type = publisherContentTypeFromJson(json['type']);
    this.quantity = json['quantity'];
  }

  Map<String, dynamic> toJson() {
    return {
      "type": publisherContentTypeToString(this.type),
      "quantity": this.quantity,
    };
  }
}
