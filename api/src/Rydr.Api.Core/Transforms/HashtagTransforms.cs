using System;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Transforms
{
    public static class HashtagTransforms
    {
        private static readonly Func<ILocalRequestCacheClient> _localRequestCacheClientFactory = () => RydrEnvironment.Container.Resolve<ILocalRequestCacheClient>();

        public static Task<DynHashtag> GetHashtagAsync(this IPocoDynamo dynamoDb, long id, bool includeDeleted = false)
            => dynamoDb.GetItemByRefAsync<DynHashtag>(id, id.ToStringInvariant(), DynItemType.Hashtag, includeDeleted);

        public static async Task<DynHashtag> GetHashtagByNameAsync(this IPocoDynamo dynamoDb, string hashtagName, PublisherType publisherType, bool includeDeleted = false)
        {
            if (hashtagName.StartsWithOrdinalCi("#") || hashtagName.StartsWithOrdinalCi("@"))
            {
                hashtagName = hashtagName.Substring(1);
            }

            var hashtagId = await _localRequestCacheClientFactory().TryGetTaskAsync(string.Concat("GetHashtagByName::", hashtagName, "|", publisherType, "|", includeDeleted),
                                                                                    async () =>
                                                                                    {
                                                                                        var dynQuery = dynamoDb.FromQueryIndex<DynItemEdgeIdGlobalIndex>(i => i.EdgeId == hashtagName &&
                                                                                                                                                              Dynamo.BeginsWith(i.TypeReference,
                                                                                                                                                                                string.Concat((int)DynItemType.Hashtag, "|")));

                                                                                        if (!includeDeleted)
                                                                                        {
                                                                                            dynQuery.Filter(i => i.DeletedOnUtc == null);
                                                                                        }

                                                                                        DynamoId dynamoId = null;

                                                                                        await foreach (var hashtagIndex in dynQuery.ExecAsync())
                                                                                        {
                                                                                            var hashtag = await dynamoDb.GetItemAsync<DynHashtag>(hashtagIndex.GetDynamoId());

                                                                                            if (hashtag == null || hashtag.PublisherType != publisherType)
                                                                                            {
                                                                                                continue;
                                                                                            }

                                                                                            dynamoId = hashtag.ToDynamoId();

                                                                                            break;
                                                                                        }

                                                                                        return dynamoId;
                                                                                    },
                                                                                    CacheConfig.LongConfig);

            // And now get the item - if it were already fetched above, it's a cheap cache lookup (in-memory), and this avoids and cache-clear issues
            return hashtagId == null
                       ? null
                       : await dynamoDb.GetItemAsync<DynHashtag>(hashtagId);
        }

        public static Hashtag ToHashtag(this DynHashtag source)
        {
            if (source == null)
            {
                throw new RecordNotFoundException();
            }

            var result = source.ConvertTo<Hashtag>();

            result.Stats = source.Stats?.AsList();

            result.Name = source.EdgeId;

            return result;
        }

        public static DynHashtag ToDynHashtag(this PostHashtag source)
            => ToDynHashtag(source.Model);

        public static DynHashtag ToDynHashtag(this PutHashtag source, DynHashtag existingBeingUpdated)
        {
            if (existingBeingUpdated == null)
            {
                throw new RecordNotFoundException();
            }

            return ToDynHashtag(source.Model, existingBeingUpdated);
        }

        public static DynHashtag ToDynHashtag(this Hashtag source, DynHashtag existingBeingUpdated = null, ISequenceSource sequenceSource = null)
        {
            var to = source.ConvertTo<DynHashtag>();

            if (existingBeingUpdated == null)
            { // New one
                to.EdgeId = source.Name;
                to.DynItemType = DynItemType.Hashtag;
                to.UpdateDateTimeTrackedValues(source);

                to.HashtagType = source.HashtagType == HashtagType.Unspecified
                                     ? HashtagType.Hashtag
                                     : source.HashtagType;
            }
            else
            {
                to.TypeId = existingBeingUpdated.TypeId;
                to.Id = existingBeingUpdated.Id;
                to.EdgeId = existingBeingUpdated.EdgeId;
                to.UpdateDateTimeDeleteTrackedValues(existingBeingUpdated);

                to.HashtagType = source.HashtagType == HashtagType.Unspecified
                                     ? existingBeingUpdated.HashtagType == HashtagType.Unspecified
                                           ? HashtagType.Hashtag
                                           : existingBeingUpdated.HashtagType
                                     : source.HashtagType;
            }

            if (to.Id <= 0)
            {
                to.Id = existingBeingUpdated != null && existingBeingUpdated.Id > 0
                            ? existingBeingUpdated.Id
                            : (sequenceSource ?? Sequences.Provider).Next();
            }

            if (to.Name.StartsWithOrdinalCi("#") || to.Name.StartsWithOrdinalCi("@"))
            {
                to.Name = to.Name.Substring(1);
            }

            to.Stats = source.Stats?.AsHashSet() ?? existingBeingUpdated?.Stats;

            to.ReferenceId = to.Id.ToStringInvariant();

            // Hashtags are generally publicly available for use by anyone
            to.WorkspaceId = UserAuthInfo.PublicWorkspaceId;

            return to;
        }
    }
}
