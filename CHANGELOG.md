# Changelog

All notable changes to Derafsh are documented here.

## 2.0.0 — 2026-09-10

- Rebuilt Derafsh around one focused job: safe relational object-graph persistence on top of Dapper.
- Added `InsertGraphAsync`, `LoadGraphAsync`, `LoadGraphsAsync`, `UpdateGraphAsync`, `SynchronizeGraphAsync`, and `DeleteGraphAsync`.
- Added `[ChildCollection]` and `[Reference]` relationship mapping on top of standard .NET data annotations.
- Added transactional graph writes, generated-key propagation, dependency ordering, cycle detection, and parent-ownership checks for existing children.
- Defined explicit synchronization semantics: `null` child collections are ignored, empty collections delete existing children, and populated collections are authoritative.
- Added official SQL Server and SQLite support with provider-specific SQL generation and fail-fast handling for unsupported ADO.NET providers.
- Added SQL Server and SQLite integration coverage, plus mapping and statement-builder unit tests.
- Added runnable Razor Pages settings samples for SQLite and SQL Server and a reproducible Derafsh-vs-manual-Dapper persistence comparison.
