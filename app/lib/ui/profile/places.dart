import 'dart:async';

import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/ui/shared/place_picker.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';

import 'package:rydr_app/models/place.dart';

import 'blocs/places.dart';

class ProfilePlacesPage extends StatefulWidget {
  @override
  _ProfilePlacesPageState createState() => _ProfilePlacesPageState();
}

class _ProfilePlacesPageState extends State<ProfilePlacesPage> {
  final _bloc = PlacesBloc();

  @override
  void initState() {
    super.initState();

    _bloc.loadPlaces();
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _showPlacePicker() {
    Navigator.push(
        context,
        MaterialPageRoute(
          fullscreenDialog: true,
          builder: (context) => LocationPickerPage(_bloc.places.value),
          settings: AppAnalytics.instance
              .getRouteSettings('profile/settings/places/locationpicker'),
        )).then((result) => _bloc.loadPlaces());
  }

  void _onPlaceMarkedPrimary(Place place) async {
    showSharedLoadingLogo(
      context,
      content: "Updating Location",
    );

    final bool success = await _bloc.markAsPrimary(place);

    Navigator.of(context).pop();

    if (!success) {
      showSharedModalError(context);
    }
  }

  Future<bool> _confirmRemove(Place place) async {
    return await showSharedModalAlert(context, Text("Delete ${place.name}"),
        content: Text("This will remove ${place.name} your list of locations."),
        actions: <ModalAlertAction>[
          ModalAlertAction(
            label: "Cancel",
            onPressed: () => Navigator.of(context).pop(false),
          ),
          ModalAlertAction(
              label: "Remove",
              isDestructiveAction: true,
              onPressed: () {
                showSharedLoadingLogo(context);

                _bloc.deletePlace(place).then((success) {
                  if (success) {
                    Navigator.of(context).pop(success);
                    Navigator.of(context).pop(success);
                  } else {
                    Navigator.of(context).pop();
                    Navigator.of(context).pop(success);
                    showSharedModalError(context,
                        title: "Unable to remove this location",
                        subTitle: "Please try again in a few moments");
                  }
                });
              }),
        ]);
  }

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<bool>(
      stream: _bloc.loading,
      builder: (context, snapshot) {
        return snapshot.data == null || snapshot.data == true
            ? _buildLoadingScaffold()
            : _buildSuccessScaffold();
      },
    );
  }

  Widget _buildLoadingScaffold() => Scaffold(
      appBar: AppBar(
        leading: AppBarBackButton(context),
        title: Text("My Locations"),
      ),
      body: ListView(children: [
        LoadingListShimmer(),
      ]));

  Widget _buildSuccessScaffold() {
    return StreamBuilder<List<Place>>(
        stream: _bloc.places,
        builder: (context, snapshot) {
          final List<Place> list = snapshot.data;

          if (list == null) {
            return _buildLoadingScaffold();
          }

          return Scaffold(
              appBar: AppBar(
                leading: AppBarBackButton(context),
                title: Text("My Locations"),
                actions: <Widget>[
                  Visibility(
                    visible: list.length > 0,
                    child: TextButton(
                      label: 'Add',
                      color: Theme.of(context).primaryColor,
                      onTap: _showPlacePicker,
                    ),
                  )
                ],
              ),
              body: list.length > 0
                  ? Column(
                      children: <Widget>[
                        Expanded(
                          child: ListView.separated(
                              separatorBuilder: (context, index) => index == 0
                                  ? Container(height: 0, width: 0)
                                  : Divider(height: 0),
                              itemCount: list.length,
                              itemBuilder: (BuildContext context, int index) =>
                                  index == 0
                                      ? _buildPrimaryLocation(
                                          list[index],
                                          list.length > 1,
                                        )
                                      : _buildAdditionalLocation(list[index])),
                        ),
                      ],
                    )
                  : _buildNoLocations());
        });
  }

  Widget _buildNoLocations() {
    return Container(
      padding: EdgeInsets.symmetric(horizontal: 16.0),
      width: double.infinity,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        mainAxisAlignment: MainAxisAlignment.center,
        mainAxisSize: MainAxisSize.max,
        children: <Widget>[
          Text('No Business Locations',
              style: Theme.of(context).textTheme.headline6),
          SizedBox(
            height: 8.0,
          ),
          Text(
            'When you add a location for your business, \nyou\'ll see it here.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyText2.merge(
                  TextStyle(color: AppColors.grey300),
                ),
          ),
          Container(
            margin: EdgeInsets.only(top: 32.0),
            child: PrimaryButton(
              hasShadow: true,
              onTap: _showPlacePicker,
              label: 'Add a Location',
            ),
          )
        ],
      ),
    );
  }

  Widget _buildPrimaryLocation(Place place, bool hasMore) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          padding: EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                'Primary Location',
                style: TextStyle(fontWeight: FontWeight.w600),
              ),
              Text(
                'This will be used as the default location on all new RYDRs.',
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(color: AppColors.grey300),
                    ),
              ),
            ],
          ),
        ),
        ListTile(
          selected: true,
          leading: Container(
              height: 40.0,
              child: Icon(
                AppIcons.dotCircle,
                color: Theme.of(context).primaryColor,
              )),
          title: Text(
            place.name,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.bodyText1.merge(
                  TextStyle(
                      fontWeight: FontWeight.w500,
                      color: Theme.of(context).primaryColor),
                ),
          ),
          subtitle: Text(
            place.address != null && place.address.address1 != null
                ? place.address.name
                : "",
            overflow: TextOverflow.ellipsis,
            style: TextStyle(color: Theme.of(context).primaryColor),
          ),
        ),
        sectionDivider(context),
        Visibility(
          visible: hasMore,
          child: Container(
            padding: EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  'Additional Locations',
                  style: TextStyle(fontWeight: FontWeight.w600),
                ),
                Text(
                  'These can be easily chosen when creating a new RYDR.',
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(color: AppColors.grey300),
                      ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildAdditionalLocation(Place place) {
    return Dismissible(
      key: Key(place.id.toString()),
      direction: DismissDirection.endToStart,
      background: Container(
        padding: EdgeInsets.only(right: 16.0),
        alignment: Alignment.centerRight,
        color: AppColors.errorRed,
        child: Text(
          'Remove',
          style: TextStyle(color: AppColors.white),
        ),
      ),
      onDismissed: (direction) => null,
      confirmDismiss: (DismissDirection direction) async =>
          await _confirmRemove(place),
      child: ListTile(
        leading: GestureDetector(
          onTap: () => _onPlaceMarkedPrimary(place),
          child: Container(
            height: 40.0,
            child: Icon(
              AppIcons.circle,
              color: AppColors.grey300,
            ),
          ),
        ),
        title: Text(
          place.name,
          style: Theme.of(context).textTheme.bodyText1.merge(
                TextStyle(
                    fontWeight: FontWeight.w500,
                    color: Theme.of(context).textTheme.bodyText2.color),
              ),
        ),
        subtitle: place.address != null && place.address.name != null
            ? Text(
                place.address.name,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(color: Theme.of(context).hintColor),
              )
            : null,
      ),
    );
  }
}
