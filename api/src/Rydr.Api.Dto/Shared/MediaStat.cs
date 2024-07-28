using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Dto.Shared;

public class MediaStat : IEquatable<MediaStat>
{
    public MediaStatType Type { get; set; }
    public string Value { get; set; }

    public bool Equals(MediaStat other)
        => other != null && Type == other.Type;

    public override bool Equals(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is MediaStat oobj && Equals(oobj);
    }

    public override int GetHashCode()
        => Type.GetHashCode();
}
