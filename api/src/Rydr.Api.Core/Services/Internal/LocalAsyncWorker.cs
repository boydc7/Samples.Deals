using System;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Internal
{
    public class LocalAsyncWorker<T> : IConsumer
        where T : IAsyncInfo
    {
        private readonly ILog _log = LogManager.GetLogger("LocalAsyncWorker");
        private readonly IBuffer<T> _buffer;
        private readonly int _maxAttemptsPer;
        private readonly int _yieldTimeoutSeconds;
        private readonly int _exceptionLimitBeforeRecycle;
        private readonly int _secondsLimitBeforeRecycle;

        public LocalAsyncWorker(string consumerId, IBuffer<T> buffer,
                                int maxAttemptsPer = 3, int yieldTimeoutSeconds = 5,
                                int exceptionLimitBeforeRecycle = 25, int secondsLimitBeforeRecycle = 900)
        {
            ConsumerId = consumerId ?? Guid.NewGuid().ToStringId();
            _buffer = buffer;
            _maxAttemptsPer = maxAttemptsPer;
            _yieldTimeoutSeconds = yieldTimeoutSeconds;
            _exceptionLimitBeforeRecycle = exceptionLimitBeforeRecycle.Gz(25);
            _secondsLimitBeforeRecycle = secondsLimitBeforeRecycle.Gz(900);
        }

        public string ConsumerId { get; }

        public async Task ReceiveAsync()
        {
            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"LocalAsyncWorker [{ConsumerId}] starting ReceiveAsync");
            }

            try
            {
                await ProcessAsync();
            }
            catch(Exception ex)
            {
                Exception = ex;
            }

            if (LogExtensions.IsTraceEnabled)
            {
                _log.TraceInfo($"LocalAsyncWorker [{ConsumerId}] exiting ReceiveAsync");
            }
        }

        public Exception Exception { get; set; }

        public string ErrorMessage
        {
            get
            {
                if (Exception == null)
                {
                    return string.Empty;
                }

                return string.Concat(Exception.Message,
                                     Exception.InnerException == null
                                         ? string.Empty
                                         : "\n Inner Exception: \n",
                                     Exception.InnerException?.Message ?? string.Empty);
            }
        }

        public bool YieldedToRecycle { get; private set; }

        private async Task ProcessAsync()
        {
            var lastReceived = DateTime.UtcNow;
            var startedProcessingAt = lastReceived;
            var myExceptionCount = 0;

            while (true)
            {
                var entity = _buffer.Take();

                if (entity == null)
                {
                    if ((DateTime.UtcNow - lastReceived).TotalSeconds >= _yieldTimeoutSeconds)
                    {
                        return;
                    }

                    await Task.Delay(100);

                    continue;
                }

                lastReceived = DateTime.UtcNow;
                var shouldRecycle = false;

                try
                {
                    await entity.ExecuteAsync();
                }
                catch(LocalWorkerBackoffException) when(entity.Attempts < entity.MaxAttempts.Nz(_maxAttemptsPer))
                {
                    await Task.Delay(Math.Min(entity.Attempts, 20) * 250);

                    _buffer.Add(entity, entity.Force);

                    shouldRecycle = true;
                }
                catch(Exception ex)
                {
                    myExceptionCount++;

                    if (LogExtensions.IsDebugInfoEnabled)
                    {
                        _log.DebugInfoFormat("Error attempting background task [{0}] on attempt [{1}], exception [{2}]", entity.ToString(), entity.Attempts, ex.ToLogMessage());
                    }

                    if (entity.Attempts < entity.MaxAttempts.Nz(_maxAttemptsPer))
                    {
                        await Task.Delay(Math.Min(entity.Attempts, 20) * 250);

                        _buffer.Add(entity, entity.Force);
                    }
                    else
                    {
                        Exception = ex is LocalWorkerBackoffException lx
                                        ? lx.InnerException
                                        : ex;

                        entity.OnError?.Invoke(entity, ex);
                    }
                }
                finally
                {
                    if (LogExtensions.IsTraceEnabled)
                    {
                        _log.TraceInfo($"LocalAsyncWorker [{ConsumerId}] finished executing [{entity.ToString()}]");
                    }
                }

                shouldRecycle = shouldRecycle || (DateTime.UtcNow - startedProcessingAt).TotalSeconds >= _secondsLimitBeforeRecycle;

                if (myExceptionCount >= _exceptionLimitBeforeRecycle || shouldRecycle)
                {
                    YieldedToRecycle = shouldRecycle;

                    return;
                }
            }
        }
    }
}
