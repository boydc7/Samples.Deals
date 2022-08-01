using System.Threading.Tasks;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IDeferredAffectedProcessingService
    {
        Task ProcessAsync(PostDeferredAffected request);
    }
}
