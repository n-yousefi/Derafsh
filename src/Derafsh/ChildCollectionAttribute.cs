namespace Derafsh;

/// <summary>
/// Maps a one-to-many child collection whose foreign key is stored on each child object.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ChildCollectionAttribute : Attribute
{
    /// <summary>
    /// Creates a child-collection mapping.
    /// </summary>
    /// <param name="foreignKeyPropertyName">The foreign-key property name on the child type.</param>
    public ChildCollectionAttribute(string foreignKeyPropertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(foreignKeyPropertyName);
        ForeignKeyPropertyName = foreignKeyPropertyName;
    }

    /// <summary>Gets the foreign-key property name on the child type.</summary>
    public string ForeignKeyPropertyName { get; }
}
