using System.Text;
using Rydr.Api.Core.Enums;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IEncryptionService
{
    Task<string> Encrypt64Async(string toEncrypt, EncryptionKeyType keyAlias = EncryptionKeyType.GeneralEncryptionKey, Encoding encoding = null);
    Task EncryptAsync(Stream source, Stream target, EncryptionKeyType keyAlias = EncryptionKeyType.GeneralEncryptionKey);

    Task<string> Decrypt64Async(string encryptedBase64, Encoding encoding = null);
    Task DecryptAsync(Stream source, Stream target);

    Task<byte[]> GenerateRandomAsync(int byteLength = 50);
}
