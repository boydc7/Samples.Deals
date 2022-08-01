import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/workspace.dart';

class WorkspaceResponse extends BaseResponse<Workspace> {
  WorkspaceResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => Workspace.fromJson(j),
        );

  WorkspaceResponse.fromModel(Workspace model)
      : super.fromModel(
          model,
        );
}

class WorkspacesResponse extends BaseResponses<Workspace> {
  WorkspacesResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => Workspace.fromJson(d))
                    .cast<Workspace>()
                    .toList()
                : []);
}

class WorkspaceAccessRequestsResponse
    extends BaseResponses<WorkspaceAccessRequest> {
  WorkspaceAccessRequestsResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => WorkspaceAccessRequest.fromJson(d))
                    .cast<WorkspaceAccessRequest>()
                    .toList()
                : []);

  WorkspaceAccessRequestsResponse.fromModels(
      List<WorkspaceAccessRequest> models)
      : super.fromModels(
          models,
        );
}

class WorkspaceUsersResponse extends BaseResponses<WorkspaceUser> {
  WorkspaceUsersResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => WorkspaceUser.fromJson(d))
                    .cast<WorkspaceUser>()
                    .toList()
                : []);

  WorkspaceUsersResponse.fromModels(List<WorkspaceUser> models)
      : super.fromModels(
          models,
        );
}

class WorkspacePublisherAccountInfoResponse
    extends BaseResponses<PublisherAccount> {
  WorkspacePublisherAccountInfoResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => PublisherAccount.fromProfileJson(d))
                    .cast<PublisherAccount>()
                    .toList()
                : []);

  WorkspacePublisherAccountInfoResponse.fromModels(
      List<PublisherAccount> models)
      : super.fromModels(
          models,
        );
}
