using Rydr.Api.Core.Interfaces.Models;

namespace Rydr.Api.Core.Models.Supporting;

public class BasicPublisherAccessToken : IPublisherAccessToken
{
    public string AccessToken { get; set; }
    public int Expires { get; set; }
    public string TokenType { get; set; }
}
