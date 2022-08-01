import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/links.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';
import 'package:rydr_app/models/deal.dart';

/// large version with big numbers showing posting requirements
/// only used for creators viewing deals & request with active status
class DealReceiveType extends StatelessWidget {
  final Deal deal;

  DealReceiveType(this.deal);

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final bool dark = theme.brightness == Brightness.dark;
    final String postReqIconUrl = 'assets/icons/post-req-icon.svg';

    final bool isPending =
        deal?.request?.status == DealRequestStatus.requested ||
            deal?.request?.status == DealRequestStatus.invited;

    final String notes = deal?.receiveNotes?.trim() ?? "";

    final RegExp hashtagExp = RegExp(r"\B#\w\w+");
    final RegExp mentionExp = RegExp(r"\B@\w\w+");

    final List<String> hashtags = hashtagExp
        .allMatches(notes)
        .map((hashtags) => hashtags.group(0))
        .toSet()
        .toList();

    final List<String> mentions = mentionExp
        .allMatches(notes)
        .map((mentions) => mentions.group(0))
        .toSet()
        .toList()
          ..removeWhere((String mention) =>
              mention == '@${deal.publisherAccount.userName}');

    /// Combine mentions and hashtags together
    final List<String> copyStrings = mentions
      ..addAll(hashtags)
      ..toSet();

    /// this should never happen but nonetheless a guard against not having receive types
    if (deal.receiveType == null) {
      return Container(
        height: 16.0,
        width: 0,
      );
    }

    if (deal.request == null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Divider(
            height: 1,
            indent: 16,
            endIndent: 16,
          ),
          Padding(
            padding: EdgeInsets.only(top: 16.0, left: 16, bottom: 8.0),
            child: Text(
              "Posting Requirements",
              style: theme.textTheme.bodyText1,
            ),
          ),
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 16),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                _buildBox(
                    context,
                    dark,
                    theme,
                    deal.requestedStories > 1 || deal.requestedStories == 0
                        ? 'Instagram\nStories'
                        : 'Instagram\nStory',
                    deal.requestedStories.toDouble()),
                SizedBox(width: 8),
                _buildBox(
                    context,
                    dark,
                    theme,
                    deal.requestedPosts > 1 || deal.requestedPosts == 0
                        ? 'Instagram\nFeed Posts'
                        : 'Instagram\nFeed Post',
                    deal.requestedPosts.toDouble())
              ],
            ),
          ),
        ],
      );
    } else {
      return Column(
        children: <Widget>[
          Divider(
            height: 1,
            indent: 16,
            endIndent: 16,
          ),
          SizedBox(height: 8),
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
                    postReqIconUrl,
                    width: 26.0,
                    color: dark
                        ? theme.appBarTheme.iconTheme.color
                        : theme.iconTheme.color,
                  ),
                ],
              ),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Padding(
                      padding: EdgeInsets.only(top: 10, bottom: 10),
                      child: Row(
                        children: <Widget>[
                          Expanded(
                            child: Text('Posting Requirements',
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
                    ),
                    Padding(
                      padding: EdgeInsets.only(right: 16, bottom: 4),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          _buildBox(
                              context,
                              dark,
                              theme,
                              deal.requestedStories > 1 ||
                                      deal.requestedStories == 0
                                  ? 'Instagram\nStories'
                                  : 'Instagram\nStory',
                              deal.requestedStories.toDouble()),
                          SizedBox(width: 8),
                          _buildBox(
                              context,
                              dark,
                              theme,
                              deal.requestedPosts > 1 ||
                                      deal.requestedPosts == 0
                                  ? 'Instagram\nFeed Posts'
                                  : 'Instagram\nFeed Post',
                              deal.requestedPosts.toDouble())
                        ],
                      ),
                    ),
                    Padding(
                      padding: EdgeInsets.only(right: 16, bottom: 4),
                      child: notes.isEmpty
                          ? RichText(
                              text: TextSpan(
                                  style: theme.textTheme.bodyText2
                                      .merge(TextStyle(color: theme.hintColor)),
                                  children: <TextSpan>[
                                    TextSpan(
                                        text: 'RYDR suggests you represent '),
                                    TextSpan(
                                        text: deal.publisherAccount.userName,
                                        style: TextStyle(
                                            fontWeight: FontWeight.w600)),
                                    TextSpan(
                                        text:
                                            ' in the best light, while also keeping true to your individual style.'),
                                  ]),
                            )
                          : SelectableText(notes,
                              style: theme.textTheme.bodyText2),
                    ),

                    // Hide the actionable stuff until you're approved
                    isPending
                        ? SizedBox(height: 16)
                        : Divider(height: 16, endIndent: 16),
                    isPending
                        ? Container(height: 0)
                        : Padding(
                            padding: EdgeInsets.only(right: 16, bottom: 8),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: <Widget>[
                                Row(
                                  children: <Widget>[
                                    _buildCopyChip(context,
                                        '@${deal.publisherAccount.userName}'),
                                    deal.place?.name != null
                                        ? Flexible(
                                            child: _buildCopyChip(
                                                context, deal.place?.name),
                                          )
                                        : Container()
                                  ],
                                ),
                                Wrap(
                                  children: copyStrings
                                      .map((String mention) =>
                                          _buildCopyChip(context, mention))
                                      .toList(),
                                )
                              ],
                            ),
                          ),
                  ],
                ),
              ),
            ],
          ),
        ],
      );
    }
  }

  Widget _buildBox(BuildContext context, bool dark, ThemeData theme,
      String title, double value) {
    return Expanded(
      child: Container(
        margin: EdgeInsets.only(bottom: 8),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(8),
          color: dark ? theme.appBarTheme.color : Colors.grey.shade200,
        ),
        padding: EdgeInsets.all(16),
        child: insightsBigStat(
            context: context,
            value: value,
            formatAsInt: true,
            labelColor: theme.textTheme.bodyText1.color,
            label: title),
      ),
    );
  }

  Widget _buildCopyChip(BuildContext context, String label) => Padding(
        padding: EdgeInsets.only(right: 8),
        child: ActionChip(
          backgroundColor: Theme.of(context).scaffoldBackgroundColor,
          shape: OutlineInputBorder(
            borderRadius: BorderRadius.circular(40),
            borderSide: BorderSide(
              width: 1.0,
              color: Theme.of(context).dividerColor,
            ),
          ),
          pressElevation: 1.0,
          avatar: Icon(AppIcons.copy, size: 16),
          label: Text(
            label,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.caption,
          ),
          onPressed: () {
            Clipboard.setData(ClipboardData(text: label));
            Scaffold.of(context).showSnackBar(
              SnackBar(content: Text("Copied!")),
            );
          },
        ),
      );
}
