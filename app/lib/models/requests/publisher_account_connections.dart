import 'package:rydr_app/models/enums/publisher_account.dart';

class PubAccountConnectionsRequest {
  int fromPublisherAccountId;
  int toPublisherAccountId;
  DateTime lastConnectedAfter;
  DateTime lastConnectedBefore;
  List<PublisherAccountConnectionType> connectionTypes;
  String search;

  PubAccountConnectionsRequest({
    this.fromPublisherAccountId,
    this.toPublisherAccountId,
    this.lastConnectedAfter,
    this.lastConnectedBefore,
    this.connectionTypes,
    this.search,
  });

  Map<String, dynamic> toMap() {
    final Map<String, dynamic> paramsMap = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        paramsMap[fieldName] = value.toString();
      }
    }

    addIfNonNull('fromPublisherAccountId', fromPublisherAccountId);
    addIfNonNull('toPublisherAccountId', toPublisherAccountId);
    addIfNonNull('lastConnectedAfter', lastConnectedAfter);
    addIfNonNull('lastConnectedBefore', lastConnectedBefore);
    addIfNonNull(
        'connectionTypes',
        connectionTypes != null
            ? '[' +
                connectionTypes
                    .map((PublisherAccountConnectionType s) =>
                        '"${publisherAccountConnectionTypeToString(s)}"')
                    .toList()
                    .join(',') +
                ']'
            : null);
    addIfNonNull('search',
        search != null && search.trim().length > 0 ? search.trim() : null);

    return paramsMap;
  }
}
