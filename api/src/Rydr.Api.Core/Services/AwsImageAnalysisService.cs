using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Supporting;
using ServiceStack;

namespace Rydr.Api.Core.Services
{
    public class AwsImageAnalysisService : IImageAnalysisService
    {
        private static readonly string _awsAccessKey = RydrEnvironment.GetAppSetting("AWSAccessKey");
        private static readonly string _awsSecretKey = RydrEnvironment.GetAppSetting("AWSSecretKey");
        private static readonly int _maxLabels = RydrEnvironment.GetAppSetting("AWS.Rekognition.MaxLabels", 15);
        private static readonly int _minImageConfidence = RydrEnvironment.GetAppSetting("AWS.Rekognition.ImagesMinConfidence", 96);
        private static readonly int _minModerationConfidence = RydrEnvironment.GetAppSetting("AWS.Rekognition.ModerationMinConfidence", 96);

        private static readonly string _imageModerationHumanFlowArn = RydrEnvironment.IsReleaseEnvironment
                                                                          ? RydrEnvironment.GetAppSetting("AWS.Rekognition.ImageModerationHumanFlowArn")
                                                                          : null;

        private static readonly HumanLoopDataAttributes _defaultHumanLoopDataAttributes = new HumanLoopDataAttributes
                                                                                          {
                                                                                              ContentClassifiers = new List<string>
                                                                                                                   {
                                                                                                                       ContentClassifier.FreeOfPersonallyIdentifiableInformation
                                                                                                                   }
                                                                                          };

        private readonly RegionEndpoint _awsRekognitionRegion;

        public AwsImageAnalysisService()
        {
            _awsRekognitionRegion = RegionEndpoint.GetBySystemName(RydrEnvironment.GetAppSetting("AWS.Rekognition.Region", "us-west-2"));
        }

        public async Task<List<Label>> GetImageLabelsAsync(FileMetaData fileMeta)
        {
            var s3File = fileMeta.Bytes == null || fileMeta.Bytes.Length <= 0
                             ? new S3BucketPrefixKey(fileMeta)
                             : null;

            using(var client = new AmazonRekognitionClient(_awsAccessKey, _awsSecretKey, _awsRekognitionRegion))
            {
                var memStream = s3File == null
                                    ? new MemoryStream(fileMeta.Bytes)
                                    : null;

                try
                {
                    var labelResponse = await client.DetectLabelsAsync(new DetectLabelsRequest
                                                                       {
                                                                           Image = new Image
                                                                                   {
                                                                                       S3Object = s3File == null
                                                                                                      ? null
                                                                                                      : new S3Object
                                                                                                        {
                                                                                                            Bucket = s3File.BucketName,
                                                                                                            Name = s3File.Key
                                                                                                        },
                                                                                       Bytes = memStream
                                                                                   },
                                                                           MaxLabels = _maxLabels,
                                                                           MinConfidence = _minImageConfidence
                                                                       });

                    return labelResponse?.Labels;
                }
                finally
                {
                    if (memStream != null)
                    {
                        await memStream.DisposeAsync();
                    }
                }
            }
        }

        public async Task<List<ModerationLabel>> GetImageModerationsAsync(FileMetaData fileMeta, string humanLoopIdentifier)
        {
            var s3File = fileMeta.Bytes == null || fileMeta.Bytes.Length <= 0
                             ? new S3BucketPrefixKey(fileMeta)
                             : null;

            using(var client = new AmazonRekognitionClient(_awsAccessKey, _awsSecretKey, _awsRekognitionRegion))
            {
                var memStream = s3File == null
                                    ? new MemoryStream(fileMeta.Bytes)
                                    : null;

                try
                {
                    var moderationLabelsResponse = await client.DetectModerationLabelsAsync(new DetectModerationLabelsRequest
                                                                                            {
                                                                                                Image = new Image
                                                                                                        {
                                                                                                            S3Object = s3File == null
                                                                                                                           ? null
                                                                                                                           : new S3Object
                                                                                                                             {
                                                                                                                                 Bucket = s3File.BucketName,
                                                                                                                                 Name = s3File.Key
                                                                                                                             },
                                                                                                            Bytes = memStream
                                                                                                        },
                                                                                                HumanLoopConfig = _imageModerationHumanFlowArn.IsNullOrEmpty()
                                                                                                                      ? null
                                                                                                                      : new HumanLoopConfig
                                                                                                                        {
                                                                                                                            FlowDefinitionArn = _imageModerationHumanFlowArn,
                                                                                                                            HumanLoopName = string.Concat(HumanLoopService.ImageModerationPrefix, "-", humanLoopIdentifier, "-", Guid.NewGuid().ToStringId()).ToLowerInvariant(),
                                                                                                                            DataAttributes = _defaultHumanLoopDataAttributes
                                                                                                                        },
                                                                                                MinConfidence = _minModerationConfidence
                                                                                            });

                    return moderationLabelsResponse?.ModerationLabels;
                }
                finally
                {
                    if (memStream != null)
                    {
                        await memStream.DisposeAsync();
                    }
                }
            }
        }

        public async Task<List<TextDetection>> GetTextAsync(FileMetaData fileMeta)
        {
            var s3File = fileMeta.Bytes == null || fileMeta.Bytes.Length <= 0
                             ? new S3BucketPrefixKey(fileMeta)
                             : null;

            using(var client = new AmazonRekognitionClient(_awsAccessKey, _awsSecretKey, _awsRekognitionRegion))
            {
                var memStream = s3File == null
                                    ? new MemoryStream(fileMeta.Bytes)
                                    : null;

                try
                {
                    var textResponse = await client.DetectTextAsync(new DetectTextRequest
                                                                    {
                                                                        Image = new Image
                                                                                {
                                                                                    S3Object = s3File == null
                                                                                                   ? null
                                                                                                   : new S3Object
                                                                                                     {
                                                                                                         Bucket = s3File.BucketName,
                                                                                                         Name = s3File.Key
                                                                                                     },
                                                                                    Bytes = memStream
                                                                                }
                                                                    });

                    return textResponse?.TextDetections;
                }
                finally
                {
                    if (memStream != null)
                    {
                        await memStream.DisposeAsync();
                    }
                }
            }
        }

        public async Task<List<FaceDetail>> GetFacesAsync(FileMetaData fileMeta)
        {
            var s3File = fileMeta.Bytes == null || fileMeta.Bytes.Length <= 0
                             ? new S3BucketPrefixKey(fileMeta)
                             : null;

            using(var client = new AmazonRekognitionClient(_awsAccessKey, _awsSecretKey, _awsRekognitionRegion))
            {
                var memStream = s3File == null
                                    ? new MemoryStream(fileMeta.Bytes)
                                    : null;

                try
                {
                    var facesResponse = await client.DetectFacesAsync(new DetectFacesRequest
                                                                      {
                                                                          Image = new Image
                                                                                  {
                                                                                      S3Object = s3File == null
                                                                                                     ? null
                                                                                                     : new S3Object
                                                                                                       {
                                                                                                           Bucket = s3File.BucketName,
                                                                                                           Name = s3File.Key
                                                                                                       },
                                                                                      Bytes = memStream
                                                                                  },
                                                                          Attributes = new List<string>
                                                                                       {
                                                                                           "ALL"
                                                                                       }
                                                                      });

                    return facesResponse?.FaceDetails;
                }
                finally
                {
                    if (memStream != null)
                    {
                        await memStream.DisposeAsync();
                    }
                }
            }
        }
    }
}
