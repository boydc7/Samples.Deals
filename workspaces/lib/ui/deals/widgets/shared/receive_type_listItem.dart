import 'package:flutter/material.dart';
import 'package:rydrworkspaces/app/theme.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';

class DealReceiveTypeListItem extends StatelessWidget {
  final Deal deal;

  DealReceiveTypeListItem(this.deal);

  @override
  Widget build(BuildContext context) {
    /// if we have no receive types then we bail
    if (deal.receiveType == null) {
      return Container();
    }

    /// if we have a completed request but no completion media yet then we bail
    if (deal?.request?.status == DealRequestStatus.completed &&
        deal?.request?.completionMedia?.length == 0) {
      return Container();
    }

    final int requestedPosts = deal.requestedPosts;
    final int requestedStories = deal.requestedStories;
    final String postReqIconUrl = 'assets/icons/post-req-icon.svg';

    return Column(
      children: <Widget>[
        SizedBox(
          height: 20.0,
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
                Icon(Icons.insert_chart)
              ],
            ),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  SizedBox(
                    height: 4.0,
                  ),
                  Text('Post Requirements'),
                  SizedBox(
                    height: 6.0,
                  ),
                  deal.request != null &&
                          deal.request.status == DealRequestStatus.completed
                      ? _buildCompleted(
                          context,
                          requestedPosts,
                          requestedStories,
                        )
                      : _buildNotCompleted(
                          context,
                          requestedPosts,
                          requestedStories,
                        ),
                ],
              ),
            )
          ],
        ),
        SizedBox(
          height: 16.0,
        ),
        Divider(
          height: 1,
          indent: 72.0,
        ),
      ],
    );
  }

  Widget _buildCompleted(
    BuildContext context,
    int requestedPosts,
    int requestedStories,
  ) {
    List<Widget> mediaLineItems = [];

    final int completedPosts = deal.request.completedPosts;
    final int completedStories = deal.request.completedStories;

    Widget item(String label, int requested, int completed) {
      Color color = AppColors.successGreen;

      if (completed / requested <= 0.25) {
        color = AppColors.errorRed;
      } else if (completed / requested <= 0.75) {
        color = Colors.orange;
      }

      return Padding(
        padding: EdgeInsets.only(right: 16.0, bottom: 16.0, top: 4.0),
        child: Column(
          children: <Widget>[
            Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    label,
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                ),
                Text('$completed / $requested',
                    style: Theme.of(context).textTheme.bodyText1)
              ],
            ),
            Padding(
              padding: EdgeInsets.only(top: 8.0),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(8.0),
                child: LinearProgressIndicator(
                  backgroundColor: AppColors.grey300.withOpacity(0.3),
                  valueColor: AlwaysStoppedAnimation<Color>(color),
                  value: (completed / requested).clamp(0, 1).toDouble(),
                ),
              ),
            ),
          ],
        ),
      );
    }

    mediaLineItems
        .add(item('Instagram Stories', requestedStories, completedStories));
    mediaLineItems.add(item('Instagram Posts', requestedPosts, completedPosts));

    return Column(
      children: mediaLineItems,
    );
  }

  Widget _buildNotCompleted(
    BuildContext context,
    int requestedPosts,
    int requestedStories,
  ) {
    List<Widget> mediaLineItems = [];

    Widget item(String label, int quantity) {
      return Padding(
        padding: EdgeInsets.only(right: 16.0, bottom: 8.0),
        child: Column(
          children: <Widget>[
            Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    label,
                    style: Theme.of(context).textTheme.bodyText1,
                  ),
                ),
                Text(quantity.toString(),
                    style: Theme.of(context).textTheme.bodyText1)
              ],
            )
          ],
        ),
      );
    }

    mediaLineItems.add(item('Instagram Stories', requestedStories));
    mediaLineItems.add(item('Instagram Posts', requestedPosts));

    return Column(
      children: mediaLineItems,
    );
  }
}
