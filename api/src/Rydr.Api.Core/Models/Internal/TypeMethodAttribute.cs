using Rydr.Api.Core.Extensions;
using Rydr.FbSdk.Extensions;

namespace Rydr.Api.Core.Models.Internal;

public class TypeMethodAttribute : IEquatable<TypeMethodAttribute>
{
    public TypeMethodAttribute(Type type, Type attributeType, string methodName = null, Type methodParam = null)
    {
        Type = type;
        AttributeType = attributeType;
        MethodName = methodName ?? string.Empty;
        MethodParamType = methodParam;
    }

    public Type Type { get; }
    public Type AttributeType { get; }
    public string MethodName { get; }
    public Type MethodParamType { get; }

    public bool Equals(TypeMethodAttribute other) => other != null &&
                                                     other.Type == Type &&
                                                     other.AttributeType == AttributeType &&
                                                     other.MethodName.EqualsOrdinalCi(MethodName) &&
                                                     ((other.MethodParamType == null && MethodParamType == null) ||
                                                      (other.MethodParamType != null && MethodParamType != null && other.MethodParamType == MethodParamType));

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

        var typeMethod = obj as TypeMethodAttribute;

        return Equals(typeMethod);
    }

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(ToString());

    private string _stringId;

    public override string ToString() => _stringId ?? (_stringId = string.Concat(Type.FullName, "|", MethodName, "|", AttributeType.FullName, "|", MethodParamType?.FullName ?? "no-method-param-type").ToShaBase64());
}
