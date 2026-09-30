# Derafsh Settings Sample — SQLite

A small ASP.NET Core Razor Pages application demonstrating Derafsh on a realistic settings/data-entry graph using `Microsoft.Data.Sqlite`.

```text
StoreSettings
├── StoreContact[]
├── ShippingZone[]
└── NotificationRecipient[]
```

The sample uses real foreign keys and indexes. `StoreSettings` is a singleton configuration row with the intentional assigned key `1`; child rows use generated integer keys.

The persistence path is intentionally small:

```csharp
var settings = await connection.LoadGraphAsync<StoreSettingsDto>(1);
await connection.SynchronizeGraphAsync(settings!);
```

## Run

The SQLite database file is created and seeded automatically on first start:

```bash
dotnet run --project samples/Derafsh.SettingsSample
```

The connection string lives in `appsettings.json` under `ConnectionStrings:DerafshSample` and can be overridden with user secrets or `ConnectionStrings__DerafshSample`.

This sample is intentionally UI-light. Its purpose is to show a complete relational form and the persistence orchestration Derafsh removes.
