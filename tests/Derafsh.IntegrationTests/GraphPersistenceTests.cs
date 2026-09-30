using Derafsh;
using Dapper;

namespace Derafsh.IntegrationTests;

public sealed class GraphPersistenceTests
{
    [Fact]
    public async Task Settings_graph_round_trips_and_synchronizes_children()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        var settings = new SettingsDto
        {
            Name = "Main store",
            Contacts =
            [
                new ContactDto { Value = "one@example.com" },
                new ContactDto { Value = "two@example.com" }
            ],
            Zones = [new ZoneDto { Code = "NL" }]
        };

        var inserted = await connection.InsertGraphAsync(settings);

        Assert.True(settings.Id > 0);
        Assert.Equal(4, inserted.InsertedCount);
        Assert.All(settings.Contacts!, contact => Assert.Equal(settings.Id, contact.SettingsId));

        var loaded = await connection.LoadGraphAsync<SettingsDto>(settings.Id);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Contacts!.Count);
        Assert.Single(loaded.Zones!);

        loaded.Name = "Updated store";
        loaded.Contacts.RemoveAt(0);
        loaded.Contacts.Add(new ContactDto { Value = "three@example.com" });
        loaded.Zones.Clear();

        var synchronized = await connection.SynchronizeGraphAsync(loaded);
        Assert.True(synchronized.InsertedCount >= 1);
        Assert.True(synchronized.DeletedCount >= 2);

        var reloaded = await connection.LoadGraphAsync<SettingsDto>(settings.Id);
        Assert.Equal("Updated store", reloaded!.Name);
        Assert.Equal(2, reloaded.Contacts!.Count);
        Assert.Empty(reloaded.Zones!);
    }

    [Fact]
    public async Task Null_collection_is_ignored_but_empty_collection_is_authoritative()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        var settings = new SettingsDto
        {
            Name = "Store",
            Contacts =
            [
                new ContactDto { Value = "one" },
                new ContactDto { Value = "two" }
            ],
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

        var afterNull = await connection.LoadGraphAsync<SettingsDto>(settings.Id);
        Assert.Equal(2, afterNull!.Contacts!.Count);

        await connection.SynchronizeGraphAsync(new SettingsDto
        {
            Id = settings.Id,
            Name = "Store",
            Contacts = [],
            Zones = null
        });

        var afterEmpty = await connection.LoadGraphAsync<SettingsDto>(settings.Id);
        Assert.Empty(afterEmpty!.Contacts!);
    }

    [Fact]
    public async Task Caller_owned_transaction_can_roll_back_a_graph_write()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await connection.InsertGraphAsync(
            new SettingsDto { Name = "Rolled back", Contacts = [new ContactDto { Value = "x" }] },
            transaction);

        await transaction.RollbackAsync();

        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DerafshTest_Settings;");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Reference_is_inserted_before_its_owner_and_key_is_propagated()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        var order = new OrderDto
        {
            Number = "SO-1001",
            Customer = new CustomerDto { Name = "Ada" }
        };

        await connection.InsertGraphAsync(order);

        Assert.True(order.Customer!.Id > 0);
        Assert.Equal(order.Customer.Id, order.CustomerId);
        Assert.True(order.Id > 0);

        var loaded = await connection.LoadGraphAsync<OrderDto>(order.Id);
        Assert.Equal("Ada", loaded!.Customer!.Name);
    }

    [Fact]
    public async Task Parameterized_values_are_stored_as_data_not_sql()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        var payload = "Robert'); DROP TABLE dbo.DerafshTest_Settings;--";
        var settings = new SettingsDto { Name = payload, Contacts = [], Zones = [] };

        await connection.InsertGraphAsync(settings);

        var loaded = await connection.LoadGraphAsync<SettingsDto>(settings.Id);
        Assert.Equal(payload, loaded!.Name);

        var tableStillExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'DerafshTest_Settings';");
        Assert.Equal(1, tableStillExists);
    }
    [Fact]
    public async Task Existing_child_cannot_be_silently_moved_to_another_parent()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        var first = new SettingsDto
        {
            Name = "First",
            Contacts = [new ContactDto { Value = "owned by first" }],
            Zones = []
        };
        var second = new SettingsDto { Name = "Second", Contacts = [], Zones = [] };

        await connection.InsertGraphAsync(first);
        await connection.InsertGraphAsync(second);

        second.Contacts =
        [
            new ContactDto
            {
                Id = first.Contacts![0].Id,
                SettingsId = second.Id,
                Value = "attempted move"
            }
        ];

        await Assert.ThrowsAsync<DerafshPersistenceException>(
            () => connection.SynchronizeGraphAsync(second));

        var originalOwner = await connection.ExecuteScalarAsync<int>(
            "SELECT SettingsId FROM dbo.DerafshTest_Contact WHERE Id = @id;",
            new { id = first.Contacts![0].Id });
        Assert.Equal(first.Id, originalOwner);
    }

    [Fact]
    public async Task Update_graph_never_deletes_missing_children()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        var settings = new SettingsDto
        {
            Name = "Store",
            Contacts = [new ContactDto { Value = "one" }, new ContactDto { Value = "two" }],
            Zones = []
        };
        await connection.InsertGraphAsync(settings);

        settings.Contacts = [settings.Contacts![0]];
        settings.Contacts[0].Value = "updated";
        await connection.UpdateGraphAsync(settings);

        var loaded = await connection.LoadGraphAsync<SettingsDto>(settings.Id);
        Assert.Equal(2, loaded!.Contacts!.Count);
        Assert.Contains(loaded.Contacts, c => c.Value == "updated");
        Assert.Contains(loaded.Contacts, c => c.Value == "two");
    }

    [Fact]
    public async Task Load_graphs_loads_multiple_roots()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        var first = new SettingsDto { Name = "First", Contacts = [new ContactDto { Value = "a" }], Zones = [] };
        var second = new SettingsDto { Name = "Second", Contacts = [], Zones = [new ZoneDto { Code = "NL" }] };
        await connection.InsertGraphAsync(first);
        await connection.InsertGraphAsync(second);

        var loaded = await connection.LoadGraphsAsync<SettingsDto>(new[] { first.Id, second.Id });

        Assert.Equal(2, loaded.Count);
        Assert.Equal("First", loaded[0].Name);
        Assert.Equal("Second", loaded[1].Name);
    }

    [Fact]
    public async Task Delete_graph_removes_owned_children_but_not_references()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        var settings = new SettingsDto
        {
            Name = "Delete me",
            Contacts = [new ContactDto { Value = "child" }],
            Zones = [new ZoneDto { Code = "NL" }]
        };
        await connection.InsertGraphAsync(settings);
        await connection.DeleteGraphAsync<SettingsDto>(settings.Id);

        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DerafshTest_Settings;"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DerafshTest_Contact;"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DerafshTest_Zone;"));

        var customer = new CustomerDto { Name = "Shared" };
        await connection.InsertGraphAsync(customer);
        var order = new OrderDto { Number = "SO-REF", Customer = customer };
        await connection.InsertGraphAsync(order);
        await connection.DeleteGraphAsync<OrderDto>(order.Id);

        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DerafshTest_Order;"));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DerafshTest_Customer;"));
    }

    [Fact]
    public async Task Insert_graph_can_reference_an_existing_keyed_row_without_reinserting_it()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        var customer = new CustomerDto { Name = "Existing" };
        await connection.InsertGraphAsync(customer);

        var order = new OrderDto { Number = "SO-EXISTING", Customer = customer };
        await connection.InsertGraphAsync(order);

        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DerafshTest_Customer;"));
    }

    [Fact]
    public async Task Key_only_entity_update_verifies_row_existence()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        await connection.InsertGraphAsync(new KeyOnlyDto { Id = 7 });

        var updated = await connection.UpdateGraphAsync(new KeyOnlyDto { Id = 7 });
        Assert.Equal(0, updated.UpdatedCount);

        await Assert.ThrowsAsync<DerafshPersistenceException>(
            () => connection.UpdateGraphAsync(new KeyOnlyDto { Id = 8 }));
    }

}
