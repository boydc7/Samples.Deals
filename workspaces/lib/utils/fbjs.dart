@JS()
library fbjs;

import 'package:js/js.dart';

@JS('FB')
class Fb {
  external static getLoginStatus(Function callback);
  external static login(Function callback, LoginOptions options);
  external static logout(Function callback);
}

@JS()
@anonymous
class LoginOptions {
  external String get scope;
  external set scope(String v);

  external factory LoginOptions({
    String scope,
  });
}
