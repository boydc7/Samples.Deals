using Rydr.Api.Dto;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IDeferredRequestProcessingService
{
    object DeserializeDto<T>(T message)
        where T : PostDeferredBase;
}

public interface IDeferRequestsService
{
    /// <summary>
    ///     Defers a request via a DEDICATED queue for the type in question
    ///     Type/DTO must register a message handler for use in MessagingConfiguration
    /// </summary>
    void PublishMessage<T>(T publishRequest)
        where T : RequestBase;

    /// <summary>
    ///     Defers a request via the SHARED message queue - shared across all DTOs that publish to this queue via low/high
    ///     priority
    ///     No type/DTO registration required, uses a shared queue
    /// </summary>
    void DeferRequest<T>(T request)
        where T : RequestBase;

    void DeferLowPriRequest<T>(T request)
        where T : RequestBase;

    void DeferFifoRequest<T>(T request)
        where T : RequestBase;

    void DeferDealRequest<T>(T request)
        where T : RequestBase;

    void DeferPrimaryDealRequest<T>(T request)
        where T : RequestBase;

    void DeferRequestScheduled<T>(T request, DateTime runAt)
        where T : RequestBase;

    void DeferRequestScheduled<T>(T request, TimeSpan delay)
        where T : RequestBase;

    void PublishMessageScheduled<T>(T dto, DateTime runAt)
        where T : RequestBase;

    void PublishMessageScheduled<T>(T dto, TimeSpan delay)
        where T : RequestBase;

    void DeferRequestRecurring<T>(T request, string cronString, string jobId = null)
        where T : RequestBase;

    void PublishMessageRecurring<T>(T dto, string cronString, string jobId = null)
        where T : RequestBase;

    void RemoveRecurringJob(string jobId);
}
