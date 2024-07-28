namespace Rydr.Api.Dto.Enums;

public enum ServerNotificationMedium
{ // i.e. how to notify someone when they get a message (i.e. send a text, an email/message, etc.)
    Unspecified,
    Email,
    AppleApn,
    AppleEnterpriseApn,
    AndroidGcm
}
