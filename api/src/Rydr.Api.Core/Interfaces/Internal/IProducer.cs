namespace Rydr.Api.Core.Interfaces.Internal;

public interface IProducer<in T>
{
    void Publish(T entity);
}

public interface ITaskProducer : IProducer<ITaskInfo> { }
