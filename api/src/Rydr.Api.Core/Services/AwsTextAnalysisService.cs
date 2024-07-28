using Amazon;
using Amazon.Comprehend;
using Amazon.Comprehend.Model;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Supporting;
using ServiceStack;

namespace Rydr.Api.Core.Services;

public class AwsTextAnalysisService : ITextAnalysisService
{
    private static readonly string _awsAccessKey = RydrEnvironment.GetAppSetting("AWSAccessKey");
    private static readonly string _awsSecretKey = RydrEnvironment.GetAppSetting("AWSSecretKey");

    private static readonly List<string> _securityGroupIds = RydrEnvironment.GetAppSetting("AWS.Comprehend.SecurityGroupIds", "sg-0e9accf59137d4148")
                                                                            .Split(new[]
                                                                                   {
                                                                                       ','
                                                                                   }, StringSplitOptions.RemoveEmptyEntries)
                                                                            .ToList();

    private static readonly List<string> _subnetIds = RydrEnvironment.GetAppSetting("AWS.Comprehend.SubnetIds", "subnet-0b8d365f9fddd38a0,subnet-08bd4398b59202908")
                                                                     .Split(new[]
                                                                            {
                                                                                ','
                                                                            }, StringSplitOptions.RemoveEmptyEntries)
                                                                     .ToList();

    private static readonly string _roleArn = RydrEnvironment.GetAppSetting("AWS.Comprehend.RoleArn", "arn:aws:iam::933347060724:role/AWSServiceRoleComprehend");
    private static readonly string _kmsKeyArn = RydrEnvironment.GetAppSetting("AWS.Comprehend.KmsKeyArn", "arn:aws:kms:us-west-2:933347060724:key/dd2ec688-5e6b-4f12-b43f-93e3644aaed7");

    private readonly RegionEndpoint _awsComprehendRegion;

    public AwsTextAnalysisService()
    {
        _awsComprehendRegion = RegionEndpoint.GetBySystemName(RydrEnvironment.GetAppSetting("AWS.Comprehend.Region", "us-west-2"));
    }

    public async Task<string> GetDominantLanguageCodeAsync(string text)
    {
        if (text.IsNullOrEmpty())
        {
            return "en";
        }

        using(var client = new AmazonComprehendClient(_awsAccessKey, _awsSecretKey, _awsComprehendRegion))
        {
            var awsResponse = await client.DetectDominantLanguageAsync(new DetectDominantLanguageRequest
                                                                       {
                                                                           Text = text
                                                                       });

            return (awsResponse?.Languages).IsNullOrEmpty()
                       ? "en"
                       : awsResponse.Languages
                                    .OrderByDescending(l => l.Score)
                                    .Take(1)
                                    .Select(l => l.LanguageCode)
                                    .FirstOrDefault() ?? "en";
        }
    }

    public async Task<List<Entity>> GetEntitiesAsync(string text, string languageCode = "en")
    {
        using(var client = new AmazonComprehendClient(_awsAccessKey, _awsSecretKey, _awsComprehendRegion))
        {
            var awsResponse = await client.DetectEntitiesAsync(new DetectEntitiesRequest
                                                               {
                                                                   LanguageCode = GetLanguageCode(languageCode),
                                                                   Text = text
                                                               });

            return awsResponse?.Entities ?? new List<Entity>();
        }
    }

    public async Task<SentimentResult> GetSentimentAsync(string text, string languageCode = "en")
    {
        using(var client = new AmazonComprehendClient(_awsAccessKey, _awsSecretKey, _awsComprehendRegion))
        {
            var awsResponse = await client.DetectSentimentAsync(new DetectSentimentRequest
                                                                {
                                                                    LanguageCode = GetLanguageCode(languageCode),
                                                                    Text = text
                                                                });

            return awsResponse == null
                       ? null
                       : new SentimentResult
                         {
                             Sentiment = awsResponse.Sentiment.Value,
                             MixedSentiment = awsResponse.SentimentScore?.Mixed ?? 0,
                             NegativeSentiment = awsResponse.SentimentScore?.Negative ?? 0,
                             NeutralSentiment = awsResponse.SentimentScore?.Neutral ?? 0,
                             PositiveSentiment = awsResponse.SentimentScore?.Positive ?? 0,
                         };
        }
    }

    public async Task<string> StartTopicModelingAsync(string s3Input, string s3Output)
    {
        s3Input = string.Concat("s3://", s3Input.TrimStart('/'));
        s3Output = string.Concat("s3://", s3Output.TrimStart('/').AppendIfNotEndsWith("/"));

        using(var client = new AmazonComprehendClient(_awsAccessKey, _awsSecretKey, _awsComprehendRegion))
        {
            var awsResponse = await client.StartTopicsDetectionJobAsync(new StartTopicsDetectionJobRequest
                                                                        {
                                                                            InputDataConfig = new InputDataConfig
                                                                                              {
                                                                                                  InputFormat = InputFormat.ONE_DOC_PER_LINE,
                                                                                                  S3Uri = s3Input
                                                                                              },
                                                                            OutputDataConfig = new OutputDataConfig
                                                                                               {
                                                                                                   KmsKeyId = _kmsKeyArn,
                                                                                                   S3Uri = s3Output
                                                                                               },
                                                                            NumberOfTopics = 25,
                                                                            VpcConfig = new VpcConfig
                                                                                        {
                                                                                            SecurityGroupIds = _securityGroupIds,
                                                                                            Subnets = _subnetIds
                                                                                        },
                                                                            DataAccessRoleArn = _roleArn,
                                                                            VolumeKmsKeyId = _kmsKeyArn
                                                                        });

            return awsResponse?.JobId;
        }
    }

    private LanguageCode GetLanguageCode(string languageCode)
    {
        LanguageCode languageCodeObject = null;

        if (languageCode.IsNullOrEmpty())
        {
            languageCodeObject = LanguageCode.En;
        }
        else
        {
            languageCodeObject = LanguageCode.FindValue(languageCode);

            if (!languageCodeObject.Value.HasValue())
            {
                languageCodeObject = LanguageCode.En;
            }
        }

        return languageCodeObject;
    }
}
