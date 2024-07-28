using MySql.Data.MySqlClient;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Shared;
using ServiceStack;

namespace Rydr.Api.Core.Services.Internal;

public class RydrSqlDeferredAffectedLookupsService : IDeferredAffectedProcessingService
{
    private readonly IRecordTypeRecordService _rtRecordService;

    public RydrSqlDeferredAffectedLookupsService(IRecordTypeRecordService rtRecordService)
    {
        _rtRecordService = rtRecordService;
    }

    public async Task ProcessAsync(PostDeferredAffected request)
    {
        var attempt = 1;
        Exception lastEx = null;

        do
        {
            try
            {
                if (request.CompositeIds.IsNullOrEmpty())
                {
                    await _rtRecordService.SaveRydrRecordsAsync(request.Type, request.Ids);
                }
                else
                {
                    await _rtRecordService.SaveRydrRecordsAsync(request.Type, request.CompositeIds);
                }

                return;
            }
            catch(MySqlException myx) when(myx.Message.ContainsAny(new[]
                                                                   {
                                                                       "Duplicate", "Deadlock"
                                                                   }, StringComparison.OrdinalIgnoreCase))
            {
                Thread.Sleep(attempt * 750);
                lastEx = myx;
            }

            attempt++;
        } while (attempt < 4);

        throw lastEx;
    }
}
