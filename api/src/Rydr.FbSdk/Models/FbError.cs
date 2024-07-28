using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbErrorResponse
{
    [DataMember(Name = "error")]
    public FbError Error { get; set; }
}

[DataContract]
public class FbError
{
    [DataMember(Name = "message")]
    public string Message { get; set; }

    [DataMember(Name = "type")]
    public string Type { get; set; }

    [DataMember(Name = "code")]
    public long Code { get; set; }

    [DataMember(Name = "error_subcode")]
    public long ErrorSubcode { get; set; }

    [DataMember(Name = "fbtrace_id")]
    public string FbTraceId { get; set; }

    [DataMember(Name = "is_transient")]
    public bool IsTransient { get; set; }

    [DataMember(Name = "error_user_title")]
    public string ErrorUserTitle { get; set; }

    [DataMember(Name = "error_user_msg")]
    public string ErrorUserMessage { get; set; }
}
