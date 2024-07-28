using System.Text;
using Amazon;
using Amazon.KinesisFirehose;
using Amazon.KinesisFirehose.Model;
using Microsoft.IO;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Services.Internal;

public class AwsKinesisDataStreamProducer : IDataStreamProducer
{
    private static readonly RecyclableMemoryStreamManager _memoryStreamManager = new();
    private static readonly string _awsAccessKey = RydrEnvironment.GetAppSetting("AWSAccessKey");
    private static readonly string _awsSecretKey = RydrEnvironment.GetAppSetting("AWSSecretKey");

    private readonly RegionEndpoint _awsKinesisRegion;

    public AwsKinesisDataStreamProducer()
    {
        _awsKinesisRegion = RegionEndpoint.GetBySystemName(RydrEnvironment.GetAppSetting("AWS.Kinesis.Region", "us-west-2"));
    }

    public async Task ProduceAsync(string streamName, string value)
    {
        using var client = new AmazonKinesisFirehoseClient(_awsAccessKey, _awsSecretKey, _awsKinesisRegion);

        await using var ms = _memoryStreamManager.GetStream("AsyncKinesisDataStreamProducer");

        await ms.WriteAsync(Encoding.UTF8.GetBytes(value));

        await client.PutRecordAsync(streamName,
                                    new Record
                                    {
                                        Data = ms
                                    });
    }

    public async Task ProduceAsync(string streamName, IEnumerable<string> values, int hintCount = 50)
    {
        using(var client = new AmazonKinesisFirehoseClient(_awsAccessKey, _awsSecretKey, _awsKinesisRegion))
        {
            var records = new List<Record>(hintCount);

            try
            {
                foreach (var value in values)
                {
                    var ms = _memoryStreamManager.GetStream("AsyncKinesisDataStreamProducer");

                    await ms.WriteAsync(Encoding.UTF8.GetBytes(value));

                    records.Add(new Record
                                {
                                    Data = ms
                                });
                }

                await client.PutRecordBatchAsync(streamName, records);
            }
            finally
            {
                foreach (var record in records)
                {
                    record.Data.TryDispose();
                }
            }
        }
    }
}
