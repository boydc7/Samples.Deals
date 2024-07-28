using EnumsNET;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.QueryDto;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services;

[RydrNeverCacheResponse("workspaces", "publisheracct")]
public class PublisherAccountConnectionService : BaseAuthenticatedApiService
{
    private static readonly string[] _publisherAccountConnectionTypePrefixes = Enums.GetNames<PublisherAccountConnectionType>()
                                                                                    .Where(n => !n.EqualsOrdinalCi(PublisherAccountConnectionType.Unspecified.ToString()))
                                                                                    .Select(n => string.Concat(n, "|"))
                                                                                    .ToArray();

    private readonly IAutoQueryService _autoQueryService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserNotificationService _userNotificationService;

    public PublisherAccountConnectionService(IAutoQueryService autoQueryService, IPublisherAccountService publisherAccountService,
                                             IWorkspaceService workspaceService, IUserNotificationService userNotificationService)
    {
        _autoQueryService = autoQueryService;
        _publisherAccountService = publisherAccountService;
        _workspaceService = workspaceService;
        _userNotificationService = userNotificationService;
    }

    public async Task<OnlyResultsResponse<PublisherAccountLinkInfo>> Put(PutPublisherAccountLinks request)
    {
        // If a publisherAccountId is included, we are linking the given accounts to it, otherwise linking to the default token-based account for the workspace
        var dynWorkspace = await _workspaceService.GetWorkspaceAsync(request.WorkspaceId);

        var tokenPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(dynWorkspace.DefaultPublisherAccountId);

        Guard.AgainstRecordNotFound(tokenPublisherAccount == null || tokenPublisherAccount.IsDeleted() ||
                                    !tokenPublisherAccount.IsTokenAccount(),
                                    "PublisherAccount must be included and be a valid user token account or DefaultAccount be present on Workspace before linking any accounts");

        // Ensure the given token pub account is linked to the workspace appropriately
        await _workspaceService.LinkTokenAccountAsync(dynWorkspace, tokenPublisherAccount);

        var linkedPublisherAccountConnectResponses = new List<PublisherAccountLinkInfo>(request.LinkAccounts.Count);

        // Link each of the included publisher accounts to the included token account or default
        foreach (var linkPublisherAccount in request.LinkAccounts)
        {
            var publisherAccount = linkPublisherAccount;

            if (linkPublisherAccount.HasUpsertData() && linkPublisherAccount.Id <= 0)
            {
                publisherAccount.Type = publisherAccount.Type.IsWritablePublisherType() ||
                                        publisherAccount.Type.WritableAlternateAccountType().IsWritablePublisherType()
                                            ? publisherAccount.Type
                                            : tokenPublisherAccount.PublisherType;
            }
            else
            {
                publisherAccount = (await _publisherAccountService.GetPublisherAccountAsync(linkPublisherAccount.Id)).ToPublisherAccount();
            }

            var connectedDynPublisherAccount = await _publisherAccountService.ConnectPublisherAccountAsync(publisherAccount);

            if (connectedDynPublisherAccount != null)
            {
                await ServiceGatewayAsyncWrappers.SendAsync(_adminServiceGatewayFactory(), new LinkPublisherAccount
                                                                                           {
                                                                                               ToWorkspaceId = dynWorkspace.Id,
                                                                                               FromPublisherAccountId = tokenPublisherAccount.PublisherAccountId,
                                                                                               ToPublisherAccountId = connectedDynPublisherAccount.PublisherAccountId
                                                                                           }.PopulateWithRequestInfo(request));

                if (!(await _workspaceService.IsWorkspaceAdmin(dynWorkspace, request.UserId)))
                {
                    await _workspaceService.LinkUserToPublisherAccountAsync(dynWorkspace.Id, request.UserId, connectedDynPublisherAccount.PublisherAccountId);
                }

                linkedPublisherAccountConnectResponses.Add(new PublisherAccountLinkInfo
                                                           {
                                                               PublisherAccount = connectedDynPublisherAccount.ToPublisherAccount(),
                                                               UnreadNotifications = _userNotificationService.GetUnreadCount(connectedDynPublisherAccount.PublisherAccountId,
                                                                                                                             connectedDynPublisherAccount.GetContextWorkspaceId(dynWorkspace.Id))
                                                           });
            }
        }

        return new OnlyResultsResponse<PublisherAccountLinkInfo>
               {
                   Results = linkedPublisherAccountConnectResponses
               };
    }

    public async Task Delete(DeletePublisherAccountLink request)
    {
        var tokenPublisherAccount = await _workspaceService.TryGetDefaultPublisherAccountAsync(request.WorkspaceId);

        await ServiceGatewayAsyncWrappers.SendAsync(_adminServiceGatewayFactory(), new DelinkPublisherAccount
                                                                                   {
                                                                                       FromWorkspaceId = request.WorkspaceId,
                                                                                       FromPublisherAccountId = tokenPublisherAccount?.PublisherAccountId ?? 0,
                                                                                       ToPublisherAccountId = request.Id
                                                                                   }.PopulateWithRequestInfo(request));
    }

