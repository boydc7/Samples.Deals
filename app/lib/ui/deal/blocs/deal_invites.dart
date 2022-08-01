import 'package:rydr_app/models/requests/deals.dart';
import 'package:rydr_app/models/responses/deals.dart';
import 'package:rydr_app/services/deals.dart';

class DealInvitesBloc {
  /// get all requests for this deal that were originally sent out as invites
  /// to the publishers, so this then returns them all including their current status
  Future<DealsResponse> loadInviteRequests(int dealId) async =>
      await DealsService.queryDeals(
        true,
        request: DealsRequest(
          requestsQuery: true,
          dealId: dealId,

          /// NOTE: this ensures we filter by requests that originated
          /// as invites, vs. filtering by the current request status
          wasInvited: true,

          /// NOTE: we have a soft-limit of 200 invites for now...
          take: 200,
        ),

        /// always look for latest data here
        forceRefresh: true,
      );
}
