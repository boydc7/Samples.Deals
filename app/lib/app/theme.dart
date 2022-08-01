import 'package:flutter/material.dart';

class AppShadows {
  static const Map<int, List<BoxShadow>> elevation = <int, List<BoxShadow>>{
    0: <BoxShadow>[
      BoxShadow(
          offset: Offset(0.0, 1.5),
          blurRadius: 0.5,
          spreadRadius: -1.0,
          color: Color(0x11000000)),
      BoxShadow(
          offset: Offset(0.0, 0.5),
          blurRadius: 0.5,
          spreadRadius: 0.0,
          color: Color(0x11000000)),
      BoxShadow(
          offset: Offset(0.0, 0.5),
          blurRadius: 2.0,
          spreadRadius: 0.0,
          color: Color(0x1F000000)),
    ],
    1: <BoxShadow>[
      BoxShadow(
          offset: Offset(0.0, 1.5),
          blurRadius: 1.0,
          spreadRadius: -1.0,
          color: Color(0x11000000)),
      BoxShadow(
          offset: Offset(0.0, 1.0),
          blurRadius: 1.0,
          spreadRadius: 0.0,
          color: Color(0x24000000)),
      BoxShadow(
          offset: Offset(0.0, 1.0),
          blurRadius: 3.0,
          spreadRadius: 0.0,
          color: Color(0x1F000000)),
    ],
    2: <BoxShadow>[
      BoxShadow(
          offset: Offset(0.0, 3.0),
          blurRadius: 1.0,
          spreadRadius: -2.0,
          color: Color(0x33000000)),
      BoxShadow(
          offset: Offset(0.0, 2.0),
          blurRadius: 2.0,
          spreadRadius: 0.0,
          color: Color(0x24000000)),
      BoxShadow(
          offset: Offset(0.0, 1.0),
          blurRadius: 5.0,
          spreadRadius: 0.0,
          color: Color(0x1F000000)),
    ],
    3: <BoxShadow>[
      BoxShadow(
          offset: Offset(0.0, 3.0),
          blurRadius: 3.0,
          spreadRadius: -2.0,
          color: Color(0x33000000)),
      BoxShadow(
          offset: Offset(0.0, 3.0),
          blurRadius: 4.0,
          spreadRadius: 0.0,
          color: Color(0x24000000)),
      BoxShadow(
          offset: Offset(0.0, 1.0),
          blurRadius: 8.0,
          spreadRadius: 0.0,
          color: Color(0x1F000000)),
    ],
    4: <BoxShadow>[
      BoxShadow(
          offset: Offset(0.0, 4.0),
          blurRadius: 4.0,
          spreadRadius: -2.0,
          color: Color(0x33000000)),
      BoxShadow(
          offset: Offset(0.0, 4.0),
          blurRadius: 5.0,
          spreadRadius: 0.0,
          color: Color(0x24000000)),
      BoxShadow(
          offset: Offset(0.0, 1.0),
          blurRadius: 8.0,
          spreadRadius: 0.0,
          color: Color(0x1F000000)),
    ],
  };
}

class AppColors {
  static const Color blue100 = const Color(0xFF4FAFFF);
  static const Color blue300 = const Color(0xFF32A3FF);
  static const Color blue = const Color(0xFF2196f3);
  static const Color blue700 = const Color(0xFF0085F2);

  static const Color teal = const Color(0xFF20DEF7);

  static const Color errorRed = const Color(0xFFff5252);
  static const Color successGreen = const Color(0xFF00C853);

  static const Color grey300 = const Color(0xFF999999);
  static const Color grey400 = const Color(0xFF666666);
  static const Color grey800 = const Color(0xFF242424);

  static const Color black = const Color(0xFF1F292E);

  static const Color white200 = const Color(0xFFebebeb);
  static const Color white100 = const Color(0xFFf4f4f4);
  static const Color white50 = const Color(0xFFfafafa);
  static const Color white20 = const Color(0xFFf0f0f0);
  static const Color white = Colors.white;
}

class AppTheme {
  ThemeData buildTheme() {
    final ThemeData base = ThemeData.light();

    return base.copyWith(
      scaffoldBackgroundColor: Colors.grey.shade50,
      accentColor: Colors.purple.shade600,
      primaryColor: Colors.blue.shade600,
      errorColor: AppColors.errorRed,
      iconTheme: IconThemeData(color: AppColors.grey800),
      hintColor: AppColors.grey300,
      textTheme: _buildAppTextTheme(base.textTheme),
      dividerColor: Color(0xFFd1d1d1),
      splashColor: AppColors.grey300.withOpacity(0.1),
      highlightColor: AppColors.grey300.withOpacity(0.2),
      canvasColor: Color(0xFFE0E0E0),
      chipTheme: _buildChipTheme(base.chipTheme),
      appBarTheme: _buildAppBarTheme(base.appBarTheme),
      tabBarTheme: _buildTabBarTheme(base.tabBarTheme),
      bottomAppBarColor: AppColors.white,
      inputDecorationTheme:
          InputDecorationTheme(filled: false, border: InputBorder.none),
    );
  }

