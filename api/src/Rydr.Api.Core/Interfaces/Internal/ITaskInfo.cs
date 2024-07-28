namespace Rydr.Api.Core.Interfaces.Internal;

public interface ITaskInfo : IAsyncInfo
{
    Func<object, Task> Callback { get; set; }
    object ObjectRef { get; set; }
}
