using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Internal;

public class LocalAsyncTaskExecuter : LocalAsyncProducer<ITaskInfo>, ITaskExecuter, ITaskProducer
{
    private readonly bool _isDefault;
    private readonly ILog _log = LogManager.GetLogger("LocalAsyncTaskExecuter");

    private LocalAsyncTaskExecuter(IBuffer<ITaskInfo> buffer, int maxDop = 0, bool isDefault = false)
        : base(buffer, maxDop.Gz(RydrEnvironment.GetAppSetting("TaskExecuter.MaxWorkers", "15").ToInteger(15)))
    {
        _isDefault = isDefault;
    }

    public static LocalAsyncTaskExecuter DefaultTaskExecuter { get; } = new(InMemoryTaskBuffer.Default, isDefault: true);

    public int Count => BufferCount;

    public void Exec<T>(T obj, Action<T> callbackAsync, bool force = false,
                        Action<T, Exception> onError = null, int maxAttempts = 0)
        where T : class
        => ExecAsync(obj,
                     t =>
                     {
                         callbackAsync(t);

                         return Task.CompletedTask;
                     },
                     force, onError, maxAttempts);

    public void ExecAsync<T>(T obj, Func<T, Task> callbackAsync, bool force = false,
                             Action<T, Exception> onError = null, int maxAttempts = 0)
        where T : class
    {
        var taskInfo = new DefaultTaskInfo
                       {
                           ObjectRef = obj,
                           Callback = o =>
                                      {
                                          if (!(o is T objT))
                                          {
                                              return Task.CompletedTask;
                                          }

                                          return callbackAsync(objT);
                                      },
                           Force = force,
                           MaxAttempts = maxAttempts
                       };

        if (onError == null)
        {
            onError = LogOnError;
        }

        taskInfo.OnError = (o, x) =>
                           {
                               var oti = o as DefaultTaskInfo;

                               if (!(oti?.ObjectRef is T ot))
                               {
                                   return;
                               }

                               onError(ot, x);
                           };

        Publish(taskInfo);
    }

    public static LocalAsyncTaskExecuter Create(int maxDop) => new(InMemoryTaskBuffer.Create, maxDop);

    public void Backoff(int threads = 1)
    {
        Guard.Against(_isDefault, "Cannot backoff on the default task executer");

        BackoffWorkers(threads);
    }

    private void LogOnError<T>(T obj, Exception ex)
    {
        _log.Warn($"General unhandled exception processing task - objType [{obj?.GetType().Name ?? "NULL"}], obj [{obj?.ToJsv().Left(300) ?? "NULL"}]", ex);
    }
}