  ThemeData buildDarkTheme() {
    final ThemeData base = ThemeData.dark();

    return base.copyWith(
      scaffoldBackgroundColor: Color(0xFF121212),
      accentColor: Colors.purple.shade400,
      primaryColor: Colors.blue.shade500,
      errorColor: AppColors.errorRed,
      iconTheme: IconThemeData(color: AppColors.grey300),
      hintColor: Color(0xFFa6a6a6),
      textTheme: _buildAppTextDarkTheme(base.textTheme),
      dividerColor: Color.fromRGBO(255, 255, 255, 0.12),
      splashColor: Color(0xFF262626),
      highlightColor: Color(0xFF262626),
      canvasColor: Color(0xFF1f1f1f),
      chipTheme: _buildChipDarkTheme(base.chipTheme),
      appBarTheme: _buildAppBarDarkTheme(base.appBarTheme),
      tabBarTheme: _buildTabBarDarkTheme(base.tabBarTheme),
      bottomAppBarColor: Color(0xFF1e1e1e),
      inputDecorationTheme:
          InputDecorationTheme(filled: false, border: InputBorder.none),
    );
  }

  ChipThemeData _buildChipTheme(ChipThemeData base) {
    return base.copyWith(backgroundColor: AppColors.grey300);
  }

  ChipThemeData _buildChipDarkTheme(ChipThemeData base) {
    return base.copyWith(backgroundColor: Color(0xFF262626));
  }

  TabBarTheme _buildTabBarTheme(TabBarTheme base) {
    return base.copyWith(
        labelColor: AppColors.grey800, unselectedLabelColor: AppColors.grey300);
  }

  TabBarTheme _buildTabBarDarkTheme(TabBarTheme base) {
    return base.copyWith(
        labelColor: AppColors.white.withOpacity(0.87),
        unselectedLabelColor: AppColors.white.withOpacity(0.38));
  }

  AppBarTheme _buildAppBarTheme(AppBarTheme base) {
    return base.copyWith(
      brightness: Brightness.light,
      elevation: 1.0,
      color: AppColors.white,
      textTheme: TextTheme(
          headline6: TextStyle(
        color: AppColors.grey800,
        fontSize: 17.0,
        fontWeight: FontWeight.w600,
      )),
      iconTheme: IconThemeData(color: AppColors.grey800, size: 19.0),
    );
  }

  AppBarTheme _buildAppBarDarkTheme(AppBarTheme base) {
    return base.copyWith(
      brightness: Brightness.dark,
      elevation: 1.0,
      color: Color(0xFF1e1e1e),
      textTheme: TextTheme(
          headline6: TextStyle(
        color: Colors.white,
        fontSize: 17.0,
        fontWeight: FontWeight.w600,
      )),
      iconTheme: IconThemeData(color: Colors.white, size: 19.0),
    );
  }

  TextTheme _buildAppTextTheme(TextTheme base) {
    return base
        .copyWith(
            button: base.button.copyWith(
                fontSize: 16, letterSpacing: 0.2, fontWeight: FontWeight.w600),
            headline5: base.headline5.copyWith(
                fontWeight: FontWeight.w500, color: AppColors.grey800),
            headline6: base.headline6.copyWith(
                color: AppColors.grey800, fontWeight: FontWeight.w600),
            headline4: base.headline4.copyWith(
                color: AppColors.grey800,
                fontWeight: FontWeight.w600,
                height: 1.02),
            headline3: base.headline4.copyWith(
                color: AppColors.grey800,
                fontWeight: FontWeight.w600,
                height: 1.02),
            headline2: base.headline4.copyWith(
                color: AppColors.grey800,
                fontWeight: FontWeight.w600,
                height: 1.02),
            bodyText2: base.bodyText2.copyWith(
                color: AppColors.grey800, fontWeight: FontWeight.normal),
            bodyText1: base.bodyText1.copyWith(
              color: AppColors.grey800,
              fontWeight: FontWeight.w500,
            ),
            caption: base.caption.copyWith(
                fontWeight: FontWeight.w400, color: AppColors.grey300),
            subtitle2: base.caption.copyWith(
                fontWeight: FontWeight.w400, color: AppColors.grey400),
            subtitle1: base.caption.copyWith(color: AppColors.grey800))
        .apply(displayColor: AppColors.grey800);
  }

  TextTheme _buildAppTextDarkTheme(TextTheme base) {
    return base
        .copyWith(
            button: base.button.copyWith(
                fontSize: 16, letterSpacing: 0.2, fontWeight: FontWeight.w600),
            headline5: base.headline5.copyWith(
                fontWeight: FontWeight.w500,
                color: AppColors.white.withOpacity(0.87)),
            headline6: base.headline6.copyWith(
                color: AppColors.white.withOpacity(0.87),
                fontWeight: FontWeight.w600),
            headline4: base.headline4.copyWith(
                color: AppColors.white.withOpacity(0.87),
                fontWeight: FontWeight.w600),
            headline3: base.headline4.copyWith(
                color: AppColors.white.withOpacity(0.87),
                fontWeight: FontWeight.w600),
            headline2: base.headline4.copyWith(
                color: AppColors.white.withOpacity(0.87),
                fontWeight: FontWeight.w600),
            bodyText2: base.bodyText2.copyWith(
                color: AppColors.white.withOpacity(0.87),
                fontWeight: FontWeight.normal),
            bodyText1: base.bodyText1.copyWith(
                color: AppColors.white.withOpacity(0.87),
                fontWeight: FontWeight.w500),
            caption: base.caption.copyWith(
                fontWeight: FontWeight.w400,
                color: AppColors.white.withOpacity(0.6)),
            subtitle2: base.caption.copyWith(
                fontWeight: FontWeight.w400,
                color: AppColors.white.withOpacity(0.6)),
            subtitle1: base.caption.copyWith(
                fontWeight: FontWeight.w400,
                color: AppColors.white.withOpacity(0.6)))
        .apply(displayColor: AppColors.white.withOpacity(0.87));
  }
}
