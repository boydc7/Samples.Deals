// ReSharper disable CollectionNeverUpdated.Local
// ReSharper disable ClassNeverInstantiated.Local

using System.Runtime.Serialization;
using System.Text;
using Amazon;
using Amazon.AugmentedAIRuntime;
using Amazon.AugmentedAIRuntime.Model;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Files;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Dapper;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Rydr.Api.Core.Services;

public class AwsHumanLoopService : IHumanLoopService
{
    private static readonly ILog _log = LogManager.GetLogger("AwsHumanLoopService");
    private static readonly string _awsAccessKey = RydrEnvironment.GetAppSetting("AWSAccessKey");
    private static readonly string _awsSecretKey = RydrEnvironment.GetAppSetting("AWSSecretKey");
    private static readonly IFileStorageProvider _s3FileStorageProvider = RydrEnvironment.Container.ResolveNamed<IFileStorageProvider>(FileStorageProviderType.S3.ToString());
    private readonly RegionEndpoint _awsRekognitionRegion;

    private static readonly Dictionary<string, Func<string, Task<HumanLoopResponse>>> _prefixHumanLoopProcessingMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            {
                HumanLoopService.ImageModerationPrefix, ProcessRekognitionImageModerationResponseAsync
            },
            {
                HumanLoopService.PublisherAccountBusinessCategoryPrefix, ProcessMultiCategoryResponseAsync
            },
            {
                HumanLoopService.PublisherAccountCreatorCategoryPrefix, ProcessMultiCategoryResponseAsync
            }
        };

    private static readonly HumanLoopDataAttributes _defaultHumanLoopDataAttributes = new()
                                                                                      {
                                                                                          ContentClassifiers = new List<string>
                                                                                                               {
                                                                                                                   ContentClassifier.FreeOfPersonallyIdentifiableInformation
                                                                                                               }
                                                                                      };

    public AwsHumanLoopService()
    {
        _awsRekognitionRegion = RegionEndpoint.GetBySystemName(RydrEnvironment.GetAppSetting("AWS.Rekognition.Region", "us-west-2"));
    }

    public async IAsyncEnumerable<HumanLoopInfo> GetHumanLoopsAsync(string flowArn, DateTime? after = null)
    {
        using(var client = new AmazonAugmentedAIRuntimeClient(_awsAccessKey, _awsSecretKey, _awsRekognitionRegion))
        {
            string nextToken = null;

            do
            {
                var listResponse = await client.ListHumanLoopsAsync(new ListHumanLoopsRequest
                                                                    {
                                                                        FlowDefinitionArn = flowArn,
                                                                        CreationTimeAfter = after ?? DateTimeHelper.UtcNow.AddDays(-5).Date,
                                                                        MaxResults = 100,
                                                                        NextToken = nextToken,
                                                                        SortOrder = SortOrder.Descending
                                                                    });

                if ((listResponse?.HumanLoopSummaries).IsNullOrEmpty())
                {
                    yield break;
                }

                foreach (var humanLoopSummary in listResponse.HumanLoopSummaries)
                {
                    yield return new HumanLoopInfo
                                 {
                                     FailureReason = humanLoopSummary.FailureReason,
                                     Name = humanLoopSummary.HumanLoopName,
                                     Status = humanLoopSummary.HumanLoopStatus.Value
                                 };
                }

                nextToken = listResponse.NextToken.ToNullIfEmpty();
            } while (nextToken.HasValue());
        }
    }

    public async Task<HumanLoopResponse> GetHumanLoopResponseAsync(string humanLoopName)
    {
        using(var client = new AmazonAugmentedAIRuntimeClient(_awsAccessKey, _awsSecretKey, _awsRekognitionRegion))
        {
            var humanLoopResponse = await client.DescribeHumanLoopAsync(new DescribeHumanLoopRequest
                                                                        {
                                                                            HumanLoopName = humanLoopName
                                                                        });

            if (!humanLoopResponse.HumanLoopStatus.IsFinal())
            {
                return HumanLoopResponse.IncompleteResponse;
            }

            if (!humanLoopResponse.HumanLoopStatus.IsSuccessful())
            {
                _log.ErrorFormat("Processing HumanLoop [{0}] failed - code [{1}], reason [{2}]. Response info at [{3}]", humanLoopName, humanLoopResponse.FailureCode, humanLoopResponse.FailureReason, humanLoopResponse.HumanLoopOutput.OutputS3Uri);

                return HumanLoopResponse.FailedResponse;
            }

            var (prefix, identifier) = HumanLoopService.TryParseHumanLoopName(humanLoopName);

            if (prefix.IsNullOrEmpty())
            {
                _log.ErrorFormat("Processing HumanLoop [{0}] failed due to missing prefix. Response info at [{1}]", humanLoopName, humanLoopResponse.HumanLoopOutput.OutputS3Uri);

                return HumanLoopResponse.FailedResponse;
            }

            if (!_prefixHumanLoopProcessingMap.ContainsKey(prefix))
            {
                _log.ErrorFormat("Processing HumanLoop [{0}] failed due to missing prefix processing map. Response info at [{1}]", humanLoopName, humanLoopResponse.HumanLoopOutput.OutputS3Uri);

                return HumanLoopResponse.FailedResponse;
            }

            var s3Url = humanLoopResponse.HumanLoopOutput.OutputS3Uri
                                         .TrimPrefixes("s3:/")
                                         .TrimPrefixes("/");

            var response = await _prefixHumanLoopProcessingMap[prefix](s3Url);

            if (response == null)
            {
                return HumanLoopResponse.FailedResponse;
            }

            response.ResponesS3Uri = s3Url;
            response.Prefix = prefix;
            response.Identifier = identifier;
            response.IsComplete = true;

            return response;
        }
    }

    public async Task StartHumanLoopAsync<T>(string flowArn, string loopPrefix, string loopIdentifier, T inputObject)
        where T : class
    {
        using(var client = new AmazonAugmentedAIRuntimeClient(_awsAccessKey, _awsSecretKey, _awsRekognitionRegion))
        {
            await client.StartHumanLoopAsync(new StartHumanLoopRequest
                                             {
                                                 DataAttributes = _defaultHumanLoopDataAttributes,
                                                 FlowDefinitionArn = flowArn,
                                                 HumanLoopInput = new HumanLoopInput
                                                                  {
                                                                      InputContent = inputObject.ToJson()
                                                                  },
                                                 HumanLoopName = string.Concat(loopPrefix, "-", loopIdentifier, "-", Guid.NewGuid().ToStringId())
                                             });
        }
    }

    public async Task DeleteHumanLoopAsync(string humanLoopName)
    {
        using(var client = new AmazonAugmentedAIRuntimeClient(_awsAccessKey, _awsSecretKey, _awsRekognitionRegion))
        {
            await client.DeleteHumanLoopAsync(new DeleteHumanLoopRequest
                                              {
                                                  HumanLoopName = humanLoopName
                                              });
        }
    }

    private static async Task<HumanLoopResponse> ProcessRekognitionImageModerationResponseAsync(string s3Uri)
    {
        var model = await DoGetS3HumanResponseModelAsync<AwsRekognitionHumanImageModerationResponse>(s3Uri);

        return model == null
                   ? null
                   : new HumanLoopResponse
                     {
                         HumanResponders = model.HumanAnswers?.Count ?? 0,
                         Inputs = model.InputContent?.AiServiceResponse?.ModerationLabels?
                                       .Select(ml => new ValueWithConfidence
                                                     {
                                                         Confidence = ml.Confidence,
                                                         Occurrences = 1,
                                                         ParentValue = ml.ParentName,
                                                         Value = ml.Name
                                                     })
                                       .AsList(),
                         Answers = model.HumanAnswers?
                                        .Where(ha => ha?.AnswerContent?.Moderations?.ModerationLabels != null)
                                        .SelectMany(ha => ha.AnswerContent.Moderations.ModerationLabels
                                                            .Select(ml => new ValueWithConfidence
                                                                          {
                                                                              Confidence = ml.Confidence,
                                                                              Occurrences = 1,
                                                                              ParentValue = ml.ParentName,
                                                                              Value = ml.Name
                                                                          }))
                                        .AsList()
                     };
    }

    private static async Task<HumanLoopResponse> ProcessMultiCategoryResponseAsync(string s3Uri)
    {
        var model = await DoGetS3HumanResponseModelAsync<AwsHumanMultiCategoryResponse>(s3Uri);

        if (model?.HumanAnswers == null)
        {
            return null;
        }

        var humanResponders = model.HumanAnswers.Count;

        // Couple of shortcuts to avoid mapping multiple reviews...just a return whatever that one person said...
        if (humanResponders <= 0)
        {
            return new HumanLoopResponse
                   {
                       Answers = new List<ValueWithConfidence>()
                   };
        }

        if (humanResponders == 1)
        {
            return new HumanLoopResponse
                   {
                       HumanResponders = 1,
                       Answers = model.HumanAnswers
                                      .Where(ha => ha?.AnswerContent?.Category?.Labels != null)
                                      .SelectMany(ha => ha.AnswerContent.Category.Labels)
                                      .Select(l => new ValueWithConfidence
                                                   {
                                                       Confidence = 100,
                                                       Occurrences = 1,
                                                       Value = l
                                                   })
                                      .AsList()
                   };
        }

        // More than 1 reviewer - build up a map of human answers given and how many times each category was selected by different human reviewers
        var answerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var answerLabel in model.HumanAnswers
                                         .Where(ha => ha?.AnswerContent?.Category?.Labels != null)
                                         .SelectMany(ha => ha.AnswerContent.Category.Labels))
        {
            var existingCount = answerMap.ContainsKey(answerLabel)
                                    ? answerMap[answerLabel]
                                    : 0;

            answerMap[answerLabel] = existingCount + 1;
        }

        // Return results where a quorum-ish of people agreed (quorum-ish being everyone if only 2 reviewers, or exactly half or more in other situations)
        var minCount = humanResponders <= 2
                           ? 2
                           : (humanResponders / 2) + (humanResponders % 2);

        return new HumanLoopResponse
               {
                   HumanResponders = humanResponders,
                   Answers = answerMap.Where(kvp => kvp.Value >= minCount)
                                      .Select(kvp => new ValueWithConfidence
                                                     {
                                                         Confidence = 100,
                                                         Occurrences = 1,
                                                         Value = kvp.Key
                                                     })
                                      .AsList()
               };
    }

    private static async Task<T> DoGetS3HumanResponseModelAsync<T>(string s3Uri)
        where T : class
    {
        var fmd = new FileMetaData(s3Uri);

        if (!(await _s3FileStorageProvider.ExistsAsync(fmd)))
        {
            return null;
        }

        var model = Encoding.UTF8.GetString(await _s3FileStorageProvider.GetAsync(fmd))
                            .FromJson<T>();

        return model;
    }

    private class AwsRekognitionHumanImageModerationResponse
    {
        public List<AwsRekognitionHumanImageModerationAnswers> HumanAnswers { get; set; }
        public AwsRekognitionHumanImageModerationInput InputContent { get; set; }
    }

    private class AwsRekognitionHumanImageModerationAnswers
    {
        public AwsRekognitionHumanImageModerationAnswerContent AnswerContent { get; set; }

        // ReSharper disable UnusedMember.Local
        public string SubmissionTime { get; set; }

        public string WorkerId { get; set; }

        // ReSharper restore UnusedMember.Local
    }

    private class AwsRekognitionHumanImageModerationInput
    {
        public AwsRekognitionHumanImageModerationAiResponse AiServiceResponse { get; set; }
    }

    private class AwsRekognitionHumanImageModerationAiResponse
    {
        public List<AwsRekognitionHumanImageModerationLabel> ModerationLabels { get; set; }
    }

    [DataContract]
    private class AwsRekognitionHumanImageModerationAnswerContent
    {
        [DataMember(Name = "AWS/Rekognition/DetectModerationLabels/Image/V3")]
        public AwsRekognitionHumanImageModerationAnswerContentModeration Moderations { get; set; }
    }

    private class AwsRekognitionHumanImageModerationAnswerContentModeration
    {
        public List<AwsRekognitionHumanImageModerationLabel> ModerationLabels { get; set; }
    }

    private class AwsRekognitionHumanImageModerationLabel
    {
        public string Name { get; set; }
        public string ParentName { get; set; }
        public double Confidence { get; set; }
    }

    private class AwsHumanMultiCategoryResponse
    {
        public List<AwsHumanMultiCategoryAnswers> HumanAnswers { get; set; }
    }

    private class AwsHumanMultiCategoryAnswers
    {
        public AwsHumanMultiCategoryAnswerContent AnswerContent { get; set; }
    }

    private class AwsHumanMultiCategoryAnswerContent
    {
        public AwsHumanMultiCategoryAnswerCategory Category { get; set; }
    }

    private class AwsHumanMultiCategoryAnswerCategory
    {
        public List<string> Labels { get; set; }
    }
}
