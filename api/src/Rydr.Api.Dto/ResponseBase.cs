using System.Collections.Generic;
using System.Runtime.Serialization;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto
{
    public abstract class ResponseBase
    {
        public ResponseStatus ResponseStatus { get; set; }
    }

    public abstract class ResultsResponseBase<T> : ResponseBase, IHaveResults<T>, IHaveResults
        where T : class
    {
        [Ignore]
        [IgnoreDataMember]
        public IReadOnlyList<object> ResultObjs => Results;

        public IReadOnlyList<T> Results { get; set; }

        //        Results == null || Results.Count <= 0
        //        ? null
        //        : Results.Select(r => r as object)
        //        .Where(r => r != null)
        //        .ToList();

        public int ResultCount => Results?.Count ?? 0;

        public long? TotalCount { get; set; }
    }

    public abstract class ResultResponseBase<T> : ResponseBase, IHaveResult<T>, IHaveResult
        where T : class
    {
        [Ignore]
        [IgnoreDataMember]
        public object ResultObj => Result;

        public virtual T Result { get; set; }
    }

    public sealed class OnlyResultsResponse<T> : ResultsResponseBase<T>
        where T : class { }
    public sealed class OnlyResultResponse<T> : ResultResponseBase<T>
        where T : class { }

    public class SimpleResponse : ResponseBase { }

    public class LongIdResponse : SimpleResponse
    {
        public long Id { get; set; }
    }

    public class StringIdResponse : SimpleResponse
    {
        public string Id { get; set; }
    }

    public class StatusSimpleResponse : SimpleResponse
    {
        public StatusSimpleResponse() { }

        public StatusSimpleResponse(string status)
        {
            Status = status;
        }

        public string Status { get; set; }
    }
}
