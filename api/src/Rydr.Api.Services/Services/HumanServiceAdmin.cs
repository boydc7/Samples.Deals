using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Users;
using ServiceStack;

namespace Rydr.Api.Services.Services
{
    public class HumanServiceAdmin : BaseAdminApiService
    {
        private static readonly IDeferRequestsService _deferRequestsService = RydrEnvironment.Container.Resolve<IDeferRequestsService>();
        private readonly IHumanLoopService _humanLoopService;

        private static readonly Dictionary<string, Action<HumanLoopResponse>> _prefixHumanLoopProcessingMap =
            new Dictionary<string, Action<HumanLoopResponse>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    HumanLoopService.ImageModerationPrefix, ProcessImageModerationHumanResponseAsync
                },
                {
                    HumanLoopService.PublisherAccountBusinessCategoryPrefix, ProcessBusinessCategoryHumanResponseAsync
                },
                {
                    HumanLoopService.PublisherAccountCreatorCategoryPrefix, ProcessCreatorCategoryHumanResponseAsync
                }
            };

        public HumanServiceAdmin(IHumanLoopService humanLoopService)
        {
            _humanLoopService = humanLoopService;
        }

        public async Task Post(PostProcessHumanLoop request)
        {
            var after = _dateTimeProvider.UtcNow.AddHours(-(request.HoursBack.Gz(24 * 7)));

            await foreach (var humanLoopInfo in _humanLoopService.GetHumanLoopsAsync(request.LoopIdentifier, after))
            {
                if (!humanLoopInfo.IsFinal())
                {
                    continue;
                }

                if (!humanLoopInfo.IsSuccessful())
                {
                    _log.ErrorFormat("Processing HumanLoop [{0}] failed - reason [{1}].", humanLoopInfo.Name, humanLoopInfo.FailureReason);

                    // We don't keep these around, logged it, just move along
                    await _humanLoopService.DeleteHumanLoopAsync(humanLoopInfo.Name);

                    continue;
                }

                var humanLoopResponse = await _humanLoopService.GetHumanLoopResponseAsync(humanLoopInfo.Name);

                if (!humanLoopResponse.IsComplete)
                {
                    // Shouldn't happen, as we already checked before...but...
                    continue;
                }

                if (humanLoopResponse.Failed || humanLoopResponse.Identifier.IsNullOrEmpty())
                { // Shouldn't happen, as we already checked before...but...don't need to log here, already did in the GETer
                    // We don't keep these around, logged it, just move along
                    await _humanLoopService.DeleteHumanLoopAsync(humanLoopInfo.Name);

                    continue;
                }

                if (!_prefixHumanLoopProcessingMap.ContainsKey(humanLoopResponse.Prefix))
                {
                    _log.ErrorFormat("Processing HumanLoop [{0}] cannot continue due to missing processing map for prefix [{1}]. Response info at [{2}].",
                                     humanLoopInfo.Name, humanLoopResponse.Prefix, humanLoopResponse.ResponesS3Uri);

                    // We don't keep these around, logged it, just move along
                    await _humanLoopService.DeleteHumanLoopAsync(humanLoopInfo.Name);

                    continue;
                }

                // Process away
                _prefixHumanLoopProcessingMap[humanLoopResponse.Prefix](humanLoopResponse);

                await _humanLoopService.DeleteHumanLoopAsync(humanLoopInfo.Name);
            }
        }

        private static void ProcessImageModerationHumanResponseAsync(HumanLoopResponse humanLoopResponse)
            => DoProcessHumanResponse(humanLoopResponse, i => new PostProcessHumanImageModerationResponse
                                                              {
                                                                  PublisherMediaId = i
                                                              });

        private static void ProcessBusinessCategoryHumanResponseAsync(HumanLoopResponse humanLoopResponse)
            => DoProcessHumanResponse(humanLoopResponse, i => new PostProcessHumanBusinessCategoryResponse
                                                              {
                                                                  PublisherAccountId = i
                                                              });

        private static void ProcessCreatorCategoryHumanResponseAsync(HumanLoopResponse humanLoopResponse)
            => DoProcessHumanResponse(humanLoopResponse, i => new PostProcessHumanCreatorCategoryResponse
                                                              {
                                                                  PublisherAccountId = i
                                                              });

        private static void DoProcessHumanResponse<T>(HumanLoopResponse humanLoopResponse, Func<long, T> factory)
            where T : ProcessHumanResponseBase
        {
            var id = humanLoopResponse.Identifier.ToLong();

            if (id <= 0)
            {
                return;
            }

            var deferItem = factory(id);

            deferItem.HumanS3Uri = humanLoopResponse.ResponesS3Uri;
            deferItem.Inputs = humanLoopResponse.Inputs;
            deferItem.Answers = humanLoopResponse.Answers;
            deferItem.HumanResponders = humanLoopResponse.HumanResponders;

            _deferRequestsService.DeferLowPriRequest(deferItem.WithAdminRequestInfo());
        }
    }
}
