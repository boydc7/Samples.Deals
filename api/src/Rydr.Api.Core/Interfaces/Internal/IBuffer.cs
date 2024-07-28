namespace Rydr.Api.Core.Interfaces.Internal;

public interface IBuffer<T>
{
    void Add(T item, bool force = false);
    T Take();
    int BufferCount { get; }
    string BufferId { get; }
}

public interface ITaskBuffer : IBuffer<ITaskInfo> { }
