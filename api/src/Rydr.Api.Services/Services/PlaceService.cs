using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using ServiceStack;

namespace Rydr.Api.Services.Services;

[RydrCacheResponse(1800, "deals", "query", "facebook")]
public class PlaceService : BaseAuthenticatedApiService
{
    private readonly IAssociationService _associationService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IRequestStateManager _requestStateManager;
    private readonly IDeferRequestsService _deferRequestsService;

    public PlaceService(IAssociationService associationService, IPublisherAccountService publisherAccountService,
                        IRequestStateManager requestStateManager, IDeferRequestsService deferRequestsService)
    {
        _associationService = associationService;
        _publisherAccountService = publisherAccountService;
        _requestStateManager = requestStateManager;
        _deferRequestsService = deferRequestsService;
    }

    public async Task<OnlyResultResponse<Place>> Get(GetPlace request)
    {
        var dynPlace = await _dynamoDb.TryGetPlaceAsync(request.Id);

        return dynPlace.ToPlace().AsOnlyResultResponse();
    }

    public async Task<OnlyResultResponse<Place>> Get(GetPlaceByPublisher request)
    {
        var dynPlace = await _dynamoDb.TryGetPlaceAsync(request.PublisherType, request.PublisherId);

        return dynPlace.ToPlace().AsOnlyResultResponse();
    }

    public async Task<OnlyResultsResponse<Place>> Get(GetPublisherAccountPlaces request)
    {
        var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);

        var places = await _dynamoDb.GetItemsFromAsync<DynPlace, string>(_associationService.GetAssociatedIdsAsync(request.PublisherAccountId,
                                                                                                                   RecordType.Place,
                                                                                                                   RecordType.PublisherAccount),
                                                                         s => s.ToLong(0).ToItemDynamoId())
                                    .Where(p => p != null && !p.IsDeleted())
                                    .Select(p =>
                                            {
                                                var place = p.ToPlace();

                                                place.IsPrimary = publisherAccount.PrimaryPlaceId > 0 && publisherAccount.PrimaryPlaceId == place.Id;

                                                return place;
                                            })
                                    .ToList(25);

        return places.AsOnlyResultsResponse();
    }

    public Task Delete(DeleteLinkedPublisherAccountPlace request)
        => _associationService.TryDeleteAssociationAsync(RecordType.PublisherAccount, request.PublisherAccountId, RecordType.Place, request.PlaceId);

    public async Task<OnlyResultResponse<LongIdResponse>> Post(LinkPublisherAccountPlace request)
    {
        DynPlace dynPlace = null;

        var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);

        if (request.Place.HasUpsertData())
        {
            await PutOrPostModelAsync<PutPlace, PostPlace, Place>(request.Place, request);
        }

        var placeId = request.Place.Id;

        if (placeId <= 0)
        {
            dynPlace = await _dynamoDb.TryGetPlaceAsync(request.Place.PublisherType, request.Place.PublisherId);

            placeId = dynPlace?.Id ?? 0;
        }

        if (dynPlace == null && placeId > 0)
        {
            dynPlace = await _dynamoDb.TryGetPlaceAsync(placeId);
        }

        Guard.AgainstRecordNotFound(dynPlace == null, "Place to link could not be located");

        if (request.IsPrimary.HasValue &&
            ((request.IsPrimary.Value && dynPlace.Id != publisherAccount.PrimaryPlaceId) ||
             (!request.IsPrimary.Value && dynPlace.Id == publisherAccount.PrimaryPlaceId)))
        { // Changing the primary marker
            await _publisherAccountService.UpdatePublisherAccountAsync(publisherAccount, pa => pa.PrimaryPlaceId = request.IsPrimary.Value
                                                                                                                       ? dynPlace.Id
                                                                                                                       : 0);
        }

        // Ensure the place is associated to the account - need to update to a system level request for the association to proceed...
        _requestStateManager.UpdateStateToSystemRequest();
        await _associationService.AssociateAsync(RecordType.PublisherAccount, publisherAccount.Id, RecordType.Place, dynPlace.Id);

        return placeId.ToLongIdResponse().AsOnlyResultResponse();
    }

    public async Task<LongIdResponse> Post(PostPlace request)
    { // Places posted without an ID but with a pubId and type are treated as a PUT for the matching id..
        if (request.Model.Id <= 0 && request.Model.PublisherType != PublisherType.Unknown && request.Model.PublisherId.HasValue())
        { // No id but did specify type and id for a publisher specific place, go get that
            var existingDynPlace = await _dynamoDb.TryGetPlaceAsync(request.Model.PublisherType, request.Model.PublisherId);

            if (existingDynPlace != null)
            { // Existing one, update it from the POSTed info
                request.Model.Id = existingDynPlace.PlaceId;

                var result = await _dynamoDb.UpdateFromExistingAsync(existingDynPlace, x => request.Model.ToDynPlace(x), request);

                return result.ToLongIdResponse();
            }
        }

        // Do not have an Id or publisher lookup values, or one doesn't exist that matches...just create a new non-publisher connected place...
        var dynPlace = request.Model.ToDynPlace();

        await _dynamoDb.PutItemAsync(dynPlace);

        _deferRequestsService.DeferLowPriRequest(new PlaceUpdated
                                                 {
                                                     PlaceId = dynPlace.PlaceId
                                                 });

        return dynPlace.PlaceId.ToLongIdResponse();
    }

    public async Task<LongIdResponse> Put(PutPlace request)
    { // NOTE: PostPlace can update an existing place as well, if this is updated here, be sure it is as well...
        var existingDynPlace = await _dynamoDb.GetPlaceAsync(request.Id);

        var result = await _dynamoDb.UpdateFromExistingAsync(existingDynPlace, x => request.Model.ToDynPlace(x), request);

        _deferRequestsService.DeferLowPriRequest(new PlaceUpdated
                                                 {
                                                     PlaceId = existingDynPlace.PlaceId
                                                 });

        return result.ToLongIdResponse();
    }

    public async Task Delete(DeletePlace request)
    {
        await _dynamoDb.SoftDeleteByRefIdAsync<DynPlace>(request.Id, DynItemType.Place, request);

        _deferRequestsService.DeferLowPriRequest(new PlaceUpdated
                                                 {
                                                     PlaceId = request.Id
                                                 });
    }
}
