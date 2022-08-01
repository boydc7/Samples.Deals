import 'package:rxdart/subjects.dart';
import 'package:rydrworkspaces/models/responses/deal.dart';
import 'package:rydrworkspaces/services/deal_request.dart';

class RequestBloc {
  final _dealResponse = BehaviorSubject<DealResponse>();

  dispose() {
    _dealResponse.close();
  }

  Stream<DealResponse> get dealResponse => _dealResponse.stream;

  void loadReport(String reportId) async {
    _dealResponse.sink
        .add(await DealRequestService.getRequestExternalReport(reportId));
  }
}
