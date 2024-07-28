using Rydr.Api.Core.Models;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IVideoConversionService
{
    Task ConvertAsync(FileMetaData inputFileMeta, string outputDestination);
    Task<FileConvertStatus> GetStatusAsync(FileMetaData inputFileMeta, string outputDestination);
}
