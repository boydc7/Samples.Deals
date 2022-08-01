import 'package:rydrworkspaces/models/enums/deal.dart';

class DealsRequest {
  bool requestsQuery;
  int skip;
  int take;
  DealSort sort;
  List<DealStatus> status;
  List<DealRequestStatus> requestStatus;
  String query;
  int dealId;
  int dealPublisherAccountId;
  int dealRequestPublisherAccountId;
  int placeId;
  double miles;
  bool isPrivateDeal;
  int minAge;
  bool refresh;

  DealsRequest({
    this.requestsQuery = false,
    this.skip = 0,
    this.take = 25,
    this.sort,
    this.status,
    this.requestStatus,
    this.query,
    this.dealId,
    this.dealPublisherAccountId,
    this.dealRequestPublisherAccountId,
    this.placeId,
    this.miles,
    this.isPrivateDeal = false,
    this.minAge,
    this.refresh,
  });

  Map<String, dynamic> toMap() {
    final Map<String, dynamic> paramsMap = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        paramsMap[fieldName] = value.toString();
      }
    }

    addIfNonNull('skip', skip);
    addIfNonNull('take', take);
    addIfNonNull('forceRefresh', refresh);
    addIfNonNull('sort', sort != null ? dealSortToString(sort) : null);
    addIfNonNull('dealId', dealId);
    addIfNonNull('dealPublisherAccountId', dealPublisherAccountId);
    addIfNonNull(
        'dealRequestPublisherAccountId', dealRequestPublisherAccountId);
    addIfNonNull('placeId', placeId);
    addIfNonNull('miles', miles);
    addIfNonNull(
        'search', query != null && query.trim() != "" ? query.trim() : null);
    addIfNonNull('isPrivateDeal',
        isPrivateDeal != null && !requestsQuery ? isPrivateDeal : null);
    addIfNonNull('minAge', minAge);

    /// NOTE: status is used for both the deal and requests query
    /// we can really only ever have one but we check either for null
    addIfNonNull(
        'status',
        status != null
            ? '[' +
                status
                    .map((DealStatus s) => '"${dealStatusToString(s)}"')
                    .toList()
                    .join(',') +
                ']'
            : null);
    addIfNonNull(
        'status',
        requestStatus != null
            ? '[' +
                requestStatus
                    .map((DealRequestStatus s) =>
                        '"${dealRequestStatusToString(s)}"')
                    .toList()
                    .join(',') +
                ']'
            : null);

    return paramsMap;
  }
}
