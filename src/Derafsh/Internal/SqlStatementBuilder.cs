using System.Data.Common;
using Dapper;
using Derafsh.Mapping;

namespace Derafsh.Internal;

internal static class SqlStatementBuilder
{
    internal sealed record Statement(string Sql, DynamicParameters Parameters);

    public static void EnsureSupported(DbConnection connection)
    {
        _ = DatabaseProviderResolver.Resolve(connection);
    }

    public static Statement Insert(DbConnection connection, MappedTypeMetadata mapping, object instance) =>
        DatabaseProviderResolver.Resolve(connection) switch
        {
            DatabaseProvider.SqlServer => SqlServerStatementBuilder.Insert(mapping, instance),
            DatabaseProvider.Sqlite => SqliteStatementBuilder.Insert(mapping, instance),
            _ => throw new InvalidOperationException("Unknown database provider.")
        };

    public static Statement? Update(
        DbConnection connection,
        MappedTypeMetadata mapping,
        object instance,
        MappedPropertyMetadata? ownerForeignKey = null,
        object? expectedOwnerKey = null) =>
        DatabaseProviderResolver.Resolve(connection) switch
        {
            DatabaseProvider.SqlServer => SqlServerStatementBuilder.Update(mapping, instance, ownerForeignKey, expectedOwnerKey),
            DatabaseProvider.Sqlite => SqliteStatementBuilder.Update(mapping, instance, ownerForeignKey, expectedOwnerKey),
            _ => throw new InvalidOperationException("Unknown database provider.")
        };

    public static Statement ExistsByKey(
        DbConnection connection,
        MappedTypeMetadata mapping,
        object instance,
        MappedPropertyMetadata? ownerForeignKey = null,
        object? expectedOwnerKey = null) =>
        DatabaseProviderResolver.Resolve(connection) switch
        {
            DatabaseProvider.SqlServer => SqlServerStatementBuilder.ExistsByKey(mapping, instance, ownerForeignKey, expectedOwnerKey),
            DatabaseProvider.Sqlite => SqliteStatementBuilder.ExistsByKey(mapping, instance, ownerForeignKey, expectedOwnerKey),
            _ => throw new InvalidOperationException("Unknown database provider.")
        };

    public static string SelectByKey(DbConnection connection, MappedTypeMetadata mapping) =>
        DatabaseProviderResolver.Resolve(connection) switch
        {
            DatabaseProvider.SqlServer => SqlServerStatementBuilder.SelectByKey(mapping),
            DatabaseProvider.Sqlite => SqliteStatementBuilder.SelectByKey(mapping),
            _ => throw new InvalidOperationException("Unknown database provider.")
        };

    public static string SelectChildKeys(DbConnection connection, MappedTypeMetadata child, MappedPropertyMetadata foreignKey) =>
        DatabaseProviderResolver.Resolve(connection) switch
        {
            DatabaseProvider.SqlServer => SqlServerStatementBuilder.SelectChildKeys(child, foreignKey),
            DatabaseProvider.Sqlite => SqliteStatementBuilder.SelectChildKeys(child, foreignKey),
            _ => throw new InvalidOperationException("Unknown database provider.")
        };

    public static string DeleteByKey(DbConnection connection, MappedTypeMetadata mapping) =>
        DatabaseProviderResolver.Resolve(connection) switch
        {
            DatabaseProvider.SqlServer => SqlServerStatementBuilder.DeleteByKey(mapping),
            DatabaseProvider.Sqlite => SqliteStatementBuilder.DeleteByKey(mapping),
            _ => throw new InvalidOperationException("Unknown database provider.")
        };
}
