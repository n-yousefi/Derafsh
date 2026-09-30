using System.Collections;

namespace Derafsh.Internal;

internal static class TypeHelpers
{
    private static readonly HashSet<Type> SimpleTypes =
    [
        typeof(string), typeof(decimal), typeof(Guid), typeof(DateTime), typeof(DateTimeOffset),
        typeof(TimeSpan), typeof(DateOnly), typeof(TimeOnly), typeof(byte[]), typeof(char)
    ];

    public static Type UnwrapNullable(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    public static bool IsSimple(Type type)
    {
        type = UnwrapNullable(type);
        return type.IsPrimitive || type.IsEnum || SimpleTypes.Contains(type);
    }

    public static bool IsDefaultValue(object? value, Type type)
    {
        if (value is null) return true;
        type = UnwrapNullable(type);
        if (type == typeof(string)) return string.IsNullOrEmpty((string)value);
        if (!type.IsValueType) return false;
        return value.Equals(Activator.CreateInstance(type));
    }

    public static bool IsInteger(Type type)
    {
        type = UnwrapNullable(type);
        return type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
               type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong);
    }

    public static Type? GetCollectionElementType(Type type)
    {
        if (type == typeof(string) || type == typeof(byte[])) return null;
        if (type.IsArray) return type.GetElementType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];

        return type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    public static IEnumerable<object> EnumerateObjects(object collection)
    {
        if (collection is not IEnumerable enumerable)
            throw new DerafshMappingException($"'{collection.GetType().Name}' is not enumerable.");

        foreach (var item in enumerable)
            if (item is not null)
                yield return item;
    }
}
