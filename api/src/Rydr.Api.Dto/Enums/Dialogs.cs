using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Enums
{
    [EnumAsInt]
    public enum DialogType
    {
        Unknown,
        OneToOne,
        Group,
        Channel
    }
}
