using Dapper;
using Derafsh.Mapping;

namespace Derafsh.Internal;

internal static class SqlServerStatementBuilder
{
    public static SqlStatementBuilder.Statement Insert(MappedTypeMetadata mapping, object instance)
    {
        var columns = mapping.ScalarProperties.Where(p => !p.IsGenerated).ToArray();
        var parameters = new DynamicParameters();
        var table = SqlIdentifier.Table(mapping.Schema, mapping.TableName);
        var usesScopeIdentity = mapping.Key.IsGenerated && !mapping.Key.IsComputed && TypeHelpers.IsInteger(mapping.Key.Property.PropertyType);
        var output = mapping.Key.IsGenerated && !usesScopeIdentity
            ? $" OUTPUT INSERTED.{SqlIdentifier.Quote(mapping.Key.ColumnName)}"
            : string.Empty;
        var identitySelect = usesScopeIdentity ? " SELECT SCOPE_IDENTITY();" : string.Empty;

        if (columns.Length == 0)
            return new SqlStatementBuilder.Statement($"INSERT INTO {table}{output} DEFAULT VALUES;{identitySelect}", parameters);

        var names = new string[columns.Length];
        var values = new string[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var parameterName = $"p{i}";
            names[i] = SqlIdentifier.Quote(columns[i].ColumnName);
            values[i] = "@" + parameterName;
            parameters.Add(parameterName, columns[i].GetValue(instance));
        }

        return new SqlStatementBuilder.Statement(
            $"INSERT INTO {table} ({string.Join(", ", names)}){output} VALUES ({string.Join(", ", values)});{identitySelect}",
            parameters);
    }

    public static SqlStatementBuilder.Statement? Update(
        MappedTypeMetadata mapping,
        object instance,
        MappedPropertyMetadata? ownerForeignKey = null,
        object? expectedOwnerKey = null)
    {
        var columns = mapping.ScalarProperties.Where(p => !p.IsKey && !p.IsGenerated).ToArray();
        if (columns.Length == 0) return null;

        var parameters = new DynamicParameters();
        var assignments = new string[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var name = $"p{i}";
            assignments[i] = $"{SqlIdentifier.Quote(columns[i].ColumnName)} = @{name}";
            parameters.Add(name, columns[i].GetValue(instance));
        }

        parameters.Add("key", mapping.Key.GetValue(instance));
        var ownershipPredicate = AddOwnershipPredicate(parameters, ownerForeignKey, expectedOwnerKey);

        return new SqlStatementBuilder.Statement(
            $"UPDATE {SqlIdentifier.Table(mapping.Schema, mapping.TableName)} SET {string.Join(", ", assignments)} WHERE {SqlIdentifier.Quote(mapping.Key.ColumnName)} = @key{ownershipPredicate};",
            parameters);
    }

    public static SqlStatementBuilder.Statement ExistsByKey(
        MappedTypeMetadata mapping,
        object instance,
        MappedPropertyMetadata? ownerForeignKey = null,
        object? expectedOwnerKey = null)
    {
        var parameters = new DynamicParameters();
        parameters.Add("key", mapping.Key.GetValue(instance));
        var ownershipPredicate = AddOwnershipPredicate(parameters, ownerForeignKey, expectedOwnerKey);
        return new SqlStatementBuilder.Statement(
            $"SELECT COUNT(1) FROM {SqlIdentifier.Table(mapping.Schema, mapping.TableName)} WHERE {SqlIdentifier.Quote(mapping.Key.ColumnName)} = @key{ownershipPredicate};",
            parameters);
    }

    public static string SelectByKey(MappedTypeMetadata mapping) =>
        $"SELECT {SelectColumns(mapping)} FROM {SqlIdentifier.Table(mapping.Schema, mapping.TableName)} WHERE {SqlIdentifier.Quote(mapping.Key.ColumnName)} = @key;";

    public static string SelectChildKeys(MappedTypeMetadata child, MappedPropertyMetadata foreignKey) =>
        $"SELECT {SqlIdentifier.Quote(child.Key.ColumnName)} FROM {SqlIdentifier.Table(child.Schema, child.TableName)} WHERE {SqlIdentifier.Quote(foreignKey.ColumnName)} = @parentKey;";

    public static string DeleteByKey(MappedTypeMetadata mapping) =>
        $"DELETE FROM {SqlIdentifier.Table(mapping.Schema, mapping.TableName)} WHERE {SqlIdentifier.Quote(mapping.Key.ColumnName)} = @key;";

    private static string SelectColumns(MappedTypeMetadata mapping) =>
        string.Join(", ", mapping.ScalarProperties.Select(s =>
            $"{SqlIdentifier.Quote(s.ColumnName)} AS {SqlIdentifier.Quote(s.Property.Name)}"));

    private static string AddOwnershipPredicate(
        DynamicParameters parameters,
        MappedPropertyMetadata? ownerForeignKey,
        object? expectedOwnerKey)
    {
        if (ownerForeignKey is null) return string.Empty;
        parameters.Add("ownerKey", expectedOwnerKey);
        return $" AND {SqlIdentifier.Quote(ownerForeignKey.ColumnName)} = @ownerKey";
    }
}
