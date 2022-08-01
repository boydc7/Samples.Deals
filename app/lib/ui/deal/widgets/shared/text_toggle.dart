import 'package:flutter/material.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class DealTextToggle extends StatelessWidget {
  final String labelText;
  final String subtitleText;
  final bool selected;
  final Function onChange;

  DealTextToggle({
    @required this.labelText,
    @required this.selected,
    @required this.onChange,
    this.subtitleText = '',
  });

  @override
  Widget build(BuildContext context) => Row(
        mainAxisAlignment: MainAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(labelText, style: Theme.of(context).textTheme.bodyText1),
                SizedBox(height: 4),
                subtitleText == ''
                    ? Container()
                    : Text(subtitleText,
                        style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor))),
              ],
            ),
          ),
          SizedBox(width: 16),
          ToggleButton(value: selected, onChanged: onChange)
        ],
      );
}
