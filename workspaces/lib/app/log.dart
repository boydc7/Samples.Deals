import 'package:logger/logger.dart';
import 'package:rydrworkspaces/app/state.dart';

class AppLogPrinter extends LogPrinter {
  final String className;

  AppLogPrinter(this.className);

  @override
  List<String> log(LogEvent event, [bool addUser = false]) {
    var color = PrettyPrinter.levelColors[event.level];
    var emoji = PrettyPrinter.levelEmojis[event.level];

    var profile = appState.currentProfile == null
        ? null
        : {
            'profile_id': appState.currentProfile.id,
            'profile_name': appState.currentProfile.userName,
          };

    return [
      color(addUser
              ? '$emoji[$className] -> ${event.message} $profile'
              : '$emoji[$className] -> ${event.message}')
          .toString()
    ];
  }
}

Logger getLogger(String className) => Logger(printer: AppLogPrinter(className));
