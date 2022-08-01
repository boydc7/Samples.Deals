using System;
using System.Threading.Tasks;

namespace Rydr.Api.Core.Interfaces.Internal
{
    public interface IConsumer
    {
        Task ReceiveAsync();
        string ConsumerId { get; }
        Exception Exception { get; set; }
        string ErrorMessage { get; }
        bool YieldedToRecycle { get; }
    }
}
