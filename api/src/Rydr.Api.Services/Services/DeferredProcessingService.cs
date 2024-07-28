using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;

namespace Rydr.Api.Services.Services;

[RequiredRole("Admin")]
public class DeferredProcessingService : BaseInternalOnlyApiService
{
    private static long _deferredSendFailuresSinceSuccess;

    private readonly IDeferredRequestProcessingService _deferredRequestProcessingService;
    private readonly IDeferredAffectedProcessingService _deferredAffectedProcessingService;
    private readonly IRequestStateManager _requestStateManager;

    public DeferredProcessingService(IDeferredRequestProcessingService deferredRequestProcessingService,
                                     IDeferredAffectedProcessingService deferredAffectedProcessingService,
                                     IRequestStateManager requestStateManager)
    {
        _deferredRequestProcessingService = deferredRequestProcessingService;
        _deferredAffectedProcessingService = deferredAffectedProcessingService;
        _requestStateManager = requestStateManager;
    }

    public async Task Post(PostDeferredMessage request)
    {
        var dto = _deferredRequestProcessingService.DeserializeDto(request);

        await SendDeserializedDtoAsync(dto);
    }

    public async Task Post(PostDeferredLowPriMessage request)
    {
        var dto = _deferredRequestProcessingService.DeserializeDto(request);

        await SendDeserializedDtoAsync(dto);
    }

    public async Task Post(PostDeferredDealMessage request)
    {
        var dto = _deferredRequestProcessingService.DeserializeDto(request);

        await SendDeserializedDtoAsync(dto);
    }

    public async Task Post(PostDeferredPrimaryDealMessage request)
    {
        var dto = _deferredRequestProcessingService.DeserializeDto(request);

        await SendDeserializedDtoAsync(dto);
    }

    public async Task Post(PostDeferredFifoMessage request)
    {
        var dto = _deferredRequestProcessingService.DeserializeDto(request);

        await SendDeserializedDtoAsync(dto);
    }

    public async Task Post(PostDeferredAffected request)
    {
        Request.RequestAttributes |= RequestAttributes.InProcess;

        _requestStateManager.UpdateStateToSystemRequest();

        try
        {
            await _deferredAffectedProcessingService.ProcessAsync(request);
        }
        catch(Exception x) when(_log.LogExceptionReturnFalse(x, $"PostDeferredAffected could not process request for type [{request.Type.ToString()}], request [{request.ToJsv().Left(300)}]"))
        {
            throw;
        }
    }

    private async Task SendDeserializedDtoAsync(object dto)
    {
        Request.RequestAttributes |= RequestAttributes.InProcess;
        var attempt = 1;

        do
        {
            try
            {
                if (dto is IReturnVoid)
                {
                    await Gateway.SendAsync<IReturnVoid>(dto);
                }
                else
                {
                    await Gateway.SendAsync(Gateway.GetResponseType(dto), dto);
                }

                _deferredSendFailuresSinceSuccess = 0;

                return;
            }
            catch(RecordNotFoundException rnx) when(rnx.Message.ContainsSafe("Record was not found or you do not have access to it") ||
                                                    (rnx.InnerException?.Message.ContainsSafe("Record was not found or you do not have access to it") ?? false))
            {
                if (attempt >= 3)
                {
                    _log.Exception(rnx, $"DeferredProcessing exception handling dto type [{dto.GetType().Name}], contents [{dto.ToJsv().Left(300)}], attempted [{attempt}] times, current fail count [{_deferredSendFailuresSinceSuccess}]");

                    return;
                }

                _log.Warn($"DeferredProcessing caught RecordNotFoundException exception, currently attempted [{attempt}] times, waiting and retrying", rnx);
            }
            catch(WebServiceException wsx) when(wsx.Message.ContainsSafe("Record was not found or you do not have access to it") ||
                                                (wsx.InnerException?.Message.ContainsSafe("Record was not found or you do not have access to it") ?? false))
            {
                if (attempt >= 3)
                {
                    _log.Exception(wsx, $"DeferredProcessing exception handling dto type [{dto.GetType().Name}], contents [{dto.ToJsv().Left(300)}], attempted [{attempt}] times, current fail count [{_deferredSendFailuresSinceSuccess}]");

                    return;
                }

                _log.Warn($"DeferredProcessing caught WebServiceException.RecordNotFoundException exception, currently attempted [{attempt}] times, waiting and retrying", wsx);
            }
            catch(Exception x) when(_log.LogExceptionReturnFalse(x, $"DeferredProcessing exception handling dto type [{dto.GetType().Name}], contents [{dto.ToJsv().Left(300)}], current fail count [{_deferredSendFailuresSinceSuccess}]"))
            {
                _deferredSendFailuresSinceSuccess++;
            }

            // Increment attempt here before the wait calc purposely...
            attempt++;

            var minWait = _deferredSendFailuresSinceSuccess > 10
                              ? 10
                              : Math.Max((int)_deferredSendFailuresSinceSuccess, attempt);

            if (minWait <= 0)
            {
                minWait = 1;
            }

            await Task.Delay(RandomProvider.GetRandomIntBeween(minWait, (int)(minWait * 2).MinGz(15)) * 1000);
        } while (attempt <= 4);
    }
}
