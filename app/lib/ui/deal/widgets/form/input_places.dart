import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/ui/deal/blocs/add_places.dart';
import 'package:rydr_app/ui/shared/place_picker.dart';
import 'package:shimmer/shimmer.dart';

class DealInputPlace extends StatefulWidget {
  final BehaviorSubject<Place> valueStream;
  final Function handlePlaceChange;
  final DealType dealType;

  DealInputPlace({
    this.valueStream,
    @required this.handlePlaceChange,
    @required this.dealType,
  });

  @override
  _DealInputPlaceState createState() => _DealInputPlaceState();
}

class _DealInputPlaceState extends State<DealInputPlace>
    with AutomaticKeepAliveClientMixin {
  final _bloc = DealAddPlacesBloc();

  PageController _pageController;
  List<Place> places;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();

    _bloc.loadPlaces(
      widget.valueStream?.value,
      widget.handlePlaceChange,
    );

    _pageController = PageController(
      viewportFraction: 0.935,
      initialPage: 0,
      keepPage: true,
    );
  }

  @override
  void dispose() {
    _bloc.dispose();
    _pageController.dispose();

    super.dispose();
  }

  //// open the place picker for searching for and adding a new place to
  /// the businesses' list of locations which they can use for deals going forward
  void _showPlacePicker(BuildContext context) {
    FocusScope.of(context).requestFocus(FocusNode());

    Navigator.push(
        context,
        MaterialPageRoute(
          fullscreenDialog: true,
          builder: (context) => LocationPickerPage(places),
          settings:
              AppAnalytics.instance.getRouteSettings('deal/add/locationpicker'),
        )).then((result) {
      /// if we have a valid "result" (place) passed back from the location picker
      /// then insert it as the first item in the list of places and set the deal to it
      if (result != null) {
        places.insert(0, result);
        widget.handlePlaceChange(result);

        /// animate the page control to the first page now
        _pageController.animateToPage(
          0,
          curve: Curves.ease,
          duration: const Duration(milliseconds: 200),
        );
      }
    });
  }

  /// listen to changes to the place and set the place of the deal,
  /// unless we're on the last page which would have the "add location" box
  void _onPlaceChanged(int page) {
    /// ensure this is not the 'last' place which would be 'add location' box instead
    /// in which case we'd want to null-out any existing place
    widget.handlePlaceChange(page < places.length ? places[page] : null);
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);

    return Container(
      height: 226,
      width: double.infinity,
      child: StreamBuilder<List<Place>>(
        stream: _bloc.places,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return PageView(
              physics: NeverScrollableScrollPhysics(),
              controller: _pageController,
              children: [_buildLoading()],
            );
          } else {
            places = snapshot.data ?? [];
            List<Widget> items = [];

            places.forEach((Place p) {
              return items.add(Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Container(
                    height: 130,
                    margin: EdgeInsets.only(left: 4.0, right: 4.0, top: 8.0),
                    decoration: BoxDecoration(
                        border: Border.all(
                            color: Theme.of(context).hintColor, width: 1.0),
                        borderRadius: BorderRadius.circular(4.0)),
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(4.0),
                      child: Stack(
                        alignment: Alignment.center,
                        children: <Widget>[
                          Container(
                            decoration: BoxDecoration(
                              color: Theme.of(context).appBarTheme.color,
                              image: DecorationImage(
                                alignment: Alignment.center,
                                fit: BoxFit.cover,
                                image: Utils.googleMapImage(context,
                                    width: 600,
                                    height: 600,
                                    zoom: 16,
                                    latitude: p.address.latitude,
                                    longitude: p.address.longitude),
                              ),
                            ),
                          ),
                          AnimatedCrossFade(
                            firstChild: Container(
                              margin: EdgeInsets.only(bottom: 32.0),
                              width: 32.0,
                              decoration: BoxDecoration(
                                image: DecorationImage(
                                  image:
                                      AssetImage('assets/marker-virtual.png'),
                                ),
                              ),
                            ),
                            secondChild: Container(
                              margin: EdgeInsets.only(bottom: 32.0),
                              width: 32.0,
                              decoration: BoxDecoration(
                                image: DecorationImage(
                                  image: AssetImage('assets/marker-deal.png'),
                                ),
                              ),
                            ),
                            crossFadeState: widget.dealType == DealType.Virtual
                                ? CrossFadeState.showFirst
                                : CrossFadeState.showSecond,
                            duration: Duration(milliseconds: 250),
                            firstCurve: Curves.easeInOut,
                            secondCurve: Curves.easeInOut,
                            sizeCurve: Curves.easeInOut,
                          ),
                          _bloc.isPlaceIsInValidRegion(p)
                              ? Container()
                              : BackdropFilter(
                                  filter: ImageFilter.blur(
                                      sigmaX: 2.0, sigmaY: 2.0),
                                  child: Container(
                                    width: double.infinity,
                                    height: 130,
                                    color: AppColors.grey800.withOpacity(0.5),
                                    child: Center(
                                      child: Text(
                                        "RYDR is not yet available in this region...",
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodyText1
                                            .merge(
                                              TextStyle(
                                                  color: AppColors.white
                                                      .withOpacity(0.87)),
                                            ),
                                      ),
                                    ),
                                  ),
                                ),
                        ],
                      ),
                    ),
                  ),
                  ListTile(
                    contentPadding:
                        EdgeInsets.symmetric(horizontal: 6.0, vertical: 0.0),
                    title: Text(p.name,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context)
                            .textTheme
                            .bodyText1
                            .merge(TextStyle(fontWeight: FontWeight.w600))),
                    subtitle: Text(
                      p.address.name,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ],
              ));
            });

            /// placeholder to add a new place, this will show at the beginning if the profile has locations
            /// and will be the first and only option if they don't have any on file yet
            items.add(_buildAddPlace(context));

            return PageView(
              controller: _pageController,
              children: items,
              onPageChanged: _onPlaceChanged,
            );
          }
        },
      ),
    );
  }

  Widget _buildLoading() {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return Stack(
      alignment: Alignment.topCenter,
      children: <Widget>[
        Container(
          margin: EdgeInsets.only(top: 16.0, right: 4.0, left: 4.0),
          height: 176,
          decoration: BoxDecoration(
              color: dark ? AppColors.white.withOpacity(0.02) : AppColors.white,
              borderRadius: BorderRadius.circular(8.0)),
        ),
        Shimmer.fromColors(
          baseColor:
              dark ? AppColors.white.withOpacity(0.02) : AppColors.white100,
          highlightColor:
              dark ? AppColors.white.withOpacity(0.1) : AppColors.white50,
          child: Container(
            margin: EdgeInsets.only(top: 16.0, right: 4.0, left: 4.0),
            height: 176,
            decoration: BoxDecoration(
                color:
                    dark ? AppColors.white.withOpacity(0.02) : AppColors.white,
                borderRadius: BorderRadius.circular(8.0)),
            child: Center(
              child: Text(
                'Loading your locations...',
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(
                        fontWeight: FontWeight.w600,
                        color: Theme.of(context).textTheme.bodyText2.color,
                      ),
                    ),
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildAddPlace(BuildContext context) => Column(
        children: <Widget>[
          GestureDetector(
            onTap: () => _showPlacePicker(context),
            child: Container(
              margin: EdgeInsets.only(top: 16.0, right: 4.0, left: 4.0),
              height: 130,
              decoration: BoxDecoration(
                  border: Border.all(color: Theme.of(context).hintColor),
                  borderRadius: BorderRadius.circular(4.0)),
              child: Center(
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: <Widget>[
                    Icon(AppIcons.storeAlt,
                        size: 24.0, color: Theme.of(context).primaryColor),
                    SizedBox(
                      width: 16.0,
                    ),
                    Text(
                      'Add a Location',
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(
                              color: Theme.of(context).primaryColor,
                              fontWeight: FontWeight.w600)),
                    )
                  ],
                ),
              ),
            ),
          ),
          Padding(
            padding: EdgeInsets.only(
              top: 16.0,
            ),
            child: RichText(
                textAlign: TextAlign.center,
                text: TextSpan(
                    style: Theme.of(context)
                        .textTheme
                        .caption
                        .merge(TextStyle(color: AppColors.grey400)),
                    children: <TextSpan>[
                      TextSpan(
                          text: 'Location · ',
                          style: TextStyle(fontWeight: FontWeight.w600)),
                      TextSpan(
                          text:
                              'Tap above to find your RYDR\'s physical location.\nThis is where the creator will capture the posts.')
                    ])),
          )
        ],
      );
}
