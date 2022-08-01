import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/shared/widgets/sprite_animation.dart';

class AnimatedLogo extends StatelessWidget {
  final Color color;

  AnimatedLogo([this.color]);

  @override
  Widget build(BuildContext context) => AnimatedContainer(
        curve: Curves.easeOutQuad,
        duration: Duration(milliseconds: 500),
        child: Center(
          child: Stack(
            alignment: Alignment.center,
            overflow: Overflow.visible,
            children: <Widget>[
              Align(
                alignment: Alignment.center,
                child: Opacity(
                  opacity: 0.125,
                  child: SpriteAnimation(color),
                ),
              ),
              AnimatedContainer(
                curve: Curves.easeOutQuad,
                duration: Duration(milliseconds: 500),
                width: 82.5,
                height: 82.5,
                decoration: BoxDecoration(
                  color: Theme.of(context).brightness == Brightness.dark
                      ? Theme.of(context).scaffoldBackgroundColor
                      : Colors.white,
                  border: Border.all(
                    color: color != null
                        ? color
                        : Theme.of(context).brightness == Brightness.dark
                            ? Colors.white
                            : AppColors.grey800,
                    width: 1.75,
                  ),
                  borderRadius: BorderRadius.circular(100.0),
                ),
              ),
              AnimatedSwitcher(
                duration: Duration(milliseconds: 500),
                child: SizedBox(
                  height: 22.5,
                  child: SvgPicture.asset(
                    'assets/icons/rydr-logo.svg',
                    color: color != null
                        ? color
                        : Theme.of(context).brightness == Brightness.dark
                            ? Colors.white
                            : AppColors.grey800,
                  ),
                ),
              ),
            ],
          ),
        ),
      );
}
