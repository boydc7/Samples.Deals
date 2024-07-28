using System.Text;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Services;

namespace Rydr.Api.Core.Services.Auth;

public class NullEncryptionService : IEncryptionService
{
    private NullEncryptionService() { }

    // ReSharper disable once UnusedMember.Global
    public static NullEncryptionService Instance { get; } = new();

    public Task<string> Encrypt64Async(string toEncrypt, EncryptionKeyType keyAlias = EncryptionKeyType.GeneralEncryptionKey, Encoding encoding = null)
        => Task.FromResult(toEncrypt);

    public async Task EncryptAsync(Stream source, Stream target, EncryptionKeyType keyAlias = EncryptionKeyType.GeneralEncryptionKey)
        => await source.CopyToAsync(target);

    public Task<string> Decrypt64Async(string encryptedBase64, Encoding encoding = null)
        => Task.FromResult(encryptedBase64);

    public async Task DecryptAsync(Stream source, Stream target)
        => await source.CopyToAsync(target);

    public Task<byte[]> GenerateRandomAsync(int byteLength = 50)
        => Task.FromResult(Guid.NewGuid().ToByteArray());
}
