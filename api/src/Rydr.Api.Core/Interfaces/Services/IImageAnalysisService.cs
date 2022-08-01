using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Rekognition.Model;
using Rydr.Api.Core.Models;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IImageAnalysisService
    {
        Task<List<FaceDetail>> GetFacesAsync(FileMetaData fileMeta);
        Task<List<TextDetection>> GetTextAsync(FileMetaData fileMeta);
        Task<List<ModerationLabel>> GetImageModerationsAsync(FileMetaData fileMeta, string humanLoopIdentifier);
        Task<List<Label>> GetImageLabelsAsync(FileMetaData fileMeta);
    }
}
