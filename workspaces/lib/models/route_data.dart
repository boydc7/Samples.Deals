class RouteData {
  final String route;
  final Map<String, String> queryParameters;

  RouteData({
    this.route,
    Map<String, String> queryParameters,
  }) : queryParameters = queryParameters;

  operator [](String key) => queryParameters[key];
}
