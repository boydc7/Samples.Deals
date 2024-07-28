using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.FbSdk.Extensions;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Services;

public static class VideoConversionHelpers
{
    public const string GenericVideoExtension = "mp4";
    public const string GenericVideoSuffix = "_Generic720";
    public const string GenericVideoThumbnailExtension = "jpg";
    public const string GenericVideoThumbnailSuffix = "_Thumbnail";
    public const string GenericVideoThumbnailDefaultSequence = "0000001";
    public const int GenericVideoThumbnailHeight = 720;
    public const int GenericVideoThumbnailWidth = 1280;
}

public class AwsVideoConversionService : IVideoConversionService
{
    private const string _missingJobId = "RydrMediaConvertJobNotCreated";

    private static readonly string _awsAccessKey = RydrEnvironment.GetAppSetting("AWSAccessKey");
    private static readonly string _awsSecretKey = RydrEnvironment.GetAppSetting("AWSSecretKey");
    private static readonly string _awsMediaConvertApiEndpoint = RydrEnvironment.GetAppSetting("AWS.MediaConvert.Endpoint", "https://hvtjrir1c.mediaconvert.us-west-2.amazonaws.com");

    private static readonly Dictionary<string, FileConvertStatus> _convertStatusMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            {
                JobStatus.ERROR.Value, FileConvertStatus.Error
            },
            {
                JobStatus.CANCELED.Value, FileConvertStatus.Canceled
            },
            {
                JobStatus.COMPLETE.Value, FileConvertStatus.Complete
            },
            {
                JobStatus.SUBMITTED.Value, FileConvertStatus.Submitted
            },
            {
                JobStatus.PROGRESSING.Value, FileConvertStatus.InProgress
            }
        };

    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly ICacheClient _cacheClient;

    public AwsVideoConversionService(IFileStorageProvider fileStorageProvider,
                                     ICacheClient cacheClient)
    {
        _fileStorageProvider = fileStorageProvider;
        _cacheClient = cacheClient;
    }

    public async Task ConvertAsync(FileMetaData inputFileMeta, string outputDestination)
    {
        var exists = await _fileStorageProvider.ExistsAsync(inputFileMeta);

        Guard.AgainstRecordNotFound(!exists, inputFileMeta.FileName);

        using(var client = new AmazonMediaConvertClient(_awsAccessKey, _awsSecretKey, new AmazonMediaConvertConfig
                                                                                      {
                                                                                          ServiceURL = _awsMediaConvertApiEndpoint
                                                                                      }))
        {
            var jobId = _missingJobId;

            try
            {
                var response = await client.CreateJobAsync(GetJobRequest(inputFileMeta.FullName, outputDestination));

                jobId = response?.Job?.Id;
            }
            finally
            {
                await _cacheClient.TrySetAsync(new SimpleCacheItem
                                               {
                                                   Data = jobId
                                               },
                                               GetCacheId(inputFileMeta, outputDestination),
                                               CacheConfig.FromHours(10));
            }
        }
    }

    public async Task<FileConvertStatus> GetStatusAsync(FileMetaData inputFileMeta, string outputDestination)
    {
        var cacheItem = _cacheClient.TryGet<SimpleCacheItem>(GetCacheId(inputFileMeta, outputDestination));

        if (!(cacheItem?.Data).HasValue() || cacheItem.Data.EqualsOrdinalCi(_missingJobId))
        {
            return FileConvertStatus.Unknown;
        }

        using(var client = new AmazonMediaConvertClient(_awsAccessKey, _awsSecretKey, new AmazonMediaConvertConfig
                                                                                      {
                                                                                          ServiceURL = _awsMediaConvertApiEndpoint
                                                                                      }))
        {
            var convertJob = await client.GetJobAsync(new GetJobRequest
                                                      {
                                                          Id = cacheItem.Data
                                                      });

            return ToFileConvertStatus(convertJob);
        }
    }

    private FileConvertStatus ToFileConvertStatus(GetJobResponse jobResponse)
    {
        if (!(jobResponse?.Job?.Status?.Value).HasValue())
        {
            return FileConvertStatus.Unknown;
        }

        return _convertStatusMap.GetValueOrDefault(jobResponse.Job.Status.Value, FileConvertStatus.Unknown);
    }

    private string GetCacheId(FileMetaData inputFileMeta, string outputDestination)
        => string.Concat(inputFileMeta.FullName, "__", outputDestination).ToSafeSha64();

    private CreateJobRequest GetJobRequest(string inputS3File, string destinationS3Path)
    {
        inputS3File = string.Concat("s3://", inputS3File.TrimStart('/'));
        destinationS3Path = string.Concat("s3://", destinationS3Path.TrimStart('/').AppendIfNotEndsWith("/"));

        return new CreateJobRequest
               {
                   Role = "arn:aws:iam::933347060724:role/AWSMediaConvertServiceRole",
                   Settings = new JobSettings
                              {
                                  AdAvailOffset = 0,
                                  Inputs = new List<Input>
                                           {
                                               new()
                                               {
                                                   FilterEnable = InputFilterEnable.AUTO,
                                                   FilterStrength = 0,
                                                   PsiControl = InputPsiControl.USE_PSI,
                                                   DeblockFilter = InputDeblockFilter.DISABLED,
                                                   DenoiseFilter = InputDenoiseFilter.DISABLED,
                                                   TimecodeSource = InputTimecodeSource.EMBEDDED,
                                                   VideoSelector = new VideoSelector
                                                                   {
                                                                       ColorSpace = ColorSpace.FOLLOW
                                                                   },
                                                   AudioSelectors = new Dictionary<string, AudioSelector>
                                                                    {
                                                                        {
                                                                            "Audio Selector 1", new AudioSelector
                                                                                                {
                                                                                                    Offset = 0,
                                                                                                    SelectorType = AudioSelectorType.TRACK,
                                                                                                    DefaultSelection = AudioDefaultSelection.DEFAULT,
                                                                                                    ProgramSelection = 1,

                                                                                                    // Tracks = new List<int>
                                                                                                    //          {
                                                                                                    //              1
                                                                                                    //          }
                                                                                                }
                                                                        }
                                                                    },
                                                   FileInput = inputS3File
                                               }
                                           },
                                  OutputGroups = new List<OutputGroup>
                                                 {
                                                     new()
                                                     {
                                                         Name = "File Group",
                                                         CustomName = "RydrGeneric Mp4WithThumbnail",
                                                         OutputGroupSettings = new OutputGroupSettings
                                                                               {
                                                                                   Type = OutputGroupType.FILE_GROUP_SETTINGS,
                                                                                   FileGroupSettings = new FileGroupSettings
                                                                                                       {
                                                                                                           Destination = destinationS3Path
                                                                                                       }
                                                                               },
                                                         Outputs = new List<Output>
                                                                   {
                                                                       new()
                                                                       {
                                                                           Preset = "System-Generic_Hd_Mp4_Avc_Aac_16x9_Sdr_1280x720p_30Hz_5Mbps_Qvbr_Vq9",
                                                                           Extension = VideoConversionHelpers.GenericVideoExtension,
                                                                           NameModifier = VideoConversionHelpers.GenericVideoSuffix
                                                                       },
                                                                       new()
                                                                       {
                                                                           Extension = VideoConversionHelpers.GenericVideoThumbnailExtension,
                                                                           NameModifier = VideoConversionHelpers.GenericVideoThumbnailSuffix,
                                                                           ContainerSettings = new ContainerSettings
                                                                                               {
                                                                                                   Container = ContainerType.RAW
                                                                                               },
                                                                           VideoDescription = new VideoDescription
                                                                                              {
                                                                                                  Width = VideoConversionHelpers.GenericVideoThumbnailWidth,
                                                                                                  ScalingBehavior = ScalingBehavior.DEFAULT,
                                                                                                  Sharpness = 50,
                                                                                                  Height = VideoConversionHelpers.GenericVideoThumbnailHeight,
                                                                                                  TimecodeInsertion = VideoTimecodeInsertion.DISABLED,
                                                                                                  AntiAlias = AntiAlias.DISABLED,
                                                                                                  DropFrameTimecode = DropFrameTimecode.ENABLED,
                                                                                                  ColorMetadata = ColorMetadata.INSERT,
                                                                                                  CodecSettings = new VideoCodecSettings
                                                                                                                  {
                                                                                                                      Codec = VideoCodec.FRAME_CAPTURE,
                                                                                                                      FrameCaptureSettings = new FrameCaptureSettings
                                                                                                                                             {
                                                                                                                                                 FramerateNumerator = 30,
                                                                                                                                                 FramerateDenominator = 30,
                                                                                                                                                 MaxCaptures = 3,
                                                                                                                                                 Quality = 80
                                                                                                                                             }
                                                                                                                  }
                                                                                              }
                                                                       }
                                                                   }
                                                     }
                                                 }
                              }
               };
    }
}
