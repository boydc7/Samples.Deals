using System.Runtime.Serialization;
using Rydr.FbSdk.Enums;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbDebugToken
{
    [DataMember(Name = "data")]
    public FbDebugTokenData Data { get; set; }
}

[DataContract]
public class FbDebugTokenData
{
    [DataMember(Name = "app_id")]
    public string AppId { get; set; }

    [DataMember(Name = "type")]
    public FbTokenType Type { get; set; }

    [DataMember(Name = "application")]
    public string Application { get; set; }

    [DataMember(Name = "expires_at")]
    public long ExpiresAt { get; set; }

    [DataMember(Name = "is_valid")]
    public bool IsValid { get; set; }

    [DataMember(Name = "issued_at")]
    public long IssuedAt { get; set; }

    [DataMember(Name = "metadata")]
    public Dictionary<string, string> MetaData { get; set; }

    [DataMember(Name = "scopes")]
    public List<string> Scopes { get; set; }

    [DataMember(Name = "user_id")]
    public long UserId { get; set; }
}
