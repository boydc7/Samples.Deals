using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;

namespace Rydr.Api.Services.Services
{
    [RydrCacheResponse(1800)]
    public class HashtagService : BaseAuthenticatedApiService
    {
        public async Task<OnlyResultResponse<Hashtag>> Get(GetHashtag request)
        {
            var result = await _dynamoDb.GetHashtagAsync(request.Id, true);

            return result.ToHashtag().AsOnlyResultResponse();
        }

        public async Task<LongIdResponse> Post(PostHashtag request)
        {
            var hashtag = request.ToDynHashtag();

            await _dynamoDb.PutItemAsync(hashtag);

            return hashtag.ToLongIdResponse();
        }

        public async Task<LongIdResponse> Put(PutHashtag request)
            => (await _dynamoDb.UpdateFromRefRequestAsync<PutHashtag, DynHashtag>(request, request.Id,
                                                                                  DynItemType.Hashtag, (r, x) => r.ToDynHashtag(x))).ToLongIdResponse();

        public Task Delete(DeleteHashtag request)
            => _dynamoDb.SoftDeleteByRefIdAsync<DynHashtag>(request.Id, DynItemType.Hashtag, request);
    }

    public class HashtagInternalService : BaseInternalOnlyApiService
    {
        public async Task<LongIdResponse> Post(PostHashtagUpsert request)
        {
            var existingModel = request.Model.Id > 0
                                    ? await _dynamoDb.GetHashtagAsync(request.Model.Id)
                                    : await _dynamoDb.GetHashtagByNameAsync(request.Model.Name, request.Model.PublisherType);

            request.Model.Id = existingModel?.Id ?? request.Model.Id;

            if (request.Model.Id > 0)
            {
                request.Model.Name = null;
            }

            await PutOrPostModelAsync<PutHashtag, PostHashtag, Hashtag>(request.Model, request);

            return request.Model.Id.ToLongIdResponse();
        }
    }
}
