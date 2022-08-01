import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/responses/deal.dart';
import 'package:rydr_app/services/deal.dart';

class DealViewBloc {
  final _dealResponse = BehaviorSubject<DealResponse>();

  dispose() {
    _dealResponse.close();
  }

  BehaviorSubject<DealResponse> get dealResponse => _dealResponse.stream;

  /// load deal either using id or deep link guid
  void loadDeal(int dealId, String dealLink) async => dealId != null
      ? _dealResponse.sink.add(await DealService.getDeal(dealId))
      : _dealResponse.sink.add(await DealService.getDealByLink(dealLink));
}
