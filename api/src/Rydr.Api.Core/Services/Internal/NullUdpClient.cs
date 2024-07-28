using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Services.Internal;

public class NullUdpClient : IUdpClient
{
    public static NullUdpClient Default { get; } = new();

    public bool Initialize(string server, int port) => true;

    public bool Send(string message) => true;

    public Task<bool> SendAsync(string message) => Task.FromResult(true);

    public void Close() { }
}
