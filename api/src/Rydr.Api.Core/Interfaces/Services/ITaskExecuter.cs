namespace Rydr.Api.Core.Interfaces.Services;

public interface ITaskExecuter
{
    void Exec<T>(T obj, Action<T> callbackAsync, bool force = false,
                 Action<T, Exception> onError = null, int maxAttempts = 0)
        where T : class;

    void ExecAsync<T>(T obj, Func<T, Task> callbackAsync, bool force = false,
                      Action<T, Exception> onError = null, int maxAttempts = 0)
        where T : class;
}
