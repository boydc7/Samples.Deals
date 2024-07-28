// ReSharper disable RedundantUsingDirective

using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using ServiceStack;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Internal;

public class NullDataStreamProducer : IDataStreamProducer
{
    private readonly ILog _log = LogManager.GetLogger("AsyncKinesisDataStreamProducer");

    private NullDataStreamProducer() { }

    public static NullDataStreamProducer Instance { get; } = new();

    public Task ProduceAsync(string streamName, string value)
    {
        if (!RydrEnvironment.IsDebugEnabled || !streamName.HasValue())
        {
            return Task.CompletedTask;
        }

        _log.DebugInfoFormat("NullDataStreamProducer produce called for stream [{0}], value [{1}]", streamName, value);

        return Task.CompletedTask;
    }

    public Task ProduceAsync(string streamName, IEnumerable<string> values, int hintCount = 50)
    {
        if (!RydrEnvironment.IsDebugEnabled || !streamName.HasValue())
        {
            return Task.CompletedTask;
        }

        _log.DebugInfoFormat("NullDataStreamProducer produce (many) called for stream [{0}]", streamName);

#if LOCALDEBUG
        values.Each((i, v) => _log.DebugInfoFormat("  NullProducerValue[{0}] - [{1}]", i, v));
#endif

        return Task.CompletedTask;
    }
}
