using Rydr.FbSdk.Configuration;
using ServiceStack.Logging;

namespace Rydr.FbSdk;

public partial class FacebookClient : BaseFacebookClient, IFacebookClient
{
    private const string _baseGraphUrl = "https://graph.facebook.com/";

    private static readonly ILog _log = LogManager.GetLogger("FacebookClient");
    private static readonly string _baseAuthDialogUrl;
    private static readonly string _defaultBaseGraphApiUrl;

    static FacebookClient()
    {
        FacebookSdkConfig.Configure();

        ApiVersion = string.IsNullOrEmpty(FacebookSdkConfig.DefaultApiVersion)
                         ? "v6.0"
                         : FacebookSdkConfig.DefaultApiVersion;

        _baseAuthDialogUrl = string.Concat("https://www.facebook.com/", ApiVersion, "/dialog/oauth/");
        _defaultBaseGraphApiUrl = string.Concat(_baseGraphUrl, ApiVersion, "/");
    }

    public FacebookClient(string appId, string appSecret, string accessToken)
        : base(_defaultBaseGraphApiUrl, appId, appSecret, accessToken) { }

    public static string ApiVersion { get; }

    public static FacebookClientFactory Factory => FacebookClientFactory.Instance;
}
