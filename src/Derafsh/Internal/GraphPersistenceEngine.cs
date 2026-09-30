using System.Collections;
using System.Data.Common;
using System.Reflection;
using Dapper;
using Derafsh.Mapping;

namespace Derafsh.Internal;

internal sealed class GraphPersistenceEngine(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
{
    private readonly DbConnection _connection = connection;
    private readonly DbTransaction? _transaction = transaction;
    private readonly CancellationToken _cancellationToken = cancellationToken;

    public async Task<GraphWriteResult> InsertAsync<T>(T graph) where T : class
    {
        ArgumentNullException.ThrowIfNull(graph);
        var context = new GraphWriteContext();
        var completed = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var stack = new HashSet<object>(ReferenceEqualityComparer.Instance);
        await InsertNodeAsync(graph, allowExistingReference: false, completed, stack, context);
        return context.ToResult(MappingCache.Get(graph.GetType()).Key.GetValue(graph));
    }

    public async Task<GraphWriteResult> UpdateAsync<T>(T graph) where T : class
    {
        ArgumentNullException.ThrowIfNull(graph);
        var rootMetadata = MappingCache.Get(graph.GetType());
        EnsureHasKey(rootMetadata, graph, "UpdateGraphAsync");

        var context = new GraphWriteContext();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var stack = new HashSet<object>(ReferenceEqualityComparer.Instance);
        await UpdateNodeAsync(graph, visited, stack, context);
        return context.ToResult(rootMetadata.Key.GetValue(graph));
    }

    public async Task<GraphWriteResult> SynchronizeAsync<T>(T graph) where T : class
    {
        ArgumentNullException.ThrowIfNull(graph);
        var rootMetadata = MappingCache.Get(graph.GetType());
        EnsureHasKey(rootMetadata, graph, "SynchronizeGraphAsync");

        var context = new GraphWriteContext();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var stack = new HashSet<object>(ReferenceEqualityComparer.Instance);
        await SynchronizeNodeAsync(graph, visited, stack, context);
        return context.ToResult(rootMetadata.Key.GetValue(graph));
    }

    public async Task<T?> LoadAsync<T>(object key) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        var metadata = MappingCache.Get<T>();
        var normalizedKey = NormalizeKey(metadata, key);
        var cache = new Dictionary<NodeIdentity, object>();
        return (T?)await LoadNodeAsync(metadata, normalizedKey!, cache);
    }

    public async Task<IReadOnlyList<T>> LoadManyAsync<T>(IEnumerable<object> keys) where T : class
    {
        ArgumentNullException.ThrowIfNull(keys);
        var metadata = MappingCache.Get<T>();
        var cache = new Dictionary<NodeIdentity, object>();
        var result = new List<T>();

        foreach (var key in keys)
        {
            if (key is null) continue;
            var normalizedKey = NormalizeKey(metadata, key);
            var item = (T?)await LoadNodeAsync(metadata, normalizedKey!, cache);
            if (item is not null) result.Add(item);
        }

        return result;
    }

    public async Task<GraphWriteResult> DeleteAsync<T>(object key) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        var metadata = MappingCache.Get<T>();
        var normalizedKey = NormalizeKey(metadata, key);
        var context = new GraphWriteContext();
        var visited = new HashSet<NodeIdentity>();
        await DeleteNodeByKeyAsync(metadata, normalizedKey!, visited, context);
        return context.ToResult(normalizedKey);
    }

    private async Task InsertNodeAsync(
        object node,
        bool allowExistingReference,
        HashSet<object> completed,
        HashSet<object> stack,
        GraphWriteContext context)
    {
        if (completed.Contains(node)) return;

        var metadata = MappingCache.Get(node.GetType());
        var keyValue = metadata.Key.GetValue(node);
        if (allowExistingReference && !TypeHelpers.IsDefaultValue(keyValue, metadata.Key.Property.PropertyType))
        {
            completed.Add(node);
            return;
        }

        if (!stack.Add(node))
            throw new DerafshMappingException($"Insert dependency cycle detected at '{metadata.ClrType.Name}'. Break the cycle or persist one side first.");

        PrepareClientGeneratedKey(metadata, node);

        foreach (var reference in metadata.References)
        {
            var target = reference.NavigationProperty.GetValue(node);
            if (target is null) continue;

            await InsertNodeAsync(target, allowExistingReference: true, completed, stack, context);
            var targetMetadata = MappingCache.Get(reference.TargetType);
            var targetKey = targetMetadata.Key.GetValue(target);
            if (TypeHelpers.IsDefaultValue(targetKey, targetMetadata.Key.Property.PropertyType))
                throw new DerafshPersistenceException($"Reference '{metadata.ClrType.Name}.{reference.NavigationProperty.Name}' has no key after persistence.");

            reference.ForeignKey.SetValue(node, DbValueConverter.ConvertTo(targetKey, reference.ForeignKey.Property.PropertyType));
        }

        await InsertCurrentAsync(metadata, node, context);
        completed.Add(node);

        foreach (var children in metadata.ChildCollections)
        {
            var collection = children.CollectionProperty.GetValue(node);
            if (collection is null) continue;

            var childMetadata = MappingCache.Get(children.ChildType);
            var childForeignKey = FindChildForeignKey(children, childMetadata);
            var parentKey = metadata.Key.GetValue(node);

            foreach (var child in TypeHelpers.EnumerateObjects(collection))
            {
                childForeignKey.SetValue(child, DbValueConverter.ConvertTo(parentKey, childForeignKey.Property.PropertyType));
                await InsertNodeAsync(child, allowExistingReference: false, completed, stack, context);
            }
        }

        stack.Remove(node);
    }

    private async Task UpdateNodeAsync(
        object node,
        HashSet<object> visited,
        HashSet<object> stack,
        GraphWriteContext context,
        MappedPropertyMetadata? ownerForeignKey = null,
        object? expectedOwnerKey = null)
    {
        if (visited.Contains(node)) return;

        var metadata = MappingCache.Get(node.GetType());
        var keyValue = metadata.Key.GetValue(node);
        if (TypeHelpers.IsDefaultValue(keyValue, metadata.Key.Property.PropertyType))
        {
            var inserted = new HashSet<object>(ReferenceEqualityComparer.Instance);
            await InsertNodeAsync(node, false, inserted, new HashSet<object>(ReferenceEqualityComparer.Instance), context);
            visited.UnionWith(inserted);
            return;
        }

        if (!stack.Add(node))
            throw new DerafshMappingException($"Update dependency cycle detected at '{metadata.ClrType.Name}'.");

        foreach (var reference in metadata.References)
        {
            var target = reference.NavigationProperty.GetValue(node);
            if (target is null) continue;

            var targetMetadata = MappingCache.Get(reference.TargetType);
            var targetKey = targetMetadata.Key.GetValue(target);
            if (TypeHelpers.IsDefaultValue(targetKey, targetMetadata.Key.Property.PropertyType))
            {
                var inserted = new HashSet<object>(ReferenceEqualityComparer.Instance);
                await InsertNodeAsync(target, false, inserted, new HashSet<object>(ReferenceEqualityComparer.Instance), context);
                visited.UnionWith(inserted);
            }
            else
            {
                await UpdateNodeAsync(target, visited, stack, context);
            }

            targetKey = targetMetadata.Key.GetValue(target);
            reference.ForeignKey.SetValue(node, DbValueConverter.ConvertTo(targetKey, reference.ForeignKey.Property.PropertyType));
        }

        await UpdateCurrentAsync(metadata, node, context, ownerForeignKey, expectedOwnerKey);
        visited.Add(node);

        foreach (var children in metadata.ChildCollections)
        {
            var collection = children.CollectionProperty.GetValue(node);
            if (collection is null) continue;

            var childMetadata = MappingCache.Get(children.ChildType);
            var childForeignKey = FindChildForeignKey(children, childMetadata);
            var parentKey = metadata.Key.GetValue(node);

            foreach (var child in TypeHelpers.EnumerateObjects(collection))
            {
                childForeignKey.SetValue(child, DbValueConverter.ConvertTo(parentKey, childForeignKey.Property.PropertyType));
                await UpdateNodeAsync(child, visited, stack, context, childForeignKey, parentKey);
            }
        }

        stack.Remove(node);
    }

    private async Task SynchronizeNodeAsync(
        object node,
        HashSet<object> visited,
        HashSet<object> stack,
        GraphWriteContext context,
        MappedPropertyMetadata? ownerForeignKey = null,
        object? expectedOwnerKey = null)
    {
        if (visited.Contains(node)) return;

        var metadata = MappingCache.Get(node.GetType());
        var keyValue = metadata.Key.GetValue(node);
        if (TypeHelpers.IsDefaultValue(keyValue, metadata.Key.Property.PropertyType))
        {
            var inserted = new HashSet<object>(ReferenceEqualityComparer.Instance);
            await InsertNodeAsync(node, false, inserted, new HashSet<object>(ReferenceEqualityComparer.Instance), context);
            visited.UnionWith(inserted);
            return;
        }

        if (!stack.Add(node))
            throw new DerafshMappingException($"Synchronization dependency cycle detected at '{metadata.ClrType.Name}'.");

        foreach (var reference in metadata.References)
        {
            var target = reference.NavigationProperty.GetValue(node);
            if (target is null) continue;

            var targetMetadata = MappingCache.Get(reference.TargetType);
            var targetKey = targetMetadata.Key.GetValue(target);
            if (TypeHelpers.IsDefaultValue(targetKey, targetMetadata.Key.Property.PropertyType))
            {
                var inserted = new HashSet<object>(ReferenceEqualityComparer.Instance);
                await InsertNodeAsync(target, false, inserted, new HashSet<object>(ReferenceEqualityComparer.Instance), context);
                visited.UnionWith(inserted);
            }
            else
            {
                await SynchronizeNodeAsync(target, visited, stack, context);
            }

            reference.ForeignKey.SetValue(node, DbValueConverter.ConvertTo(targetMetadata.Key.GetValue(target), reference.ForeignKey.Property.PropertyType));
        }

        await UpdateCurrentAsync(metadata, node, context, ownerForeignKey, expectedOwnerKey);
        visited.Add(node);

        foreach (var children in metadata.ChildCollections)
        {
            var childMetadata = MappingCache.Get(children.ChildType);
            var childForeignKey = FindChildForeignKey(children, childMetadata);
            var parentKey = metadata.Key.GetValue(node);
            var collection = children.CollectionProperty.GetValue(node);
            if (collection is null) continue;

            var submittedKeys = new HashSet<object>();
            foreach (var child in TypeHelpers.EnumerateObjects(collection))
            {
                childForeignKey.SetValue(child, DbValueConverter.ConvertTo(parentKey, childForeignKey.Property.PropertyType));
                await SynchronizeNodeAsync(child, visited, stack, context, childForeignKey, parentKey);
                var submittedKey = childMetadata.Key.GetValue(child);
                if (!TypeHelpers.IsDefaultValue(submittedKey, childMetadata.Key.Property.PropertyType))
                    submittedKeys.Add(NormalizeKey(childMetadata, submittedKey!)!);
            }

            var existingKeys = await SelectChildKeysAsync(childMetadata, childForeignKey, parentKey!);
            foreach (var existingKey in existingKeys)
            {
                if (!submittedKeys.Contains(existingKey))
                    await DeleteNodeByKeyAsync(childMetadata, existingKey, new HashSet<NodeIdentity>(), context);
            }
        }

        stack.Remove(node);
    }

    private async Task InsertCurrentAsync(MappedTypeMetadata metadata, object node, GraphWriteContext context)
    {
        var currentKey = metadata.Key.GetValue(node);
        if (metadata.Key.IsGenerated && !TypeHelpers.IsDefaultValue(currentKey, metadata.Key.Property.PropertyType))
            throw new DerafshPersistenceException($"InsertGraphAsync refuses to insert '{metadata.ClrType.Name}' with a non-default database-generated key. Use UpdateGraphAsync for existing objects.");

        var statement = SqlStatementBuilder.Insert(_connection, metadata, node);
        if (metadata.Key.IsGenerated)
        {
            var generated = await _connection.ExecuteScalarAsync<object?>(new CommandDefinition(
                statement.Sql, statement.Parameters, _transaction, cancellationToken: _cancellationToken));

            if (generated is null || generated is DBNull)
                throw new DerafshPersistenceException($"The database did not return a generated key for '{metadata.DisplayName}'.");

            metadata.Key.SetValue(node, DbValueConverter.ConvertTo(generated, metadata.Key.Property.PropertyType));
        }
        else
        {
            await _connection.ExecuteAsync(new CommandDefinition(
                statement.Sql, statement.Parameters, _transaction, cancellationToken: _cancellationToken));
        }

        context.InsertedCount++;
    }

    private async Task UpdateCurrentAsync(
        MappedTypeMetadata metadata,
        object node,
        GraphWriteContext context,
        MappedPropertyMetadata? ownerForeignKey,
        object? expectedOwnerKey)
    {
        EnsureHasKey(metadata, node, "update");
        var statement = SqlStatementBuilder.Update(_connection, metadata, node, ownerForeignKey, expectedOwnerKey);
        if (statement is null)
        {
            var exists = SqlStatementBuilder.ExistsByKey(_connection, metadata, node, ownerForeignKey, expectedOwnerKey);
            var matched = await _connection.ExecuteScalarAsync<long>(new CommandDefinition(
                exists.Sql, exists.Parameters, _transaction, cancellationToken: _cancellationToken));
            if (matched == 0)
                ThrowMissingOrUnownedRow(metadata, node, ownerForeignKey);
            return;
        }

        var affected = await _connection.ExecuteAsync(new CommandDefinition(
            statement.Sql, statement.Parameters, _transaction, cancellationToken: _cancellationToken));

        if (affected == 0)
            ThrowMissingOrUnownedRow(metadata, node, ownerForeignKey);

        context.UpdatedCount++;
    }

    private static void ThrowMissingOrUnownedRow(
        MappedTypeMetadata metadata,
        object node,
        MappedPropertyMetadata? ownerForeignKey)
    {
        throw new DerafshPersistenceException(
            ownerForeignKey is null
                ? $"No row in '{metadata.DisplayName}' matched key '{metadata.Key.GetValue(node)}'."
                : $"'{metadata.DisplayName}' with key '{metadata.Key.GetValue(node)}' does not belong to the submitted parent graph.");
    }

    private async Task<object?> LoadNodeAsync(MappedTypeMetadata metadata, object key, Dictionary<NodeIdentity, object> cache)
    {
        var identity = new NodeIdentity(metadata.ClrType, key);
        if (cache.TryGetValue(identity, out var cached)) return cached;

        var rows = await _connection.QueryAsync(metadata.ClrType, new CommandDefinition(
            SqlStatementBuilder.SelectByKey(_connection, metadata), new { key }, _transaction, cancellationToken: _cancellationToken));

        var instance = rows.FirstOrDefault();
        if (instance is null) return null;

        cache[identity] = instance;

        foreach (var reference in metadata.References)
        {
            var foreignKey = reference.ForeignKey.GetValue(instance);
            if (foreignKey is null || TypeHelpers.IsDefaultValue(foreignKey, reference.ForeignKey.Property.PropertyType)) continue;

            var targetMetadata = MappingCache.Get(reference.TargetType);
            var normalizedKey = NormalizeKey(targetMetadata, foreignKey);
            var target = await LoadNodeAsync(targetMetadata, normalizedKey!, cache);
            SetNavigation(reference.NavigationProperty, instance, target);
        }

        foreach (var children in metadata.ChildCollections)
        {
            var childMetadata = MappingCache.Get(children.ChildType);
            var childForeignKey = FindChildForeignKey(children, childMetadata);
            var parentKey = metadata.Key.GetValue(instance)!;
            var childKeys = await SelectChildKeysAsync(childMetadata, childForeignKey, parentKey);
            var childInstances = new List<object>();

            foreach (var childKey in childKeys)
            {
                var child = await LoadNodeAsync(childMetadata, childKey, cache);
                if (child is not null) childInstances.Add(child);
            }

            SetCollection(children.CollectionProperty, instance, children.ChildType, childInstances);
        }

        return instance;
    }

    private async Task DeleteNodeByKeyAsync(MappedTypeMetadata metadata, object key, HashSet<NodeIdentity> visited, GraphWriteContext context)
    {
        key = NormalizeKey(metadata, key)!;
        var identity = new NodeIdentity(metadata.ClrType, key);
        if (!visited.Add(identity)) return;

        foreach (var children in metadata.ChildCollections)
        {
            var childMetadata = MappingCache.Get(children.ChildType);
            var childForeignKey = FindChildForeignKey(children, childMetadata);
            var childKeys = await SelectChildKeysAsync(childMetadata, childForeignKey, key);
            foreach (var childKey in childKeys)
                await DeleteNodeByKeyAsync(childMetadata, childKey, visited, context);
        }

        var affected = await _connection.ExecuteAsync(new CommandDefinition(
            SqlStatementBuilder.DeleteByKey(_connection, metadata), new { key }, _transaction, cancellationToken: _cancellationToken));
        context.DeletedCount += affected;
    }

    private async Task<IReadOnlyList<object>> SelectChildKeysAsync(MappedTypeMetadata child, MappedPropertyMetadata foreignKey, object parentKey)
    {
        var rows = await _connection.QueryAsync(child.Key.Property.PropertyType, new CommandDefinition(
            SqlStatementBuilder.SelectChildKeys(_connection, child, foreignKey), new { parentKey }, _transaction, cancellationToken: _cancellationToken));

        return rows.Select(k => NormalizeKey(child, k)!).ToArray();
    }

    private static MappedPropertyMetadata FindChildForeignKey(ChildCollectionMetadata relation, MappedTypeMetadata child)
    {
        var property = child.ScalarProperties.FirstOrDefault(p => p.Property.Name == relation.ForeignKeyPropertyName)
            ?? throw new DerafshMappingException($"[ChildCollection] '{relation.CollectionProperty.DeclaringType?.Name}.{relation.CollectionProperty.Name}' points to missing child scalar property '{relation.ForeignKeyPropertyName}' on '{child.ClrType.Name}'.");
        if (property.Property.SetMethod is null)
            throw new DerafshMappingException($"Child foreign key '{child.ClrType.Name}.{property.Property.Name}' must be writable so Derafsh can propagate parent keys.");

        return property;
    }

    private static void PrepareClientGeneratedKey(MappedTypeMetadata metadata, object node)
    {
        if (metadata.Key.IsGenerated) return;
        var type = TypeHelpers.UnwrapNullable(metadata.Key.Property.PropertyType);
        var value = metadata.Key.GetValue(node);
        if (type == typeof(Guid) && TypeHelpers.IsDefaultValue(value, metadata.Key.Property.PropertyType))
            metadata.Key.SetValue(node, Guid.NewGuid());
    }

    private static object? NormalizeKey(MappedTypeMetadata metadata, object key) =>
        DbValueConverter.ConvertTo(key, metadata.Key.Property.PropertyType);

    private static void EnsureHasKey(MappedTypeMetadata metadata, object node, string operation)
    {
        var key = metadata.Key.GetValue(node);
        if (TypeHelpers.IsDefaultValue(key, metadata.Key.Property.PropertyType))
            throw new DerafshPersistenceException($"'{metadata.ClrType.Name}' needs a non-default key for {operation}.");
    }

    private static void SetNavigation(PropertyInfo property, object parent, object? value)
    {
        if (property.SetMethod is null)
            throw new DerafshMappingException($"Navigation '{property.DeclaringType?.Name}.{property.Name}' must be writable for graph reads.");
        property.SetValue(parent, value);
    }

    private static void SetCollection(PropertyInfo property, object parent, Type elementType, IReadOnlyList<object> values)
    {
        if (property.PropertyType.IsArray)
        {
            var array = Array.CreateInstance(elementType, values.Count);
            for (var i = 0; i < values.Count; i++) array.SetValue(values[i], i);
            SetNavigation(property, parent, array);
            return;
        }

        var current = property.GetValue(parent);
        if (current is not null)
        {
            var clear = current.GetType().GetMethod("Clear", Type.EmptyTypes);
            var add = current.GetType().GetMethod("Add", [elementType]);
            if (clear is not null && add is not null)
            {
                clear.Invoke(current, null);
                foreach (var value in values) add.Invoke(current, [value]);
                return;
            }
        }

        if (!property.PropertyType.IsAbstract && !property.PropertyType.IsInterface &&
            property.PropertyType.GetConstructor(Type.EmptyTypes) is not null)
        {
            var concrete = Activator.CreateInstance(property.PropertyType);
            var add = concrete?.GetType().GetMethod("Add", [elementType]);
            if (concrete is not null && add is not null && property.SetMethod is not null)
            {
                foreach (var value in values) add.Invoke(concrete, [value]);
                property.SetValue(parent, concrete);
                return;
            }
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var value in values) list.Add(value);

        if (property.SetMethod is null || !property.PropertyType.IsAssignableFrom(listType))
            throw new DerafshMappingException($"Collection '{property.DeclaringType?.Name}.{property.Name}' must be writable or expose a mutable collection.");

        property.SetValue(parent, list);
    }

    private readonly record struct NodeIdentity(Type Type, object Key);
}
