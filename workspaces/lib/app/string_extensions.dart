import 'package:rydrworkspaces/models/route_data.dart';

extension StringExtension on String {
  RouteData get getRoutingData {
    var uriData = Uri.parse(this);

    return RouteData(
      queryParameters: uriData.queryParameters,
      route: uriData.path,
    );
  }
}
