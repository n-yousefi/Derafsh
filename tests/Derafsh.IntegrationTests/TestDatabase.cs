using Dapper;
using Microsoft.Data.SqlClient;

namespace Derafsh.IntegrationTests;

internal static class TestDatabase
{
    private const string DefaultConnectionString =
        "Server=localhost,1433;Database=master;User Id=sa;Password=Derafsh_Test_2026!;Encrypt=False;TrustServerCertificate=True";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("DERAFSH_TEST_CONNECTION_STRING") ?? DefaultConnectionString;

    public static async Task<SqlConnection> OpenAndResetAsync(CancellationToken cancellationToken = default)
    {
        var connection = await OpenWithRetryAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: cancellationToken));
        return connection;
    }

    private static async Task<SqlConnection> OpenWithRetryAsync(CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var connection = new SqlConnection(ConnectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch (Exception error) when (error is SqlException or InvalidOperationException)
            {
                lastError = error;
                await connection.DisposeAsync();
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        throw new InvalidOperationException("SQL Server was not reachable for Derafsh integration tests.", lastError);
    }

    private const string SchemaSql = """
        IF OBJECT_ID(N'dbo.DerafshTest_Order', N'U') IS NOT NULL DROP TABLE dbo.DerafshTest_Order;
        IF OBJECT_ID(N'dbo.DerafshTest_KeyOnly', N'U') IS NOT NULL DROP TABLE dbo.DerafshTest_KeyOnly;
        IF OBJECT_ID(N'dbo.DerafshTest_Customer', N'U') IS NOT NULL DROP TABLE dbo.DerafshTest_Customer;
        IF OBJECT_ID(N'dbo.DerafshTest_Zone', N'U') IS NOT NULL DROP TABLE dbo.DerafshTest_Zone;
        IF OBJECT_ID(N'dbo.DerafshTest_Contact', N'U') IS NOT NULL DROP TABLE dbo.DerafshTest_Contact;
        IF OBJECT_ID(N'dbo.DerafshTest_Settings', N'U') IS NOT NULL DROP TABLE dbo.DerafshTest_Settings;

        CREATE TABLE dbo.DerafshTest_KeyOnly
        (
            Id int NOT NULL PRIMARY KEY
        );

        CREATE TABLE dbo.DerafshTest_Settings
        (
            Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            Name nvarchar(120) NOT NULL
        );

        CREATE TABLE dbo.DerafshTest_Contact
        (
            Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            SettingsId int NOT NULL,
            Value nvarchar(200) NOT NULL,
            CONSTRAINT FK_DerafshTest_Contact_Settings FOREIGN KEY (SettingsId) REFERENCES dbo.DerafshTest_Settings(Id)
        );

        CREATE TABLE dbo.DerafshTest_Zone
        (
            Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            SettingsId int NOT NULL,
            Code nvarchar(10) NOT NULL,
            CONSTRAINT FK_DerafshTest_Zone_Settings FOREIGN KEY (SettingsId) REFERENCES dbo.DerafshTest_Settings(Id)
        );

        CREATE TABLE dbo.DerafshTest_Customer
        (
            Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            Name nvarchar(120) NOT NULL
        );

        CREATE TABLE dbo.DerafshTest_Order
        (
            Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            CustomerId int NOT NULL,
            Number nvarchar(40) NOT NULL,
            CONSTRAINT FK_DerafshTest_Order_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.DerafshTest_Customer(Id)
        );
        """;
}
