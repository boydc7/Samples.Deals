import 'dart:math';

import 'package:flutter/material.dart';

class SpriteAnimation extends StatefulWidget {
  final Color color;

  SpriteAnimation(this.color);

  @override
  _SpriteAnimationState createState() => _SpriteAnimationState();
}

class _SpriteAnimationState extends State<SpriteAnimation>
    with SingleTickerProviderStateMixin {
  AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this);
    _startAnimation();
  }

  @override
  void dispose() {
    _controller.dispose();

    super.dispose();
  }

  void _startAnimation() {
    _controller.stop();
    _controller.reset();
    _controller.repeat(
      period: Duration(seconds: 5),
    );
  }

  @override
  Widget build(BuildContext context) => CustomPaint(
        painter: SpritePainter(_controller, widget.color),
        child: SizedBox(
          width: MediaQuery.of(context).size.width - 32,
          height: MediaQuery.of(context).size.width - 32,
        ),
      );
}

class SpritePainter extends CustomPainter {
  final Animation<double> _animation;
  final Color logoColor;

  SpritePainter(this._animation, this.logoColor) : super(repaint: _animation);

  void circle(Canvas canvas, Rect rect, double value) {
    double opacity = (1.0 - (value / 2.0)).clamp(0.0, 0.5);
    Color color = logoColor.withOpacity(opacity);

    double size = rect.width / 2;
    double area = size * size;
    double radius = sqrt(area * value / 4);

    final Paint paint = Paint()..color = color;
    canvas.drawCircle(rect.center, radius, paint);
  }

  @override
  void paint(Canvas canvas, Size size) {
    Rect rect = Rect.fromLTRB(0.0, 0.0, size.width, size.height);

    for (int wave = 3; wave >= 0; wave--) {
      circle(canvas, rect, wave + _animation.value);
    }
  }

  @override
  bool shouldRepaint(SpritePainter oldDelegate) {
    return true;
  }
}
