using System.Collections.Concurrent;
using Funq;
using ServiceStack.Configuration;

namespace Rydr.Api.Core.Configuration
{
    public static class RydrEnvironment
    {
        private static bool? _enableDebug;

        public static Container Container { get; private set; }
        public static void SetContainer(Container container) => Container = container;

        public static string AdminKey { get; private set; }
        public static void SetAdminKey(string key) => AdminKey = key;

#pragma warning disable 162
        public static IAppSettings AppSettings { get; private set; }
        public static void SetAppSettings(IAppSettings appSettings) => AppSettings = appSettings;

        public static string CurrentEnvironment => AppSettings.GetString("Environment.RydrEnvironment") ?? "Local";

        public static bool IsDebugEnabled => IsDevelopmentEnvironment || IsLocalEnvironment || (_enableDebug ?? (_enableDebug = AppSettings.Get("Environment.EnableDebug", false)).Value);

        public static bool IsTestEnvironment
        {
            get
            {
#if TEST
                return true;
#endif
                return AppSettings.Get("Environment.IsTest", false);
            }
        }

        public static bool IsReleaseEnvironment
        {
            get
            {
#if (REMOTE && PRODUCTION)
                return true;
#endif

                return !IsDevelopmentEnvironment && !IsLocalEnvironment && !IsTestEnvironment;
            }
        }

        public static bool IsDevelopmentEnvironment
        {
            get
            {
#if DEBUG
                return true;
#endif

                return AppSettings.Get("Environment.IsDevelopment", true);
            }
        }

        public static bool IsLocalEnvironment
        {
            get
            {
#if LOCAL || LOCALDOCKER
                return true;
#endif

                return AppSettings.Get("Environment.IsLocal", true);
            }
        }

        public static T GetAppSetting<T>(string key, T defaultIfMissing = default) => AppSettings.Get(key, defaultIfMissing);

        public static string GetAppSetting(string key) => AppSettings.Get<string>(key, null);

        private static readonly ConcurrentDictionary<string, string> _connectionStrings = new ConcurrentDictionary<string, string>();

        public static string GetConnectionString(string key, string defaultIfMissing = null)
            => _connectionStrings.GetOrAdd(key, k => GetAppSetting(k, defaultIfMissing));
    }

#pragma warning restore 162
}
