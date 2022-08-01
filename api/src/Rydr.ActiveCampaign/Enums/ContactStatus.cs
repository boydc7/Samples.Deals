using ServiceStack.DataAnnotations;

namespace Rydr.ActiveCampaign.Enums
{
    [EnumAsInt]
    public enum ContactStatus
    {
        Any = -1,
        Unconfirmed = 0,
        Active = 1,
        Unsubscribed = 2,
        Bounced = 3,
    }
}
