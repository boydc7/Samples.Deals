namespace Rydr.Api.Core.Enums;

public enum AuthorizerFailLevel
{
    Unspecified,
    ExplicitlyAuthorized,
    FailUnlessExplicitlyAuthorized,
    Unauthorized,
    ExplicitRead
}
