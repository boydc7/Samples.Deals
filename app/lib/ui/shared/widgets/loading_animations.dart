import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:simple_animations/simple_animations.dart';
import 'package:shimmer/shimmer.dart';

import 'package:rydr_app/app/theme.dart';

class LoadingDetailsShimmer extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    bool dark = Theme.of(context).brightness == Brightness.dark;

    return Shimmer.fromColors(
      baseColor: dark ? Color(0xFF121212) : AppColors.white100,
      highlightColor: dark ? Colors.black : AppColors.white50,
      child: Container(
        padding:
            EdgeInsets.only(left: 16.0, top: 48.0, right: 16.0, bottom: 20.0),
        child: Column(
          children: <Widget>[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: <Widget>[
                Expanded(
                  child: Container(
                    width: double.infinity,
                    height: 60.0,
                    decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(4.0)),
                  ),
                ),
              ],
            ),
            SizedBox(
              height: 16,
            ),
            Container(
              width: double.infinity,
              height: 200.0,
              decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(4.0)),
            ),
            SizedBox(
              height: 16,
            ),
            Column(
              children: [0, 1, 2]
                  .map(
                    (e) => Padding(
                      padding: EdgeInsets.only(bottom: 16.0),
                      child: Column(
                        children: <Widget>[
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.center,
                            children: [
                              Expanded(
                                child: Container(
                                  width: double.infinity,
                                  height: 12.0,
                                  decoration: BoxDecoration(
                                      color: Colors.white,
                                      borderRadius: BorderRadius.circular(4.0)),
                                ),
                              ),
                              Padding(
                                padding: EdgeInsets.symmetric(horizontal: 8.0),
                              ),
                              Container(
                                width: 100,
                                height: 12.0,
                                decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(4.0)),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  )
                  .toList(),
            ),
            SizedBox(height: 8),
            Column(
              children: [0, 1, 2, 3, 4]
                  .map(
                    (e) => Padding(
                      padding: EdgeInsets.only(bottom: 24.0),
                      child: Column(
                        children: <Widget>[
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Container(
                                width: 50.0,
                                height: 50.0,
                                decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(6.0)),
                              ),
                              Padding(
                                padding: EdgeInsets.symmetric(horizontal: 8.0),
                              ),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Container(
                                      width: double.infinity,
                                      height: 12.0,
                                      decoration: BoxDecoration(
                                          color: Colors.white,
                                          borderRadius:
                                              BorderRadius.circular(4.0)),
                                    ),
                                    Padding(
                                      padding:
                                          EdgeInsets.symmetric(vertical: 2.0),
                                    ),
                                    Container(
                                      width: 90.0,
                                      height: 12.0,
                                      decoration: BoxDecoration(
                                          color: Colors.white,
                                          borderRadius:
                                              BorderRadius.circular(4.0)),
                                    ),
                                  ],
                                ),
                              )
                            ],
                          ),
                        ],
                      ),
                    ),
                  )
                  .toList(),
            ),
          ],
        ),
      ),
    );
  }
}

class LoadingListShimmer extends StatelessWidget {
  final bool reversed;
  final bool short;

  LoadingListShimmer({
    this.reversed = false,
    this.short = false,
  });

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    List<int> childrenCount = short
        ? [0, 1, 2, 3, 4, 5, 6, 7]
        : [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

    return Shimmer.fromColors(
      baseColor: dark ? Color(0xFF121212) : AppColors.white100,
      highlightColor: dark ? Colors.black : AppColors.white50,
      child: Column(
        children: childrenCount
            .map(
              (e) => Padding(
                padding: EdgeInsets.only(bottom: 24.0),
                child: Column(
                  children: <Widget>[
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.center,
                      children: [
                        reversed
                            ? Container()
                            : Container(
                                width: 40.0,
                                height: 40.0,
                                decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(20.0)),
                              ),
                        reversed
                            ? Container()
                            : Padding(
                                padding: EdgeInsets.symmetric(horizontal: 8.0),
                              ),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Container(
                                width: double.infinity,
                                height: 12.0,
                                decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(4.0)),
                              ),
                              Padding(
                                padding: EdgeInsets.symmetric(vertical: 2.0),
                              ),
                              Container(
                                width: 90.0,
                                height: 12.0,
                                decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(4.0)),
                              ),
                            ],
                          ),
                        ),
                        !reversed
                            ? Container()
                            : Padding(
                                padding: EdgeInsets.symmetric(horizontal: 8.0),
                              ),
                        !reversed
                            ? Container()
                            : Container(
                                width: 40.0,
                                height: 40.0,
                                decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(20.0)),
                              ),
                      ],
                    ),
                  ],
                ),
              ),
            )
            .toList(),
      ),
    );
  }
}

