import 'package:flutter/material.dart';
import 'package:shimmer/shimmer.dart';

import 'package:rydr_app/app/theme.dart';

class EngagementCalculator extends StatelessWidget {
  final int followerCount;
  final double engagementRate;

  EngagementCalculator({
    @required this.followerCount,
    @required this.engagementRate,
  });

  final List<EngagementRate> rates = [
    EngagementRate(0, 2000, 10.7),
    EngagementRate(2000, 5000, 6.0),
    EngagementRate(5000, 10000, 4.9),
    EngagementRate(1000, 25000, 3.6),
    EngagementRate(25000, 50000, 3.1),
    EngagementRate(50000, 75000, 2.6),
    EngagementRate(75000, 100000, 2.5),
    EngagementRate(100000, 150000, 2.5),
    EngagementRate(150000, 250000, 2.4),
    EngagementRate(250000, 500000, 2.9),
    EngagementRate(500000, 1000000, 2.3),
    EngagementRate(1000000, 1000000000, 1.5),
  ];

  @override
  Widget build(BuildContext context) {
    final double averageEngRate = rates
        .firstWhere(
            (EngagementRate rate) =>
                rate.min >= followerCount && rate.max < followerCount,
            orElse: () => EngagementRate(0, 0, 6.0))
        .rate;

    final double engRateDifference = averageEngRate - engagementRate;
    final double engRateDiffAbsolute = engRateDifference.abs();
    final double differenceFactor =
        engRateDiffAbsolute > 0 && averageEngRate > 0
            ? engRateDiffAbsolute / averageEngRate
            : 0;
    final bool isBetter = engRateDifference.isNegative;

    ///if true, this means the users engagement rating is higher than the average

    final bool isLow = differenceFactor >= 1 && !isBetter;
    final bool isOk =
        differenceFactor > 0.4 && differenceFactor < 1 && !isBetter;
    final bool isNormal = differenceFactor > 0 && differenceFactor <= 0.4;
    final bool isHigh =
        differenceFactor > 0.4 && differenceFactor <= 1.4 && isBetter;
    final bool isAwesome = differenceFactor > 1.4 && isBetter;

    final String label = isLow
        ? 'Very Low'
        : isOk
            ? 'Average'
            : isNormal
                ? 'Above Average'
                : isHigh ? 'Very High' : isAwesome ? 'Influencer Status' : '';

    final Color chipColor = isLow
        ? AppColors.grey400
        : isOk
            ? Colors.yellow.shade700
            : isNormal
                ? AppColors.teal
                : isHigh
                    ? AppColors.successGreen
                    : isAwesome
                        ? Theme.of(context).appBarTheme.color
                        : AppColors.white;

    return Container(
      margin: EdgeInsets.only(top: 4.0),
      decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(8.0), color: chipColor),
      padding: EdgeInsets.symmetric(horizontal: 8.0, vertical: 4.0),
      child: isAwesome
          ? Shimmer.fromColors(
              baseColor: Colors.red,
              highlightColor: Colors.yellow,
              child: Text(label,
                  style: TextStyle(
                      fontSize: 12.0,
                      color: AppColors.white,
                      height: 1.0,
                      fontWeight: FontWeight.w500)),
            )
          : Text(label,
              style: TextStyle(
                  fontSize: 12.0,
                  color: Theme.of(context).appBarTheme.color,
                  height: 1.0,
                  fontWeight: FontWeight.w500)),
    );
  }
}

class EngagementRate {
  int min;
  int max;
  double rate;

  EngagementRate(
    this.min,
    this.max,
    this.rate,
  );
}
