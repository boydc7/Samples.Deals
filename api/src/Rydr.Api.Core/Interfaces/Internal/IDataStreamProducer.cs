using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rydr.Api.Core.Interfaces.Internal
{
    public interface IDataStreamProducer
    {
        Task ProduceAsync(string streamName, string value);
        Task ProduceAsync(string streamName, IEnumerable<string> values, int hintCount = 50);
    }
}
