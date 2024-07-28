using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Services.Internal;

public class AwsSecretService : ISecretService
{
    private static readonly string _awsAccessKey = RydrEnvironment.GetAppSetting("AWSAccessKey");
    private static readonly string _awsSecretKey = RydrEnvironment.GetAppSetting("AWSSecretKey");
    private readonly IAmazonSecretsManager _client;

    public AwsSecretService()
    {
        var awsSecretManagerRegion = RegionEndpoint.GetBySystemName(RydrEnvironment.GetAppSetting("AWS.SecretsManager.Region", "us-west-2"));

        _client = new AmazonSecretsManagerClient(_awsAccessKey, _awsSecretKey, awsSecretManagerRegion);
    }

    public async Task<string> TryGetSecretStringAsync(string secretName)
    {
        try
        {
            var secret = await GetSecretStringAsync(secretName);

            return secret;
        }
        catch(ResourceNotFoundException)
        {
            return null;
        }
    }

    public async Task<string> GetSecretStringAsync(string secretName)
    {
        var secretResponse = await _client.GetSecretValueAsync(new GetSecretValueRequest
                                                               {
                                                                   SecretId = secretName
                                                               });

        var secretValue = secretResponse.SecretString;

        return secretValue;
    }
}
