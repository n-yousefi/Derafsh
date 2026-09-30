# Derafsh Settings Sample — SQL Server

A small ASP.NET Core Razor Pages application demonstrating the same settings/data-entry graph as the SQLite sample, using `Microsoft.Data.SqlClient`.

```text
StoreSettings
├── StoreContact[]
├── ShippingZone[]
└── NotificationRecipient[]
```

The schema includes foreign keys and indexes. `StoreSettings` is a singleton configuration row with the intentional assigned key `1`; child rows use SQL Server identity keys.

The persistence path is intentionally small:

```csharp
var settings = await connection.LoadGraphAsync<StoreSettingsDto>(1);
await connection.SynchronizeGraphAsync(settings!);
```

## Run

The default connection uses SQL Server LocalDB on Windows. The initializer creates the configured database, schema, and seed row when needed:

```bash
dotnet run --project samples/Derafsh.SettingsSample.SqlServer
```

To target another SQL Server instance, override `ConnectionStrings:DerafshSample` in `appsettings.json`, user secrets, or `ConnectionStrings__DerafshSample`.

Example:

```bash
ConnectionStrings__DerafshSample="Server=localhost,1433;Initial Catalog=DerafshSample;User Id=sa;Password=your-password;Encrypt=False" \
  dotnet run --project samples/Derafsh.SettingsSample.SqlServer
```

This sample is intentionally UI-light. Its purpose is to show a complete relational form and the persistence orchestration Derafsh removes.
