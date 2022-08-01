using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Internal
{
    public class WrappedUdpClient : IUdpClient
    {
        private const int _logEvery = 100;
        private readonly object _lock = new object();
        private readonly bool _logUdpExceptions = RydrEnvironment.GetAppSetting("Stats.LogSendExceptions", "false").ToBoolean();
        private UdpClient _client;
        private bool _initialized;
        private ILog _log;
        private int _loggedCount;

        public static WrappedUdpClient DefaultClient { get; } = new WrappedUdpClient();

        public bool Initialize(string server, int port)
        {
            if (_initialized)
            {
                return true;
            }

            if (server == null)
            {
                return false;
            }

            lock(_lock)
            {
                if (_initialized)
                {
                    return true;
                }

                _client = new UdpClient(server, port);
                _initialized = true;

                _log = LogManager.GetLogger(GetType());

                return true;
            }
        }

        public bool Send(string message)
        {
            try
            {
                var dgram = Encoding.ASCII.GetBytes(message);
                _client.SendAsync(dgram, dgram.Length);

                return true;
            }
            catch(Exception ex)
            {
                LogException(ex);

                return false;
            }
        }

        public async Task<bool> SendAsync(string message)
        {
            try
            {
                var dgram = Encoding.ASCII.GetBytes(message);
                await _client.SendAsync(dgram, dgram.Length);

                return true;
            }
            catch(Exception ex)
            {
                LogException(ex);

                return false;
            }
        }

        public void Close()
        {
            _client.Close();
            _initialized = false;
        }

        private void LogException(Exception ex)
        {
            if (!_logUdpExceptions)
            {
                return;
            }

            if (_loggedCount % _logEvery == 0)
            {
                _log.Exception(ex, "Could not Send packet in WrappedUdpClient");
            }

            _loggedCount++;
        }
    }
}
