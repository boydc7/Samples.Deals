enum MediaStatType {
  comments,
  likes,
  unspecified,
}
MediaStatType mediaStatTypeFromString(String type) {
  if (type == null) {
    return null;
  }

  switch (type.toLowerCase()) {
    case "comments":
      return MediaStatType.comments;
    case "likes":
      return MediaStatType.likes;
    default:
      return MediaStatType.unspecified;
  }
}

mediaStatTypeToString(MediaStatType type) {
  return type.toString().replaceAll('MediaStatType.', '');
}

enum MediaType {
  unknown,
  carouselAlbum,
  video,
  image,
}
MediaType mediaTypeFromString(String type) {
  if (type == null) {
    return null;
  }
  switch (type.toLowerCase()) {
    case "carousel_album":
      return MediaType.carouselAlbum;
    case "video":
      return MediaType.video;
    case "image":
      return MediaType.image;
    default:
      return MediaType.unknown;
  }
}

mediaTypeToString(MediaType type) {
  return type.toString().replaceAll('MediaType.', '');
}

enum PublisherContentType { unkonwn, post, story }
PublisherContentType publisherContentTypeFromJson(String type) {
  if (type == null) {
    return PublisherContentType.unkonwn;
  }

  switch (type.toLowerCase()) {
    case "post":
      return PublisherContentType.post;
      break;
    case "story":
      return PublisherContentType.story;
      break;
    default:
      return PublisherContentType.unkonwn;
      break;
  }
}

String publisherContentTypeToString(PublisherContentType type) {
  return type.toString().replaceFirst('PublisherContentType.', '');
}
