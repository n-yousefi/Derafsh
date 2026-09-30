# Settings Persistence Comparison

This project implements the same settings persistence adapter in two ways:

- `DerafshSettingsStore.cs` uses Derafsh graph operations.
- `ManualDapperSettingsStore.cs` uses explicit Dapper orchestration.

Both implementations use the same models and SQL shape. The comparison is not intended to criticize Dapper; Derafsh itself uses Dapper. It isolates the repetitive graph CRUD that applications otherwise have to write around Dapper.

Run the repository-level counter:

```bash
python3 scripts/count-comparison.py
```

The counter reports non-blank, non-comment physical lines for the two persistence adapter files only.
