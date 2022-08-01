using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Enums
{
    [EnumAsInt]
    public enum AddressType
    {
        Unspecified,
        Business,
        Work,
        Home,
        Other
    }
}
