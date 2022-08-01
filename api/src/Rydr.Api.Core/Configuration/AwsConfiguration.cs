using Amazon;
using Amazon.S3;
using Funq;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Auth;
using Rydr.Api.Core.Services.Internal;
using ServiceStack;

namespace Rydr.Api.Core.Configuration
{
    public class AwsConfiguration : IAppHostConfigurer
    {
        public void Apply(ServiceStackHost appHost, Container container)
        {
            AWSConfigsDynamoDB.Context.TableNamePrefix = RydrEnvironment.GetAppSetting("AWS.Dynamo.TableNamePrefix", "dev_");

            var awsDynRegionSetting = RegionEndpoint.GetBySystemName(RydrEnvironment.GetAppSetting("AWS.Dynamo.Region", "us-west-2"));
            container.Register("Dynamo", awsDynRegionSetting);

            var awsSqsRegionSetting = RegionEndpoint.GetBySystemName(RydrEnvironment.GetAppSetting("AWS.SQS.Region", "us-west-2"));
            container.Register("SQS", awsSqsRegionSetting);

            var awsS3RegionSetting = RegionEndpoint.GetBySystemName(RydrEnvironment.GetAppSetting("AWS.S3.Region", "us-west-2"));
            container.Register("S3", awsS3RegionSetting);

            container.Register<IAmazonS3>(c => new AmazonS3Client(RydrEnvironment.GetAppSetting("AWSAccessKey"),
                                                                  RydrEnvironment.GetAppSetting("AWSSecretKey"),
                                                                  new AmazonS3Config
                                                                  {
                                                                      RegionEndpoint = c.ResolveNamed<RegionEndpoint>("S3"),
                                                                      DisableLogging = true
                                                                  }))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.RegisterAutoWiredAs<AwsSnsService, IAwsSnsService>()
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<ISmsService>(c => c.Resolve<IAwsSnsService>())
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IHumanLoopService>(c => new AwsHumanLoopService())
                     .ReusedWithin(ReuseScope.Hierarchy);

            if (RydrEnvironment.IsLocalEnvironment || !RydrEnvironment.GetAppSetting("DealMetrics.StreamName").HasValue())
            {
                container.RegisterAutoWiredAs<SqlDataStreamProducer, IDataStreamProducer>()
                         .ReusedWithin(ReuseScope.Hierarchy);
            }
            else
            {
                container.Register<IDataStreamProducer>(c => new CompositeDataStreamProducer(new IDataStreamProducer[]
                                                                                             {
                                                                                                 new SqlDataStreamProducer(c.Resolve<IRydrDataService>()), new AwsKinesisDataStreamProducer()
                                                                                             }))
                         .ReusedWithin(ReuseScope.Hierarchy);
            }

#if FBCLIENT_USENULL
            container.Register<IEncryptionService>(NullEncryptionService.Instance);
#else
            container.Register<IEncryptionService>(AwsEncryptionService.Instance);
#endif
        }
    }
}
