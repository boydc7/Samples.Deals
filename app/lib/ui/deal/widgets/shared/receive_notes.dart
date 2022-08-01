import 'package:flutter/material.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/app/links.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class DealReceiveNotes extends StatelessWidget {
  final Deal deal;
  final bool hideBorder;

  DealReceiveNotes(this.deal, {this.hideBorder = false});

  @override
  Widget build(BuildContext context) {
    final String postSugIconUrl = 'assets/icons/post-sug-icon.svg';

    return Column(
      children: <Widget>[
        SizedBox(
          height: 16.0,
        ),
        Row(
          mainAxisAlignment: MainAxisAlignment.start,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Stack(
              alignment: Alignment.center,
              children: <Widget>[
                Container(
                  width: 72,
                  height: 40,
                ),
                SvgPicture.asset(
                  postSugIconUrl,
                  width: 24.0,
                  color: Theme.of(context).brightness == Brightness.dark
                      ? Theme.of(context).appBarTheme.iconTheme.color
                      : Theme.of(context).iconTheme.color,
                ),
              ],
            ),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  SizedBox(
                    height: 4.0,
                  ),
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: Text('Post Suggestion',
                            style: Theme.of(context).textTheme.bodyText1),
                      ),
                      Padding(
                        padding: EdgeInsets.only(right: 8.0),
                        child: TextButton(
                          isBasic: true,
                          bold: false,
                          caption: true,
                          label: "FTC Guidelines",
                          color: Theme.of(context).primaryColor,
                          onTap: () {
                            Utils.launchUrl(
                              context,
                              AppLinks.ftcGuidelines,
                              trackingName: 'ftcguidelines',
                            );
                          },
                        ),
                      )
                    ],
                  ),
                  SizedBox(
                    height: 6.0,
                  ),
                  Padding(
                    padding: EdgeInsets.only(right: 16.0),
                    child: (deal.receiveNotes == null ||
                            deal.receiveNotes.trim() == "")
                        ? RichText(
                            text: TextSpan(
                                style: Theme.of(context)
                                    .textTheme
                                    .bodyText2
                                    .merge(TextStyle(color: AppColors.grey400)),
                                children: <TextSpan>[
                                  TextSpan(
                                      text: 'RYDR suggests you represent '),
                                  TextSpan(
                                      text: '${deal.publisherAccount.userName}',
                                      style: TextStyle(
                                          fontWeight: FontWeight.w600)),
                                  TextSpan(
                                      text:
                                          ' in the best light, while also keeping true to your individual style.'),
                                ]),
                          )
                        : SelectableText(deal.receiveNotes,
                            style: Theme.of(context).textTheme.bodyText2),
                  )
                ],
              ),
            )
          ],
        ),
        SizedBox(
          height: 24.0,
        ),
        Visibility(
          visible: !hideBorder,
          child: Divider(
            height: 1,
            indent: 72.0,
          ),
        )
      ],
    );
  }
}