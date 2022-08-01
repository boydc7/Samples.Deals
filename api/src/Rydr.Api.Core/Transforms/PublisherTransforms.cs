using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnumsNET;
using Nest;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Transforms
{
    public static class PublisherTransforms
    {
        private static readonly IEncryptionService _encryptionService = RydrEnvironment.Container.Resolve<IEncryptionService>();
        private static readonly IRydrDataService _rydrDataService = RydrEnvironment.Container.Resolve<IRydrDataService>();
        private static readonly IPocoDynamo _dynamoDb = RydrEnvironment.Container.Resolve<IPocoDynamo>();

        public static async Task<EsBusiness> ToEsBusinessAsync(this DynPublisherAccount source)
        {
            if (source == null)
            {
                return null;
            }

            var place = source.PrimaryPlaceId > 0
                            ? await _dynamoDb.TryGetPlaceAsync(source.PrimaryPlaceId)
                            : null;

            var latitude = place?.Address?.Latitude;
            var longitude = place?.Address?.Longitude;

            GeoLocation location = null;

            if (GeoExtensions.IsValidLatLon(latitude, longitude))
            {
                location = GeoLocation.TryCreate(latitude.Value, longitude.Value);
            }

            return new EsBusiness
                   {
                       SearchValue = string.Concat(source.Id, " ", source.UserName.ToLowerInvariant(), " ", source.AccountId),
                       Tags = source.Tags?.Select(t => t.ToString()).AsList().NullIfEmpty(),
                       IsDeleted = source.IsDeleted() || !source.IsBusiness(),
                       PublisherAccountId = source.PublisherAccountId,
                       AccountId = source.AccountId,
                       PublisherType = (int)source.PublisherType,
                       PublisherLinkType = (int)(source.IsBasicLink
                                                     ? PublisherLinkType.Basic
                                                     : source.IsSoftLinked
                                                         ? PublisherLinkType.None
                                                         : PublisherLinkType.Full),
                       Location = location
                   };
        }

        public static void ConvertToRydrSoftLinkedAccount(this DynPublisherAccount dynPublisherAccount)
        {
            // Only non-writable publisherTypes get converted...
            if (dynPublisherAccount == null || dynPublisherAccount.PublisherType.IsWritablePublisherType())
            {
                return;
            }

            var writableAlternateAccountType = dynPublisherAccount.PublisherType.WritableAlternateAccountType();

            if (writableAlternateAccountType == PublisherType.Unknown)
            {
                return;
            }

            // Rewrite the accountIds and type here to the writable alternative - used for allowing sales team at RYDR to run rydrs for businesses
            // that do not want to link/permission accounts to our rydr fb/ig accounts until they're proven to work...we rewrite them to the proper
            // values if/when the account is actually linked at some point...
            // OR
            // Is an influencer who was invited to a deal/platform from a public IG search, in which case we want the same end result, i.e. rewrite the
            // user onto this record if/when they signup...
            dynPublisherAccount.AlternateAccountId = dynPublisherAccount.AccountId;
            dynPublisherAccount.AccountId = ToRydrSoftLinkedAccountId(dynPublisherAccount.PublisherType, dynPublisherAccount.AlternateAccountId);
            dynPublisherAccount.PublisherType = writableAlternateAccountType;
            dynPublisherAccount.EdgeId = dynPublisherAccount.GetEdgeId();
        }

        public static string ToRydrSoftLinkedAccountId(PublisherType nonWritablePublisherType, string nonWritableAccountId)
            => $"rydr_{(int)nonWritablePublisherType}_{nonWritableAccountId}";

        public static PublisherAccountInfo ToPublisherAccountInfo(this DynPublisherAccount source)
            => source?.ToPublisherAccount().CreateCopy<PublisherAccountInfo>();

        public static PublisherAccountProfile ToPublisherAccountProfile(this DynPublisherAccount source)
            => source?.ToPublisherAccount().CreateCopy<PublisherAccountProfile>();

        public static string DisplayName(this DynPublisherAccount source)
            => source == null
                   ? "NOT FOUND"
                   : string.Concat(source.UserName, " (", source.Id, ")");

        public static IEnumerable<RydrPublisherAccount> ToRydrPublisherAccounts(this DynPublisherAccount source)
        {
            var to = source.ConvertTo<RydrPublisherAccount>();

            to.LastEngagementMetricsUpdatedOn = source.LastEngagementMetricsUpdatedOn > 0
                                                    ? source.LastEngagementMetricsUpdatedOn.ToDateTime()
                                                    : (DateTime?)null;

            to.LastMediaSyncedOn = source.LastMediaSyncedOn > 0
                                       ? source.LastMediaSyncedOn.ToDateTime()
                                       : (DateTime?)null;

            yield return to;
        }

        public static IEnumerable<RydrDailyStat> ToRydrDailyStats(this DynDailyStat source)
            => ToRydrDailyStatBases<RydrDailyStat, DynDailyStat>(source);

        public static IEnumerable<RydrDailyStatSnapshot> ToRydrDailyStatSnapshots(this DynDailyStatSnapshot source)
            => ToRydrDailyStatBases<RydrDailyStatSnapshot, DynDailyStatSnapshot>(source);

        private static IEnumerable<TRydr> ToRydrDailyStatBases<TRydr, TDyn>(this TDyn source)
            where TDyn : DynDailyStatBase
            where TRydr : RydrDailyStatBase, new()
        {
            if (source?.Stats == null)
            {
                yield break;
            }

            var day = source.DayTimestamp.ToDateTime().Date;

            foreach (var stat in source.Stats)
            {
                var rydrStat = new TRydr
                               {
                                   StatEnumId = _rydrDataService.GetOrCreateRydrEnumId(stat.Key),
                                   RecordId = source.Id,
                                   RecordType = source.AssociatedType,
                                   DayUtc = day,
                                   PublisherAccountId = source.PublisherAccountId,
                                   Val = stat.Value.Value,
                                   MinVal = stat.Value.MinValue,
                                   MaxVal = stat.Value.MaxValue,
                                   Measurements = stat.Value.Measurements
                               };

                yield return rydrStat;
            }
        }

        public static PublisherApp ToPublisherApp(this DynPublisherApp source)
        {
            if (source == null)
            {
                throw new RecordNotFoundException();
            }

            var result = source.ConvertTo<PublisherApp>();

            result.AppId = source.AppId;
            result.Type = source.PublisherType;
            result.AppSecret = null; // Purposely not ever returned/set

            return result;
        }

        public static PublisherAccount ToPublisherAccount(this DynPublisherAccount source, bool scrubbed = false)
        {
            if (source == null)
            {
                throw new RecordNotFoundException();
            }

            var result = source.ConvertTo<PublisherAccount>();

            result.AccountId = source.AccountId;
            result.Type = source.PublisherType;
            result.AccessToken = null; // Purposely not ever returned/set
            result.Metrics = source.Metrics;
            result.LastSyncedOn = source.LastMediaSyncedOn.ToDateTime();

            result.LinkType = source.IsBasicLink
                                  ? PublisherLinkType.Basic
                                  : source.IsSoftLinked
                                      ? PublisherLinkType.None
                                      : PublisherLinkType.Full;

            result.AccessToken = null;
            result.AccessTokenExpiresIn = 0;

            result.MaxDelinquent = source.MaxDelinquentAllowed.HasValue && source.MaxDelinquentAllowed.Value >= 0
                                       ? source.MaxDelinquentAllowed.Value
                                       : 5;

            if (scrubbed)
            {
                result.Email = null;
            }

            return result;
        }

        public static Task<DynPublisherApp> ToDynPublisherAppAsync(this PostPublisherApp source)
            => ToDynPublisherAppAsync(source.Model);

        public static Task<DynPublisherApp> ToDynPublisherAppAsync(this PutPublisherApp source, DynPublisherApp existingBeingUpdated)
        {
            if (existingBeingUpdated == null)
            {
                throw new RecordNotFoundException();
            }

            return ToDynPublisherAppAsync(source.Model, existingBeingUpdated);
        }

        public static async Task<DynPublisherApp> ToDynPublisherAppAsync(this PublisherApp source, DynPublisherApp existingBeingUpdated = null, ISequenceSource sequenceSource = null)
        {
            var to = source.ConvertTo<DynPublisherApp>();

            if (existingBeingUpdated == null)
            { // New one
                to.DynItemType = DynItemType.PublisherApp;
                to.PublisherType = source.Type;
                to.AppId = source.AppId;
                to.EdgeId = to.GetEdgeId();
                to.UpdateDateTimeTrackedValues(source);
            }
            else
            {
                to.TypeId = existingBeingUpdated.TypeId;
                to.Id = existingBeingUpdated.Id;
                to.AppId = existingBeingUpdated.AppId;
                to.EdgeId = existingBeingUpdated.EdgeId;
                to.UpdateDateTimeDeleteTrackedValues(existingBeingUpdated);

                to.DedicatedWorkspaceId = existingBeingUpdated.DedicatedWorkspaceId;
                to.OwnerId = existingBeingUpdated.DedicatedWorkspaceId;
                to.WorkspaceId = existingBeingUpdated.DedicatedWorkspaceId;

                to.PublisherType = source.Type == PublisherType.Unknown
                                       ? existingBeingUpdated.PublisherType
                                       : source.Type;
            }

            if (to.Id <= 0)
            {
                to.Id = existingBeingUpdated != null && existingBeingUpdated.Id > 0
                            ? existingBeingUpdated.Id
                            : (sequenceSource ?? Sequences.Provider).Next();
            }

            to.ApiVersion = source.ApiVersion.Coalesce(existingBeingUpdated?.ApiVersion);
            to.ReferenceId = to.Id.ToStringInvariant();

            to.AppSecret = source.AppSecret.HasValue()
                               ? await _encryptionService.Encrypt64Async(source.AppSecret)
                               : existingBeingUpdated?.AppSecret;

            // Apps are generally publicly available for use by anyone
            if (to.OwnerId <= 0)
            {
                to.OwnerId = UserAuthInfo.PublicOwnerId;
            }

            return to;
        }

        public static void PopulateIgAccountWithFbInfo(this DynPublisherAccount source, FacebookAccount fbAccount)
        {
            Guard.AgainstArgumentOutOfRange(source == null || source.PublisherType != PublisherType.Instagram, "Only IG accounts can be populated with Fb account info");

            // Adjust the type of the stored account from an ig account to the fb account, while keeping the Id the same...
            // Store the new one (which will have a different EdgeId), then delete the old one...

            var newAccountId = (fbAccount.InstagramBusinessAccount?.Id).Coalesce(fbAccount.Id);

            source.AlternateAccountId = source.AccountId;
            source.AccountId = newAccountId;
            source.PublisherType = PublisherType.Facebook;
            source.EdgeId = DynPublisherAccount.BuildEdgeId(PublisherType.Facebook, newAccountId);
            source.AccountType = PublisherAccountType.FbIgUser;
            source.FullName = (fbAccount.InstagramBusinessAccount?.Name).Coalesce(fbAccount.Name).Coalesce(source.FullName);
            source.UserName = (fbAccount.InstagramBusinessAccount?.UserName).Coalesce(fbAccount.UserName).Coalesce(source.UserName);
            source.Description = (fbAccount.InstagramBusinessAccount?.Description).Coalesce(fbAccount.About).Coalesce(source.Description);
            source.ProfilePicture = (fbAccount.InstagramBusinessAccount?.ProfilePictureUrl).Coalesce(source.ProfilePicture);
            source.Website = (fbAccount.InstagramBusinessAccount?.Website).Coalesce(fbAccount.Website).Coalesce(source.Website);
        }

        public static DynPublisherAccount ToDynPublisherAccount(this PublisherAccount source, DynPublisherAccount existingBeingUpdated = null, ISequenceSource sequenceSource = null)
        {
            var to = source.ConvertTo<DynPublisherAccount>();

            if (existingBeingUpdated == null)
            { // New one
                to.DynItemType = DynItemType.PublisherAccount;
                to.PublisherType = source.Type;
                to.AccountId = source.AccountId;
                to.EdgeId = to.GetEdgeId();
                to.Metrics = source.Metrics;
                to.UpdateDateTimeTrackedValues(source);

                to.Tags = source.Tags?.Where(t => t.Value.HasValue()).AsHashSet().NullIfEmpty();

                if (source.AccessToken.IsNullOrEmpty())
                { // If an access token is included, it's a basicIg account or similar, i.e. not a soft-linked rydr account
                    ConvertToRydrSoftLinkedAccount(to);
                }
            }
            else
            {
                to.TypeId = existingBeingUpdated.TypeId;
                to.Id = existingBeingUpdated.Id;
                to.AccountId = existingBeingUpdated.AccountId;
                to.AlternateAccountId = existingBeingUpdated.AlternateAccountId;
                to.PageId = source.PageId.Coalesce(existingBeingUpdated.PageId);
                to.EdgeId = existingBeingUpdated.EdgeId;
                to.UpdateDateTimeDeleteTrackedValues(existingBeingUpdated);
                to.Metrics = existingBeingUpdated.Metrics;
                to.UserName = to.UserName.Coalesce(existingBeingUpdated.UserName);
                to.FullName = to.FullName.Coalesce(existingBeingUpdated.FullName);
                to.Description = to.Description.Coalesce(existingBeingUpdated.Description);
                to.Website = to.Website.Coalesce(existingBeingUpdated.Website);
                to.ProfilePicture = to.ProfilePicture.Coalesce(existingBeingUpdated.ProfilePicture);
                to.MaxDelinquentAllowed = existingBeingUpdated.MaxDelinquentAllowed;

                to.Tags = (source.Tags == null
                               ? existingBeingUpdated.Tags
                               : source.Tags?.Where(t => t.Value.HasValue()).AsHashSet()).NullIfEmpty();

                to.AccountType = source.AccountType == PublisherAccountType.Unknown
                                     ? existingBeingUpdated.AccountType
                                     : source.AccountType;

                to.PublisherType = source.Type == PublisherType.Unknown
                                       ? existingBeingUpdated.PublisherType
                                       : source.Type;
            }

            to.OptInToAi = source.OptInToAi ?? (existingBeingUpdated?.OptInToAi ?? false);

            if (existingBeingUpdated != null && existingBeingUpdated.RydrAccountType.HasAnyFlags(RydrAccountType.Admin | RydrAccountType.TokenAccount))
            {
                to.RydrAccountType = existingBeingUpdated.RydrAccountType;
            }
            else
            {
                to.RydrAccountType = source.RydrAccountType == RydrAccountType.None
                                         ? existingBeingUpdated?.RydrAccountType ?? RydrAccountType.None
                                         : source.RydrAccountType;
            }

            if (to.Id <= 0)
            {
                to.Id = existingBeingUpdated != null && existingBeingUpdated.Id > 0
                            ? existingBeingUpdated.Id
                            : (sequenceSource ?? Sequences.Provider).Next();
            }

            to.ReferenceId = to.Id.ToStringInvariant();
            to.PageId = source.PageId.Coalesce(existingBeingUpdated?.PageId).ToNullIfEmpty();

            return to;
        }
    }
}
