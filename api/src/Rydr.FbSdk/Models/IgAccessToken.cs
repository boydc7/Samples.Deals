using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class IgAccessToken
    {
        [DataMember(Name = "client_id")]
        public string ClientId { get; set; }

        [DataMember(Name = "client_secret")]
        public string ClientSecret { get; set; }

        [DataMember(Name = "code")]
        public string Code { get; set; }

        [DataMember(Name = "grant_type")]
        public string GrantType { get; set; }

        [DataMember(Name = "redirect_uri")]
        public string RedirectUrl { get; set; }
    }

    [DataContract]
    public class IgAccessTokenResponse
    {
        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        [DataMember(Name = "user_id")]
        public long UserId { get; set; }

        [DataMember(Name = "error_type")]
        public string ErrorType { get; set; }

        [DataMember(Name = "code")]
        public string Code { get; set; }

        [DataMember(Name = "error_message")]
        public string ErrorMessage { get; set; }

        public bool HasError() => !string.IsNullOrEmpty(ErrorType) || !string.IsNullOrEmpty(Code) || !string.IsNullOrEmpty(ErrorMessage);
    }

    [DataContract]
    public class IgLongLivedAccessTokenResponse
    {
        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        [DataMember(Name = "token_type")]
        public string TokenType { get; set; }

        [DataMember(Name = "expires_in")]
        public int ExpiresInSeconds { get; set; }

        public bool IsValid() => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(TokenType);
    }
}
