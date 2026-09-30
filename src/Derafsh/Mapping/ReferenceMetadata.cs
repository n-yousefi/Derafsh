using System.Reflection;

namespace Derafsh.Mapping;

internal sealed record ReferenceMetadata(
    PropertyInfo NavigationProperty,
    MappedPropertyMetadata ForeignKey,
    Type TargetType);
