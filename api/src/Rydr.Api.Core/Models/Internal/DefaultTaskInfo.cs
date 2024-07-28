using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Models.Internal;

public class DefaultTaskInfo : ITaskInfo
{
    public string TaskId { get; set; } = Guid.NewGuid().ToStringId();
    public Func<object, Task> Callback { get; set; }
    public object ObjectRef { get; set; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; set; }
    public bool Force { get; set; }

    public Action<object, Exception> OnError { get; set; }

    public Task ExecuteAsync()
    {
        Attempts++;

        return Callback == null
                   ? Task.CompletedTask
                   : Callback(ObjectRef);
    }

    private string _toString;

    public override string ToString() => _toString ??= string.Concat("TaskId [", TaskId, "] - [", ObjectRef?.GetType().Name ?? "NULL", "].[", Callback?.Method.Name ?? "UNKNOWN");
}
