import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

class DealAddThresholdSection extends StatelessWidget {
  final Stream<DealVisibilityType> valueStream;
  final Stream<DealThresholdType> thresholdValueStream;
  final Function handleUpdate;
  final bool canUseInsights;

  DealAddThresholdSection({
    @required this.valueStream,
    @required this.thresholdValueStream,
    @required this.handleUpdate,
    @required this.canUseInsights,
  });

  final Map<String, String> _pageContent = {
    'title': 'Choosing Creators',
    'subtitle': 'Who should be able to see this RYDR?',
    'restrictions_title': 'Based on',
    'restrictions_subtitle': 'Followers & Engagement',
    'insights_title': 'Based on',
    'insights_subtitle': 'RYDR Insights',
  };

  void _handleTap(BuildContext context, DealThresholdType type) {
    FocusScope.of(context).requestFocus(FocusNode());

    handleUpdate(type);
  }

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<DealVisibilityType>(
        stream: valueStream,
        builder: (context, snapshot) {
          return snapshot.data != null &&
                  snapshot.data == DealVisibilityType.Marketplace
              ? StreamBuilder<DealThresholdType>(
                  stream: thresholdValueStream,
                  builder: (context, snapshot) {
                    DealThresholdType currentType = snapshot.data;

                    return Column(
                      children: <Widget>[
                        SizedBox(height: 20.0),
                        sectionDivider(context),
                        SizedBox(height: 32.0),
                        Padding(
                          padding: EdgeInsets.only(left: 16, right: 16),
                          child: Column(
                            children: <Widget>[
                              Text(
                                _pageContent['title'],
                                style: Theme.of(context).textTheme.bodyText1,
                              ),
                              SizedBox(height: 2.0),
                              Text(
                                _pageContent['subtitle'],
                                style: Theme.of(context)
                                    .textTheme
                                    .caption
                                    .merge(
                                      TextStyle(
                                          color: Theme.of(context).hintColor),
                                    ),
                              ),
                              SizedBox(height: 24.0),

                              /// only show the choice between marketplace and rydr insights
                              /// if we have insights enabled...
                              canUseInsights
                                  ? Column(
                                      children: <Widget>[
                                        Row(
                                          children: <Widget>[
                                            _buildBox(
                                              context,
                                              DealThresholdType.Restrictions,
                                              currentType,
                                              _pageContent[
                                                  'restrictions_title'],
                                              _pageContent[
                                                  'restrictions_subtitle'],
                                              _handleTap,
                                            ),
                                            SizedBox(
                                              width: 8.0,
                                            ),
                                            _buildBox(
                                              context,
                                              DealThresholdType.Insights,
                                              currentType,
                                              _pageContent['insights_title'],
                                              _pageContent['insights_subtitle'],
                                              _handleTap,
                                            ),
                                          ],
                                        ),
                                        SizedBox(height: 12.0),
                                      ],
                                    )
                                  : Container(height: 0),
                            ],
                          ),
                        ),
                      ],
                    );
                  })
              : Container();
        });
  }

  Widget _buildBox(
    BuildContext context,
    DealThresholdType type,
    DealThresholdType currentType,
    String title,
    String subTitle,
    Function onTap,
  ) {
    return Expanded(
      child: GestureDetector(
        onTap: () => onTap(context, type),
        child: AnimatedContainer(
          height: 100.0,
          duration: Duration(milliseconds: 250),
          decoration: BoxDecoration(
              color: currentType == null
                  ? Theme.of(context).scaffoldBackgroundColor
                  : type == currentType
                      ? Theme.of(context).appBarTheme.color
                      : Theme.of(context).scaffoldBackgroundColor,
              border: Border.all(
                  width: currentType == null
                      ? 1.0
                      : type == currentType ? 2.0 : 1.0,
                  color: currentType == null
                      ? Theme.of(context).hintColor
                      : type == currentType
                          ? Theme.of(context).primaryColor
                          : Theme.of(context).hintColor),
              borderRadius: BorderRadius.circular(4.0)),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              Text(
                title,
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(
                          color: currentType == null
                              ? Theme.of(context).hintColor
                              : type != currentType
                                  ? Theme.of(context).hintColor
                                  : Theme.of(context).primaryColor),
                    ),
              ),
              Text(subTitle,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                      color: currentType == null
                          ? Theme.of(context).textTheme.bodyText2.color
                          : type != currentType
                              ? Theme.of(context).textTheme.bodyText2.color
                              : Theme.of(context).primaryColor)),
            ],
          ),
        ),
      ),
    );
  }
}
