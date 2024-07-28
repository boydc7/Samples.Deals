using Amazon.Comprehend.Model;
using Rydr.Api.Core.Models.Supporting;

namespace Rydr.Api.Core.Interfaces.Services;

public interface ITextAnalysisService
{
    Task<string> GetDominantLanguageCodeAsync(string text);
    Task<List<Entity>> GetEntitiesAsync(string text, string languageCode = "en");
    Task<SentimentResult> GetSentimentAsync(string text, string languageCode = "en");
    Task<string> StartTopicModelingAsync(string s3Input, string s3Output);
}
