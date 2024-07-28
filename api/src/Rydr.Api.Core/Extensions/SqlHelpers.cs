using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Rydr.FbSdk.Extensions;
using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.Extensions;

public static class SqlHelpers
{
    private static readonly Dictionary<string, string> _strings = new();
    private static readonly ConcurrentDictionary<string, PropertyInfo> _propertyNameMap = new();
    private static readonly ConcurrentDictionary<PropertyInfo, string> _propertyInfoAliasMap = new();

    private static string GetCacheString<T>(IOrmLiteDialectProvider dialectProvider, List<string> excludeProperties = null,
                                            string extraKeyPart = null, [CallerMemberName] string methodName = null)
    {
        if (excludeProperties == null)
        {
            excludeProperties = new List<string>();
        }

        var stringKey = string.Concat(methodName, "|", dialectProvider.GetType().Name, "|",
                                      typeof(T).Name,
                                      string.Join("|", excludeProperties), "|", extraKeyPart).ToShaBase64();

        return stringKey;
    }

    public static string GetSelectString<T>(IOrmLiteDialectProvider dialectProvider, bool aliasWithPropertyName,
                                            string fieldPrefix = null, List<string> excludeProperties = null,
                                            string aggregateFunction = null)
    {
        var haveAggregateFunction = aggregateFunction.HasValue();
        var haveFieldPrefix = fieldPrefix.HasValue();

        if (haveAggregateFunction && !aggregateFunction.EndsWith("("))
        {
            aggregateFunction = string.Concat(aggregateFunction.Trim(), "(");
        }

        if (haveFieldPrefix && !fieldPrefix.EndsWith("."))
        {
            fieldPrefix = string.Concat(fieldPrefix, ".");
        }

        var extraKeyPart = string.Concat(aliasWithPropertyName.ToString(), aggregateFunction, fieldPrefix);

        var stringKey = GetCacheString<T>(dialectProvider, excludeProperties, extraKeyPart);

        if (_strings.ContainsKey(stringKey))
        {
            return _strings[stringKey];
        }

        var sb = new StringBuilder();
        var first = true;

        foreach (var fieldDefinition in GetFieldDefinitions<T>(excludeProperties))
        {
            var fieldName = dialectProvider.GetQuotedColumnName(fieldDefinition.FieldName);

            sb.AppendFormat("{0}{1}{2}{3}{4}{5}",
                            first
                                ? string.Empty
                                : ", ",
                            haveAggregateFunction
                                ? aggregateFunction
                                : string.Empty,
                            haveFieldPrefix
                                ? fieldPrefix
                                : string.Empty,
                            fieldName,
                            haveAggregateFunction
                                ? ")"
                                : string.Empty,
                            aliasWithPropertyName
                                ? string.Concat(" AS ", fieldDefinition.Name)
                                : haveAggregateFunction
                                    ? string.Concat(" AS ", fieldName)
                                    : string.Empty);

            first = false;
        }

        _strings[stringKey] = sb.ToString();

        return _strings[stringKey];
    }

    public static string GetTableName<T>(IOrmLiteDialectProvider dialectProvider, bool withSchema = false)
    {
        var modelDef = ModelDefinition<T>.Definition;

        return GetTableName(dialectProvider, modelDef, withSchema);
    }

    public static string GetTableName(IOrmLiteDialectProvider dialectProvider, Type forModel, bool withSchema = false)
    {
        var modelDef = forModel.GetModelMetadata();

        return GetTableName(dialectProvider, modelDef, withSchema);
    }

    private static string GetTableName(IOrmLiteDialectProvider dialectProvider, ModelDefinition modelDefinition, bool withSchema = false)
    {
        var tableName = dialectProvider.GetQuotedTableName(modelDefinition.ModelName);

        var schema = modelDefinition.Schema;

        var schemaPart = withSchema && schema.HasValue()
                             ? string.Concat(schema, ".")
                             : string.Empty;

        return string.Concat(schemaPart, tableName);
    }

    public static string GetQuotedFieldValue<T, TValue>(TValue value, string propertyName, IOrmLiteDialectProvider dialectProvider)
    {
        var fieldDefinition = GetFieldDefinitions<T>().FirstOrDefault(f => f.Name.EqualsOrdinalCi(propertyName));

        return dialectProvider.GetQuotedValue(value, fieldDefinition.ColumnType);
    }

    public static string GetQuotedFieldValue<T>(T entity, string propertyName, IOrmLiteDialectProvider dialectProvider)
    {
        var fieldDefinition = GetFieldDefinitions<T>().FirstOrDefault(f => f.Name.EqualsOrdinalCi(propertyName));

        return fieldDefinition?.GetQuotedValue(entity, dialectProvider);
    }

    public static string GetValuesString<T>(T entity, IOrmLiteDialectProvider dialectProvider, List<string> excludeProperties = null)
    {
        var sb = new StringBuilder("(");
        var first = true;

        foreach (var fieldDefinition in GetFieldDefinitions<T>(excludeProperties).Where(f => !f.ShouldSkipInsert()))
        {
            var fieldValue = fieldDefinition.GetQuotedValue(entity, dialectProvider);

            sb.AppendFormat("{0}{1}",
                            first
                                ? string.Empty
                                : ",",
                            fieldValue);

            first = false;
        }

        sb.Append(")");

        return sb.ToString();
    }

    public static string GetInsertString<T>(IOrmLiteDialectProvider dialectProvider, List<string> excludeProperties = null)
    {
        var stringKey = GetCacheString<T>(dialectProvider, excludeProperties);

        if (_strings.ContainsKey(stringKey))
        {
            return _strings[stringKey];
        }

        var sb = new StringBuilder();
        var first = true;

        foreach (var fieldDefinition in GetFieldDefinitions<T>(excludeProperties).Where(f => !f.ShouldSkipInsert()))
        {
            var fieldName = dialectProvider.GetQuotedColumnName(fieldDefinition.FieldName);

            sb.AppendFormat("{0}{1}",
                            first
                                ? string.Empty
                                : ", ",
                            fieldName);

            first = false;
        }

        _strings[stringKey] = sb.ToString();

        return _strings[stringKey];
    }

    public static List<FieldDefinition> GetFieldDefinitions<T>(List<string> excludeProperties = null)
    {
        if (excludeProperties == null)
        {
            excludeProperties = new List<string>();
        }

        return typeof(T).GetModelMetadata().FieldDefinitions.Where(f => !excludeProperties.Contains(f.Name)).ToList();
    }

    public static string GetOrmLiteFieldAliasName<T>(string propertyName)
        => GetOrmLiteFieldAliasName(typeof(T), propertyName);

    public static string GetOrmLiteFieldAliasName(Type type, string propertyName)
    {
        var propertyKey = string.Concat(type.FullName, "|", propertyName);

        var property = _propertyNameMap.GetOrAdd(propertyKey, type.GetProperty(propertyName));

        return GetOrmLiteFieldAliasName(property);
    }

    public static string GetOrmLiteFieldAliasName(this PropertyInfo property)
        => _propertyInfoAliasMap.GetOrAdd(property, (property.GetCustomAttribute<AliasAttribute>(true)?.Name).Coalesce(property.Name));
}
