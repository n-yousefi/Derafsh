namespace Derafsh.Internal;

internal sealed class GraphWriteContext
{
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DeletedCount { get; set; }

    public GraphWriteResult ToResult(object? rootKey) => new(rootKey, InsertedCount, UpdatedCount, DeletedCount);
}
