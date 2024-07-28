using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Enums;

[EnumAsInt]
public enum PublisherType
{
    Unknown = 0,
    Facebook = 1,
    Instagram = 2,
    Google = 3,
    Rydr = 4,
    Firebase = 5,
    Apple = 6
}

[EnumAsInt]
public enum PublisherContentType
{
    Unknown = 0,
    Post = 1,
    Story = 2,
    Media = 3
}

[EnumAsInt]
public enum PublisherAccountType
{
    Unknown,
    User,
    FbIgUser,
    Page,
    SystemUser
}

public enum PublisherLinkType
{
    None,
    Basic,
    Full
}

[EnumAsInt]
public enum PublisherAccountConnectionType
{
    Unspecified,
    Contacted,
    ContactedBy,
    DealtWith
}

[EnumAsInt]
public enum AudienceLocationType
{
    Unspecified,
    City,
    Country
}

[EnumAsInt]
public enum HashtagType
{
    Unspecified,
    Hashtag,
    Mention
}

public static class PublisherTypeHelpers
{
    public static bool IsPublicContentType(this PublisherContentType contentType)
        => contentType == PublisherContentType.Post;

    public static bool IsTimeBoxedContentType(this PublisherContentType contentType)
        => contentType == PublisherContentType.Story;

    public static bool IsUserAccount(this PublisherAccountType publisherAccountType)
        => publisherAccountType == PublisherAccountType.User || publisherAccountType == PublisherAccountType.SystemUser;

    public static bool IsSystemAccount(this PublisherAccountType publisherAccountType)
        => publisherAccountType == PublisherAccountType.SystemUser;

    public static PublisherType NonWritableAlternateAccountType(this PublisherType forType)
        => forType == PublisherType.Facebook
               ? PublisherType.Instagram
               : PublisherType.Unknown;

    public static PublisherType WritableAlternateAccountType(this PublisherType forType)
        => forType == PublisherType.Instagram
               ? PublisherType.Facebook
               : PublisherType.Unknown;

    public static bool IsWritablePublisherType(this PublisherType forType)
        => forType == PublisherType.Facebook;
}
