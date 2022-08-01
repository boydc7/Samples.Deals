using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Enums
{
    [EnumAsInt]
    public enum UserType
    {
        Unknown, // 0
        User, // 1
        Admin // 2
    }

    [EnumAsInt]
    public enum GenderType
    {
        Unknown,
        Male,
        Female,
        Other
    }
}
