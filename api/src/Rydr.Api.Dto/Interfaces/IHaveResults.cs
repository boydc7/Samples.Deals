using System.Collections.Generic;

namespace Rydr.Api.Dto.Interfaces
{
    public interface IHaveResults<out T>
        where T : class
    {
        IReadOnlyList<T> Results { get; }
        int ResultCount { get; }
    }

    public interface IHaveResult<out T>
        where T : class
    {
        T Result { get; }
    }

    public interface IHaveResult
    {
        object ResultObj { get; }
    }

    public interface IHaveResults
    {
        IReadOnlyList<object> ResultObjs { get; }
        int ResultCount { get; }
    }

    public interface IHaveModel<T>
    {
        T Model { get; set; }
    }

    public interface IRequestBaseWithModel<T> : IRequestBase, IHaveModel<T> { }
}
