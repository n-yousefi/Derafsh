using System.Globalization;

namespace Derafsh.Internal;

internal static class DbValueConverter
{
    public static object? ConvertTo(object? value, Type destinationType)
    {
        if (value is null || value is DBNull)
            return null;

        var target = Nullable.GetUnderlyingType(destinationType) ?? destinationType;
        if (target.IsInstanceOfType(value)) return value;
        if (target.IsEnum) return Enum.ToObject(target, value);
        if (target == typeof(Guid)) return value is Guid guid ? guid : Guid.Parse(value.ToString()!);
        if (target == typeof(DateOnly)) return value is DateTime dt ? DateOnly.FromDateTime(dt) : DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture);
        if (target == typeof(TimeOnly)) return value is TimeSpan ts ? TimeOnly.FromTimeSpan(ts) : TimeOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture);

        return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }
}
