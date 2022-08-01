import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/links.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class DealAddContinue extends StatelessWidget {
  final Stream<bool> canPreviewStream;
  final Function handleTap;
  final DealType dealType;

  DealAddContinue({
    @required this.canPreviewStream,
    @required this.handleTap,
    this.dealType = DealType.Deal,
  });

  @override
  Widget build(BuildContext context) => Padding(
        padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
        child: Column(
          children: <Widget>[
            StreamBuilder<bool>(
                stream: canPreviewStream,
                builder: (context, snapshot) => PrimaryButton(
                      buttonColor: dealType == DealType.Virtual
                          ? Colors.deepOrange
                          : Theme.of(context).primaryColor,
                      context: context,
                      label: 'Next',
                      onTap: snapshot.data != null && snapshot.data == true
                          ? handleTap
                          : null,
                    )),
            SizedBox(height: 8),
            RichText(
              textAlign: TextAlign.center,
              text: TextSpan(
                  style: Theme.of(context)
                      .textTheme
                      .caption
                      .merge(TextStyle(color: Theme.of(context).hintColor)),
                  children: <TextSpan>[
                    TextSpan(
                        text:
                            'By posting in the RYDR Marketplace, you confirm this listing complies with RYDR\'s '),
                    LinkTextSpan(
                      context: context,
                      text: 'Terms of Service',
                      url: AppLinks.termsUrl,
                    ),
                    TextSpan(text: ', '),
                    LinkTextSpan(
                      context: context,
                      text: 'Privacy Policy',
                      url: AppLinks.privacyUrl,
                    ),
                    TextSpan(text: ', and all applicable laws.'),
                  ]),
            ),
          ],
        ),
      );
}

class LinkTextSpan extends TextSpan {
  LinkTextSpan({@required BuildContext context, String url, String text})
      : super(
            style: TextStyle(color: Theme.of(context).primaryColor),
            text: text ?? url,
            recognizer: TapGestureRecognizer()
              ..onTap =
                  () => Utils.launchUrl(context, url, trackingName: 'adddeal'));
}
