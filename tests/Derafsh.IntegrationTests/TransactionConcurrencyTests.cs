using Dapper;
using Microsoft.Data.SqlClient;

namespace Derafsh.IntegrationTests;

public sealed class TransactionConcurrencyTests
{
    [Fact]
    public async Task Failed_graph_insert_can_be_rolled_back_without_leaving_partial_rows()
    {
        await using var connection = await TestDatabase.OpenAndResetAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var graph = new SettingsDto
        {
            Name = "Must roll back",
            Contacts = [new ContactDto { Value = null! }],
            Zones = []
        };

        await Assert.ThrowsAnyAsync<Exception>(
            () => connection.InsertGraphAsync(graph, transaction));

        await transaction.RollbackAsync();

        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.DerafshTest_Settings;"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.DerafshTest_Contact;"));
    }

    [Fact]
    public async Task Concurrent_inserts_on_separate_connections_preserve_both_graphs()
    {
        await using var setupConnection = await TestDatabase.OpenAndResetAsync();

        await using var firstConnection = new SqlConnection(TestDatabase.ConnectionString);
        await using var secondConnection = new SqlConnection(TestDatabase.ConnectionString);
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();

        var first = new SettingsDto
        {
            Name = "Concurrent A",
            Contacts = [new ContactDto { Value = "a@example.com" }],
            Zones = []
        };
        var second = new SettingsDto
        {
            Name = "Concurrent B",
            Contacts = [new ContactDto { Value = "b@example.com" }],
            Zones = []
        };

        await Task.WhenAll(
            firstConnection.InsertGraphAsync(first),
            secondConnection.InsertGraphAsync(second));

        var rows = (await setupConnection.QueryAsync<(int Id, string Name)>(
            "SELECT Id, Name FROM dbo.DerafshTest_Settings WHERE Name IN (@first, @second);",
            new { first = first.Name, second = second.Name })).ToDictionary(x => x.Name);

        Assert.Equal(2, rows.Count);
        Assert.Contains(first.Name, rows.Keys);
        Assert.Contains(second.Name, rows.Keys);

        var childCounts = await setupConnection.QueryAsync<(int SettingsId, int ChildCount)>(
            """
            SELECT s.Id AS SettingsId, COUNT(c.Id) AS ChildCount
            FROM dbo.DerafshTest_Settings s
            LEFT JOIN dbo.DerafshTest_Contact c ON c.SettingsId = s.Id
            WHERE s.Name IN (@first, @second)
            GROUP BY s.Id;
            """,
            new { first = first.Name, second = second.Name });

        Assert.Equal(2, childCounts.Count());
        Assert.All(childCounts, row => Assert.Equal(1, row.ChildCount));
    }
}
