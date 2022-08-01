import 'package:flutter/material.dart';

import 'package:rydr_app/app/theme.dart';

class Badge extends StatelessWidget {
  final Color color;
  final Color valueColor;
  final String value;
  final double elevation;
  final bool large;
  final bool medium;

  Badge(
      {this.color = Colors.red,
      this.elevation = 2.0,
      this.value,
      this.large = false,
      this.medium = false,
      this.valueColor = AppColors.white});

  @override
  Widget build(BuildContext context) {
    return Material(
        type: MaterialType.button,
        borderRadius: BorderRadius.circular(30.0),
        elevation: elevation,
        color: color,
        child: Container(
          constraints: BoxConstraints(
            minWidth: large ? 30.0 : 17.0,
          ),
          padding: EdgeInsets.only(
            left: large ? 9.0 : medium ? 7.5 : 5.0,
            right: large ? 9.0 : medium ? 7.5 : 5.0,
            top: large ? 6.0 : medium ? 4.0 : 2.0,
            bottom: large ? 6.0 : medium ? 4.0 : 2.0,
          ),
          child: Text(
            value,
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: large ? 14.0 : medium ? 12.5 : 11.0,
              color: valueColor,
              fontWeight: large ? FontWeight.w500 : FontWeight.bold,
            ),
          ),
        ));
  }
}
