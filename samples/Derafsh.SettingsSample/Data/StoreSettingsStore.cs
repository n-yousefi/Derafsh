using Derafsh;
using Microsoft.Data.Sqlite;

namespace Derafsh.SettingsSample.Data;

public sealed class StoreSettingsStore(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DerafshSample")
        ?? throw new InvalidOperationException("Connection string 'DerafshSample' is not configured.");

    public async Task<StoreSettingsDto> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.LoadGraphAsync<StoreSettingsDto>(1, cancellationToken)
            ?? throw new InvalidOperationException("Store settings row 1 was not found.");
    }

    public async Task<GraphWriteResult> SaveAsync(
        StoreSettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.SynchronizeGraphAsync(settings, cancellationToken);
    }
}
