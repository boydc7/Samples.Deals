import 'package:flutter/material.dart';
import 'package:flutter/widgets.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/publisher_account.dart';
import 'package:rydr_app/ui/map/blocs/map.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class ListPublishers extends StatelessWidget {
  final MapBloc mapBloc;
  final int index;
  final Function onFilterPublisher;

  ListPublishers({
    @required this.mapBloc,
    @required this.index,
    @required this.onFilterPublisher,
  });

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<PublisherAccountsResponse>(
      stream: mapBloc.publishers,
      builder: (context, snapshot) {
        return snapshot.data == null ||
                snapshot.data.hasError ||
                snapshot.data.models == null ||
                snapshot.data.models.isEmpty
            ? Container()
            : _buildPublishers(context, snapshot.data.models);
      },
    );
  }

  Widget _buildPublishers(
    BuildContext context,
    List<PublisherAccount> publishers,
  ) =>
      Container(
        decoration: BoxDecoration(
          color: Theme.of(context).brightness == Brightness.dark
              ? Theme.of(context).appBarTheme.color
              : Theme.of(context).scaffoldBackgroundColor,
          border: Border(
            bottom: BorderSide(
              width: 0.5,
              color: Theme.of(context).dividerColor,
            ),
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Padding(
              padding: EdgeInsets.only(top: 16, left: 16, bottom: 8),
              child: Text(
                "Top Restaurants",
                style: Theme.of(context).textTheme.bodyText1,
              ),
            ),
            Container(
              width: double.infinity,
              height: 80,
              padding: EdgeInsets.only(bottom: 16),
              margin: EdgeInsets.only(bottom: 4),
              child: ListView(
                  padding: EdgeInsets.symmetric(horizontal: 16),
                  scrollDirection: Axis.horizontal,
                  children: publishers
                      .map((PublisherAccount profile) =>
                          _buildBusinessChip(context, profile))
                      .toList()),
            ),
          ],
        ),
      );

  Widget _buildBusinessChip(BuildContext context, PublisherAccount profile) =>
      GestureDetector(
        onTap: () => onFilterPublisher(profile),
        child: Container(
          margin: EdgeInsets.only(right: 8),
          padding: EdgeInsets.only(right: 16, left: 8, top: 8, bottom: 8),
          decoration: BoxDecoration(
            border: Border.all(color: Theme.of(context).dividerColor),
            borderRadius: BorderRadius.circular(4),
            color: Theme.of(context).brightness == Brightness.dark
                ? Theme.of(context).scaffoldBackgroundColor
                : Theme.of(context).appBarTheme.color,
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              UserAvatar(profile),
              SizedBox(width: 8),
              Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    profile.nameDisplay,
                    style: Theme.of(context).textTheme.bodyText1.merge(
                          TextStyle(
                            color: Theme.of(context).primaryColor,
                          ),
                        ),
                  ),
                  SizedBox(height: 2),
                  Text(
                    profile.getTagsAsString() ?? "",
                    style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(
                            color: Theme.of(context).hintColor,
                          ),
                        ),
                  ),
                ],
              )
            ],
          ),
        ),
      );
}
