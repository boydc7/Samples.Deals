using System.Text;
using System.Web;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Users;
using Rydr.FbSdk.Extensions;
using ServiceStack;

namespace Rydr.Api.Services.Services;

public class TrackPublicService : BaseApiService
{
    private static readonly Dictionary<string, Action<RydrDeepLinkTrack, string>> _queryStringKeyProcessingMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            {
                "campaign", (r, v) => r.Campaign = v
            },
            {
                "medium", (r, v) => r.Medium = v
            },
            {
                "source", (r, v) => r.Source = v
            },
            {
                "content", (r, v) => r.Content = v
            },
            {
                "term", (r, v) => r.Term = v
            },
            {
                "userId", (r, v) => r.UserId = v.ToLong(0).Gz(0)
            },
            {
                "workspaceId", (r, v) => r.WorkspaceId = v.ToLong(0).Gz(0)
            },
            {
                "profileId", (r, v) => r.PublisherAccountId = v.ToLong(0).Gz(0)
            },
        };

    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IRequestStateManager _requestStateManager;
    private readonly IRydrDataService _rydrDataService;
    private readonly IAssociationService _associationService;

    public TrackPublicService(IFileStorageProvider fileStorageProvider, IRequestStateManager requestStateManager,
                              IRydrDataService rydrDataService, IAssociationService associationService)
    {
        _fileStorageProvider = fileStorageProvider;
        _requestStateManager = requestStateManager;
        _rydrDataService = rydrDataService;
        _associationService = associationService;
    }

    [RequiredRole("Admin")]
    public async Task Post(PostProcessTrackLinkSources request)
    {
        var folder = RydrEnvironment.IsReleaseEnvironment
                         ? "rydr-workflows/prod/tracks"
                         : "rydr-workflows/dev/tracks";

        await foreach (var trackLink in _fileStorageProvider.ListFolderAsync(folder))
        {
            var fmd = new FileMetaData(trackLink);

            var contents = await _fileStorageProvider.GetAsync(fmd);

            if (contents.IsNullOrEmpty())
            {
                if (request.DeleteOnSuccess)
                {
                    await _fileStorageProvider.DeleteAsync(fmd);
                }

                continue;
            }

            var url = Encoding.UTF8.GetString(contents);
            var timestamp = fmd.FileName.LeftPart('-').ToLong(0);

            var stored = await ParseAndStoreShareLink(url, r => Guid.NewGuid().ToStringId(), timestamp > DateTimeHelper.MinApplicationDateTs
                                                                                                 ? timestamp * 1000
                                                                                                 : 0);

            if (stored && request.DeleteOnSuccess)
            {
                await _fileStorageProvider.DeleteAsync(fmd);
            }
        }
    }

    public Task Get(PostTrackLinkSource request)
        => Post(request);

    public async Task Post(PostTrackLinkSource request)
    {
        if (request.LinkUrl.IsNullOrEmpty())
        {
            return;
        }

        var stored = await ParseAndStoreShareLink(request.LinkUrl);

        if (stored)
        {
            return;
        }

        // Not processable, or an unhandled link...drop it out in file storage...
        var trackSourceFile = new FileMetaData(RydrEnvironment.IsReleaseEnvironment
                                                   ? "rydr-workflows/prod/tracks"
                                                   : "rydr-workflows/dev/tracks",
                                               string.Concat(_dateTimeProvider.UtcNowTs, "-", Guid.NewGuid().ToStringId(), ".txt"))
                              {
                                  Bytes = Encoding.UTF8.GetBytes(request.LinkUrl)
                              };

        await _fileStorageProvider.StoreAsync(trackSourceFile, new FileStorageOptions
                                                               {
                                                                   ContentType = "text/plain",
                                                                   Encrypt = true,
                                                                   StorageClass = FileStorageClass.Intelligent
                                                               });
    }

    private async ValueTask<bool> ParseAndStoreShareLink(string url, Func<RydrDeepLinkTrack, string> uniqueifierFactory = null, long timestamp = 0)
    {
        if (!url.StartsWithOrdinalCi("https://in.getrydr.com"))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var dealSegmentIndex = int.MinValue;
        var dealId = 0L;
        var deepLinkPath = uri.AbsolutePath;

        for (var segmentIndex = 0; segmentIndex < uri.Segments.Length; segmentIndex++)
        {
            if (!uri.Segments[segmentIndex].Trim('/').EqualsOrdinalCi("deal"))
            {
                continue;
            }

            dealSegmentIndex = segmentIndex;

            break;
        }

        if (dealSegmentIndex >= 0)
        {
            var dealExternalId = uri.Segments.Length > (dealSegmentIndex + 1)
                                     ? uri.Segments[dealSegmentIndex + 1].Trim('/')
                                     : null;

            if (dealExternalId.HasValue())
            {
                deepLinkPath = string.Join('/', uri.Segments.TakeWhile((s, i) => i <= dealSegmentIndex));

                var dealLinkAssociation = await _associationService.GetAssociationsToAsync(dealExternalId, RecordType.Deal, RecordType.DealLink)
                                                                   .FirstOrDefaultAsync();

                dealId = dealLinkAssociation?.FromRecordId ?? 0;
            }
        }

        var rydrLinkTrack = new RydrDeepLinkTrack
                            {
                                Timestamp = timestamp > DateTimeHelper.MinApplicationDateTs
                                                ? timestamp
                                                : _dateTimeProvider.UtcNow.ToUnixTimestampMs(),
                                DealId = dealId.Gz(0),
                                Path = deepLinkPath.Trim('/').ReplaceAll("/", "-").Replace("--", "-").Left(100)
                            };

        var queryString = HttpUtility.ParseQueryString(uri.Query);

        for (var index = 0; index < queryString.Count; index++)
        {
            var key = queryString.GetKey(index);

            if (key.IsNullOrEmpty() || !_queryStringKeyProcessingMap.ContainsKey(key))
            {
                continue;
            }

            var queryKeyValue = queryString.GetValues(index)
                                           .FirstOrDefault(v => v.HasValue())?
                                           .UrlDecode()?
                                           .Trim();

            if (queryKeyValue.IsNullOrEmpty())
            {
                continue;
            }

            _queryStringKeyProcessingMap[key](rydrLinkTrack, queryKeyValue);
        }

        var state = _requestStateManager.GetState();

        rydrLinkTrack.Uniqueifier = uniqueifierFactory?.Invoke(rydrLinkTrack) ?? state.RequestId;

        await _rydrDataService.ExecAdHocAsync(@"
REPLACE   INTO DeepLinkTracks
          (Timestamp, DealId, Uniqueifier, Path, Campaign, Medium, Source, Content, Term, WorkspaceId, UserId, PublisherAccountId)
VALUES    (@Timestamp, @DealId, @Uniqueifier, @Path, @Campaign, @Medium, @Source, @Content, @Term, @WorkspaceId, @UserId, @PublisherAccountId);
",
                                              new
                                              {
                                                  rydrLinkTrack.Timestamp,
                                                  rydrLinkTrack.DealId,
                                                  rydrLinkTrack.Uniqueifier,
                                                  Path = rydrLinkTrack.Path.ToNullIfEmpty()?.ToLowerInvariant(),
                                                  Campaign = rydrLinkTrack.Campaign.ToNullIfEmpty()?.ToLowerInvariant(),
                                                  Medium = rydrLinkTrack.Medium.ToNullIfEmpty()?.ToLowerInvariant(),
                                                  Source = rydrLinkTrack.Source.ToNullIfEmpty()?.ToLowerInvariant(),
                                                  Content = rydrLinkTrack.Content.ToNullIfEmpty()?.ToLowerInvariant(),
                                                  Term = rydrLinkTrack.Term.ToNullIfEmpty()?.ToLowerInvariant(),
                                                  WorkspaceId = rydrLinkTrack.WorkspaceId.Gz(0),
                                                  UserId = rydrLinkTrack.UserId.Gz(0),
                                                  PublisherAccountId = rydrLinkTrack.PublisherAccountId.Gz(0)
                                              });

        return true;
    }
}
