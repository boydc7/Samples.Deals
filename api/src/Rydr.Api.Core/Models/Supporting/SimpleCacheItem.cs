using Rydr.Api.Core.Services.Internal;

namespace Rydr.Api.Core.Models.Supporting
{
    public class SimpleCacheItem
    {
        public long CreatedOnUtc { get; set; } = DateTimeHelper.UtcNowTs;
        public string Data { get; set; }
        public string ReferenceCode { get; set; }
        public long ReferenceId { get; set; }
    }
}
