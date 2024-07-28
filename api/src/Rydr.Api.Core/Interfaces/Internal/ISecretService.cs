namespace Rydr.Api.Core.Interfaces.Internal;

public interface ISecretService
{
    Task<string> TryGetSecretStringAsync(string secretName);
    Task<string> GetSecretStringAsync(string secretName);
}
