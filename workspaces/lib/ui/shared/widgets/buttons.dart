import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/cupertino.dart';
import 'package:rydrworkspaces/app/theme.dart';
import 'package:rydrworkspaces/ui/shared/blocs/buttons.dart';
import 'package:rydrworkspaces/ui/shared/widgets/badge.dart';

Widget backButtonIcon([Color color]) => Icon(
      Icons.chevron_left,
      size: 24.0,
      color: color,
    );

class AppBarBackButton extends StatelessWidget {
  final BuildContext context;
  final Function onPressed;
  final Function onLongPress;
  final Color color;

  AppBarBackButton(
    this.context, {
    this.onPressed,
    this.onLongPress,
    this.color,
  });

  @override
  Widget build(BuildContext context) => GestureDetector(
        onLongPress: onLongPress != null ? onLongPress : () {},
        child: IconButton(
          icon: backButtonIcon(color),
          onPressed: onPressed != null
              ? onPressed
              : () {
                  Navigator.of(context).pop();
                },
        ),
      );
}

class AppBarCloseButton extends StatelessWidget {
  final BuildContext context;
  final Function onPressed;
  final Function onLongPress;
  final Color iconColor;

  AppBarCloseButton(
    this.context, {
    this.onPressed,
    this.onLongPress,
    this.iconColor,
  });

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    return GestureDetector(
      onLongPress: onLongPress != null ? onLongPress : () {},
      child: IconButton(
        icon: Icon(
          Icons.close,
          size: 24.0,
          color: iconColor == null
              ? dark ? Colors.white : Colors.black
              : iconColor,
        ),
        // splashColor: Colors.transparent,
        // highlightColor: Colors.transparent,
        onPressed: onPressed != null
            ? onPressed
            : () {
                Navigator.of(context).pop();
              },
      ),
    );
  }
}

class ToggleButton extends StatelessWidget {
  final Function onChanged;
  final bool value;
  final Color color;

  ToggleButton({
    this.onChanged,
    this.value,
    this.color,
  });

  @override
  Widget build(BuildContext context) => Switch(
        onChanged: onChanged,
        value: value,
        activeColor: color != null ? color : Theme.of(context).primaryColor,
      );
}

class SecondaryButton extends StatefulWidget {
  final String label;
  final Function onTap;
  final BuildContext context;
  final bool fullWidth;
  final bool hasBadge;
  final bool primary;
  final int badgeCount;
  final Color badgeColor;

  SecondaryButton(
      {this.label,
      this.onTap,
      this.context,
      this.fullWidth = false,
      this.badgeCount,
      this.badgeColor,
      this.primary = false,
      this.hasBadge = false});

  @override
  _SecondaryButtonState createState() => _SecondaryButtonState();
}

class _SecondaryButtonState extends State<SecondaryButton> {
  final ButtonsBloc _bloc = ButtonsBloc();

  @override
  void initState() {
    _bloc.buttonPress(false);
    super.initState();
  }

