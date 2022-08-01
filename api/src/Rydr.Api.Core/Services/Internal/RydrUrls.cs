using System;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;

namespace Rydr.Api.Core.Services.Internal
{
    public static class RydrUrls
    {
        static RydrUrls()
        {
            var whCustomPath = RydrEnvironment.GetAppSetting("Environment.SelfHost.WebHostCustomPath", "");

            WebHostCustomPath = whCustomPath.HasValue()
                                    ? whCustomPath.Replace("/", string.Empty)
                                    : null;

            var clientUrlString = RydrEnvironment.GetAppSetting("Environment.Client.UrlRoot");

            ClientRootUri = clientUrlString.HasValue()
                                ? new UriBuilder(clientUrlString).Uri
                                : null;

            var webHostUrlString = RydrEnvironment.IsLocalEnvironment || !WebHostCustomPath.HasValue() || ClientRootUri == null
                                       ? RydrEnvironment.GetAppSetting("Environment.WebHostUrl", "http://localhost")
                                       : string.Concat(ClientRootUri.AbsoluteUri.AppendIfNotEndsWith("/"), WebHostCustomPath);

            WebHostUri = new UriBuilder(webHostUrlString).Uri;
        }

        public static string WebHostCustomPath { get; }
        public static Uri WebHostUri { get; }
        public static Uri ClientRootUri { get; }
    }
}
