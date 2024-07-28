namespace Rydr.Api.Dto.Shared;

public class Tag : IEquatable<Tag>
{
    public const string TagRydrExternalDeal = "rydrexternaldeal";
    public const string TagRydrInternalDeal = "rydrinternaldeal";
    public const string TagRydrCategory = "category";
    public const string TagRydrAccountManager = "rydracctmgr";

    public Tag() { }

    public Tag(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentNullException(nameof(value));
        }

        _key = string.IsNullOrWhiteSpace(key)
                   ? null
                   : key.Trim().ToLowerInvariant();

        _value = value.Trim().ToLowerInvariant();
    }

    private string _tagString;

    private string _key;

    public string Key
    {
        get => _key;
        set
        {
            _key = string.IsNullOrWhiteSpace(value)
                       ? null
                       : value.Trim().ToLowerInvariant();

            _tagString = null;
        }
    }

    private string _value;

    public string Value
    {
        get => _value;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            _value = value.Trim().ToLowerInvariant();
            _tagString = null;
        }
    }

    public static implicit operator string(Tag t)
        => t?.ToString();

    public static implicit operator Tag(string ts)
    {
        if (string.IsNullOrWhiteSpace(ts))
        {
            throw new ArgumentNullException(nameof(ts));
        }

        return new Tag
               {
                   Value = ts.Trim().ToLowerInvariant()
               };
    }

    public override string ToString() => _tagString ??= string.Concat(_key,
                                                                      _key == null
                                                                          ? string.Empty
                                                                          : ":",
                                                                      _value);

    public bool Equals(Tag other)
        => other != null &&
           ToString().Equals(other.ToString(), StringComparison.OrdinalIgnoreCase);

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

        return obj is Tag thisObj && Equals(thisObj);
    }

    public override int GetHashCode() => ToString().GetHashCode();

    private sealed class TagDictionaryEqualityComparer : IEqualityComparer<KeyValuePair<string, string>>
    {
        public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
            => string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(KeyValuePair<string, string> obj)
            => unchecked(obj.Key.GetHashCode() + obj.Value.GetHashCode());
    }

    public static IEqualityComparer<KeyValuePair<string, string>> DefaultTagDictionaryComparer { get; } = new TagDictionaryEqualityComparer();
}
