namespace Rydr.FbSdk.Models;

public class FbValidateAccessResponse
{
    public bool IsTransientError { get; set; }
    public bool RequiresReAuthentication { get; set; }
    public bool Unauthorized { get; set; }

    public bool VerifiedAccess => !IsTransientError && !RequiresReAuthentication && !Unauthorized;
}
