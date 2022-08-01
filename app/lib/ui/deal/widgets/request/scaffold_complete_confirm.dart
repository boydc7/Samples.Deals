import 'dart:async';
import 'dart:ui';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/deal/blocs/request_complete_confirm.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_field.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class RequestCompleteConfirmPage extends StatefulWidget {
  final Deal deal;
  final List<PublisherMedia> completionMedia;
  final int stories;
  final int posts;

  RequestCompleteConfirmPage({
    @required this.deal,
    @required this.completionMedia,
    this.stories,
    this.posts,
  });

  @override
  State<StatefulWidget> createState() => _RequestCompleteConfirmPageState();
}

class _RequestCompleteConfirmPageState
    extends State<RequestCompleteConfirmPage> {
  final RequestCompleteConfirmBloc _bloc = RequestCompleteConfirmBloc();

  final TextEditingController _notesController = TextEditingController();
  final PageController _pageController = PageController();

  ThemeData _theme;
  bool _darkMode;

  @override
  void initState() {
    super.initState();

    /// empty notes controller text to avoid nulls
    _notesController.text = "";
  }

  @override
  void dispose() {
    _notesController.dispose();

    super.dispose();
  }

  void _updateRequest() async {
    showSharedLoadingLogo(
      context,
      content: "Completing RYDR",
    );

    final bool success = await _bloc.completeRequest(
      widget.deal,
      widget.completionMedia,
      _notesController.text.trim(),
    );

    /// close the "updating alert"
    Navigator.of(context).pop();

    /// if the request update was successful then clear entire stack and reload page
    if (success) {
      Navigator.of(context).pushNamedAndRemoveUntil(
          AppRouting.getRequestRoute(
              widget.deal.id,
              appState.currentProfile.isBusiness
                  ? widget.deal.request.publisherAccount.id
                  : null),
          (Route<dynamic> route) => false);
    } else {
      showSharedModalError(
        context,
        title: 'Unable to update RYDR',
        subTitle:
            'We were unable to update your RYDR, please try again in a few moments.',
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;

    return Scaffold(
      body: SafeArea(
        bottom: true,
        child: PageView(
          controller: _pageController,
          children: <Widget>[
            _buildRatingPage(),
            _buildSubmitPage(),
          ],
        ),
      ),
    );
  }

  Widget _buildRatingPage() {
    final String mediaType = widget.stories != 0 ? "story" : "post";

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        SizedBox(height: 32),
        Expanded(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              _buildThumbnails(),
              SizedBox(height: 16),
              Text("All Wrapped Up!", style: _theme.textTheme.headline4),
              SizedBox(height: 8),
              Padding(
                padding: EdgeInsets.symmetric(horizontal: 32),
                child: RichText(
                  textAlign: TextAlign.center,
                  text: TextSpan(
                      style: _theme.textTheme.bodyText2
                          .merge(TextStyle(color: _theme.hintColor)),
                      children: <TextSpan>[
                        TextSpan(
                          text: widget.completionMedia.length > 1
                              ? widget.stories != 0 && widget.posts != 0
                                  ? "We've bundled your stories and posts\ntogether and will send them off to"
                                  : widget.stories != 0
                                      ? "We've bundled your stories together\nand will send them off to"
                                      : "We've bundled your posts together\nand will send them off to"
                              : "We're ready to send your $mediaType\nover to",
                        ),
                        TextSpan(
                            text: ' ${widget.deal.publisherAccount.userName}.',
                            style: TextStyle(fontWeight: FontWeight.w600)),
                      ]),
                ),
              ),
            ],
          ),
        ),
        Text(
          "How was your experience?",
          style: _theme.textTheme.bodyText1.merge(
            TextStyle(
              color: _theme.primaryColor,
            ),
          ),
        ),
        SizedBox(height: 16),
        StreamBuilder<int>(
          stream: _bloc.rating,
          builder: (context, snapshot) => StarRating(
            value: snapshot.data ?? 0,
            onChanged: (i) {
              _bloc.setRating(i);
              Future.delayed(Duration(milliseconds: 500), () {
                _pageController.animateToPage(
                  1,
                  duration: Duration(milliseconds: 250),
                  curve: Curves.easeInOut,
                );
              });
            },
          ),
        ),
        SizedBox(height: 16),
        TextButton(
          label: "Skip",
          color: _darkMode ? _theme.hintColor : _theme.canvasColor,
          onTap: () => _pageController.animateToPage(
            1,
            duration: Duration(milliseconds: 250),
            curve: Curves.easeInOut,
          ),
        ),
        SizedBox(height: 16),
      ],
    );
  }

  Widget _buildSubmitPage() => Container(
        padding: EdgeInsets.symmetric(horizontal: 32, vertical: 16),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.center,
          children: <Widget>[
            Text("Send a Note", style: _theme.appBarTheme.textTheme.headline6),
            SizedBox(height: 4),
            Text(
                "You can send a quick message to ${widget.deal.publisherAccount.userName}\ndescribing your experience.",
                textAlign: TextAlign.center,
                style: _theme.textTheme.caption.merge(TextStyle(
                  color: _theme.hintColor,
                ))),
            SizedBox(height: 16),
            DealTextField(
              controller: _notesController,
              maxCharacters: 250,
              minLines: 4,
              maxLines: 4,
              autoFocus: true,
              labelText:
                  "Tell ${widget.deal.publisherAccount.userName} about your experience",
              hintText: "The food was amazing! I'll definitely be back soon!",
            ),
            SizedBox(height: 16),
            PrimaryButton(
              label: 'Complete',
              onTap: _updateRequest,
            ),
          ],
        ),
      );

  Widget _buildThumbnails() {
    List<Widget> _media = [];

    widget.completionMedia.asMap().forEach((int index, dynamic m) {
      final bool first = index == 0;
      final bool second = index == 1;
      final bool third = index == 2;
      final int length = widget.completionMedia.length;
      final double angle = first
          ? length == 2 ? -0.05 : length == 1 ? 0.0 : -0.12
          : second ? length == 2 ? 0.05 : 0.0 : third ? 0.12 : 0.0;
      final double storyHeight = length == 1 ? 140.0 * 1.815 : 120.0 * 1.815;

      Widget mediaCard() => CachedNetworkImage(
            imageUrl: m.previewUrl,
            imageBuilder: (context, imageProvider) => Container(
              width: m.contentType == PublisherContentType.post
                  ? length == 1 ? 92.0 * 1.815 : 92.0 * 1.815
                  : storyHeight * 0.5625,
              height: m.contentType == PublisherContentType.post
                  ? length == 1 ? 120.0 * 1.815 : 92.0 * 1.815
                  : storyHeight,
              decoration: BoxDecoration(
                color: _theme.appBarTheme.color,
                border: Border.all(color: Colors.white, width: 1.0),
                borderRadius: BorderRadius.circular(2.0),
                boxShadow: AppShadows.elevation[index == 0 ? 0 : 1],
                image: DecorationImage(
                    image: imageProvider,
                    fit: BoxFit.cover,
                    alignment: Alignment.center),
              ),
            ),
            errorWidget: (context, url, error) => ImageError(
              logUrl: url,
              logParentName:
                  'deal/widgets/request/scaffold_complete_confirm.dart > _buildThumbnails',
            ),
          );

      Widget mediaItem(int index) {
        if (length >= 3) {
          if (index == 0) {
            return SlideInTopLeftRight(
              1,
              Transform.rotate(
                angle: angle,
                child: mediaCard(),
              ),
              3000,
              beginLeft: -10.0,
            );
          } else if (index == 1) {
            return FadeInTopBottom(
              1,
              mediaCard(),
              3000,
              begin: -50.0,
            );
          } else if (index == 2) {
            return SlideInTopRightLeft(
              1,
              Transform.rotate(
                angle: angle,
                child: mediaCard(),
              ),
              3000,
              beginRight: 10.0,
            );
          } else {
            return mediaCard();
          }
        } else if (length == 2) {
          if (index == 0) {
            return SlideInTopLeftRight(
              1,
              Transform.rotate(
                angle: angle,
                child: mediaCard(),
              ),
              3000,
              beginLeft: -10.0,
            );
          } else {
            return SlideInTopRightLeft(
              1,
              Transform.rotate(
                angle: angle,
                child: mediaCard(),
              ),
              3000,
              beginRight: 10.0,
            );
          }
        } else {
          return SlideInTopBottom(
            1,
            mediaCard(),
            3000,
          );
        }
      }

      if (index < 3) {
        return _media.add(mediaItem(index));
      } else {
        _media.add(
          SlideInTopBottom(
            1,
            Container(
              height: 32,
              width: 32,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(20),
                color: _theme.scaffoldBackgroundColor.withOpacity(0.4),
              ),
              child: Center(
                child: Text(length > 3 ? "+${length - 3}" : "s",
                    style: _theme.textTheme.bodyText1),
              ),
            ),
            3000,
          ),
        );
      }
    });

    return Stack(
      alignment: Alignment.bottomCenter,
      overflow: Overflow.visible,
      children: <Widget>[
        Container(
          height: 180,
          width: 180,
          decoration: BoxDecoration(
            color: _theme.scaffoldBackgroundColor,
            borderRadius: BorderRadius.circular(100),
            border:
                Border.all(color: _theme.textTheme.bodyText1.color, width: 4),
            boxShadow: <BoxShadow>[
              BoxShadow(
                  offset: Offset(0.0, 3.0),
                  blurRadius: 3.0,
                  spreadRadius: -2.0,
                  color: Color(0x33000000)),
              BoxShadow(
                  offset: Offset(0.0, 3.0),
                  blurRadius: 4.0,
                  spreadRadius: 0.0,
                  color: Color(0x24000000)),
              BoxShadow(
                  offset: Offset(0.0, 1.0),
                  blurRadius: 8.0,
                  spreadRadius: 0.0,
                  color: Color(0x1F000000)),
            ],
          ),
        ),
        Positioned(
          bottom: 4,
          child: ClipPath(
            clipper: CompletedClipper(),
            child: Container(
              width: MediaQuery.of(context).size.width,
              height: 300,
              alignment: Alignment.bottomCenter,
              child: Stack(
                alignment: Alignment.center,
                children: _media,
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class CompletedClipper extends CustomClipper<Path> {
  @override
  Path getClip(Size size) {
    double width = size.width;
    double height = size.height;
    double rheight = height - 93;
    double gap = (width - 172) / 2;

    final path = Path()
      ..lineTo(0, rheight)
      ..lineTo(gap, rheight)
      ..quadraticBezierTo(gap, height - 10, width / 2, height + 1)
      ..quadraticBezierTo(width - gap, height - 10, width - gap, rheight)
      ..lineTo(width - gap, rheight)
      ..lineTo(width, rheight)
      ..lineTo(width, 0);
    return path;
  }

  @override
  bool shouldReclip(CustomClipper<Path> oldClipper) => true;
}

class StarDisplayWidget extends StatelessWidget {
  final int value;
  final Widget filledStar;
  final Widget unfilledStar;

  const StarDisplayWidget({
    Key key,
    this.value = 0,
    @required this.filledStar,
    @required this.unfilledStar,
  })  : assert(value != null),
        super(key: key);

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: List.generate(5, (index) {
        return index < value ? filledStar : unfilledStar;
      }),
    );
  }
}

class StarDisplay extends StarDisplayWidget {
  StarDisplay({Key key, int value = 0})
      : super(
          key: key,
          value: value,
          filledStar: Icon(AppIcons.starSolid),
          unfilledStar: Icon(AppIcons.starLight),
        );
}

class StarRating extends StatelessWidget {
  final void Function(int index) onChanged;
  final int value;

  StarRating({
    Key key,
    @required this.onChanged,
    this.value = 0,
  })  : assert(value != null),
        super(key: key);

  @override
  Widget build(BuildContext context) {
    final color = Theme.of(context).primaryColor;
    final size = 32.0;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: List.generate(5, (index) {
        return IconButton(
          onPressed: onChanged != null
              ? () {
                  onChanged(value == index + 1 ? index : index + 1);
                }
              : null,
          color: index < value ? color : color,
          iconSize: size,
          icon: Icon(
            index < value ? AppIcons.starSolid : AppIcons.starLight,
          ),
          padding: EdgeInsets.zero,
        );
      }),
    );
  }
}
