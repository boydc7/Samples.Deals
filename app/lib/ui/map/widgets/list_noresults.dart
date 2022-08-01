import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:flutter/widgets.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';

import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';

import 'package:rydr_app/models/place.dart';

import 'package:rydr_app/ui/map/blocs/map.dart';

class ListNoResults extends StatelessWidget {
  final MapBloc mapBloc;
  final Function goToLocation;
  final bool isPlaceList;

  ListNoResults({
    @required this.mapBloc,
    @required this.goToLocation,
    @required this.isPlaceList,
  });

  @override
  Widget build(BuildContext context) {
    if (isPlaceList) {
      return SliverList(
        delegate: SliverChildListDelegate([
          Container(
            color: Theme.of(context).scaffoldBackgroundColor,
            padding: EdgeInsets.all(16),
            child: Text(
              "No RYDRs at this location...",
              style: TextStyle(
                color: Theme.of(context).hintColor,
              ),
            ),
          )
        ]),
      );
    } else {
      /// if this is not a places list, then build out a list of region, cities, or neighborhoods
      /// if availalbe... these would have been generated for us in the bloc as part of the no results response
      final MapListNoResultsOptions options = mapBloc.noResultOptions;

      final List<Widget> children = options != null
          ? options.regions.isNotEmpty
              ? options.regions
                  .map((AvailableLocation loc) =>
                      _buildNoResultImageCard(context, loc))
                  .toList()
              : options.cities.isNotEmpty
                  ? options.cities
                      .map((AvailableLocation loc) =>
                          _buildNoResultImageCard(context, loc))
                      .toList()
                  : options.neighborhoods.isNotEmpty
                      ? options.neighborhoods
                          .map((AvailableLocation loc) =>
                              _buildNoResultImageCard(context, loc))
                          .toList()
                      : []
          : []
        ..insert(
          0,
          Container(
            padding: EdgeInsets.symmetric(vertical: 24),
            color: Theme.of(context).scaffoldBackgroundColor,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.center,
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(
                  options.regions.isNotEmpty
                      ? "We are not in your area yet."
                      : "No RYDRs in this area.",
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.headline6.merge(
                        TextStyle(
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                ),
                SizedBox(height: 4),
                Text(
                  options.regions.isNotEmpty
                      ? 'View our supported regions below'
                      : "Check out some other neighborhoods",
                  style: Theme.of(context).textTheme.bodyText2.merge(
                        TextStyle(
                          color: Theme.of(context).hintColor,
                        ),
                      ),
                  textAlign: TextAlign.center,
                ),
              ],
            ),
          ),
        )
        ..add(Container(
          padding: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          color: Theme.of(context).scaffoldBackgroundColor,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              Text(
                'Where should we go?',
                style: Theme.of(context).textTheme.bodyText1,
                textAlign: TextAlign.center,
              ),
              Text(
                "Let us know where you'd like to see RYDR next.",
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(
                        color: Theme.of(context).hintColor,
                      ),
                    ),
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ))
        ..add(Container(
            padding: EdgeInsets.only(right: 16, left: 16, top: 8, bottom: 60),
            color: Theme.of(context).scaffoldBackgroundColor,
            child: PrimaryButton(
              buttonColor: Theme.of(context).primaryColor,
              onTap: () => Utils.launchUrl(
                  context, "https://getrydr.com/bring-rydr-to-your-city/"),
              label: 'Vote for Your City',
              hasIcon: true,
              icon: AppIcons.mapMarkerAltSolid,
            )));

      return SliverList(delegate: SliverChildListDelegate(children));
    }
  }

  Widget _buildNoResultImageCard(BuildContext context, AvailableLocation loc) =>
      GestureDetector(
        onTap: () => goToLocation(loc),
        child: Container(
          height: 180.0,
          color: Theme.of(context).scaffoldBackgroundColor,
          width: MediaQuery.of(context).size.width - 32.0,
          padding: EdgeInsets.only(top: 8, bottom: 8, left: 16, right: 16),
          child: CachedNetworkImage(
            imageUrl: loc.url,
            imageBuilder: (context, imageProvider) => Container(
              decoration: loc.url != null
                  ? BoxDecoration(
                      borderRadius: BorderRadius.circular(4.0),
                      image: DecorationImage(
                          fit: BoxFit.cover, image: imageProvider))
                  : BoxDecoration(
                      borderRadius: BorderRadius.circular(4.0),
                    ),
              alignment: Alignment.bottomLeft,
              child: Center(
                child: Theme.of(context).brightness == Brightness.dark
                    ? Chip(
                        backgroundColor: Theme.of(context)
                            .chipTheme
                            .backgroundColor
                            .withOpacity(0.87),
                        labelPadding:
                            EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                        label: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          crossAxisAlignment: CrossAxisAlignment.center,
                          mainAxisSize: MainAxisSize.min,
                          children: <Widget>[
                            Text(
                              loc.name,
                              style: Theme.of(context)
                                  .textTheme
                                  .bodyText1
                                  .merge(
                                    TextStyle(
                                      color: AppColors.white.withOpacity(0.87),
                                    ),
                                  ),
                            ),
                            Text(
                              "Tap to explore",
                              style: Theme.of(context).textTheme.caption.merge(
                                    TextStyle(
                                      color: Theme.of(context).hintColor,
                                    ),
                                  ),
                            ),
                          ],
                        ),
                      )
                    : Container(
                        padding:
                            EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                        decoration: BoxDecoration(
                          color: Colors.white.withOpacity(0.8),
                          borderRadius: BorderRadius.circular(50),
                        ),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          crossAxisAlignment: CrossAxisAlignment.center,
                          mainAxisSize: MainAxisSize.min,
                          children: <Widget>[
                            Text(loc.name,
                                style: Theme.of(context).textTheme.bodyText1),
                            Text(
                              "Tap to explore",
                              style: Theme.of(context).textTheme.caption.merge(
                                    TextStyle(
                                      color: Theme.of(context).hintColor,
                                    ),
                                  ),
                            ),
                          ],
                        ),
                      ),
              ),
            ),
            errorWidget: (context, url, error) => ImageError(
              logUrl: url,
              logParentName: 'map/widgets/list.dart > _buildNoResultImageCard',
            ),
          ),
        ),
      );
}
