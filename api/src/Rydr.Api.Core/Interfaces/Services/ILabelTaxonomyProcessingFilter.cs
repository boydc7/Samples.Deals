using System.Threading.Tasks;
using Amazon.Rekognition.Model;
using Rydr.Api.Core.Models.Doc;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface ILabelTaxonomyProcessingFilter
    {
        Task<Label> LookupLabelAsync(Label labelEntity);
        Task<bool> ProcessIgnoreAsync(Label label);
        Task<DynMediaLabel> TryGetMediaLabelAsync(string name, string parentName);
        Task<DynMediaLabel> UpdateLabelAsync(string name, string parentName, bool? ignore, string rewriteName, string rewriteParent);
    }
}
