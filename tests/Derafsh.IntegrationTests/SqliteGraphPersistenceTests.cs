using Derafsh;
using Dapper;

namespace Derafsh.IntegrationTests;

public sealed class SqliteGraphPersistenceTests
{
    [Fact]
    public async Task Graph_round_trips_and_synchronizes_on_sqlite()
    {
        await using var connection = await SqliteTestDatabase.OpenAndResetAsync();
        var settings = new SettingsDto
        {
            Name = "SQLite store",
            Contacts = [new ContactDto { Value = "one" }, new ContactDto { Value = "two" }],
            Zones = [new ZoneDto { Code = "NL" }]
        };

        var inserted = await connection.InsertGraphAsync(settings);

        Assert.True(settings.Id > 0);
        Assert.Equal(4, inserted.InsertedCount);

        var loaded = await connection.LoadGraphAsync<SettingsDto>(settings.Id);
        Assert.NotNull(loaded);
        loaded.Contacts!.RemoveAt(0);
        loaded.Contacts.Add(new ContactDto { Value = "three" });
        loaded.Zones = [];

        await connection.SynchronizeGraphAsync(loaded);

        var reloaded = await connection.LoadGraphAsync<SettingsDto>(settings.Id);
        Assert.Equal(2, reloaded!.Contacts!.Count);
        Assert.Empty(reloaded.Zones!);
    }

    [Fact]
    public async Task Null_and_empty_collection_semantics_are_identical_on_sqlite()
    {
        await using var connection = await SqliteTestDatabase.OpenAndResetAsync();
        var settings = new SettingsDto
        {
            Name = "Store",
            Contacts = [new ContactDto { Value = "one" }, new ContactDto { Value = "two" }],
            Zones = []
        };
        await connection.InsertGraphAsync(settings);

        await connection.SynchronizeGraphAsync(new SettingsDto
        {
            Id = settings.Id,
            Name = "Store",
            Contacts = null,
            Zones = null
        });
        Assert.Equal(2, (await connection.LoadGraphAsync<SettingsDto>(settings.Id))!.Contacts!.Count);

        await connection.SynchronizeGraphAsync(new SettingsDto
        {
            Id = settings.Id,
            Name = "Store",
            Contacts = [],
            Zones = null
        });
        Assert.Empty((await connection.LoadGraphAsync<SettingsDto>(settings.Id))!.Contacts!);
    }

    [Fact]
    public async Task Reference_generated_key_is_propagated_on_sqlite()
    {
        await using var connection = await SqliteTestDatabase.OpenAndResetAsync();
        var order = new OrderDto
        {
            Number = "SQLITE-1",
            Customer = new CustomerDto { Name = "Ada" }
        };

        await connection.InsertGraphAsync(order);

        Assert.True(order.Customer!.Id > 0);
        Assert.Equal(order.Customer.Id, order.CustomerId);
        Assert.Equal("Ada", (await connection.LoadGraphAsync<OrderDto>(order.Id))!.Customer!.Name);
    }

    [Fact]
    public async Task Existing_child_cannot_be_moved_between_parents_on_sqlite()
    {
        await using var connection = await SqliteTestDatabase.OpenAndResetAsync();
        var first = new SettingsDto { Name = "First", Contacts = [new ContactDto { Value = "owned" }], Zones = [] };
        var second = new SettingsDto { Name = "Second", Contacts = [], Zones = [] };
        await connection.InsertGraphAsync(first);
        await connection.InsertGraphAsync(second);

        second.Contacts = [new ContactDto { Id = first.Contacts![0].Id, Value = "move" }];

        await Assert.ThrowsAsync<DerafshPersistenceException>(() => connection.SynchronizeGraphAsync(second));
        var owner = await connection.ExecuteScalarAsync<int>(
            "SELECT SettingsId FROM DerafshTest_Contact WHERE Id = @id;",
            new { id = first.Contacts![0].Id });
        Assert.Equal(first.Id, owner);
    }

    [Fact]
    public async Task Caller_owned_transaction_can_roll_back_on_sqlite()
    {
        await using var connection = await SqliteTestDatabase.OpenAndResetAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await connection.InsertGraphAsync(
            new SettingsDto { Name = "Rollback", Contacts = [new ContactDto { Value = "x" }], Zones = [] },
            transaction);
        await transaction.RollbackAsync();

        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DerafshTest_Settings;"));
    }
    [Fact]
    public async Task Key_only_entity_update_verifies_row_existence_on_sqlite()
    {
        await using var connection = await SqliteTestDatabase.OpenAndResetAsync();
        await connection.InsertGraphAsync(new KeyOnlyDto { Id = 7 });

        var updated = await connection.UpdateGraphAsync(new KeyOnlyDto { Id = 7 });
        Assert.Equal(0, updated.UpdatedCount);

        await Assert.ThrowsAsync<DerafshPersistenceException>(
            () => connection.UpdateGraphAsync(new KeyOnlyDto { Id = 8 }));
    }

}
