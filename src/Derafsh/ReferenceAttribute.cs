namespace Derafsh;

/// <summary>
/// Maps a reference navigation whose foreign key is stored on the declaring object.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ReferenceAttribute : Attribute
{
    /// <summary>
    /// Creates a reference mapping.
    /// </summary>
    /// <param name="foreignKeyPropertyName">The foreign-key property name on the declaring type.</param>
    public ReferenceAttribute(string foreignKeyPropertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(foreignKeyPropertyName);
        ForeignKeyPropertyName = foreignKeyPropertyName;
    }

    /// <summary>Gets the foreign-key property name on the declaring type.</summary>
    public string ForeignKeyPropertyName { get; }
}
