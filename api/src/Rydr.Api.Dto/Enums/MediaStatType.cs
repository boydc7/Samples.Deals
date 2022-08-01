using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Enums
{
    [EnumAsInt]
    public enum MediaStatType
    {
        Unknown, // 0
        MediaCount,
        Followers,
        FollowedBy
    }
}