class LoadingStatsShimmer extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return Shimmer.fromColors(
      baseColor: dark ? Color(0xFF121212) : AppColors.white100,
      highlightColor: dark ? Colors.black : AppColors.white50,
      child: Container(
        padding:
            EdgeInsets.only(left: 16.0, top: 48.0, right: 16.0, bottom: 20.0),
        child: Column(
          children: <Widget>[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: <Widget>[
                Expanded(
                  child: Container(
                    width: double.infinity,
                    height: 60.0,
                    decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(4.0)),
                  ),
                ),
                SizedBox(
                  width: 16,
                ),
                Expanded(
                  child: Container(
                    width: double.infinity,
                    height: 60.0,
                    decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(4.0)),
                  ),
                ),
              ],
            ),
            SizedBox(
              height: 16,
            ),
            Container(
              width: double.infinity,
              height: 200.0,
              decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(4.0)),
            ),
            SizedBox(
              height: 16,
            ),
            Column(
              children: [0, 1, 2]
                  .map(
                    (e) => Padding(
                      padding: EdgeInsets.only(bottom: 16.0),
                      child: Column(
                        children: <Widget>[
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.center,
                            children: [
                              Expanded(
                                child: Container(
                                  width: double.infinity,
                                  height: 12.0,
                                  decoration: BoxDecoration(
                                      color: Colors.white,
                                      borderRadius: BorderRadius.circular(4.0)),
                                ),
                              ),
                              Padding(
                                padding: EdgeInsets.symmetric(horizontal: 8.0),
                              ),
                              Container(
                                width: 100,
                                height: 12.0,
                                decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(4.0)),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  )
                  .toList(),
            ),
          ],
        ),
      ),
    );
  }
}

class LoadingGridShimmer extends StatelessWidget {
  _buildCell(BuildContext context) {
    return Expanded(
      child: Container(
        width: double.infinity,
        height: MediaQuery.of(context).size.width / 3,
        margin: EdgeInsets.only(bottom: 1),
        decoration: BoxDecoration(
          color: Colors.white,
        ),
      ),
    );
  }

  _buildRow(BuildContext context) {
    return Row(
      children: <Widget>[
        _buildCell(context),
        SizedBox(width: 1),
        _buildCell(context),
        SizedBox(width: 1),
        _buildCell(context),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    bool dark = Theme.of(context).brightness == Brightness.dark;

    return Shimmer.fromColors(
      baseColor: dark ? Color(0xFF121212) : AppColors.white100,
      highlightColor: dark ? Colors.black : AppColors.white50,
      child: Padding(
        padding: EdgeInsets.only(top: 1.0),
        child: Column(
          children: <Widget>[
            _buildRow(context),
            _buildRow(context),
            _buildRow(context),
            _buildRow(context),
            _buildRow(context),
            _buildRow(context),
            _buildRow(context),
            _buildRow(context),
          ],
        ),
      ),
    );
  }
}

class LoadingSliverGridShimmer extends StatelessWidget {
  final bool isStory;

  LoadingSliverGridShimmer({
    this.isStory = false,
  });

  @override
  Widget build(BuildContext context) {
    bool dark = Theme.of(context).brightness == Brightness.dark;
    return SliverGrid(
      gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        crossAxisSpacing: 2,
        mainAxisSpacing: 2,
        childAspectRatio: isStory ? 0.5625 : 1.0,
      ),
      delegate: SliverChildBuilderDelegate((BuildContext context, int index) {
        return Shimmer.fromColors(
          baseColor: dark ? Color(0xFF121212) : AppColors.white100,
          highlightColor: dark ? Colors.black : AppColors.white50,
          child: Container(color: Theme.of(context).canvasColor),
        );
      }),
    );
  }
}

class LoadingBox extends StatelessWidget {
  final double boxHeight;
  final double boxWidth;

