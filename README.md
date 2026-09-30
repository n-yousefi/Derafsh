# Derafsh

[![CI](https://github.com/n-yousefi/Derafsh/actions/workflows/ci.yml/badge.svg)](https://github.com/n-yousefi/Derafsh/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

**Relational graph persistence for .NET applications that use Dapper.**

> Define a small relational graph once. Load it and save it as one operation. Keep Dapper and SQL for everything else.

Derafsh removes the repetitive multi-table persistence code behind features such as **application settings, master data, admin panels, customer entry, product entry, and other relational CRUD screens**.

It is intentionally not a full ORM. There is no `DbContext`, change tracker, LINQ provider, repository framework, migration system, proxy generation, or application-wide data-access abstraction.

**Dapper handles rows. Derafsh handles graphs.**

## Why Derafsh exists

A settings page can look simple while spanning several tables:

```text
StoreSettings
├── Contacts[]
├── ShippingZones[]
└── NotificationRecipients[]
```

Saving it manually usually means coordinating all of this:

```text
load the root row
load each child collection
begin a transaction
update the root row
insert new children
update existing children
propagate generated keys
delete removed children
commit / rollback
```

With Derafsh, the persistence path becomes:

```csharp
var settings = await connection.LoadGraphAsync<StoreSettingsDto>(1);

// The application edits the graph...

await connection.SynchronizeGraphAsync(settings!);
```

That focused graph-persistence problem is the reason to use Derafsh.

## Supported databases

Derafsh 2.0 officially supports:

| Database | ADO.NET provider | Status |
|---|---|---|
| SQL Server | `Microsoft.Data.SqlClient` | Supported and integration-tested |
| SQLite | `Microsoft.Data.Sqlite` | Supported and integration-tested |

Derafsh does not silently guess SQL syntax for unknown providers. PostgreSQL, MySQL, Oracle, and other providers currently fail fast with a clear `NotSupportedException`.

This is deliberate: a small list of providers with tested persistence semantics is better than broad but unreliable compatibility.

SQL Server honors `[Table(..., Schema = "...")]`. SQLite uses the mapped table name and intentionally ignores SQL Server-style schema names.

## 60-second example

```csharp
using Derafsh;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("StoreSettings")]
public sealed class StoreSettingsDto
{
    [Key]
    public int Id { get; set; }

    public string StoreName { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public string TimeZone { get; set; } = "Europe/Amsterdam";

    [ChildCollection(nameof(StoreContactDto.StoreSettingsId))]
    public List<StoreContactDto>? Contacts { get; set; } = [];

    [ChildCollection(nameof(ShippingZoneDto.StoreSettingsId))]
    public List<ShippingZoneDto>? ShippingZones { get; set; } = [];
}

[Table("StoreContact")]
public sealed class StoreContactDto
{
    [Key] public int Id { get; set; }
    public int StoreSettingsId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

[Table("ShippingZone")]
public sealed class ShippingZoneDto
{
    [Key] public int Id { get; set; }
    public int StoreSettingsId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public decimal Fee { get; set; }
}
```

The persistence adapter stays small:

```csharp
using Derafsh;
using Microsoft.Data.SqlClient;

public sealed class StoreSettingsStore(string connectionString)
{
    public async Task<StoreSettingsDto?> LoadAsync(int id, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.LoadGraphAsync<StoreSettingsDto>(id, ct);
    }

    public async Task<GraphWriteResult> SaveAsync(StoreSettingsDto settings, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.SynchronizeGraphAsync(settings, ct);
    }
}
```

The same Derafsh API works with `Microsoft.Data.Sqlite.SqliteConnection`.

## Core API

### Insert

```csharp
var result = await connection.InsertGraphAsync(customer);
```

Derafsh inserts references before the object that owns their foreign key, inserts parents before owned children, and propagates generated keys back into the graph.

### Load one graph

```csharp
var customer = await connection.LoadGraphAsync<CustomerDto>(id);
```

Mapped references and child collections are recursively hydrated.

### Load multiple graphs

```csharp
var customers = await connection.LoadGraphsAsync<CustomerDto>(ids);
```

The load operation reuses an identity cache so shared references loaded during that call reuse the same object instance.

### Update without deleting missing children

```csharp
await connection.UpdateGraphAsync(customer);
```

Use `UpdateGraphAsync` when the submitted graph may be partial. Keyed objects are updated and new nested objects are inserted, but children omitted from a collection are **not deleted**.

### Synchronize authoritative child collections

```csharp
await connection.SynchronizeGraphAsync(customer);
```

Collection semantics are explicit:

| Collection value | Behavior |
|---|---|
| `null` | Not supplied; leave existing database children unchanged |
| `[]` | Authoritatively empty; delete existing mapped children |
| non-empty | Insert/update submitted children and delete missing existing children |

For existing keyed children, Derafsh also verifies that the row already belongs to the submitted parent. It fails instead of silently moving a child from another aggregate.

### Delete

```csharp
await connection.DeleteGraphAsync<CustomerDto>(id);
```

Owned child collections are deleted before their parent. Rows reached through `[Reference]` are not deleted because references may be shared.

## Relationship model

Derafsh adds only two relationship concepts on top of standard .NET data annotations.

### `ChildCollection`

Use `[ChildCollection]` when the foreign key lives on the child row:

```csharp
[ChildCollection(nameof(PhoneDto.CustomerId))]
public List<PhoneDto>? Phones { get; set; } = [];
```

```text
Customer.Id ─────→ Phone.CustomerId
```

On insert, Derafsh persists the parent, obtains its key, copies the key into each child foreign key, and then inserts the children.

### `Reference`

Use `[Reference]` when the foreign key lives on the current object:

```csharp
public int CustomerId { get; set; }

[Reference(nameof(CustomerId))]
public CustomerDto? Customer { get; set; }
```

```text
Order.CustomerId ─────→ Customer.Id
```

For a new reference object, Derafsh persists the reference first and propagates its key to the owner. A reference navigation must be readable and writable so graph loading can hydrate it.

## Mapping conventions

Derafsh supports standard attributes from `System.ComponentModel.DataAnnotations` and `System.ComponentModel.DataAnnotations.Schema`:

```csharp
[Table("Customer")]
public sealed class CustomerDto
{
    [Key]
    public int Id { get; set; }

    [Column("display_name")]
    public string Name { get; set; } = string.Empty;

    [NotMapped]
    public string? UiOnlyValue { get; set; }
}
```

Key conventions:

- `[Key]` is preferred; otherwise `Id` or `<TypeName>Id` is used.
- Composite keys are not supported.
- Integer keys are treated as database-generated unless `[DatabaseGenerated(DatabaseGeneratedOption.None)]` is specified.
- Empty client-assigned `Guid` keys are generated by Derafsh.
- Computed/generated scalar properties are excluded from inserts and updates.

The `Dto` suffix has no special meaning. Derafsh does not require a base class, interface, marker type, repository, or context.

## Transactions and connections

Graph writes are atomic by default:

```csharp
await connection.SynchronizeGraphAsync(settings);
```

If the caller does not supply a transaction, Derafsh opens one, commits on success, and rolls back on failure.

You can provide your own transaction:

```csharp
await using var transaction = await connection.BeginTransactionAsync();

await connection.SynchronizeGraphAsync(settings, transaction);
await connection.ExecuteAsync(otherSql, otherArgs, transaction);

await transaction.CommitAsync();
```

Caller-owned transactions must belong to the same `DbConnection`.

Derafsh opens a closed connection for the operation and closes it afterward. If the caller supplied an already-open connection, Derafsh leaves it open.

## Dapper stays available

Derafsh is not a query language. Keep using Dapper for queries that deserve explicit SQL:

```csharp
var rows = await connection.QueryAsync<SalesSummaryDto>(
    """
    SELECT Region, SUM(Total) AS Revenue
    FROM Sales
    WHERE CreatedAt >= @from
    GROUP BY Region
    """,
    new { from });
```

A healthy application can use:

```text
Derafsh → repetitive graph CRUD
Dapper  → custom queries and commands
SQL     → reporting, bulk work, provider-specific optimization
```

## Where Derafsh fits best

Derafsh is strongest for small and medium relational aggregates whose persistence is mostly mechanical:

- application or tenant settings;
- master-data screens;
- customer/contact entry;
- employee/profile entry;
- product entry with prices, barcodes, or attributes;
- admin/back-office forms;
- configuration pages with multiple child collections;
- ordinary CRUD aggregates in Dapper-based applications.

Examples:

```text
Customer
├── Phones[]
├── Addresses[]
└── Contacts[]
```

```text
Product
├── Barcodes[]
├── Prices[]
└── Attributes[]
```

## Where Derafsh does not fit

Use normal Dapper/SQL, EF Core, or a specialized tool when the problem is primarily:

- reporting and analytics;
- complex search/filter/query composition;
- large read models;
- bulk import/export or ETL;
- very large graphs;
- many-to-many graph synchronization;
- polymorphic persistence;
- composite keys;
- change tracking or unit-of-work semantics;
- schema migrations;
- LINQ-based querying;
- lazy loading;
- transparent portability across unsupported database providers.

The current read strategy intentionally uses multiple parameterized queries while hydrating a graph. It targets data-entry/settings aggregates, not large analytical graphs.

## Safety and correctness

Derafsh is built around a few non-negotiable behaviors:

- runtime values are parameterized;
- identifiers come from mapping metadata, not submitted values;
- unknown database providers fail fast;
- writes are transactional by default;
- caller-owned transactions are validated;
- generated keys are propagated through the graph;
- insert/update dependency cycles are detected;
- destructive collection synchronization is explicit;
- existing child updates are constrained to the submitted parent;
- `null` child collections never mean delete-all;
- updates of key-only/computed-only entities still verify row existence and ownership;
- mapping metadata is cached.

Authorization remains the application's responsibility. Derafsh's parent-ownership checks protect graph integrity; they are not a substitute for user or tenant authorization.

## How much persistence code does it remove?

The repository contains the same settings persistence feature implemented with Derafsh and with manual Dapper:

- [`DerafshSettingsStore.cs`](comparison/Derafsh.SettingsComparison/DerafshSettingsStore.cs)
- [`ManualDapperSettingsStore.cs`](comparison/Derafsh.SettingsComparison/ManualDapperSettingsStore.cs)

The comparison counts persistence-adapter code only.

| Implementation | Non-blank, non-comment persistence lines |
|---|---:|
| Derafsh | **16** |
| Manual Dapper | **189** |
| Reduction | **91.5%** |

Recalculate it:

```bash
python3 scripts/count-comparison.py
```

This does not mean every application becomes 91.5% smaller. Validation, authorization, UI, business rules, reporting, and custom queries still belong to the application.

## Samples

### SQLite: zero-setup sample

[`samples/Derafsh.SettingsSample`](samples/Derafsh.SettingsSample) is an ASP.NET Core Razor Pages settings application backed by SQLite. The database file, schema, foreign keys, indexes, and seed row are created on first run.

```bash
dotnet run --project samples/Derafsh.SettingsSample
```

### SQL Server sample

[`samples/Derafsh.SettingsSample.SqlServer`](samples/Derafsh.SettingsSample.SqlServer) demonstrates the same graph against SQL Server. It uses LocalDB by default on Windows; the connection string can be replaced with any reachable SQL Server instance.

```bash
dotnet run --project samples/Derafsh.SettingsSample.SqlServer
```

Both samples edit the same conceptual graph:

```text
StoreSettings
├── StoreContact[]
├── ShippingZone[]
└── NotificationRecipient[]
```

The settings root uses an intentionally assigned key (`1`) because it represents a singleton application-settings record. Child rows still demonstrate generated integer keys and graph synchronization.

## Installation

Install Derafsh:

```bash
dotnet add package Derafsh --version 2.0.0
```

Install the ADO.NET provider your application uses:

```bash
# SQL Server
dotnet add package Microsoft.Data.SqlClient

# or SQLite
dotnet add package Microsoft.Data.Sqlite
```

Derafsh itself depends on Dapper and intentionally does not create or configure application connections.

When working directly from this repository, the samples use a project reference to `src/Derafsh/Derafsh.csproj`.

## Requirements and current boundaries

- .NET 10;
- SQL Server through `Microsoft.Data.SqlClient`, or SQLite through `Microsoft.Data.Sqlite`;
- one scalar primary key per mapped type;
- no composite-key support;
- no many-to-many mapping abstraction;
- no polymorphic graph mapping;
- no migration system;
- no LINQ provider;
- no change tracker;
- no lazy loading;
- no bulk API;
- updates write mapped scalar columns instead of computing property-level diffs.

## Why not EF Core?

Use EF Core when you want a full ORM: LINQ translation, change tracking, migrations, rich relationship modeling, broad provider support, and a `DbContext`-centered unit of work.

Use Derafsh when you deliberately prefer Dapper/SQL for the application but do not want to repeatedly hand-write mechanical multi-table graph CRUD.

Derafsh is not trying to be a smaller EF Core. It is trying to remove one specific class of persistence boilerplate without taking ownership of the rest of your data-access architecture.

## Repository layout

```text
Derafsh/
├── src/Derafsh/                           # Runtime library
├── tests/Derafsh.Tests/                   # Mapping/provider/SQL unit tests
├── tests/Derafsh.IntegrationTests/        # SQL Server + SQLite graph tests
├── samples/Derafsh.SettingsSample/        # SQLite Razor Pages sample
├── samples/Derafsh.SettingsSample.SqlServer/ # SQL Server Razor Pages sample
├── comparison/Derafsh.SettingsComparison/ # Derafsh vs manual Dapper
└── scripts/count-comparison.py            # Reproducible LOC comparison
```

CI restores, builds, runs unit tests, runs SQL Server and SQLite integration tests, verifies the persistence comparison, and packs the NuGet package.

## Versioning

The package version in this repository is **2.0.0**.

See [CHANGELOG.md](CHANGELOG.md) for release notes.

## Contributing

Issues and focused pull requests are welcome. Derafsh should remain small; new features should directly improve safe relational graph persistence instead of expanding it into a general application framework.

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Apache-2.0. See [LICENSE](LICENSE).
