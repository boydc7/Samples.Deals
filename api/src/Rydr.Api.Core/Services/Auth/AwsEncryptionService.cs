using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.FbSdk.Extensions;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Auth
{
    public class AwsEncryptionService : AwsBaseEncryptionService, IEncryptionService
    {
        private AwsEncryptionService() { }

        public static AwsEncryptionService Instance { get; } = new AwsEncryptionService();

        public async Task<string> Encrypt64Async(string toEncrypt, EncryptionKeyType keyAlias = EncryptionKeyType.GeneralEncryptionKey, Encoding encoding = null)
        {
            if (!toEncrypt.HasValue())
            {
                return null;
            }

            using(var target = new MemoryStream())
            using(var source = new MemoryStream((encoding ?? Encoding.UTF8).GetBytes(toEncrypt)))
            {
                await EncryptAsync(source, target, keyAlias);

                return target.ToArray().ToBase64();
            }
        }

        public async Task EncryptAsync(Stream source, Stream target, EncryptionKeyType keyAlias = EncryptionKeyType.GeneralEncryptionKey)
        {
            using(var dataKey = await GetKeyAsync(keyAlias))
            {
                await DoEncryptAsync(source, target, dataKey.Object);
            }
        }

        private async Task DoEncryptAsync(Stream source, Stream target, GenerateDataKeyResponse dataKey)
        {
            try
            {
                target.WriteByte((byte)dataKey.CiphertextBlob.Length);

                await dataKey.CiphertextBlob.CopyToAsync(target);

                using(var aes = Aes.Create())
                {
                    aes.GenerateIV();

                    aes.Key = dataKey.Plaintext.ToArray();

                    await target.WriteAsync(aes.IV, 0, aes.IV.Length);

                    using(var cryptoStream = new CryptoStream(target, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        await source.CopyToAsync(cryptoStream);
                        cryptoStream.FlushFinalBlock();
                    }
                }
            }
            catch(Exception x)
            {
                LogEncryptionException(x);

                throw new ApplicationException("Could not successfully encode/decode text or stream passed, see log for details");
            }
        }

        public async Task<string> Decrypt64Async(string encryptedBase64, Encoding encoding = null)
        {
            if (!encryptedBase64.HasValue())
            {
                return null;
            }

            using(var target = new MemoryStream())
            using(var source = new MemoryStream(encryptedBase64.FromBase64()))
            {
                await DecryptAsync(source, target);

                return (encoding ?? Encoding.UTF8).GetString(target.ToArray());
            }
        }

        public async Task DecryptAsync(Stream source, Stream target)
        {
            try
            {
                var length = source.ReadByte();

                var buffer = new byte[length];

                await source.ReadAsync(buffer, 0, length);

                using(var client = GetClient())
                using(var request = new Disposer<DecryptRequest>(new DecryptRequest
                                                                 {
                                                                     CiphertextBlob = new MemoryStream(buffer)
                                                                 },
                                                                 r => r.CiphertextBlob.TryClose()))
                using(var response = new Disposer<DecryptResponse>(await client.DecryptAsync(request.Object),
                                                                   r => r.Plaintext.TryClose()))
                using(var aes = Aes.Create())
                {
                    aes.Key = response.Object.Plaintext.ToArray();

                    var iv = aes.IV;
                    await source.ReadAsync(iv, 0, iv.Length);
                    aes.IV = iv;

                    using(var crypto = new CryptoStream(source, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        await crypto.CopyToAsync(target);
                    }
                }
            }
            catch(Exception x)
            {
                LogEncryptionException(x);

                throw new ApplicationException("Could not successfully encode/decode text or stream passed, see log for details");
            }
        }
    }

    public abstract class AwsBaseEncryptionService
    {
        protected static readonly string _awsAccessKey = RydrEnvironment.GetAppSetting("AWSAccessKey");
        protected static readonly string _awsSecretKey = RydrEnvironment.GetAppSetting("AWSSecretKey");
        protected static readonly string _awsIamClientRegion = RydrEnvironment.GetAppSetting("AWS.IAM.Region", "us-west-2");

        protected readonly ILog _log = LogManager.GetLogger("AwsEncryptionService");

        protected AmazonKeyManagementServiceClient GetClient()
            => new AmazonKeyManagementServiceClient(_awsAccessKey, _awsSecretKey, RegionEndpoint.GetBySystemName(_awsIamClientRegion));

        public async Task<byte[]> GenerateRandomAsync(int byteLength = 50)
        {
            using(var client = GetClient())
            using(var response = new Disposer<GenerateRandomResponse>(await client.GenerateRandomAsync(byteLength),
                                                                      r => r.Plaintext.TryClose()))
            {
                return response.Object.Plaintext.ToArray();
            }
        }

        protected void LogEncryptionException(Exception x, [CallerMemberName] string methodName = null)
        { // logging exception internally instead of passing out in order to avoid giving hints in case of attack
            _log.Exception(x, $"Encryption service exception from method [{methodName}]");
        }

        protected async Task<Disposer<GenerateDataKeyResponse>> GetKeyAsync(EncryptionKeyType keyAlias)
        {
            GenerateDataKeyResponse response = null;

            var alias = string.Concat("alias/", keyAlias.ToString());

            using(var client = GetClient())
            {
                response = await client.GenerateDataKeyAsync(new GenerateDataKeyRequest
                                                             {
                                                                 KeyId = alias,
                                                                 KeySpec = DataKeySpec.AES_256
                                                             });
            }

            return new Disposer<GenerateDataKeyResponse>(response, OnDataKeyDispose);
        }

        protected static void OnDataKeyDispose(GenerateDataKeyResponse dataKey)
        {
            dataKey.CiphertextBlob.TryClose();
            dataKey.Plaintext.TryClose();
        }
    }
}