  LoadingBox({
    @required this.boxHeight,
    @required this.boxWidth,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      alignment: Alignment.center,
      width: double.infinity,
      child: Container(
        width: boxWidth,
        height: boxHeight,
        child: Center(
            child: SizedBox(
          height: 1.5,
          width: MediaQuery.of(context).size.width / 3,
          child: LinearProgressIndicator(
            backgroundColor: Colors.transparent,
            valueColor: AlwaysStoppedAnimation<Color>(AppColors.grey800),
          ),
        )),
      ),
    );
  }
}

class LoadingList extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Container(
      alignment: Alignment.center,
      width: double.infinity,
      child: Container(
        width: MediaQuery.of(context).size.width / 3,
        height: MediaQuery.of(context).size.height - 200.0,
        child: Center(
            child: SizedBox(
          height: 1.5,
          width: MediaQuery.of(context).size.width / 3,
          child: LinearProgressIndicator(
            backgroundColor: Theme.of(context).scaffoldBackgroundColor,
            valueColor: AlwaysStoppedAnimation<Color>(
                Theme.of(context).textTheme.bodyText2.color),
          ),
        )),
      ),
    );
  }
}

class LoadingLogo extends StatelessWidget {
  final double radius;
  final Color color;
  final bool background;

  LoadingLogo({
    @required this.radius,
    this.color,
    this.background = false,
  });

  @override
  Widget build(BuildContext context) {
    final String logoUrl = 'assets/icons/rydr-logo.svg';
    return Stack(
      alignment: Alignment.center,
      children: <Widget>[
        Container(
          width: radius,
          height: radius,
          decoration: BoxDecoration(
            color: background
                ? Theme.of(context).scaffoldBackgroundColor.withOpacity(0.5)
                : Colors.transparent,
            borderRadius: BorderRadius.circular(100.0),
          ),
          child: CircularProgressIndicator(
            strokeWidth: radius * 0.032,
            valueColor: AlwaysStoppedAnimation<Color>(color == null
                ? Theme.of(context).chipTheme.backgroundColor
                : color),
          ),
        ),
        SizedBox(
          height: radius * 0.286,
          child: SvgPicture.asset(logoUrl,
              color: color == null
                  ? Theme.of(context).chipTheme.backgroundColor
                  : color),
        ),
      ],
    );
  }
}

class LoadingDetails extends StatelessWidget {
  final bool dark;

  const LoadingDetails({this.dark = false});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Container(
        alignment: Alignment.center,
        width: double.infinity,
        child: Container(
          width: MediaQuery.of(context).size.width / 3,
          height: MediaQuery.of(context).size.height - 200.0,
          child: Center(
              child: SizedBox(
            height: 1.5,
            width: MediaQuery.of(context).size.width / 3,
            child: LinearProgressIndicator(
              backgroundColor: dark ? Colors.black : Colors.white,
              valueColor: AlwaysStoppedAnimation<Color>(
                  dark ? AppColors.grey400 : AppColors.grey800),
            ),
          )),
        ),
      ),
    );
  }
}

class FadeInLeftRight extends StatelessWidget {
  final double delay;
  final Widget child;
  final int duration;

  FadeInLeftRight(this.delay, this.child, this.duration);

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("opacity").add(
          Duration(milliseconds: duration ~/ 2), Tween(begin: 0.0, end: 1.0)),
      Track("translateX").add(
          Duration(milliseconds: duration), Tween(begin: -80.0, end: 0.0),
          curve: Curves.fastOutSlowIn)
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      child: child,
      builderWithChild: (context, child, animation) => Opacity(
        opacity: animation["opacity"],
        child: Transform.translate(
            offset: Offset(animation["translateX"], 0), child: child),
      ),
    );
  }
}

