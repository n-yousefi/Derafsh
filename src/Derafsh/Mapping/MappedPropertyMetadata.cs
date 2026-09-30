using System.Reflection;

namespace Derafsh.Mapping;

internal sealed record MappedPropertyMetadata(
    PropertyInfo Property,
    string ColumnName,
    bool IsKey,
    bool IsGenerated,
    bool IsComputed)
{
    public object? GetValue(object instance) => Property.GetValue(instance);

    public void SetValue(object instance, object? value)
    {
        if (Property.SetMethod is null)
            throw new DerafshMappingException($"Property '{Property.DeclaringType?.Name}.{Property.Name}' must be writable.");

        Property.SetValue(instance, value);
    }
}
