using System;
using Rydr.ActiveCampaign.Models;

namespace Rydr.ActiveCampaign.Enums
{
    public class AcApiException : Exception
    {
        public AcApiException() { }

        public AcApiException(string message, AcErrors acErrors, Exception inner = null, string url = null)
            : base(ToErrorMessage(acErrors, message, url), inner)
        {
            AcErrors = acErrors;
            Url = url;
        }

        public string Url { get; set; }
        public AcErrors AcErrors { get; }

        public static string ToErrorMessage(AcErrors acErrors, string rawMessage, string url)
            => $"AcApiException - Count [{acErrors?.Errors?.Count ?? 0}], Messages [{((acErrors?.Errors) == null || acErrors.Errors.Count <= 0 ? rawMessage : string.Join('|', acErrors?.Errors))}], Url [{url.Left(100)}]";

        public override string ToString() => ToErrorMessage(AcErrors, "N/A", Url);
    }
}
