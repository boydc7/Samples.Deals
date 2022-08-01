import 'package:flutter/material.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class DealChipInput extends StatelessWidget {
  final Function onTap;
  final String labelText;
  final String hintText;
  final String value;
  final List<Widget> valueDisplay;

  DealChipInput({
    @required this.onTap,
    @required this.labelText,
    @required this.value,
    @required this.valueDisplay,
    this.hintText,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Stack(
        overflow: Overflow.visible,
        children: <Widget>[
          Container(
            padding: EdgeInsets.all(12.0),
            alignment: Alignment.centerLeft,
            // height: 58.0,
            width: double.infinity,
            decoration: BoxDecoration(
              border: Border.all(color: Theme.of(context).hintColor),
              borderRadius: BorderRadius.circular(4.0),
            ),
            child: valueDisplay == null
                ? Row(children: <Widget>[
                    AnimatedDefaultTextStyle(
                      duration: Duration(milliseconds: 150),
                      child: Text(hintText),
                      style: Theme.of(context).textTheme.bodyText2.merge(
                            TextStyle(
                                fontSize: 16.0,
                                color: value != ""
                                    ? Theme.of(context)
                                        .textTheme
                                        .bodyText2
                                        .color
                                    : Theme.of(context).hintColor),
                          ),
                    )
                  ])
                : Wrap(children: valueDisplay),
          ),
          value != ""
              ? Positioned(
                  top: -6.0,
                  left: 8.0,
                  child: FadeInBottomTop(
                      0,
                      Container(
                        padding: EdgeInsets.symmetric(horizontal: 4.0),
                        color: Theme.of(context).scaffoldBackgroundColor,
                        child: Text(
                          labelText,
                          style: Theme.of(context).textTheme.caption.merge(
                                TextStyle(color: Theme.of(context).hintColor),
                              ),
                        ),
                      ),
                      250),
                )
              : Container(
                  width: 0,
                  height: 0,
                )
        ],
      ),
    );
  }
}
