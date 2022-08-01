using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Amazon.S3;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;
using ServiceStack.OrmLite.Dapper;

// ReSharper disable CoVariantArrayConversion

namespace Rydr.Api.Core.Extensions
{
    public static class ObjectExtensions
    {
        private static readonly ConcurrentDictionary<string, bool> _implementsInterfaceMap = new ConcurrentDictionary<string, bool>();
        private static readonly ConcurrentDictionary<string, List<PropertyInfo>> _typePropertyMap = new ConcurrentDictionary<string, List<PropertyInfo>>();
        private static readonly ConcurrentDictionary<TypeMethodAttribute, Attribute[]> _typeMethodAttributeMap = new ConcurrentDictionary<TypeMethodAttribute, Attribute[]>();
        private static readonly ConcurrentDictionary<string, Attribute[]> _propInfoAttributeMap = new ConcurrentDictionary<string, Attribute[]>();

        public static T? NullIf<T>(this T? source, T nullIf)
            where T : struct
            => NullIf(source, d => d.Equals(nullIf));

        public static T? NullIf<T>(this T? source, Func<T, bool> nullIf)
            where T : struct
            => source.HasValue
                   ? nullIf(source.Value)
                         ? null
                         : source
                   : null;

        public static bool IsDialogOrMessageNotification(this ServerNotificationType source)
            => source == ServerNotificationType.Dialog || source == ServerNotificationType.Message;

        public static S3StorageClass ToS3StorageClass(this FileStorageClass storageClass)
        {
            switch (storageClass)
            {
                case FileStorageClass.Standard:

                    return RydrEnvironment.IsReleaseEnvironment
                               ? S3StorageClass.IntelligentTiering
                               : S3StorageClass.Standard;

                case FileStorageClass.Intelligent:

                    return S3StorageClass.IntelligentTiering;

                case FileStorageClass.InfrequentAccess:

                    return RydrEnvironment.IsDevelopmentEnvironment
                               ? S3StorageClass.OneZoneInfrequentAccess
                               : S3StorageClass.StandardInfrequentAccess;

                case FileStorageClass.Archive:

                    return S3StorageClass.Glacier;

                default:

                    throw new ArgumentOutOfRangeException($"No mapepd S3StorageClass for FileStorageClass of [{storageClass}]");
            }
        }

        public static void AsVoid<T>(this T result)
        {
            // Here to simply cast a result as void return...not much can go wrong here...
        }

        public static ValueWrap<long> AsValueWrap(this long result)
            => new ValueWrap<long>
               {
                   Value = result
               };

        public static LongIdResponse ToLongIdResponse(this long result)
            => new LongIdResponse
               {
                   Id = result
               };

        public static LongIdResponse ToLongIdResponse<T>(this T result)
            where T : IHasId<long>
            => new LongIdResponse
               {
                   Id = result.Id
               };

        public static Task<LongIdResponse> ToLongIdResponse<T>(this Task<T> model)
            where T : IHasId<long>
            => model.Then(m => new LongIdResponse
                               {
                                   Id = m.Id
                               });

        public static bool ImplementsInterface<TInterface>(this Type type)
        {
            var iType = typeof(TInterface);
            var cachedKey = string.Concat(type.FullName, "::", iType.FullName);

            var implements = _implementsInterfaceMap.GetOrAdd(cachedKey, k => type.HasInterface(iType));

            return implements;
        }

        public static bool HasIdentityId(this Type dbModel)
            => dbModel.ImplementsInterface<IHasLongIdentity>() || dbModel.ImplementsInterface<IHasIdentity>();

        public static bool IsCompilerGenerated(this Type t)
        {
            if (t == null)
            {
                return false;
            }

            return t.IsDefined(typeof(CompilerGeneratedAttribute), false) ||
                   IsCompilerGenerated(t.DeclaringType);
        }

        public static bool IsNull<T>(this T value)
        {
            var type = typeof(T);

            return (type.IsClass || Nullable.GetUnderlyingType(type) != null) &&
                   EqualityComparer<T>.Default.Equals(value, default);
        }

        public static bool IsDefault<T>(this T value) => EqualityComparer<T>.Default.Equals(value, default);

        public static void AddIfNotExists<TKey, TValue>(this Dictionary<TKey, TValue> map, TKey key, TValue value)
        {
            if (map.ContainsKey(key))
            {
                return;
            }

            map[key] = value;
        }

        public static void Flush<T>(this IEnumerable<T> source)
        {
            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            source.Count();
        }

