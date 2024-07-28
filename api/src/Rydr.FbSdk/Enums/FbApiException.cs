using Rydr.FbSdk.Extensions;
using Rydr.FbSdk.Models;

namespace Rydr.FbSdk.Enums;

public class FbApiException : Exception
{
    private static readonly HashSet<long> _accessTokenRefreshRequiredCodes = new()
                                                                             {
                                                                                 190,
                                                                                 102
                                                                             };

    private static readonly HashSet<long> _accessTokenRefreshRequiredSubCodes = new()
                                                                                {
                                                                                    458,
                                                                                    459,
                                                                                    460,
                                                                                    463,
                                                                                    464,
                                                                                    467,
                                                                                    492
                                                                                };

    private static readonly HashSet<long> _permissionErrorCodes = new()
                                                                  {
                                                                      33,
                                                                      100,
                                                                      2018001,
                                                                      2018164,
                                                                      10303
                                                                  };

    public FbApiException() { }

    public FbApiException(string message, FbError fbError, Exception inner = null, string url = null)
        : base(ToErrorMessage(fbError, message, url), inner)
    {
        FbError = fbError;
        Url = url;
    }

    public string Url { get; set; }
    public FbError FbError { get; }

    public virtual bool IsTransient => FbError?.IsTransient ?? false;
    public virtual long FbErrorCode => FbError?.Code ?? 0;

    public virtual bool IsApiStepPermissionError => FbError != null &&
                                                    !FbError.IsTransient &&
                                                    !RequiresOAuthRefresh &&
                                                    (
                                                        IsPermissionError
                                                        ||
                                                        (FbError.Code == 10 ||
                                                         (FbError.Code >= 200 && FbError.Code <= 299) ||
                                                         (FbError.Code == 100 && FbError.ErrorSubcode == 33))
                                                    );

    public virtual bool IsPermissionError => FbError?.Message != null &&
                                             !FbError.IsTransient &&
                                             (
                                                 RequiresOAuthRefresh
                                                 ||
                                                 (_permissionErrorCodes.Contains(FbError.Code) &&
                                                  _permissionErrorCodes.Contains(FbError.ErrorSubcode))
                                             );

    public virtual bool RequiresOAuthRefresh => FbError != null &&
                                                !FbError.IsTransient &&
                                                (_accessTokenRefreshRequiredCodes.Contains(FbError.Code) ||
                                                 _accessTokenRefreshRequiredSubCodes.Contains(FbError.ErrorSubcode)) &&
                                                FbError.Type != null &&
                                                FbError.Type.Equals("OAuthException", StringComparison.OrdinalIgnoreCase);

    private static string ToErrorMessage(FbError fbError, string rawMessage, string url)
        => $"FbApiException - [{fbError?.Message ?? rawMessage}], Code [{fbError?.Code ?? -1}], Subcode [{fbError?.ErrorSubcode ?? -1}], Type [{fbError?.Type ?? "N/A"}], Transient [{fbError?.IsTransient.ToString() ?? "N/A"}], Url [{url.Left(100)}]";

    public override string ToString() => ToErrorMessage(FbError, "N/A", Url);
}

public class FbApiAggregateException : FbApiException
{
    private readonly IReadOnlyList<FbApiException> _fbApiExceptions;

    public FbApiAggregateException(IReadOnlyList<FbApiException> fbApiExceptions)
    {
        _fbApiExceptions = fbApiExceptions;

        if (_fbApiExceptions == null || _fbApiExceptions.Count <= 0)
        {
            throw new ArgumentNullException(nameof(fbApiExceptions));
        }
    }

    public int Count => _fbApiExceptions.Count;

    public override bool IsApiStepPermissionError => _fbApiExceptions.Any(f => f.IsApiStepPermissionError);

    public override long FbErrorCode => _fbApiExceptions.FirstOrDefault(f => f.FbErrorCode > 0)?.FbErrorCode ?? 0;

    public override bool IsTransient => _fbApiExceptions.Any(f => f.IsTransient);

    public override bool IsPermissionError => _fbApiExceptions.Any(f => f.IsPermissionError);

    public override bool RequiresOAuthRefresh => _fbApiExceptions.Any(f => f.RequiresOAuthRefresh);

    public override string ToString() => $"Aggregate FbApiExceptions ({_fbApiExceptions.Count}). First = [{_fbApiExceptions.First()}]";
}

public class FbMediaCreatedBeforeBusinessConversion : Exception
{
    public FbMediaCreatedBeforeBusinessConversion(FbApiException fbx) : base("Cannot get insights for media that was created before account was converted to business account", fbx) { }
}
