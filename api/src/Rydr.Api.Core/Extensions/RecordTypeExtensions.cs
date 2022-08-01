using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Extensions
{
    public static class RecordTypeExtensions
    {
        public const string MinLongEdgeId = "00000000000000000001";
        public const string MaxLongEdgeId = "99999999999999999999";

        public static Task ValidateAsync(this IRecordTypeRecordService recordTypeService, RecordTypeId recordTypeId, IHasUserAuthorizationInfo state = null)
            => recordTypeService.ValidateAsync(recordTypeId.Type, recordTypeId.Id, state);

        public static async Task<bool> HasAccessToAsync(this IRecordTypeRecordService recordTypeService, RecordTypeId recordTypeId, IHasUserAuthorizationInfo state = null)
        {
            try
            {
                await recordTypeService.ValidateAsync(recordTypeId.Type, recordTypeId.Id, state);

                return true;
            }
            catch(RydrAuthorizationException)
            {
                return false;
            }
        }

        public static Task<T> GetRecordAsync<T>(this IRecordTypeRecordService recordTypeService, RecordTypeId recordTypeId, IHasUserAuthorizationInfo state = null, bool isInternal = false)
            where T : class, ICanBeRecordLookup
            => recordTypeId == null
                   ? null
                   : recordTypeService.GetRecordAsync<T>(recordTypeId.Type, recordTypeId.Id, state, isInternal);

        public static Task<T> TryGetRecordAsync<T>(this IRecordTypeRecordService recordTypeService, RecordTypeId recordTypeId, IHasUserAuthorizationInfo state = null, bool isInternal = false)
            where T : class, ICanBeRecordLookup
            => recordTypeId == null
                   ? null
                   : Try.GetAsync(() => recordTypeService.GetRecordAsync<T>(recordTypeId.Type, recordTypeId.Id, state, isInternal));

        public static string ToEdgeId(this long source)
            => source > 0
                   ? source.ToStringInvariant().PadLeft(20, '0')
                   : null;

        public static string ToEdgeId(this long? source)
            => source.GetValueOrDefault() > 0
                   ? source.Value.ToStringInvariant().PadLeft(20, '0')
                   : null;
    }
}
