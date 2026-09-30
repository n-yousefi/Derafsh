namespace Derafsh.Mapping;

internal sealed class MappedTypeMetadata
{
    public required Type ClrType { get; init; }
    public required string TableName { get; init; }
    public required string Schema { get; init; }
    public required MappedPropertyMetadata Key { get; init; }
    public required IReadOnlyList<MappedPropertyMetadata> ScalarProperties { get; init; }
    public required IReadOnlyList<ReferenceMetadata> References { get; init; }
    public required IReadOnlyList<ChildCollectionMetadata> ChildCollections { get; init; }

    public string DisplayName => $"{Schema}.{TableName}";
}
