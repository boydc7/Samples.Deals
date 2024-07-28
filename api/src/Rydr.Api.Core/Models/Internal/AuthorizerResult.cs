using Rydr.Api.Core.Enums;

namespace Rydr.Api.Core.Models.Internal;

public class AuthorizerResult
{
    public Exception AuthException { get; set; }
    public AuthorizerFailLevel FailLevel { get; set; }

    public static readonly AuthorizerResult Unspecified = new()
                                                          {
                                                              FailLevel = AuthorizerFailLevel.Unspecified
                                                          };

    public static readonly Task<AuthorizerResult> UnspecifiedAsync = Task.FromResult(Unspecified);

    public static readonly AuthorizerResult ExplicitlyAuthorized = new()
                                                                   {
                                                                       FailLevel = AuthorizerFailLevel.ExplicitlyAuthorized
                                                                   };

    public static readonly AuthorizerResult ExplicitRead = new()
                                                           {
                                                               FailLevel = AuthorizerFailLevel.ExplicitRead
                                                           };

    public static readonly Task<AuthorizerResult> ExplicitReadAsync = Task.FromResult(new AuthorizerResult
                                                                                      {
                                                                                          FailLevel = AuthorizerFailLevel.ExplicitRead
                                                                                      });

    public static readonly Task<AuthorizerResult> ExplicitlyAuthorizedAsync = Task.FromResult(ExplicitlyAuthorized);

    public static AuthorizerResult Unauthorized(string message) => new()
                                                                   {
                                                                       FailLevel = AuthorizerFailLevel.Unauthorized,
                                                                       AuthException = new UnauthorizedException(message)
                                                                   };

    public static Task<AuthorizerResult> UnauthorizedAsync(string message) => Task.FromResult(Unauthorized(message));

    public static AuthorizerResult InvalidData(string message) => new()
                                                                  {
                                                                      FailLevel = AuthorizerFailLevel.Unauthorized,
                                                                      AuthException = new InvalidDataArgumentException(message)
                                                                  };

    public static Task<AuthorizerResult> InvalidDataAsync(string message) => Task.FromResult(InvalidData(message));

    public static AuthorizerResult UnauthorizedIf(bool unauthorizedIf)
        => unauthorizedIf
               ? Unauthorized("You do not have access to the resource requested")
               : Unspecified;
}
