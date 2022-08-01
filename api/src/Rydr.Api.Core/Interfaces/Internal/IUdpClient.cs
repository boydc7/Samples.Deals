using System.Threading.Tasks;

namespace Rydr.Api.Core.Interfaces.Internal
{
    public interface IUdpClient
    {
        bool Initialize(string server, int port);
        bool Send(string message);
        Task<bool> SendAsync(string message);
        void Close();
    }
}
