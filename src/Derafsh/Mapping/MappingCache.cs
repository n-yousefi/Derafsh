using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Derafsh.Internal;

namespace Derafsh.Mapping;

internal static class MappingCache
{
    private static readonly ConcurrentDictionary<Type, MappedTypeMetadata> Cache = new();

    public static MappedTypeMetadata Get(Type type) => Cache.GetOrAdd(type, Build);
    public static MappedTypeMetadata Get<T>() => Get(typeof(T));

    private static MappedTypeMetadata Build(Type type)
    {
        if (type.IsAbstract || type.IsInterface)
            throw new DerafshMappingException($"Type '{type.FullName}' must be a concrete class.");

        var tableAttribute = type.GetCustomAttribute<TableAttribute>(inherit: true);
        var tableName = tableAttribute?.Name ?? type.Name;
        var schema = string.IsNullOrWhiteSpace(tableAttribute?.Schema) ? "dbo" : tableAttribute!.Schema!;

        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToArray();

        var scalarProperties = properties
            .Where(property => property.GetCustomAttribute<NotMappedAttribute>(true) is null)
            .Where(property => property.GetMethod is not null)
            .Where(property => TypeHelpers.IsSimple(property.PropertyType))
            .ToArray();

        var explicitKeys = scalarProperties
            .Where(property => property.GetCustomAttribute<KeyAttribute>(true) is not null)
            .ToArray();

        if (explicitKeys.Length > 1)
            throw new DerafshMappingException($"Composite keys are not supported on '{type.FullName}'.");

        var keyProperty = explicitKeys.SingleOrDefault()
            ?? FindConventionalKey(type, scalarProperties)
            ?? throw new DerafshMappingException(
                $"'{type.FullName}' needs a [Key] property or a conventional key named 'Id' or '{type.Name}Id'.");

        var scalars = scalarProperties.Select(property =>
        {
            var columnName = property.GetCustomAttribute<ColumnAttribute>(true)?.Name ?? property.Name;
            var generated = property.GetCustomAttribute<DatabaseGeneratedAttribute>(true);
            var timestamp = property.GetCustomAttribute<TimestampAttribute>(true) is not null;
            var isKey = property == keyProperty;
            var isComputed = timestamp || generated?.DatabaseGeneratedOption == DatabaseGeneratedOption.Computed;
            var isGenerated = isComputed ||
                              generated?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity ||
                              (isKey && generated is null && TypeHelpers.IsInteger(property.PropertyType));

            return new MappedPropertyMetadata(property, columnName, isKey, isGenerated, isComputed);
        }).ToArray();

        var duplicateColumn = scalars
            .GroupBy(property => property.ColumnName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateColumn is not null)
        {
            throw new DerafshMappingException(
                $"'{type.FullName}' maps more than one scalar property to column '{duplicateColumn.Key}'.");
        }

        var key = scalars.Single(property => property.IsKey);
        if (key.Property.SetMethod is null)
            throw new DerafshMappingException($"Key property '{type.Name}.{key.Property.Name}' must be writable.");

        var references = new List<ReferenceMetadata>();
        var childCollections = new List<ChildCollectionMetadata>();

        foreach (var property in properties)
        {
            var reference = property.GetCustomAttribute<ReferenceAttribute>(true);
            var childCollection = property.GetCustomAttribute<ChildCollectionAttribute>(true);

            if (reference is not null && childCollection is not null)
            {
                throw new DerafshMappingException(
                    $"'{type.Name}.{property.Name}' cannot be both [Reference] and [ChildCollection].");
            }

            if (reference is not null)
                references.Add(BuildReference(type, property, reference, scalars));

            if (childCollection is not null)
                childCollections.Add(BuildChildCollection(type, property, childCollection));
        }

        return new MappedTypeMetadata
        {
            ClrType = type,
            TableName = tableName,
            Schema = schema,
            Key = key,
            ScalarProperties = scalars,
            References = references,
            ChildCollections = childCollections
        };
    }

    private static ReferenceMetadata BuildReference(
        Type declaringType,
        PropertyInfo property,
        ReferenceAttribute mapping,
        IReadOnlyList<MappedPropertyMetadata> scalars)
    {
        if (TypeHelpers.IsSimple(property.PropertyType) ||
            TypeHelpers.GetCollectionElementType(property.PropertyType) is not null)
        {
            throw new DerafshMappingException(
                $"[Reference] '{declaringType.Name}.{property.Name}' must be a single complex object.");
        }

        if (property.PropertyType.IsAbstract || property.PropertyType.IsInterface)
        {
            throw new DerafshMappingException(
                $"[Reference] '{declaringType.Name}.{property.Name}' must use a concrete navigation type. Polymorphic relationships are not supported.");
        }

        if (property.GetMethod is null)
            throw new DerafshMappingException($"[Reference] '{declaringType.Name}.{property.Name}' must be readable.");

        if (property.SetMethod is null)
            throw new DerafshMappingException($"[Reference] '{declaringType.Name}.{property.Name}' must be writable so Derafsh can hydrate graph reads.");

        var foreignKey = scalars.FirstOrDefault(
            scalar => scalar.Property.Name == mapping.ForeignKeyPropertyName)
            ?? throw new DerafshMappingException(
                $"[Reference] '{declaringType.Name}.{property.Name}' points to missing scalar property '{mapping.ForeignKeyPropertyName}'.");

        if (foreignKey.Property.SetMethod is null)
        {
            throw new DerafshMappingException(
                $"Foreign key '{declaringType.Name}.{foreignKey.Property.Name}' must be writable so Derafsh can propagate keys.");
        }

        return new ReferenceMetadata(property, foreignKey, property.PropertyType);
    }

    private static ChildCollectionMetadata BuildChildCollection(
        Type declaringType,
        PropertyInfo property,
        ChildCollectionAttribute mapping)
    {
        if (property.GetMethod is null)
        {
            throw new DerafshMappingException(
                $"[ChildCollection] '{declaringType.Name}.{property.Name}' must be readable.");
        }

        var elementType = TypeHelpers.GetCollectionElementType(property.PropertyType)
            ?? throw new DerafshMappingException(
                $"[ChildCollection] '{declaringType.Name}.{property.Name}' must be a collection.");

        if (TypeHelpers.IsSimple(elementType) || elementType.IsAbstract || elementType.IsInterface)
        {
            throw new DerafshMappingException(
                $"[ChildCollection] '{declaringType.Name}.{property.Name}' must contain concrete mapped objects. Polymorphic relationships are not supported.");
        }

        return new ChildCollectionMetadata(property, mapping.ForeignKeyPropertyName, elementType);
    }

    private static PropertyInfo? FindConventionalKey(
        Type type,
        IReadOnlyList<PropertyInfo> scalarProperties)
    {
        var names = new[] { "Id", type.Name + "Id" };

        foreach (var name in names)
        {
            var match = scalarProperties.FirstOrDefault(
                property => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return null;
    }
}
