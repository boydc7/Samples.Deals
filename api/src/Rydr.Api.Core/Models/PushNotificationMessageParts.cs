namespace Rydr.Api.Core.Models;

public class PushNotificationMessageParts
{
    public string Title { get; set; }
    public string Body { get; set; }
    public object CustomObj { get; set; }
    public long Badge { get; set; }
    public bool IsBackgroundUpdate { get; set; }
}
