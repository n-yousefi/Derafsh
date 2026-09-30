using System.Collections;
using System.Data.Common;
using Derafsh.Internal;

namespace Derafsh;

/// <summary>
/// Provides transactional graph-persistence operations for <see cref="DbConnection"/>.
/// </summary>
public static class DerafshConnectionExtensions
{
    /// <summary>
    /// Inserts a mapped object graph in dependency order and propagates generated keys back to the object instances.
    /// </summary>
    public static Task<GraphWriteResult> InsertGraphAsync<T>(
        this DbConnection connection,
        T graph,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteOwnedTransactionAsync(connection, null, engine => engine.InsertAsync(graph), cancellationToken);

    /// <summary>
    /// Inserts a mapped object graph using a caller-owned transaction.
    /// </summary>
    public static Task<GraphWriteResult> InsertGraphAsync<T>(
        this DbConnection connection,
        T graph,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteOwnedTransactionAsync(connection, transaction, engine => engine.InsertAsync(graph), cancellationToken);

    /// <summary>
    /// Updates keyed objects in a graph and inserts nested objects whose keys are not populated.
    /// Missing children are never deleted.
    /// </summary>
    public static Task<GraphWriteResult> UpdateGraphAsync<T>(
        this DbConnection connection,
        T graph,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteOwnedTransactionAsync(connection, null, engine => engine.UpdateAsync(graph), cancellationToken);

    /// <summary>
    /// Updates a mapped object graph using a caller-owned transaction. Missing children are never deleted.
    /// </summary>
    public static Task<GraphWriteResult> UpdateGraphAsync<T>(
        this DbConnection connection,
        T graph,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteOwnedTransactionAsync(connection, transaction, engine => engine.UpdateAsync(graph), cancellationToken);

    /// <summary>
    /// Updates a graph and synchronizes supplied child collections with the database.
    /// A null collection is ignored; an empty collection removes all mapped children in that collection.
    /// </summary>
    public static Task<GraphWriteResult> SynchronizeGraphAsync<T>(
        this DbConnection connection,
        T graph,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteOwnedTransactionAsync(connection, null, engine => engine.SynchronizeAsync(graph), cancellationToken);

    /// <summary>
    /// Updates and synchronizes a mapped object graph using a caller-owned transaction.
    /// </summary>
    public static Task<GraphWriteResult> SynchronizeGraphAsync<T>(
        this DbConnection connection,
        T graph,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteOwnedTransactionAsync(connection, transaction, engine => engine.SynchronizeAsync(graph), cancellationToken);

    /// <summary>
    /// Loads a root object by key and recursively hydrates its mapped references and child collections.
    /// </summary>
    public static Task<T?> LoadGraphAsync<T>(
        this DbConnection connection,
        object key,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteReadAsync(connection, null, engine => engine.LoadAsync<T>(key), cancellationToken);

    /// <summary>
    /// Loads a mapped object graph using a caller-owned transaction.
    /// </summary>
    public static Task<T?> LoadGraphAsync<T>(
        this DbConnection connection,
        object key,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteReadAsync(connection, transaction, engine => engine.LoadAsync<T>(key), cancellationToken);

    /// <summary>
    /// Loads multiple graphs by root key while reusing an identity cache for shared references.
    /// </summary>
    public static Task<IReadOnlyList<T>> LoadGraphsAsync<T>(
        this DbConnection connection,
        IEnumerable keys,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteReadAsync(connection, null, engine => engine.LoadManyAsync<T>(keys.Cast<object>()), cancellationToken);

    /// <summary>
    /// Loads multiple graphs by root key using a caller-owned transaction.
    /// </summary>
    public static Task<IReadOnlyList<T>> LoadGraphsAsync<T>(
        this DbConnection connection,
        IEnumerable keys,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteReadAsync(connection, transaction, engine => engine.LoadManyAsync<T>(keys.Cast<object>()), cancellationToken);

    /// <summary>
    /// Deletes a mapped graph by root key. Child collections are deleted before their parent;
    /// referenced rows are not deleted because they may be shared.
    /// </summary>
    public static Task<GraphWriteResult> DeleteGraphAsync<T>(
        this DbConnection connection,
        object key,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteOwnedTransactionAsync(connection, null, engine => engine.DeleteAsync<T>(key), cancellationToken);

    /// <summary>
    /// Deletes a mapped graph by root key using a caller-owned transaction.
    /// </summary>
    public static Task<GraphWriteResult> DeleteGraphAsync<T>(
        this DbConnection connection,
        object key,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteOwnedTransactionAsync(connection, transaction, engine => engine.DeleteAsync<T>(key), cancellationToken);

    private static async Task<TResult> ExecuteOwnedTransactionAsync<TResult>(
        DbConnection connection,
        DbTransaction? externalTransaction,
        Func<GraphPersistenceEngine, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);
        SqlStatementBuilder.EnsureSupported(connection);
        if (externalTransaction is not null)
            ValidateTransaction(connection, externalTransaction);

        await using var lease = await ConnectionLease.AcquireAsync(connection, cancellationToken);

        if (externalTransaction is not null)
            return await action(new GraphPersistenceEngine(connection, externalTransaction, cancellationToken));

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action(new GraphPersistenceEngine(connection, transaction, cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original exception.
            }

            throw;
        }
    }

    private static async Task<TResult> ExecuteReadAsync<TResult>(
        DbConnection connection,
        DbTransaction? transaction,
        Func<GraphPersistenceEngine, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);
        SqlStatementBuilder.EnsureSupported(connection);
        if (transaction is not null)
            ValidateTransaction(connection, transaction);

        await using var lease = await ConnectionLease.AcquireAsync(connection, cancellationToken);
        return await action(new GraphPersistenceEngine(connection, transaction, cancellationToken));
    }

    private static void ValidateTransaction(DbConnection connection, DbTransaction transaction)
    {
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException(
                "The transaction belongs to a different DbConnection.",
                nameof(transaction));
        }
    }
}
