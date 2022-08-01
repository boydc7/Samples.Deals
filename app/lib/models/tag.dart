class Tag {
  final String key;
  final String value;

  Tag(this.key, this.value);

  Tag.fromJson(Map<String, dynamic> json)
      : key = json['key'],
        value = json['value'];

  toJson() => {
        "key": key,
        "value": value,
      };
}