        public static void TryDispose(this IDisposable obj)
        {
            if (obj == null)
            {
                return;
            }

            try
            {
                obj?.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        public static void TryClose(this Stream obj)
        {
            if (obj == null)
            {
                return;
            }

            try
            {
                obj?.Close();
            }
            catch
            {
                // ignored
            }
        }

        public static void TryClose(this IDbConnection obj)
        {
            if (obj == null)
            {
                return;
            }

            try
            {
                obj?.Close();
            }
            catch
            {
                // ignored
            }
        }

        public static IEnumerable<TAttr> GetAllAttributes<TAttr>(this Type from, string methodName = null, Type methodParam = null)
            where TAttr : Attribute
        {
            var tm = new TypeMethodAttribute(from, typeof(TAttr), methodName, methodParam);

            var attributes = _typeMethodAttributeMap.GetOrAdd(tm, k =>
                                                                  {
                                                                      if (!k.MethodName.HasValue())
                                                                      {
                                                                          return from.AllAttributes<TAttr>();
                                                                      }

                                                                      MethodInfo mi = null;

                                                                      try
                                                                      {
                                                                          mi = from.GetMethod(k.MethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                                                                      }
                                                                      catch(AmbiguousMatchException)
                                                                      {
                                                                          if (methodParam != null)
                                                                          {
                                                                              mi = from.GetMethod(k.MethodName,
                                                                                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase,
                                                                                                  null,
                                                                                                  new[]
                                                                                                  {
                                                                                                      methodParam
                                                                                                  },
                                                                                                  null);
                                                                          }
                                                                      }

                                                                      if (mi?.AllAttributes<TAttr>()?.Length > 0)
                                                                      {
                                                                          return mi.AllAttributes<TAttr>();
                                                                      }

                                                                      return from.AllAttributes<TAttr>();
                                                                  });

            return attributes.SafeCast<TAttr>();
        }

        public static IEnumerable<TAttr> GetAllAttributes<TAttr>(this PropertyInfo from)
            where TAttr : Attribute
        {
            var attributes = _propInfoAttributeMap.GetOrAdd(string.Concat(from.DeclaringType.Namespace, "|",
                                                                          from.DeclaringType.Name, "|",
                                                                          from.Name),
                                                            k => from.AllAttributes<TAttr>());

            return attributes.SafeCast<TAttr>();
        }

        public static bool HasAttributeRydr<TAttr>(this Type from)
            where TAttr : Attribute
            => GetFirstAttribute<TAttr>(from) != null;

        public static TAttr GetFirstAttribute<TAttr>(this Type from, string methodName = null, Type methodParam = null)
            where TAttr : Attribute
            => GetAllAttributes<TAttr>(from, methodName, methodParam).FirstOrDefault();

        public static TAttr GetFirstAttribute<TAttr>(this PropertyInfo from)
            where TAttr : Attribute
            => GetAllAttributes<TAttr>(from).FirstOrDefault();

        public static List<PropertyInfo> GetPublicTypeProperties(this Type from, params Type[] attributesToExclude)
        {
            var cacheKey = string.Concat(from.FullName, "_", string.Join("_", attributesToExclude?.Select(a => a.Name).DefaultIfEmpty("ALL") ?? new[]
                                                                                                                                                {
                                                                                                                                                    "ALL"
                                                                                                                                                })).ToShaBase64();

            var props = _typePropertyMap.GetOrAdd(cacheKey,
                                                  k =>
                                                  {
                                                      var ip = from.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                                                   .Where(p => attributesToExclude == null ||
                                                                               !attributesToExclude.Any(x => Attribute.IsDefined(p, x)))
                                                                   .ToList();

                                                      return ip;
                                                  });

            return props;
        }

        public static List<PropertyInfo> GetPublicTypePropertiesWithAttributes(this Type from, params Type[] withAnyOfTheseAttributes)
        {
            Guard.AgainstNullArgument(withAnyOfTheseAttributes == null || withAnyOfTheseAttributes.Length <= 0, "withAnyOfTheseAttributes");

            var cacheKey = string.Concat(from.FullName, "_WITHATTRIBUTES_", string.Join("_", withAnyOfTheseAttributes.Select(a => a.Name))).ToShaBase64();

            var props = _typePropertyMap.GetOrAdd(cacheKey,
                                                  k =>
                                                  {
                                                      var ip = from.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                                                   .Where(p => withAnyOfTheseAttributes.Any(x => Attribute.IsDefined(p, x)))
                                                                   .ToList();

                                                      return ip;
                                                  });

            return props;
        }

        public static PropertyInfo GetPublicProperty(this Type from, string propertyName)
        {
            var cacheKey = string.Concat(from.FullName, "_PUBLICPROP_", propertyName);

            var prop = _typePropertyMap.GetOrAdd(cacheKey,
                                                 k =>
                                                 {
                                                     var ip = from.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                                                     return ip?.AsEnumerable().AsList();
                                                 });

            return prop?.SingleOrDefault();
        }

        public static bool HasLoadReferences<T>()
            => HasLoadReferences(typeof(T));

        public static bool HasLoadReferences(this Type type)
            => !type.GetPublicTypePropertiesWithAttributes(typeof(ReferenceAttribute), typeof(ReferencesAttribute)).IsNullOrEmpty();

        public static IEnumerable<Type> GetTypesInNamespace(this Assembly assembly, string namespaceToLoad)
            => assembly.GetTypes().Where(t => t.Namespace.StartsWithOrdinalCi(namespaceToLoad));

        public static void SetAttributedProperties<TAttr, TIdType, TSetType>(this object instance, Func<TAttr, string> idPropertyFactory, Func<TIdType, TSetType> setValueFactory)
            where TAttr : Attribute
        {
            var instanceType = instance.GetType();

            foreach (var propertyInfo in instanceType.GetPublicTypePropertiesWithAttributes(typeof(TAttr)))
            {
                var attr = propertyInfo.GetFirstAttribute<TAttr>();

                if (attr == null)
                {
                    continue;
                }

                var idProperty = instanceType.GetPublicProperty(idPropertyFactory(attr));

                if (idProperty == null)
                {
                    continue;
                }

                if (!Try.Get(() => (TIdType)idProperty.GetValue(instance), out var idValue) || idValue == null)
                {
                    return;
                }

                if (!Try.Get(() => setValueFactory(idValue), out var setValue) || setValue == null)
                {
                    return;
                }

                Try.Exec(() => propertyInfo.SetValue(instance, setValue));
            }
        }
    }
}
