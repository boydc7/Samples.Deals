using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using ServiceStack;
using ServiceStack.Logging;
using LogExtensions = Rydr.Api.Core.Extensions.LogExtensions;

namespace Rydr.Api.Core.Services.Internal
{
    public class LocalAsyncProducer<T> : IProducer<T>, IDisposable
        where T : IAsyncInfo
    {
        private readonly ILog _log = LogManager.GetLogger("LocalAsyncProducer");
        private readonly IBuffer<T> _buffer;
        private readonly Dictionary<string, Task> _workers;
        private readonly object _lockObject = new object();
        private readonly Func<string, IBuffer<T>, IConsumer> _workerFactory;
        private int _disposalCount;

        public LocalAsyncProducer(IBuffer<T> buffer,
                                  int maxWorkers = 9,
                                  Func<string, IBuffer<T>, IConsumer> workerFactory = null)
        {
            MaxWorkers = maxWorkers > 0
                             ? maxWorkers
                             : 9;

            _buffer = buffer;

            if (workerFactory == null)
            {
                _workerFactory = (i, b) => new LocalAsyncWorker<T>(i, b);
            }
            else
            {
                _workerFactory = workerFactory;
            }

            _workers = new Dictionary<string, Task>(MaxWorkers);
        }

        public int BufferCount => _buffer.BufferCount;
        public bool InShutdown { get; set; }
        public string ProducerId { get; } = Guid.NewGuid().ToStringId();
        public string BufferId => _buffer.BufferId;

        protected void BackoffWorkers(int threads)
        {
            lock(_lockObject)
            {
                if (MaxWorkers > threads)
                {
                    MaxWorkers -= threads;
                }
                else
                {
                    MaxWorkers = 1;
                }
            }
        }

        public void Publish(T entity)
        {
            if (InShutdown)
            {
                throw new ApplicationInShutdownException();
            }

            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"LocalAsyncProducer [{ProducerId}] publishing [{entity.ToString()}]");
            }

            _buffer.Add(entity, entity.Force);

            StartWorker();
        }

        public int MaxWorkers { get; private set; }

        public int WorkerCount => _workers.Count;

        private void StartWorker()
        {
            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"LocalAsyncProducer [{ProducerId}] StartWorker starting - current worker count [{_workers.Count}], current buffer count [{BufferCount}]");
            }

            if (_workers.Count >= MaxWorkers || (_workers.Count > 0 && _workers.Count >= _buffer.BufferCount))
            {
                return;
            }

            var consumerId = Guid.NewGuid().ToStringId();
            IConsumer consumer = null;

            try
            { // We got work to do
                lock(_lockObject)
                {
                    if (_workers.Count >= MaxWorkers)
                    {
                        return;
                    }

                    consumer = _workerFactory(consumerId, _buffer);

                    _workers.Add(consumerId,
                                 Task.Run(async () => await consumer.ReceiveAsync())
                                     .ContinueWith(t => OnWorkerComplete(consumer)));
                }
            }
            catch(Exception ex)
            {
                if (consumer != null)
                {
                    if (consumer.Exception == null)
                    {
                        consumer.Exception = ex;
                    }

                    OnWorkerComplete(consumer);
                }

                if (_workers.ContainsKey(consumerId))
                {
                    lock(_lockObject)
                    {
                        var removed = Try.Exec(() => _workers.Remove(consumerId));

                        if (LogExtensions.IsTraceEnabled)
                        {
                            _log.TraceInfo($"LocalAsyncProducer [{ProducerId}] caught exception and tried to remove consumerId [{consumerId}] from worker list, removed from worker? [{removed}]");
                        }
                    }
                }
            }
        }

        private void OnWorkerComplete(IConsumer taskConsumer)
        {
            if (taskConsumer == null || !taskConsumer.ConsumerId.HasValue())
            {
                return;
            }

            var consumerErrorMsg = taskConsumer.ErrorMessage;

            if (consumerErrorMsg.HasValue())
            {
                _log.Warn($"LocalAsyncProducer [{ProducerId}] consumer [{taskConsumer.ConsumerId}] error [{consumerErrorMsg}]", taskConsumer.Exception);
            }

            if (_workers.ContainsKey(taskConsumer.ConsumerId))
            {
                lock(_lockObject)
                {
                    var removed = Try.Exec(() => _workers.Remove(taskConsumer.ConsumerId));

                    if (LogExtensions.IsTraceEnabled)
                    {
                        _log.TraceInfo($"LocalAsyncProducer [{ProducerId}] OnWorkerComplete had consumerId [{taskConsumer.ConsumerId}], and removed from worker? [{removed}]");
                    }
                }

                if (taskConsumer.YieldedToRecycle)
                { // If due to a recycle requirement, go back to try and start a new worker again
                    StartWorker();
                }
            }

            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"LocalAsyncProducer [{ProducerId}] finished OnWorkerComplete for consumer [{taskConsumer?.ConsumerId ?? "NULL"}] - current worker count [{_workers.Count}], current buffer count [{BufferCount}]");
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposalCount, 1, 0) > 0)
            {
                return;
            }

            if (_workers.Count > 0)
            {
                Task.WaitAll(_workers.Values.ToArray(), 25000);
            }

            Disposed = true;
        }

        public bool Disposed { get; private set; }
    }
}
