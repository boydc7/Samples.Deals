import 'package:rxdart/rxdart.dart';
import 'package:package_info/package_info.dart';

class AboutBloc {
  final _version = BehaviorSubject<String>();

  void getVersion() {
    PackageInfo.fromPlatform().then((PackageInfo packageInfo) {
      _version.sink.add('${packageInfo.buildNumber}:${packageInfo.version}');
    });
  }

  dispose() {
    _version.close();
  }

  BehaviorSubject<String> get version => _version;
}
