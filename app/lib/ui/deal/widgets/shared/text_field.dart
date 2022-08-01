import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:rydr_app/app/icons.dart';

class DealTextField extends StatelessWidget {
  final TextEditingController controller;
  final FocusNode focusNode;
  final TextInputType keyboardType;
  final int minLines;
  final int maxLines;
  final int maxCharacters;
  final String labelText;
  final String hintText;
  final bool autoFocus;
  final bool isVirtual;

  DealTextField({
    @required this.controller,
    this.focusNode,
    @required this.minLines,
    @required this.maxLines,
    @required this.maxCharacters,
    @required this.labelText,
    @required this.hintText,
    this.keyboardType = TextInputType.text,
    this.autoFocus = false,
    this.isVirtual = false,
  });

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      textInputAction: TextInputAction.next,
      focusNode: focusNode,
      controller: controller,
      style: Theme.of(context).textTheme.bodyText2.merge(
            TextStyle(
              fontSize: 16.0,
            ),
          ),
      minLines: minLines,
      maxLines: maxLines,
      keyboardType: keyboardType,
      autofocus: autoFocus,
      keyboardAppearance: Theme.of(context).brightness,
      textCapitalization: TextCapitalization.sentences,
      inputFormatters: [
        LengthLimitingTextInputFormatter(maxCharacters),
      ],
      decoration: InputDecoration(
        alignLabelWithHint: true,
        labelText: labelText,
        labelStyle: TextStyle(color: Theme.of(context).hintColor),
        hintText: hintText,
        prefixIcon: keyboardType == TextInputType.number
            ? Icon(AppIcons.dollarSign,
                size: 18, color: Theme.of(context).hintColor)
            : null,
        focusedBorder: OutlineInputBorder(
          borderSide: BorderSide(
              color: isVirtual
                  ? Colors.deepOrange
                  : Theme.of(context).primaryColor,
              width: 2.0),
        ),
        enabledBorder: OutlineInputBorder(
          borderSide: BorderSide(color: Theme.of(context).hintColor),
        ),
        border: OutlineInputBorder(
          borderSide: BorderSide(color: Theme.of(context).hintColor),
        ),
      ),
      cursorColor:
          isVirtual ? Colors.deepOrange : Theme.of(context).primaryColor,
    );
  }
}