class FadeInTopLeftRight extends StatelessWidget {
  final double delay;
  final Widget child;
  final int duration;
  final double beginLeft;

  FadeInTopLeftRight(this.delay, this.child, this.duration,
      {this.beginLeft = 20.0});

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("opacity").add(
          Duration(milliseconds: duration ~/ 2), Tween(begin: 0.0, end: 1.0)),
      Track("translateX").add(
          Duration(milliseconds: duration), Tween(begin: beginLeft, end: 0.0),
          curve: Curves.fastOutSlowIn),
      Track("translateY").add(
          Duration(milliseconds: duration), Tween(begin: -50.0, end: 0.0),
          curve: Curves.fastOutSlowIn)
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      child: child,
      builderWithChild: (context, child, animation) => Opacity(
        opacity: animation["opacity"],
        child: Transform.translate(
            offset: Offset(animation["translateX"], animation["translateY"]),
            child: child),
      ),
    );
  }
}

class SlideInTopLeftRight extends StatelessWidget {
  final double delay;
  final Widget child;
  final int duration;
  final double beginLeft;

  SlideInTopLeftRight(this.delay, this.child, this.duration,
      {this.beginLeft = 20.0});

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("translateX").add(
          Duration(milliseconds: duration), Tween(begin: beginLeft, end: 0.0),
          curve: Curves.fastOutSlowIn),
      Track("translateY").add(
          Duration(milliseconds: duration), Tween(begin: -50.0, end: 0.0),
          curve: Curves.fastOutSlowIn)
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      child: child,
      builderWithChild: (context, child, animation) => Transform.translate(
          offset: Offset(animation["translateX"], animation["translateY"]),
          child: child),
    );
  }
}

class SlideInTopRightLeft extends StatelessWidget {
  final double delay;
  final Widget child;
  final int duration;
  final double beginRight;

  SlideInTopRightLeft(this.delay, this.child, this.duration,
      {this.beginRight = 20.0});

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("translateX").add(
          Duration(milliseconds: duration), Tween(begin: beginRight, end: 0.0),
          curve: Curves.fastOutSlowIn),
      Track("translateY").add(
          Duration(milliseconds: duration), Tween(begin: -50.0, end: 0.0),
          curve: Curves.fastOutSlowIn)
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      child: child,
      builderWithChild: (context, child, animation) => Transform.translate(
          offset: Offset(animation["translateX"], animation["translateY"]),
          child: child),
    );
  }
}

class FadeInTopRightLeft extends StatelessWidget {
  final double delay;
  final Widget child;
  final int duration;
  final double beginRight;

  FadeInTopRightLeft(this.delay, this.child, this.duration,
      {this.beginRight = 20.0});

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("opacity").add(
          Duration(milliseconds: duration ~/ 2), Tween(begin: 0.0, end: 1.0)),
      Track("translateX").add(
          Duration(milliseconds: duration), Tween(begin: beginRight, end: 0.0),
          curve: Curves.fastOutSlowIn),
      Track("translateY").add(
          Duration(milliseconds: duration), Tween(begin: -50.0, end: 0.0),
          curve: Curves.fastOutSlowIn)
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      child: child,
      builderWithChild: (context, child, animation) => Opacity(
        opacity: animation["opacity"],
        child: Transform.translate(
            offset: Offset(animation["translateX"], animation["translateY"]),
            child: child),
      ),
    );
  }
}

class FadeInTopBottom extends StatelessWidget {
  final double delay;
  final Widget child;
  final int duration;
  // this should be a negative
  final double begin;

  FadeInTopBottom(this.delay, this.child, this.duration, {this.begin = -80.0});

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("opacity").add(
          Duration(milliseconds: duration ~/ 2), Tween(begin: 0.0, end: 1.0)),
      Track("translateY").add(
          Duration(milliseconds: duration), Tween(begin: begin, end: 0.0),
          curve: Curves.easeOutQuad)
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      child: child,
      builderWithChild: (context, child, animation) => Opacity(
        opacity: animation["opacity"],
        child: Transform.translate(
            offset: Offset(0, animation["translateY"]), child: child),
      ),
    );
  }
}

