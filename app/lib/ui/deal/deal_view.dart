import 'package:flutter/material.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/responses/deal.dart';
import 'package:rydr_app/ui/deal/blocs/deal_view.dart';
import 'package:rydr_app/ui/deal/request.dart';
import 'package:rydr_app/ui/map/widgets/deal.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

/// stand-alone deal page that uses same deal layout as map page
/// can be accessed via a deep link from a share, or (eventually) via id from a notification
///
/// NOTE: when the deal was successfully loaded - AND - we already have a request with it for the given creator
/// then send the user to the RequestPage widget which will render the correct request based on current status
class DealPage extends StatefulWidget {
  final int dealId;
  final String dealLink;

  DealPage(this.dealId, this.dealLink);

  @override
  _DealPageState createState() => _DealPageState();
}

class _DealPageState extends State<DealPage> {
  final DealViewBloc _bloc = DealViewBloc();

  @override
  void initState() {
    super.initState();

    _bloc.loadDeal(widget.dealId, widget.dealLink);
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _onClose() {
    /// ensure we can pop
    if (Navigator.of(context).canPop()) {
      Navigator.of(context).pop();
    } else {
      Navigator.of(context).pushNamed(appState.getInitialRoute());
    }
  }

  @override
  Widget build(BuildContext context) => StreamBuilder<DealResponse>(
      stream: _bloc.dealResponse,
      builder: (context, snapshot) =>
          snapshot.connectionState == ConnectionState.waiting
              ? _buildLoadingBody()
              : snapshot.error != null || snapshot.data.error != null
                  ? _buildErrorBody(snapshot.data)
                  : snapshot.data.model.request != null
                      ? RequestPage(deal: snapshot.data.model)
                      : _buildSuccessBody(snapshot.data));

  Widget _buildLoadingBody() => Scaffold(
        body: ListView(
          children: <Widget>[
            LoadingDetailsShimmer(),
          ],
        ),
      );

  Widget _buildErrorBody(DealResponse dealResponse) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
        ),
        body: RetryError(
          onRetry: () => _bloc.loadDeal(widget.dealId, widget.dealLink),
          error: dealResponse.error,
        ),
      );

  Widget _buildSuccessBody(DealResponse dealResponse) => Scaffold(
        body: InfluencerDeal(deal: dealResponse.model, onClose: _onClose),
      );
}
