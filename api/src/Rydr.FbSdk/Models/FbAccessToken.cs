using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class FbAccessToken
    {
        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        [DataMember(Name = "expires")]
        public int Expires { get; set; }

        [DataMember(Name = "token_type")]
        public string TokenType { get; set; }
    }

    public static class FacebookAccessToken
    {
        public const string RydrAppScopesString = "public_profile,email,instagram_basic,pages_show_list,instagram_manage_insights,manage_pages";
        public const string RydrAppIgBasicScopesString = "user_profile,user_media";

        public const string ScopeInstagramBasic = "instagram_basic";
        public const string ScopeBusinessManagement = "business_management";
        public const string ScopeReadInsights = "read_insights";
        public const string ScopePagesShowList = "pages_show_list";
        public const string ScopeInstagramManageInsights = "instagram_manage_insights";
        public const string ScopeUserAgeRange = "user_age_range";
        public const string ScopeManagePages = "manage_pages";
    }
}
