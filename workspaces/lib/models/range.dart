class LongRange {
  final int min;
  final int max;

  LongRange(this.min, this.max);

  LongRange.fromJson(Map<String, dynamic> json)
      : min = json != null ? json['min'] : 0,
        max = json != null ? json['max'] : 0;

  @override
  String toString() => "{min:$min,max:$max}";
}

class DoubleRange {
  final double min;
  final double max;

  DoubleRange(this.min, this.max);

  DoubleRange.fromJson(Map<String, dynamic> json)
      : min = json != null ? json['min'].toDouble() : 0.0,
        max = json != null ? json['max'].toDouble() : 0.0;
}