    public async Task<QueryResponse<PublisherAccountInfo>> Get(QueryPublisherAccountConnections request)
    {
        var queryResponse = new QueryResponse<PublisherAccountInfo>
                            {
                                Offset = request.Skip.Value
                            };

        // Always set the Id (FROM portion of an authorization)
        request.Id = request.FromPublisherAccountId ?? request.RequestPublisherAccountId;

        if (request.ConnectionTypes.IsNullOrEmpty())
        {
            request.ConnectionTypes = null;
        }

        // If we have all components to get by singleton lookup, do so
        if (request.ToPublisherAccountId.HasValue && request.ConnectionTypes != null && request.ConnectionTypes.Length == 1)
        {
            request.EdgeId = DynAuthorization.BuildEdgeId(request.ToPublisherAccountId.Value, request.ConnectionTypes.Single().ToString());

            var publisherAuthorization = await _dynamoDb.GetItemAsync<DynAuthorization>(request.Id, request.EdgeId);

            var publisherAccount = publisherAuthorization == null || publisherAuthorization.IsDeleted()
                                       ? null
                                       : await _publisherAccountService.TryGetPublisherAccountAsync(publisherAuthorization.ToRecordId);

            queryResponse.Results = publisherAccount == null || publisherAccount.IsDeleted()
                                        ? null
                                        : new List<PublisherAccountInfo>
                                          {
                                              publisherAccount.ToPublisherAccountInfo()
                                          };

            queryResponse.Total = queryResponse.Results?.Count ?? 0;

            return queryResponse;
        }

        // If we have a ToPublisherAccount value, will simply query for all possible combinations of EdgeIds for a given To...(remember, from is required, so this is a from/to combination for all edges)
        if (request.ToPublisherAccountId.HasValue)
        {
            var authTypesToGet = (request.ConnectionTypes?.Select(at => string.Concat(at, "|")) ?? _publisherAccountConnectionTypePrefixes);

            var publisherAuthorization = await _dynamoDb.QueryItemsAsync<DynAuthorization>(authTypesToGet.Select(p => new DynamoId(request.Id, string.Concat(p, request.ToPublisherAccountId.Value))))
                                                        .FirstOrDefaultAsync(da => (!request.LastConnectedAfter.HasValue || da.ReferenceId.ToLong(0) > request.LastConnectedAfter.ToUnixTimestamp()) &&
                                                                                   (!request.LastConnectedBefore.HasValue || da.ReferenceId.ToLong(0) < request.LastConnectedBefore.ToUnixTimestamp()));

            var publisherAccount = publisherAuthorization == null || publisherAuthorization.IsDeleted()
                                       ? null
                                       : await _publisherAccountService.TryGetPublisherAccountAsync(publisherAuthorization.ToRecordId);

            queryResponse.Results = publisherAccount == null || publisherAccount.IsDeleted()
                                        ? null
                                        : new List<PublisherAccountInfo>
                                          {
                                              publisherAccount.ToPublisherAccountInfo()
                                          };

            queryResponse.Total = queryResponse.Results?.Count ?? 0;

            return queryResponse;
        }

        // Otherwise, set filters and secondary range query attributes to the degree possible
        // If we have 0 or 1 connection types, use a prefix Edge filter - we can use this with 0 types as the user is basically asking for all connections,
        // and to be connected by anything at all, they must be either Contacted or ContactedBy first...all other types follow-on from that
        if (request.ConnectionTypes == null || request.ConnectionTypes.Length == 0)
        {
            request.EdgeIdStartsWith = PublisherAccountConnectionType.Contacted.ToString();
        }
        else if (request.ConnectionTypes != null && request.ConnectionTypes.Length == 1)
        {
            request.EdgeIdStartsWith = string.Concat(request.ConnectionTypes.Single().ToString(), "|");
        }

        request.ReferenceIdBetween = new[]
                                     {
                                         request.LastConnectedAfter.HasValue
                                             ? request.LastConnectedAfter.ToUnixTimestamp().ToString()
                                             : "1500000000",
                                         request.LastConnectedBefore.HasValue
                                             ? request.LastConnectedBefore.ToUnixTimestamp().ToString()
                                             : _dateTimeProvider.UtcNowTs.ToStringInvariant()
                                     };

        var publisherAccountResults = new HashSet<long>();

        do
        {
            var queryAuthResponse = await _autoQueryService.QueryDataAsync<QueryPublisherAccountConnections, DynAuthorization>(request, Request);

            if (queryResponse.Total <= 0 && queryAuthResponse.Total > 0)
            {
                queryResponse.Total = queryAuthResponse.Total;
                queryResponse.ResponseStatus = queryAuthResponse.ResponseStatus;
            }

            if (queryAuthResponse.Results.IsNullOrEmpty())
            {
                break;
            }

            // Go get the publishers
            queryAuthResponse.Results
                             .Where(da => request.ConnectionTypes == null ||
                                          request.ConnectionTypes.Any(ct => da.EdgeId.StartsWithOrdinalCi(string.Concat(ct.ToString(), "|"))))
                             .Select(q => q.GetToRecordIdFromEdgeId())
                             .Each(par => publisherAccountResults.Add(par));

            if (queryAuthResponse.Results.Count < request.Take.Value)
            {
                break;
            }

            request.Skip += request.Take;
        } while (publisherAccountResults.Count < request.Take.Value);

        var results = await _publisherAccountService.GetPublisherAccountsAsync(publisherAccountResults)
                                                    .Where(p => p != null && !p.IsDeleted())
                                                    .Take(500)
                                                    .Select(p => p.ToPublisherAccountInfo())
                                                    .ToList();

        if (queryResponse.Results.IsNullOrEmpty())
        {
            queryResponse.Total = 0;
            queryResponse.Results = results;
        }
        else
        {
            if (queryResponse.Total < queryResponse.Results.Count ||
                (request.Skip.Value <= 0 && queryResponse.Results.Count < request.Take.Value))
            {
                queryResponse.Total = queryResponse.Results.Count;
            }
            else if (queryResponse.Results.Count < request.Take.Value)
            {
                queryResponse.Total = request.Skip.Value + queryResponse.Results.Count;
            }

            queryResponse.Results = results.OrderByDescending(p => p.ModifiedOn).AsList();
        }

        return queryResponse;
    }
}
