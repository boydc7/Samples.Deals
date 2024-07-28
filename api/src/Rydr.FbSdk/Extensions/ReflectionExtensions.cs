using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using ServiceStack;

// ReSharper disable CoVariantArrayConversion

namespace Rydr.FbSdk.Extensions;

public static class ReflectionExtensions
{
    private static readonly ConcurrentDictionary<Type, List<string>> _dataMemberNamesMap = new();
    private static readonly ConcurrentDictionary<string, List<PropertyInfo>> _typePropertyMap = new();
    private static readonly ConcurrentDictionary<string, Attribute[]> _propInfoAttributeMap = new();

    public static List<string> GetAllDataMemberNames(this Type forType)
    {
        if (_dataMemberNamesMap.TryGetValue(forType, out var memberList))
        {
            return memberList;
        }

        var nameMap = _dataMemberNamesMap.GetOrAdd(forType,
                                                   t =>
                                                   {
                                                       var propInfos = GetPublicTypePropertiesWithAttributes(t, typeof(DataMemberAttribute));

                                                       return propInfos.Select(GetFirstAttribute<DataMemberAttribute>)
                                                                       .Where(dma => !string.IsNullOrEmpty(dma?.Name))
                                                                       .Select(dma => dma.Name)
                                                                       .ToList();
                                                   });

        return nameMap;
    }

    public static List<PropertyInfo> GetPublicTypePropertiesWithAttributes(this Type from, params Type[] withAnyOfTheseAttributes)
    {
        var cacheKey = string.Concat(from.FullName, withAnyOfTheseAttributes == null || withAnyOfTheseAttributes.Length <= 0
                                                        ? string.Empty
                                                        : string.Concat("_WITHATTRIBUTES_", string.Join("_", withAnyOfTheseAttributes.Select(a => a.Name))))
                             .ToShaBase64();

        var props = _typePropertyMap.GetOrAdd(cacheKey,
                                              k => from.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                                       .Where(p => withAnyOfTheseAttributes.Any(x => Attribute.IsDefined(p, x)))
                                                       .ToList());

        return props;
    }

    public static IEnumerable<TAttr> GetAllAttributes<TAttr>(this PropertyInfo from)
        where TAttr : Attribute
    {
        var attributes = _propInfoAttributeMap.GetOrAdd(string.Concat(from.DeclaringType.Namespace, "|",
                                                                      from.DeclaringType.Name, "|",
                                                                      from.Name),
                                                        k => from.AllAttributes<TAttr>());

        return attributes == null || attributes.Length <= 0
                   ? Enumerable.Empty<TAttr>()
                   : attributes.Select(a => a as TAttr)
                               .Where(ta => ta != null);
    }

    public static TAttr GetFirstAttribute<TAttr>(this PropertyInfo from)
        where TAttr : Attribute
        => GetAllAttributes<TAttr>(from).FirstOrDefault();
}
