# Contributing to Derafsh

Derafsh is intentionally small. Contributions are welcome when they improve safe, predictable relational graph persistence without turning the project into a general ORM or application framework.

## Good feature scope

A feature is a good fit when it directly improves graph mapping, dependency ordering, generated-key propagation, graph loading, transactional writes, child synchronization, provider correctness, parameter safety, diagnostics, or measurable performance of the existing graph operations.

Features such as schema migrations, a LINQ provider, repositories, a `DbContext` equivalent, UI generation, authorization, caching, or application scaffolding should normally live elsewhere.

Database-provider additions must include a deliberate SQL dialect implementation and integration tests. Derafsh should never claim compatibility by merely falling back to another provider's SQL syntax.

## Development

Requirements:

- .NET 10 SDK;
- Docker or another reachable SQL Server instance for SQL Server integration tests.

Run unit tests:

```bash
dotnet test tests/Derafsh.Tests/Derafsh.Tests.csproj
```

Run integration tests (SQLite tests are self-contained; SQL Server tests use the configured connection):

```bash
DERAFSH_TEST_CONNECTION_STRING="your SQL Server connection string" \
  dotnet test tests/Derafsh.IntegrationTests/Derafsh.IntegrationTests.csproj
```

Build everything:

```bash
dotnet build Derafsh.sln
```

Recalculate the persistence comparison:

```bash
python3 scripts/count-comparison.py
```

## Pull requests

Keep pull requests focused. Add tests for behavior changes and update the README when public behavior or constraints change. Public API additions should be justified by a concrete graph-persistence use case.
