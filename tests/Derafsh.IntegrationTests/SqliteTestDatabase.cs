using Dapper;
using Microsoft.Data.Sqlite;

namespace Derafsh.IntegrationTests;

internal static class SqliteTestDatabase
{
    public static async Task<SqliteConnection> OpenAndResetAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: cancellationToken));
        return connection;
    }

    private const string SchemaSql = """
        PRAGMA foreign_keys = ON;

        CREATE TABLE DerafshTest_KeyOnly
        (
            Id INTEGER NOT NULL PRIMARY KEY
        );

        CREATE TABLE DerafshTest_Settings
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL
        );

        CREATE TABLE DerafshTest_Contact
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SettingsId INTEGER NOT NULL,
            Value TEXT NOT NULL,
            FOREIGN KEY (SettingsId) REFERENCES DerafshTest_Settings(Id)
        );

        CREATE INDEX IX_DerafshTest_Contact_SettingsId ON DerafshTest_Contact(SettingsId);

        CREATE TABLE DerafshTest_Zone
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SettingsId INTEGER NOT NULL,
            Code TEXT NOT NULL,
            FOREIGN KEY (SettingsId) REFERENCES DerafshTest_Settings(Id)
        );

        CREATE INDEX IX_DerafshTest_Zone_SettingsId ON DerafshTest_Zone(SettingsId);

        CREATE TABLE DerafshTest_Customer
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL
        );

        CREATE TABLE DerafshTest_Order
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CustomerId INTEGER NOT NULL,
            Number TEXT NOT NULL,
            FOREIGN KEY (CustomerId) REFERENCES DerafshTest_Customer(Id)
        );
        """;
}