class SlideInTopBottom extends StatelessWidget {
  final double delay;
  final Widget child;
  final int duration;
  // this should be a negative
  final double begin;

  SlideInTopBottom(this.delay, this.child, this.duration, {this.begin = -80.0});

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("translateY").add(
          Duration(milliseconds: duration), Tween(begin: begin, end: 0.0),
          curve: Curves.easeOutQuad)
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      child: child,
      builderWithChild: (context, child, animation) => Transform.translate(
          offset: Offset(0, animation["translateY"]), child: child),
    );
  }
}

class FadeInBottomTop extends StatelessWidget {
  final double delay;
  final Widget child;
  final int duration;

  FadeInBottomTop(this.delay, this.child, this.duration);

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("opacity").add(
          Duration(milliseconds: duration ~/ 2), Tween(begin: 0.0, end: 1.0)),
      Track("translateY").add(
          Duration(milliseconds: duration), Tween(begin: 80.0, end: 0.0),
          curve: Curves.decelerate)
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      child: child,
      builderWithChild: (context, child, animation) => Opacity(
        opacity: animation["opacity"],
        child: Transform.translate(
            offset: Offset(0, animation["translateY"]), child: child),
      ),
    );
  }
}

class FadeInRightLeft extends StatelessWidget {
  final double delay;
  final Widget child;
  final int duration;
  final double begin;

  FadeInRightLeft(this.delay, this.child, this.duration, {this.begin = 80.0});

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("opacity").add(
          Duration(milliseconds: duration ~/ 2), Tween(begin: 0.0, end: 1.0)),
      Track("translateX").add(
          Duration(milliseconds: duration), Tween(begin: begin, end: 0.0),
          curve: Curves.fastOutSlowIn)
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      child: child,
      builderWithChild: (context, child, animation) => Opacity(
        opacity: animation["opacity"],
        child: Transform.translate(
            offset: Offset(animation["translateX"], 0), child: child),
      ),
    );
  }
}

class FadeInOpacityOnly extends StatelessWidget {
  final double delay;
  final Widget child;
  final int duration;
  final Curve curve;

  FadeInOpacityOnly(this.delay, this.child,
      {this.duration = 250, this.curve = Curves.fastOutSlowIn});

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("opacity")
          .add(Duration(milliseconds: duration), Tween(begin: 0.0, end: 1.0)),
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      curve: curve,
      child: child,
      builderWithChild: (context, child, animation) => Opacity(
        opacity: animation["opacity"],
        child: child,
      ),
    );
  }
}

class FadeInScaleUp extends StatelessWidget {
  final double delay;
  final double startScale;
  final Widget child;
  final int duration;
  final Curve curve;

  FadeInScaleUp(
    this.delay,
    this.child, {
    this.duration = 250,
    this.curve = Curves.fastOutSlowIn,
    this.startScale = 0.8,
  });

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("opacity")
          .add(Duration(milliseconds: duration), Tween(begin: 0.0, end: 1.0)),
      Track("scale").add(
          Duration(milliseconds: duration), Tween(begin: startScale, end: 1.0)),
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      curve: curve,
      child: child,
      builderWithChild: (context, child, animation) => Transform.scale(
        scale: animation["scale"],
        child: Opacity(
          opacity: animation["opacity"],
          child: child,
        ),
      ),
    );
  }
}

class FadeOutOpacityOnly extends StatelessWidget {
  final double delay;
  final Widget child;

  FadeOutOpacityOnly(this.delay, this.child);

  @override
  Widget build(BuildContext context) {
    final tween = MultiTrackTween([
      Track("opacity")
          .add(Duration(milliseconds: 250), Tween(begin: 1.0, end: 0.0)),
    ]);

    return ControlledAnimation(
      delay: Duration(milliseconds: (50 * delay).round()),
      duration: tween.duration,
      tween: tween,
      child: child,
      builderWithChild: (context, child, animation) => Opacity(
        opacity: animation["opacity"],
        child: child,
      ),
    );
  }
}
