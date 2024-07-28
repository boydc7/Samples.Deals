using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IFileConversionStorageHandler
{
    FileMetaData GetDefaultConvertedFileMeta(long fileId, string fileExtension, char? dirSeparatorCharacter = null);

    FileMetaData GetConvertedFileMeta<T>(long fileId, string fileExtension, T convertArguments, char? dirSeparatorCharacter = null)
        where T : FileConvertTypeArgumentsBase;

    Task<FileMetaData> ConvertAndStoreAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
        where T : FileConvertTypeArgumentsBase;

    Task<FileConvertStatus> GetConvertStatusAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
        where T : FileConvertTypeArgumentsBase;
}
