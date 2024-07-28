namespace Rydr.Api.Core.Interfaces.Models;

public interface IPublisherAccessToken
{
    string AccessToken { get; set; }
    int Expires { get; set; }
    string TokenType { get; set; }
}
