using System;
using System.Collections.Generic;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Services.Publishers
{
    public class EngagementRatingCalcMediaStatDecorator : IPublisherMediaStatDecorator
    {
        private readonly IPublisherAccountService _publisherAccountService;

        public EngagementRatingCalcMediaStatDecorator(IPublisherAccountService publisherAccountService)
        {
            _publisherAccountService = publisherAccountService;
        }

        public async IAsyncEnumerable<DynPublisherMediaStat> DecorateAsync(IAsyncEnumerable<DynPublisherMediaStat> stats)
        {
            var knownSkipPublisherAccountIds = new HashSet<long>();

            await foreach (var stat in stats)
            {
                stat.EngagementRating = 0;

                if (stat.Stats.IsNullOrEmpty() ||
                    knownSkipPublisherAccountIds.Contains(stat.PublisherAccountId))
                {
                    yield return stat;

                    continue;
                }

                var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(stat.PublisherAccountId);

                if (publisherAccount?.Metrics == null)
                {
                    knownSkipPublisherAccountIds.Add(stat.PublisherAccountId);

                    yield return stat;

                    continue;
                }

                var ratingStats = stat.GetRatingStats();

                var followers = publisherAccount.Metrics.ContainsKey(PublisherMetricName.FollowedBy)
                                    ? publisherAccount.Metrics[PublisherMetricName.FollowedBy].MinGz(0)
                                    : 0;

                // Engagement Rating for posts = (Actions + Comments) / Followers x 100
                //            Engagements is sent from fb, it's likes + comments and we add replies into it on decoration
                // For stories we add reach (engagements for stories is basically the replies count)
                stat.EngagementRating = followers <= 0
                                            ? 0
                                            : Math.Round(((ratingStats.Engagements + (stat.ContentType == PublisherContentType.Story
                                                                                          ? ratingStats.Impressions
                                                                                          : 0)) / followers) * 100.0,
                                                         6);

                // "True" engagement rating = engagements / reach * 100
                // Note that for stories, we don't actually show true engagement rating anywhere, it doesn't currently apply
                var reachDouble = (double)ratingStats.Reach;

                stat.TrueEngagementRating = reachDouble <= 0
                                                ? 0
                                                : Math.Round((ratingStats.Engagements / reachDouble) * 100.0,
                                                             6);

                yield return stat;
            }
        }
    }
}
