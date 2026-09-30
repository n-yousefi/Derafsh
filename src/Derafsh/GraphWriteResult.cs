namespace Derafsh;

/// <summary>Describes the rows affected by a graph write operation.</summary>
/// <param name="RootKey">The root graph key after the operation completes.</param>
/// <param name="InsertedCount">Number of inserted rows.</param>
/// <param name="UpdatedCount">Number of updated rows.</param>
/// <param name="DeletedCount">Number of deleted rows.</param>
public sealed record GraphWriteResult(
    object? RootKey,
    int InsertedCount,
    int UpdatedCount,
    int DeletedCount);
