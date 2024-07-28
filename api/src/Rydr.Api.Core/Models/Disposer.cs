namespace Rydr.Api.Core.Models;

public class Disposer<T> : IDisposable
{
    private readonly Action<T> _onDispose;
    private readonly bool _onceOnly;
    private readonly object _lockObject = new();

    private bool _disposed;

    public Disposer(T toDispose, Action<T> onDispose, bool onceOnly = true)
    {
        Object = toDispose;
        _onDispose = onDispose;
        _onceOnly = onceOnly;
    }

    public T Object { get; }

    public void Dispose()
    {
        if (_onDispose == null || Object == null || (_onceOnly && _disposed))
        {
            return;
        }

        lock(_lockObject)
        {
            if (_onceOnly && _disposed)
            {
                return;
            }

            _disposed = true;
        }

        _onDispose(Object);
    }
}

public class NullDisposer<T> : IDisposable
{
    public NullDisposer(T obj)
    {
        Object = obj;
    }

    public T Object { get; }

    public void Dispose() { }
}
