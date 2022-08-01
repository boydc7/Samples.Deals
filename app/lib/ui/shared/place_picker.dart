import 'dart:ui';

import 'package:flutter/material.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';

import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/ui/shared/blocs/place_picker.dart';

import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class LocationPickerPage extends StatefulWidget {
  final List<Place> existingPlaces;
  final Function onSelected;

  LocationPickerPage(
    this.existingPlaces, {
    this.onSelected,
  });

  @override
  State<StatefulWidget> createState() => _LocationPickerPageState();
}

class _LocationPickerPageState extends State<LocationPickerPage> {
  PlacePickerBloc _bloc;
  final TextEditingController _controller = TextEditingController();

  @override
  void initState() {
    super.initState();

    _bloc = PlacePickerBloc(widget.existingPlaces);
  }

  @override
  void dispose() {
    _bloc.dispose();
    _controller.dispose();

    super.dispose();
  }

  void _onPlaceConfirmed(FacebookPlaceInfo fbPlace) async {
    showSharedLoadingLogo(
      context,
      content: "Adding Location",
    );

    final Place place = await _bloc.addPlace(fbPlace);

    Navigator.of(context).pop();

    if (place != null) {
      if (widget.onSelected != null) {
        widget.onSelected(place);
      } else {
        Navigator.of(context).pop(place);
      }
    } else {
      showSharedModalError(context);
    }
  }

  void _clearSearch() {
    _controller.text = '';
    _bloc.clearSearch();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        automaticallyImplyLeading: true,
        leading: AppBarCloseButton(context),
        centerTitle: false,
        title: TextField(
          onEditingComplete: () => _bloc.search(_controller.text),
          cursorColor: AppColors.grey800,
          style: Theme.of(context).textTheme.bodyText2,
          controller: _controller,
          autofocus: true,
          decoration: InputDecoration(
              hintText: "Search Places...",
              hintStyle: Theme.of(context).textTheme.bodyText2.merge(
                    TextStyle(color: AppColors.grey400),
                  ),
              border: UnderlineInputBorder(
                borderSide: BorderSide.none,
              )),
        ),
        actions: <Widget>[
          Padding(
            padding: EdgeInsets.only(top: 6.0, bottom: 4.0),
            child: TextButton(
              label: 'Search',
              color: Theme.of(context).primaryColor,
              onTap: () => _bloc.search(_controller.text),
            ),
          )
        ],
      ),
      body: Column(
        mainAxisAlignment: MainAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: _buildLocationResults(context),
          )
        ],
      ),
    );
  }

  Widget _buildLocationResults(BuildContext context) {
    final loadingWidget = Container(
      width: double.infinity,
      child: LoadingLogo(
        radius: 80.0,
      ),
    );

    return StreamBuilder<PlacePickerSearchResponse>(
      stream: _bloc.searchResponse,
      builder: (context, snapshot) {
        final List<FacebookPlaceInfo> places = snapshot.data != null &&
                snapshot.data.isSearching == false &&
                snapshot.data.response != null &&
                snapshot.data.response.models != null
            ? snapshot.data.response.models
            : [];

        return snapshot.data?.isSearching == true
            ? loadingWidget
            : places.length == 0
                ? _buildNoResults()
                : ListView.separated(
                    separatorBuilder: (context, index) => Divider(
                      height: 0,
                      color: Theme.of(context).dividerColor,
                    ),
                    itemBuilder: (BuildContext context, int index) {
                      final FacebookPlaceInfo place = places[index];

                      return ListTile(
                        title: Text(place.name,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(fontWeight: FontWeight.w600)),
                        subtitle: place.singleLineAddress == ''
                            ? null
                            : Text(place.singleLineAddress,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(color: AppColors.grey300)),
                        onTap: () => _onPlaceConfirmed(place),
                      );
                    },
                    itemCount: places.length,
                  );
      },
    );
  }

  Widget _buildNoResults() {
    return Container(
      alignment: Alignment.center,
      padding: EdgeInsets.all(32),
      child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(
              AppIcons.searchLocation,
              size: 80.0,
              color: AppColors.grey300.withOpacity(0.4),
            ),
            SizedBox(height: 16.0),
            Text(
              _controller.text.trim().length < 2
                  ? ""
                  : "No places match your search\n\nTry adding your city or otherwise adjusting your search keywords",
              textAlign: TextAlign.center,
            ),
            SizedBox(height: 8.0),
            _controller.text == ""
                ? Container()
                : FlatButton(
                    textColor: Theme.of(context).primaryColor,
                    child: Text("Clear search"),
                    onPressed: _clearSearch,
                  ),
          ]),
    );
  }
}
