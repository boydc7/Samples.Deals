using System;
using System.Collections.Concurrent;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Services.Internal
{
    public class InMemoryBuffer<T> : IBuffer<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly int _maxQueueSize = RydrEnvironment.GetAppSetting("GenericBuffer.MaxQueueSize", "125000").ToInteger(125000);

        private readonly ConcurrentQueue<QueueItem<T>> _buffer = new ConcurrentQueue<QueueItem<T>>();

        public int MaxQueueSize => _maxQueueSize;
        public int BufferCount => _buffer.Count;
        public string BufferId { get; } = Guid.NewGuid().ToStringId();

        public void Add(T item, bool force = false)
        {
            CheckSize();

            _buffer.Enqueue(new QueueItem<T>
                            {
                                Obj = item,
                                Force = force
                            });
        }

        public T Take() => _buffer.TryDequeue(out var buffered)
                               ? buffered.Obj
                               : default;

        private void CheckSize()
        {
            while (_buffer.Count >= MaxQueueSize)
            {
                _buffer.TryDequeue(out var buffered);

                if (buffered.Force)
                {
                    _buffer.Enqueue(buffered);

                    // Just exit after finding a single force obj
                    return;
                }
            }
        }

        private class QueueItem<TQ>
        {
            public TQ Obj { get; set; }
            public bool Force { get; set; }
        }
    }

    public class InMemoryTaskBuffer : InMemoryBuffer<ITaskInfo>, ITaskBuffer
    {
        public static ITaskBuffer Default { get; } = new InMemoryTaskBuffer();

        public static InMemoryTaskBuffer Create => new InMemoryTaskBuffer();
    }
}
