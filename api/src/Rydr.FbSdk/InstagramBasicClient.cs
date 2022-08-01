using Rydr.FbSdk.Configuration;

namespace Rydr.FbSdk
{
    public partial class InstagramBasicClient : BaseFacebookClient, IInstagramBasicClient
    {
        private const string _baseGraphUrl = "https://graph.instagram.com/";

        private static readonly string _defaultBaseGraphApiUrl;

        static InstagramBasicClient()
        {
            FacebookSdkConfig.Configure();

            BaseAuthDialogUrl = "https://api.instagram.com/";
            _defaultBaseGraphApiUrl = _baseGraphUrl;
        }

        public InstagramBasicClient(string appId, string appSecret, string accessToken)
            : base(_defaultBaseGraphApiUrl, appId, appSecret, accessToken) { }

        public static InstagramBaseClientFactory Factory => InstagramBaseClientFactory.Instance;
    }
}
