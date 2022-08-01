import 'package:flutter/material.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class InsightsMediaAnalysisQueuePage extends StatefulWidget {
  @override
  _InsightsMediaAnalysisQueuePageState createState() =>
      _InsightsMediaAnalysisQueuePageState();
}

class _InsightsMediaAnalysisQueuePageState
    extends State<InsightsMediaAnalysisQueuePage> {
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: AppBarBackButton(context),
        title: Text("Selfie Vision™ Queue"),
      ),
      body: Text("hello"),
    );
  }
}
