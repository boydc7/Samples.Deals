using System;
using System.Collections.Generic;
using System.Linq;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Dto.Helpers;
using Rydr.FbSdk.Extensions;
using ServiceStack;

namespace Rydr.Api.Core.Extensions
{
    public static class PublisherMediaExtensions
    {
        public static IEnumerable<RydrPublisherApprovedMedia> ToRydrPublisherApprovedMedia(this DynPublisherApprovedMedia source)
        {
            var to = source.ConvertTo<RydrPublisherApprovedMedia>();

            to.PublisherAccountId = source.PublisherAccountId;
            to.FileId = source.MediaFileId;
            to.Caption = source.Caption.Left(150);

            yield return to;
        }

        public static EsMedia ToEsMedia(this DynPublisherMedia dynPublisherMedia, DynPublisherMediaAnalysis dynPublisherMediaAnalysis)
        {
            var tags = (dynPublisherMediaAnalysis.ImageFacesEmotions?.Keys.Select(k => k)
                        ??
                        Enumerable.Empty<string>()).Union(dynPublisherMediaAnalysis.Moderations?
                                                              .Select(m => string.Concat(m.Value,
                                                                                         " ",
                                                                                         m.ParentValue))
                                                          ??
                                                          Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                                                   .Union(dynPublisherMediaAnalysis.PopularEntities?
                                                                                   .Select(e => e.ParentValue)
                                                                                   .Distinct()
                                                          ??
                                                          Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                                                   .Union((dynPublisherMediaAnalysis.Sentiment ?? "").AsEnumerable())
                                                   .Union((dynPublisherMediaAnalysis.ImageFacesSmiles > 0
                                                               ? "smile"
                                                               : "").AsEnumerable())
                                                   .Where(s => s.HasValue())
                                                   .Select(s => s.Trim().ToLowerInvariant());

            var searchValue = (dynPublisherMediaAnalysis.ImageLabels?.Select(l => string.Concat(l.Value, " ", l.ParentValue))
                               ??
                               Enumerable.Empty<string>()).Union(dynPublisherMediaAnalysis.PopularEntities?.Select(e => e.Value)
                                                                 ??
                                                                 Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                                                          .Where(s => s.HasValue())
                                                          .Select(s =>
                                                                  {
                                                                      var val = s.Trim()
                                                                                 .ToLowerInvariant();

                                                                      if (val.StartsWithOrdinalCi("#") || val.StartsWithOrdinalCi("@"))
                                                                      {
                                                                          return string.Concat("_", val.Substring(1));
                                                                      }

                                                                      return val;
                                                                  });

            var esMedia = new EsMedia
                          {
                              Tags = string.Join(' ', tags),
                              SearchValue = string.Join(' ', searchValue),
                              PublisherAccountId = dynPublisherMediaAnalysis.PublisherAccountId,
                              PublisherMediaId = dynPublisherMediaAnalysis.PublisherMediaId,
                              MediaId = dynPublisherMedia.MediaId,
                              PublisherType = (int)dynPublisherMedia.PublisherType,
                              ContentType = (int)dynPublisherMedia.ContentType,
                              Sentiment = dynPublisherMediaAnalysis.Sentiment?.ToLowerInvariant(),
                              ImageFacesCount = dynPublisherMediaAnalysis.ImageFacesCount,
                              ImageFacesAgeAvg = dynPublisherMediaAnalysis.ImageFacesCount > 0
                                                     ? (int)Math.Round((dynPublisherMediaAnalysis.ImageFacesAgeSum / (double)dynPublisherMediaAnalysis.ImageFacesCount), 0)
                                                     : 0,
                              ImageFacesMales = (int)dynPublisherMediaAnalysis.ImageFacesMales,
                              ImageFacesFemales = (int)dynPublisherMediaAnalysis.ImageFacesFemales,
                              ImageFacesSmiles = (int)dynPublisherMediaAnalysis.ImageFacesSmiles,
                              ImageFacesBeards = (int)dynPublisherMediaAnalysis.ImageFacesBeards,
                              ImageFacesMustaches = (int)dynPublisherMediaAnalysis.ImageFacesMustaches,
                              ImageFacesEyeglasses = (int)dynPublisherMediaAnalysis.ImageFacesEyeglasses,
                              ImageFacesSunglasses = (int)dynPublisherMediaAnalysis.ImageFacesSunglasses
                          };

            return esMedia;
        }
    }
}
