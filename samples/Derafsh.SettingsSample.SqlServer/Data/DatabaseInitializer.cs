using Microsoft.Data.SqlClient;

namespace Derafsh.SettingsSample.SqlServer.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DerafshSample")
            ?? throw new InvalidOperationException("Connection string 'DerafshSample' is not configured.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("The 'DerafshSample' connection string must specify an Initial Catalog.");

        var masterConnectionString = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;

        await using var masterConnection = new SqlConnection(masterConnectionString);
        await masterConnection.OpenAsync();
        await using (var createDb = masterConnection.CreateCommand())
        {
            createDb.CommandText = """
                IF DB_ID(@databaseName) IS NULL
                BEGIN
                    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName);
                    EXEC sys.sp_executesql @sql;
                END;
                """;
            createDb.Parameters.AddWithValue("@databaseName", databaseName);
            await createDb.ExecuteNonQueryAsync();
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.StoreSettings', N'U') IS NULL
            CREATE TABLE dbo.StoreSettings (
                Id int NOT NULL CONSTRAINT PK_StoreSettings PRIMARY KEY,
                StoreName nvarchar(120) NOT NULL,
                Currency nvarchar(3) NOT NULL,
                TimeZone nvarchar(80) NOT NULL
            );

            IF OBJECT_ID(N'dbo.StoreContact', N'U') IS NULL
            CREATE TABLE dbo.StoreContact (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StoreContact PRIMARY KEY,
                StoreSettingsId int NOT NULL,
                Type nvarchar(20) NOT NULL,
                Value nvarchar(160) NOT NULL,
                CONSTRAINT FK_StoreContact_StoreSettings FOREIGN KEY (StoreSettingsId)
                    REFERENCES dbo.StoreSettings(Id) ON DELETE CASCADE
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoreContact_StoreSettingsId' AND object_id = OBJECT_ID(N'dbo.StoreContact'))
                CREATE INDEX IX_StoreContact_StoreSettingsId ON dbo.StoreContact(StoreSettingsId);

            IF OBJECT_ID(N'dbo.ShippingZone', N'U') IS NULL
            CREATE TABLE dbo.ShippingZone (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShippingZone PRIMARY KEY,
                StoreSettingsId int NOT NULL,
                CountryCode char(2) NOT NULL,
                Fee decimal(18,2) NOT NULL,
                CONSTRAINT FK_ShippingZone_StoreSettings FOREIGN KEY (StoreSettingsId)
                    REFERENCES dbo.StoreSettings(Id) ON DELETE CASCADE
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ShippingZone_StoreSettingsId' AND object_id = OBJECT_ID(N'dbo.ShippingZone'))
                CREATE INDEX IX_ShippingZone_StoreSettingsId ON dbo.ShippingZone(StoreSettingsId);

            IF OBJECT_ID(N'dbo.NotificationRecipient', N'U') IS NULL
            CREATE TABLE dbo.NotificationRecipient (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationRecipient PRIMARY KEY,
                StoreSettingsId int NOT NULL,
                Email nvarchar(200) NOT NULL,
                CONSTRAINT FK_NotificationRecipient_StoreSettings FOREIGN KEY (StoreSettingsId)
                    REFERENCES dbo.StoreSettings(Id) ON DELETE CASCADE
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NotificationRecipient_StoreSettingsId' AND object_id = OBJECT_ID(N'dbo.NotificationRecipient'))
                CREATE INDEX IX_NotificationRecipient_StoreSettingsId ON dbo.NotificationRecipient(StoreSettingsId);

            IF NOT EXISTS (SELECT 1 FROM dbo.StoreSettings WHERE Id = 1)
                INSERT INTO dbo.StoreSettings (Id, StoreName, Currency, TimeZone)
                VALUES (1, N'Derafsh Demo Store', N'EUR', N'Europe/Amsterdam');
            """;
        await command.ExecuteNonQueryAsync();
    }

}
