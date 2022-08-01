import 'dart:math';
import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/publisher_media_vision.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_detail.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_helpers.dart';

class ProfileInsightsMediaAnalysisNotable extends StatefulWidget {
  final PublisherAccount profile;
  final PublisherAccountMediaVisionSection section;

  ProfileInsightsMediaAnalysisNotable(this.profile, this.section);

  @override
  _ProfileInsightsMediaAnalysisNotableState createState() =>
      _ProfileInsightsMediaAnalysisNotableState();
}

class _ProfileInsightsMediaAnalysisNotableState
    extends State<ProfileInsightsMediaAnalysisNotable>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  List _shuffle(medias) {
    var random = Random();
    for (var i = medias.length - 1; i > 0; i--) {
      var n = random.nextInt(i + 1);
      var temp = medias[i];
      medias[i] = medias[n];
      medias[n] = temp;
    }
    return medias;
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);

    final bool darkTheme = Theme.of(context).brightness == Brightness.dark;
    final double cardWidth = (MediaQuery.of(context).size.width / 1.5) - 8;

    /// filter out sections that don't have any media
    final List<PublisherAccountMediaVisionSectionItem> items =
        widget.section?.items != null
            ? widget.section.items
                .where((PublisherAccountMediaVisionSectionItem item) =>
                    item.medias != null && item.medias.isNotEmpty)
                .toList()
            : [];

    return items.isEmpty
        ? Container(
            height: 16,
          )
        : Container(
            height: cardWidth,
            color: darkTheme ? Color(0xFF0D0D0D) : AppColors.white200,
            padding: EdgeInsets.only(top: 8, bottom: 8),
            width: MediaQuery.of(context).size.width,
            child: ListView(
              padding: EdgeInsets.symmetric(horizontal: 8.0),
              scrollDirection: Axis.horizontal,
              children:
                  items.map((PublisherAccountMediaVisionSectionItem item) {
                /// pick only up to two media images for the cards
                final List<PublisherMedia> medias = item.medias.length > 3
                    ? item.medias.take(4).toList()
                    : item.medias.length > 1
                        ? item.medias.take(2).toList()
                        : item.medias;

                final List<PublisherMedia> random = _shuffle(medias);

                List<Widget> mediaItem = [];

                random.asMap().forEach((int i, PublisherMedia m) {
                  return mediaItem.add(
                    Expanded(
                        child: MediaCachedImage(
                      imageUrl: m.previewUrl,
                      width: cardWidth / 2,
                      height: cardWidth / 2,
                      marginRight: i != 1 && i != 3 ? 2 : 0,
                    )),
                  );
                });

                return Container(
                  width: cardWidth,
                  child: Card(
                    margin: EdgeInsets.all(8),
                    child: InkWell(
                      onTap: () => Navigator.push(
                        context,
                        MaterialPageRoute(
                            builder: (context) =>
                                ProfileInsightsMediaAnalysisDetail(
                                  widget.profile,
                                  item.title,
                                  sectionItem: item,
                                  subTitle: item.subTitle,
                                ),
                            settings: AppAnalytics.instance.getRouteSettings(
                                'profile/insights/ai/details')),
                      ),
                      child: Stack(
                        children: <Widget>[
                          ClipRRect(
                            borderRadius: BorderRadius.circular(4.0),
                            child: Container(
                              width: MediaQuery.of(context).size.width,
                              foregroundDecoration: BoxDecoration(
                                gradient: LinearGradient(
                                  end: Alignment.center,
                                  begin: Alignment.bottomCenter,
                                  stops: [0.0, 0.5, 1.0],
                                  colors: [
                                    Colors.black.withOpacity(0.65),
                                    Colors.black.withOpacity(0.35),
                                    Colors.black.withOpacity(0.0)
                                  ],
                                ),
                              ),
                              child: Column(
                                children: <Widget>[
                                  Expanded(
                                    child: Row(
                                      children: mediaItem.length == 4
                                          ? mediaItem.take(2).toList()
                                          : mediaItem,
                                    ),
                                  ),
                                  Visibility(
                                    visible: mediaItem.length == 4,
                                    child: SizedBox(height: 2),
                                  ),
                                  mediaItem.length == 4
                                      ? Expanded(
                                          child: Row(
                                            children: mediaItem
                                                .skip(2)
                                                .take(2)
                                                .toList(),
                                          ),
                                        )
                                      : Container(height: 0),
                                ],
                              ),
                            ),
                          ),
                          Positioned(
                            bottom: 12,
                            left: 12,
                            child: Text(
                              item.title,
                              style: TextStyle(
                                color: Colors.white,
                                fontSize: 16,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                          )
                        ],
                      ),
                    ),
                  ),
                );
              }).toList(),
            ),
          );
  }
}
