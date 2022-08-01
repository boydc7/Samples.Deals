import "dart:math";
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_detail.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';

class ProfileInsightsMediaAnalysisRecent extends StatelessWidget {
  final PublisherAccount profile;
  final List<PublisherMedia> recent;
  final List<PublisherMedia> posts;
  final List<PublisherMedia> stories;

  ProfileInsightsMediaAnalysisRecent(
    this.profile,
    this.recent,
    this.posts,
    this.stories,
  );

  @override
  Widget build(BuildContext context) {
    /// combine posts and stories into one to send to the detail list
    final List<PublisherMedia> items = List.from(this.posts ?? [])
      ..addAll(this.stories ?? []);

    /// want to shuffle the list so we show two random images from this list as thumb
    List shuffle(items) {
      var random = Random();
      for (var i = items.length - 1; i > 0; i--) {
        var n = random.nextInt(i + 1);
        var temp = items[i];
        items[i] = items[n];
        items[n] = temp;
      }
      return items;
    }

    List<PublisherMedia> random = shuffle(items);
    List<Widget> mediaItem = [];
    random.asMap().forEach((int i, PublisherMedia m) {
      if (i <= 2) {
        return mediaItem.add(
          Align(
            alignment: i == 0
                ? Alignment.centerLeft
                : i == 1 ? Alignment.center : Alignment.centerRight,
            child: Transform.rotate(
              angle: i == 0 ? -0.174533 : i == 1 ? 0.0 : 0.174533,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(4),
                child: CachedNetworkImage(
                  imageUrl: m.previewUrl,
                  imageBuilder: (context, imageProvider) => Container(
                    height:
                        m.contentType == PublisherContentType.story ? 72 : 48,
                    width:
                        m.contentType == PublisherContentType.story ? 40.5 : 48,
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(4),
                      color: Theme.of(context).canvasColor,
                      border: Border.all(
                          color: Theme.of(context).appBarTheme.color, width: 2),
                      image: DecorationImage(
                        fit: BoxFit.cover,
                        image: imageProvider,
                      ),
                    ),
                  ),
                  errorWidget: (context, url, error) => ImageError(
                    logUrl: url,
                    logParentName:
                        'profile/widgets/insights_media_analysis_recent.dart',
                  ),
                ),
              ),
            ),
          ),
        );
      } else {
        Container(height: 0, width: 0);
      }
    });

    if (appState.currentProfile.id == profile.id) {
      return GestureDetector(
        onTap: () => Navigator.push(
          context,
          MaterialPageRoute(
            builder: (context) => ProfileInsightsMediaAnalysisDetail(
              profile,
              "Recently Analyzed",
              media: items,
            ),
            settings: AppAnalytics.instance
                .getRouteSettings('profile/insights/ai/details'),
          ),
        ),
        child: Container(
          color: Colors.transparent,
          padding: EdgeInsets.only(bottom: 16.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              SizedBox(
                width: 116,
                height: 72,
                child: Stack(
                  alignment: Alignment.center,
                  overflow: Overflow.visible,
                  children: mediaItem,
                ),
              ),
              Padding(
                padding: EdgeInsets.only(top: 4.0),
                child: Text(
                  "${items.length} new posts",
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(
                          color: Theme.of(context).hintColor,
                        ),
                      ),
                ),
              ),
            ],
          ),
        ),
      );
    } else {
      return Container(height: 0, width: 0);
    }
  }
}
