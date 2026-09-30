using Microsoft.Data.Sqlite;

namespace Derafsh.SettingsSample.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DerafshSample")
            ?? throw new InvalidOperationException("Connection string 'DerafshSample' is not configured.");

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var schema = """
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS StoreSettings (
            Id INTEGER NOT NULL PRIMARY KEY,
            StoreName TEXT NOT NULL,
            Currency TEXT NOT NULL,
            TimeZone TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS StoreContact (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            StoreSettingsId INTEGER NOT NULL,
            Type TEXT NOT NULL,
            Value TEXT NOT NULL,
            FOREIGN KEY (StoreSettingsId) REFERENCES StoreSettings(Id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS IX_StoreContact_StoreSettingsId
            ON StoreContact(StoreSettingsId);

        CREATE TABLE IF NOT EXISTS ShippingZone (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            StoreSettingsId INTEGER NOT NULL,
            CountryCode TEXT NOT NULL,
            Fee REAL NOT NULL,
            FOREIGN KEY (StoreSettingsId) REFERENCES StoreSettings(Id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS IX_ShippingZone_StoreSettingsId
            ON ShippingZone(StoreSettingsId);

        CREATE TABLE IF NOT EXISTS NotificationRecipient (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            StoreSettingsId INTEGER NOT NULL,
            Email TEXT NOT NULL,
            FOREIGN KEY (StoreSettingsId) REFERENCES StoreSettings(Id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS IX_NotificationRecipient_StoreSettingsId
            ON NotificationRecipient(StoreSettingsId);

        INSERT INTO StoreSettings (Id, StoreName, Currency, TimeZone)
        SELECT 1, 'Derafsh Demo Store', 'EUR', 'Europe/Amsterdam'
        WHERE NOT EXISTS (SELECT 1 FROM StoreSettings WHERE Id = 1);
        """;

        await using var command = connection.CreateCommand();
        command.CommandText = schema;
        await command.ExecuteNonQueryAsync();
    }
}
