using System.Reflection;

namespace Derafsh.Mapping;

internal sealed record ChildCollectionMetadata(
    PropertyInfo CollectionProperty,
    string ForeignKeyPropertyName,
    Type ChildType);
