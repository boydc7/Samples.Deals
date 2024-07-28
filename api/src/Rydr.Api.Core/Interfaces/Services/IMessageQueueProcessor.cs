using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IMessageQueueProcessor
{
    Task<MqRetryResponse> ReprocessDlqAsync(MqRetry request);
    Task<MqRetryResponse> ProcessInqAsync(MqRetry request);
}
