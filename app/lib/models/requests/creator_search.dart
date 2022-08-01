class CreatorSearchRequest {
  int skip;
  int take;
  int excludeInvitesDealId;
  List<int> excludePublisherAccountIds;
  String search;

  CreatorSearchRequest({
    this.skip = 0,
    this.take = 25,
    this.excludeInvitesDealId,
    this.excludePublisherAccountIds,
    this.search,
  });

  Map<String, dynamic> toMap() {
    final Map<String, dynamic> paramsMap = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        paramsMap[fieldName] = value;
      }
    }

    addIfNonNull('skip', skip);
    addIfNonNull('take', take);
    addIfNonNull('excludeInvitesDealId', excludeInvitesDealId);
    addIfNonNull('excludePublisherAccountIds', excludePublisherAccountIds);
    addIfNonNull('query',
        search != null && search.trim().length > 0 ? search.trim() : null);

    return paramsMap;
  }
}
