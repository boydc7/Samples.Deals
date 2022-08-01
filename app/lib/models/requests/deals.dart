import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/tag.dart';

class DealsRequest {
  bool requestsQuery;
  int skip;
  int take;
  DealSort sort;
  List<DealType> dealTypes;
  List<DealStatus> status;
  List<DealRequestStatus> requestStatus;
  List<Tag> tags;
  String query;
  int dealId;
  int publisherAccountId;
  int dealRequestPublisherAccountId;
  int placeId;
  PlaceLatLng latLng;
  PlaceLatLng userLatLng;
  PlaceLatLngBounds boundingBox;
  double miles;
  bool isPrivateDeal;
  bool includeExpired;
  bool wasInvited;
  int minAge;
  bool refresh;

  DealsRequest({
    this.requestsQuery = false,
    this.skip = 0,
    this.take = 25,
    this.sort,
    this.dealTypes,
    this.status,
    this.requestStatus,
    this.tags,
    this.query,
    this.dealId,
    this.publisherAccountId,
    this.dealRequestPublisherAccountId,
    this.placeId,
    this.latLng,
    this.userLatLng,
    this.boundingBox,
    this.miles,
    this.isPrivateDeal = false,
    this.includeExpired,
    this.wasInvited,
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
    addIfNonNull('publisherAccountId', publisherAccountId);
    addIfNonNull(
        'dealRequestPublisherAccountId', dealRequestPublisherAccountId);
    addIfNonNull('placeId', placeId);
    addIfNonNull('latitude', latLng?.latitude);
    addIfNonNull('longitude', latLng?.longitude);
    addIfNonNull('userLatitude', userLatLng?.latitude);
    addIfNonNull('userLongitude', userLatLng?.longitude);
    addIfNonNull('boundingBox',
        boundingBox != null ? boundingBox.toQueryString() : null);
    addIfNonNull('miles', miles);
    addIfNonNull(
        'search', query != null && query.trim() != "" ? query.trim() : null);
    addIfNonNull('isPrivateDeal',
        isPrivateDeal != null && !requestsQuery ? isPrivateDeal : null);
    addIfNonNull('includeExpired', includeExpired);
    addIfNonNull('wasInvited', wasInvited);
    addIfNonNull('minAge', minAge);

    addIfNonNull(
        'dealTypes',
        dealTypes != null
            ? '[' +
                dealTypes
                    .map((DealType t) => '"${dealTypeToString(t)}"')
                    .toList()
                    .join(',') +
                ']'
            : null);

    addIfNonNull(
        'tags',
        tags != null && tags.isNotEmpty
            ? tags.map((t) => t.toJson()).toList().toString()
            : null);

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
