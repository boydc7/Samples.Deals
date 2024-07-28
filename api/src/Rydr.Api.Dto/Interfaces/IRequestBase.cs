namespace Rydr.Api.Dto.Interfaces;

public interface IRequestBase : IHasUserAuthorizationInfo
{
    DateTime ReceivedAt { get; }
    bool ForceRefresh { get; set; }
}

public interface IPagedRequest : IRequestBase, IHasSkipTake { }

public interface IHaveOriginalRequestId
{
    string OriginatingRequestId { get; }
}
