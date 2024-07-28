// ReSharper disable UnreachableCode

using Microsoft.Extensions.Hosting;

#pragma warning disable 162

namespace Rydr.Api.Core.Configuration;

public class RydrBuildEnvironment
{
#if LOCALDEBUG
    public const bool IsLocalDebug = true;
#else
        public const bool IsLocalDebug = false;
#endif

#if LOCALPRODUCTION
         public const bool IsLocalProd = true;
#else
    public const bool IsLocalProd = false;
#endif

#if LOCALDEVELOPMENT
        public const bool IsLocalDev = true;
#else
    public const bool IsLocalDev = false;
#endif

#if DEVELOPMENT
        public const bool IsDev = true;
#else
    public const bool IsDev = false;
#endif

#if (PRODUCTION || RELEASE)
        public const bool IsProd = true;
#else
    public const bool IsProd = false;
#endif

#if TEST
        public const bool IsTest = true;
#else
    public const bool IsTest = false;
#endif

#if LOCALDOCKER
        public const bool IsLocalDocker = true;
#else
    public const bool IsLocalDocker = false;
#endif

    public static readonly string EnvName = IsProd
                                                ? Environments.Production
                                                : Environments.Development;

    public const int ShutdownTimeSeconds = IsProd
                                               ? 300
                                               : IsDev
                                                   ? 900
                                                   : 3;

    public static readonly string[] ListenOn =
    {
        "http://0.0.0.0:2080", "http://[::]:2080"
    };

    public const ConfigurationName Configuration =
        IsLocalDebug
            ? ConfigurationName.Debug
            : IsTest
                ? ConfigurationName.Debug
                : IsLocalDev
                    ? ConfigurationName.LocalDevelopment
                    : IsLocalProd
                        ? ConfigurationName.LocalProduction
                        : IsLocalDocker
                            ? ConfigurationName.LocalDocker
                            : IsDev
                                ? ConfigurationName.Development
                                : IsProd
                                    ? ConfigurationName.Production
                                    : ConfigurationName.Development;
}

public enum ConfigurationName
{
    Debug,
    LocalDevelopment,
    LocalProduction,
    LocalDocker,
    Development,
    Production
}
