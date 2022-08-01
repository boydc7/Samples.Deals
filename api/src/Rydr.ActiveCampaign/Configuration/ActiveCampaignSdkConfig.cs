using System;
using System.Text;
using ServiceStack.Text;

#pragma warning disable 162

namespace Rydr.ActiveCampaign.Configuration
{
    public class ActiveCampaignSdkConfig
    {
        private static readonly object _lockObject = new object();
        private static bool _configured;
        public static bool ClientPoolingDisabled { get; set; }

        private static bool _useLoggedClient;

        public static bool UseLoggedClient
        {
            get
            {
#if DEBUG || LOCAL
                return true;
#endif

                return _useLoggedClient;
            }
            set => _useLoggedClient = value;
        }

        public static Func<(string accountName, string apiKey, string eventTrackingKey, string eventTrackingAcctId), IActiveCampaignClient> ClientFactory { get; set; } = null;

        public static void Configure()
        {
            if (_configured)
            {
                return;
            }

            try
            {
                lock(_lockObject)
                {
                    if (_configured)
                    {
                        return;
                    }

                    _configured = true;
                }

                JsConfig.TextCase = TextCase.CamelCase;
                JsConfig.DateHandler = DateHandler.ISO8601;
                JsConfig.AssumeUtc = true;

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch(Exception) when(ResetConfigured())
            { // Unreachable code
                throw;
            }
        }

        private static bool ResetConfigured()
        {
            _configured = false;

            return false;
        }
    }
}
