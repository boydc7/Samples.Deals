using System;
using ServiceStack.Host;

namespace Rydr.Api.Core.Models.Internal
{
    public class RydrBasicRequest : BasicRequest
    {
        public RydrBasicRequest()
        {
            RequestKey = RydrRequestKey;
        }

        public Guid RequestKey { get; }

        public static Guid RydrRequestKey { get; } = Guid.NewGuid();
    }
}
