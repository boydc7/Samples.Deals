using System;
using System.Threading.Tasks;

namespace Rydr.Api.Core.Interfaces.Internal
{
    public interface IAsyncInfo
    {
        Task ExecuteAsync();
        int Attempts { get; }
        int MaxAttempts { get; set; }
        bool Force { get; }
        Action<object, Exception> OnError { get; set; }
    }
}