  @override
  void dispose() {
    _bloc.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    Widget button(bool pressed) {
      return widget.fullWidth
          ? Transform.scale(
              scale: pressed ? 0.992 : 1.0,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: Container(
                  color: dark
                      ? Theme.of(context).appBarTheme.color
                      : AppColors.white.withOpacity(0.8),
                  child: Material(
                    color: Colors.transparent,
                    child: Container(
                      width: MediaQuery.of(context).size.width,
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(
                          color: widget.primary
                              ? Theme.of(context).primaryColor
                              : dark
                                  ? Theme.of(context).dividerColor
                                  : AppColors.grey300.withOpacity(0.5),
                          width: 1.25,
                        ),
                      ),
                      child: InkWell(
                        onTap: widget.onTap,
                        splashColor: dark
                            ? widget.primary
                                ? Theme.of(context)
                                    .primaryColor
                                    .withOpacity(0.2)
                                : Theme.of(context).dividerColor
                            : widget.primary
                                ? Theme.of(context)
                                    .primaryColor
                                    .withOpacity(0.2)
                                : AppColors.grey300.withOpacity(0.2),
                        highlightColor: dark
                            ? widget.primary
                                ? Theme.of(context)
                                    .primaryColor
                                    .withOpacity(0.3)
                                : Theme.of(context).dividerColor
                            : widget.primary
                                ? Theme.of(context)
                                    .primaryColor
                                    .withOpacity(0.3)
                                : AppColors.grey300.withOpacity(0.3),
                        enableFeedback: true,
                        child: Container(
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(8),
                          ),
                          padding: EdgeInsets.symmetric(
                              horizontal: 16.0, vertical: 8.0),
                          child: Text(widget.label,
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                  fontWeight: FontWeight.w600,
                                  fontSize: 14.0,
                                  color: widget.primary
                                      ? Theme.of(context).primaryColor
                                      : dark
                                          ? Colors.white
                                          : AppColors.grey800)),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            )
          : Transform.scale(
              scale: pressed ? 0.992 : 1.0,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: Container(
                  color: dark
                      ? Theme.of(context).appBarTheme.color
                      : AppColors.white.withOpacity(0.8),
                  child: Material(
                    color: Colors.transparent,
                    child: Container(
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(
                          color: widget.primary
                              ? Theme.of(context).primaryColor
                              : dark
                                  ? Theme.of(context).dividerColor
                                  : AppColors.grey300.withOpacity(0.5),
                          width: 1.25,
                        ),
                      ),
                      child: InkWell(
                        onTap: widget.onTap,
                        splashColor: dark
                            ? widget.primary
                                ? Theme.of(context)
                                    .primaryColor
                                    .withOpacity(0.2)
                                : Theme.of(context).dividerColor
                            : widget.primary
                                ? Theme.of(context)
                                    .primaryColor
                                    .withOpacity(0.2)
                                : AppColors.grey300.withOpacity(0.2),
                        highlightColor: dark
                            ? widget.primary
                                ? Theme.of(context)
                                    .primaryColor
                                    .withOpacity(0.3)
                                : Theme.of(context).dividerColor
                            : widget.primary
                                ? Theme.of(context)
                                    .primaryColor
                                    .withOpacity(0.3)
                                : AppColors.grey300.withOpacity(0.3),
                        enableFeedback: true,
                        child: Container(
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(8),
                          ),
                          padding: EdgeInsets.symmetric(
                              horizontal: 16.0, vertical: 8.5),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: <Widget>[
                              Text(widget.label,
                                  textAlign: TextAlign.center,
                                  style: TextStyle(
                                      fontWeight: FontWeight.w600,
                                      fontSize: 14.0,
                                      color: widget.primary
                                          ? Theme.of(context).primaryColor
                                          : dark
                                              ? Colors.white
                                              : AppColors.grey800)),
                              widget.hasBadge
                                  ? SizedBox(
                                      width: 6.0,
                                    )
                                  : Container(width: 0, height: 0),
                              widget.hasBadge
                                  ? Badge(
                                      color: widget.badgeColor,
                                      elevation: 0.0,
                                      value: widget.badgeCount.toString(),
                                    )
                                  : Container(width: 0, height: 0)
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            );
    }

    return StreamBuilder<Object>(
      stream: _bloc.tapDown,
      builder: (context, snapshot) {
        return Listener(
          onPointerDown: (PointerDownEvent event) => _bloc.buttonPress(true),
          onPointerUp: (PointerUpEvent event) => _bloc.buttonPress(false),
          child: button(snapshot.data != null ? snapshot.data : false),
        );
      },
    );
  }
}

class PrimaryButton extends StatefulWidget {
  final String label;
  final Function onTap;
  final BuildContext context;
  final Color buttonColor;
  final Color labelColor;
  final bool hasIcon;
  final bool bold;
  final bool rotateIcon;
  final bool hasShadow;
  final IconData icon;

  PrimaryButton(
      {this.label,
      this.onTap,
      this.context,
      this.icon,
      this.rotateIcon = false,
      this.hasIcon = false,
      this.buttonColor = AppColors.blue,
      this.hasShadow = false,
      this.labelColor = AppColors.white,
      this.bold = false});

  @override
  _PrimaryButtonState createState() => _PrimaryButtonState();
}

class _PrimaryButtonState extends State<PrimaryButton>
    with SingleTickerProviderStateMixin {
  final ButtonsBloc _bloc = ButtonsBloc();

  @override
  void initState() {
    _bloc.buttonPress(false);
    super.initState();
  }

  @override
  void dispose() {
    _bloc.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<bool>(
        stream: _bloc.tapDown,
        builder: (context, snapshot) {
          return Listener(
            onPointerDown: (PointerDownEvent event) => _bloc.buttonPress(true),
            onPointerUp: (PointerUpEvent event) => _bloc.buttonPress(false),
            child: GestureDetector(
              onTap: widget.onTap,
              child: Stack(
                alignment: Alignment.center,
                children: <Widget>[
                  Transform.scale(
                    scale: snapshot.data == true ? 0.992 : 1.0,
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(8),
                      child: AnimatedContainer(
                        duration: Duration(milliseconds: 250),
                        decoration: BoxDecoration(
                          boxShadow: widget.hasShadow
                              ? AppShadows.elevation[0]
                              : <BoxShadow>[
                                  BoxShadow(
                                      offset: Offset(0.0, 0.0),
                                      blurRadius: 0.0,
                                      spreadRadius: 0.0,
                                      color: Colors.transparent)
                                ],
                          color: widget.onTap != null
                              ? snapshot.data == true
                                  ? widget.buttonColor
                                  : widget.buttonColor.withOpacity(0.9)
                              : AppColors.grey300.withOpacity(0.5),
                        ),
                        child: Material(
                          color: Colors.transparent,
                          child: Container(
                            width: double.infinity,
                            child: InkWell(
                              onTap: () {
                                if (widget.onTap != null) {
                                  widget.onTap();
                                }
                              },
                              splashColor: widget.buttonColor !=
                                      Theme.of(context).primaryColor
                                  ? Colors.white30
                                  : AppColors.blue100,
                              highlightColor: widget.buttonColor,
                              enableFeedback: true,
                              child: Container(
                                height: 34.0,

                                padding: EdgeInsets.symmetric(
                                    horizontal: 16.0, vertical: 8.0),
                                // child:
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      Text(widget.label,
                          textAlign: TextAlign.center,
                          style: TextStyle(
                              fontWeight: widget.bold
                                  ? FontWeight.w700
                                  : FontWeight.w600,
                              fontSize: 16.0,
                              color: widget.labelColor)),
                      widget.hasIcon
                          ? SizedBox(
                              width: 6.0,
                            )
                          : Container(width: 0, height: 0),
                      widget.hasIcon
                          ? Transform.rotate(
                              angle: widget.rotateIcon ? 180 / pi : 0,
                              child: Padding(
                                padding: EdgeInsets.only(
                                    right: widget.rotateIcon ? 3.0 : 0.0),
                                child: Icon(
                                  widget.icon,
                                  size: 16.0,
                                  color: widget.labelColor,
                                ),
                              ),
                            )
                          : Container(width: 0, height: 0)
                    ],
                  ),
                ],
              ),
            ),
          );
        });
  }
}

class TextButton extends StatelessWidget {
  final String label;
  final Function onTap;
  final bool isPrimary;
  final bool isBasic;
  final bool bold;
  final bool caption;
  final Color color;

  TextButton({
    this.label,
    this.onTap,
    this.isPrimary = true,
    this.isBasic = false,
    this.caption = false,
    this.bold = true,
    this.color = AppColors.grey800,
  });

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return Container(
      child: InkWell(
        highlightColor: Colors.transparent,
        onTap: onTap,
        child: Container(
          padding: EdgeInsets.symmetric(
              horizontal: isBasic ? 8.0 : 16.0, vertical: isBasic ? 4.0 : 8.0),
          child: Center(
            child: Text(
              label,
              textAlign: TextAlign.center,
              style: TextStyle(
                  fontWeight: bold ? FontWeight.w600 : FontWeight.w400,
                  fontSize: isBasic
                      ? caption
                          ? Theme.of(context).textTheme.caption.fontSize
                          : 14.0
                      : caption
                          ? Theme.of(context).textTheme.caption.fontSize
                          : 16.0,
                  color: onTap == null
                      ? AppColors.grey300
                      : color == null
                          ? isPrimary
                              ? Theme.of(context).primaryColor
                              : dark ? Colors.white : AppColors.grey800
                          : color),
            ),
          ),
        ),
      ),
    );
  }
}

class FloatingButton extends StatelessWidget {
  final String label;
  final Function onTap;
  final BuildContext context;
  final bool fullWidth;
  final bool hasBadge;
  final int badgeCount;
  final Color badgeColor;
  final Color labelColor;

  FloatingButton(
      {this.label,
      this.onTap,
      this.context,
      this.fullWidth = false,
      this.badgeCount,
      this.badgeColor,
      this.hasBadge = false,
      this.labelColor = AppColors.blue});

  @override
  Widget build(BuildContext context) {
    return fullWidth
        ? Container(
            width: MediaQuery.of(context).size.width,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(4.0),
              boxShadow: AppShadows.elevation[0],
            ),
            child: InkWell(
              onTap: onTap,
              child: Container(
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(4.0),
                  color: AppColors.white,
                ),
                padding: EdgeInsets.symmetric(horizontal: 16.0, vertical: 8.5),
                child: Text(label,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 16.0,
                        color: labelColor)),
              ),
            ),
          )
        : Ink(
            decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(4.0),
                boxShadow: AppShadows.elevation[0],
                color: Colors.white),
            child: InkWell(
              onTap: onTap,
              child: Container(
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(4.0),
                  color: AppColors.white,
                ),
                padding: EdgeInsets.symmetric(horizontal: 16.0, vertical: 8.5),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Text(label,
                        textAlign: TextAlign.center,
                        style: TextStyle(
                            fontWeight: FontWeight.w600,
                            fontSize: 16.0,
                            color: labelColor)),
                    hasBadge
                        ? SizedBox(
                            width: 6.0,
                          )
                        : Container(width: 0, height: 0),
                    hasBadge
                        ? Badge(
                            color: badgeColor,
                            elevation: 0.0,
                            value: badgeCount.toString(),
                          )
                        : Container(width: 0, height: 0)
                  ],
                ),
              ),
            ),
          );
  }
}
