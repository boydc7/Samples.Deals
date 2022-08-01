using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.FbSdk.Enums;
using Rydr.FbSdk.Extensions;
using Rydr.FbSdk.Models;

namespace Rydr.FbSdk
{
    public partial class FacebookClient
    {
        private static readonly string _igStoriesFields = string.Join(",", typeof(FbIgMedia).GetAllDataMemberNames()
                                                                                            .Where(dmn => !dmn.Equals("comments_count", StringComparison.OrdinalIgnoreCase) &&
                                                                                                          !dmn.Equals("like_count", StringComparison.OrdinalIgnoreCase)));

        public async Task<bool> InstallAppOnFacebookPageAsync(string pageId)
        {
            var pageAccessToken = await GetAsync<FbPageAccessToken>(pageId, new
                                                                            {
                                                                                fields = "access_token"
                                                                            });

            var response = await PostAsync<FbBoolResponse>($"{pageId}/subscribed_apps",
                                                           new
                                                           {
                                                               access_token = pageAccessToken.AccessToken,
                                                               subscribed_fields = "[\"mention\"]"
                                                           },
                                                           ByteExtensions.GenerateFacebookSecretProof(pageAccessToken.AccessToken, _appSecret));

            return response?.Success ?? false;
        }

        public async Task<FbIgBusinessAccount> GetFbIgBusinessAccountAsync(string fbAccountId, bool honorEtag = true)
        {
            var igBusinessAccount = await GetAsync<FbIgBusinessAccount>(fbAccountId,
                                                                        new
                                                                        {
                                                                            fields = GetFieldStringForType<FbIgBusinessAccount>()
                                                                        },
                                                                        honorEtag);

            return igBusinessAccount;
        }

        public async IAsyncEnumerable<List<FbIgMedia>> GetFbIgAccountMediaAsync(string fbIgBusinessAccountId, int pageLimit = 50)
        {
            var url = $"{fbIgBusinessAccountId}/media";

            var param = new
                        {
                            fields = GetFieldStringForType<FbIgMedia>(),
                            limit = pageLimit
                        };

            await foreach (var accountMedias in GetPagedAsync<FbIgMedia>(url, param, true).ConfigureAwait(false))
            {
                yield return accountMedias;
            }
        }

        public async IAsyncEnumerable<List<FbIgMedia>> GetFbIgAccountStoriesAsync(string fbIgBusinessAccountId, int pageLimit = 50)
        {
            var url = $"{fbIgBusinessAccountId}/stories";

            var param = new
                        {
                            fields = _igStoriesFields,
                            limit = pageLimit
                        };

            await foreach (var stories in GetPagedAsync<FbIgMedia>(url, param, true).ConfigureAwait(false))
            {
                yield return stories;
            }
        }

        public async IAsyncEnumerable<List<FbIgMediaComment>> GetFbIgMediaCommentsAsync(string fbMediaId, int pageLimit = 50)
        {
            var url = $"{fbMediaId}/comments";

            var param = new
                        {
                            fields = GetFieldStringForType<FbIgMediaComment>(),
                            limit = pageLimit
                        };

            await foreach (var comments in GetPagedAsync<FbIgMediaComment>(url, param, true).ConfigureAwait(false))
            {
                yield return comments;
            }
        }

        public async IAsyncEnumerable<List<FbIgMediaInsight>> GetFbIgMediaInsightsAsync(string fbMediaId, string mediaType, bool isStory, int pageLimit = 50)
        {
            var url = $"{fbMediaId}/insights";

            var param = new
                        {
                            metric = FbIgInsights.GetInsightFieldsStringForMediaType(mediaType, isStory),
                            limit = pageLimit
                        };

            await foreach (var insights in GetPagedAsync<FbIgMediaInsight>(url, param, isStory).ConfigureAwait(false))
            {
                yield return insights;
            }
        }

        public async IAsyncEnumerable<List<FbIgMediaInsight>> GetFbIgUserDailyInsightsAsync(string fbIgBusinessAccountId, int daysBack = 1, int pageLimit = 50)
        {
            if (daysBack > 30)
            {
                throw new ArgumentOutOfRangeException(nameof(daysBack), "Days back cannot be more than 30");
            }

            var today = DateTime.UtcNow.Date;
            var until = today.AddDays(1);
            var since = today.AddDays(-(daysBack - 1));

            var url = $"{fbIgBusinessAccountId}/insights";

            var param = new
                        {
                            metric = FbIgInsights.GetUserDailyInsightFieldsString(),
                            since,
                            until,
                            period = "day",
                            limit = pageLimit
                        };

            await foreach (var insights in GetPagedAsync<FbIgMediaInsight>(url, param, true).ConfigureAwait(false))
            {
                yield return insights;
            }
        }

        public async IAsyncEnumerable<List<FbComplexIgMediaInsight>> GetFbIgUserLifetimeInsightsAsync(string fbIgBusinessAccountId, int pageLimit = 50)
        {
            var url = $"{fbIgBusinessAccountId}/insights";

            var param = new
                        {
                            metric = FbIgInsights.GetUserLifetimeInsightFieldsString(),
                            period = "lifetime",
                            limit = pageLimit
                        };

            await foreach (var insights in GetPagedAsync<FbComplexIgMediaInsight>(url, param, true).ConfigureAwait(false))
            {
                yield return insights;
            }
        }

        public async Task<FbIgMedia> GetFbIgMediaAsync(string fbMediaId)
        {
            var media = await GetAsync<FbIgMedia>(fbMediaId,
                                                  new
                                                  {
                                                      fields = GetFieldStringForType<FbIgMedia>()
                                                  },
                                                  true);

            return media;
        }

        public async IAsyncEnumerable<IEnumerable<FbAccount>> GetFbIgBusinessAccountsAsync(string fbAccountId, int pageLimit = 50)
        {
            await foreach (var accounts in GetAccountsAsync(fbAccountId, pageLimit).ConfigureAwait(false))
            {
                yield return accounts.Where(a => !string.IsNullOrEmpty(a?.InstagramBusinessAccount?.Id));
            }
        }
    }
}
