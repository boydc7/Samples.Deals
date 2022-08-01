// ReSharper disable RedundantUsingDirective

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Internal
{
    public class CompositeDataStreamProducer : IDataStreamProducer
    {
        private readonly IReadOnlyList<IDataStreamProducer> _producers;

        public CompositeDataStreamProducer(IEnumerable<IDataStreamProducer> producers)
        {
            _producers = producers.AsListReadOnly();
        }

        public async Task ProduceAsync(string streamName, string value)
        {
            foreach (var producer in _producers)
            {
                await producer.ProduceAsync(streamName, value);
            }
        }

        public async Task ProduceAsync(string streamName, IEnumerable<string> values, int hintCount = 50)
        {
            foreach (var producer in _producers)
            {
                await producer.ProduceAsync(streamName, values, hintCount);
            }
        }
    }
}
