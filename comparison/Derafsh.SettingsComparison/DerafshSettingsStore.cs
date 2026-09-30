using Derafsh;
using Microsoft.Data.SqlClient;

namespace Derafsh.SettingsComparison;

public sealed class DerafshSettingsStore(string connectionString)
{
    public async Task<StoreSettingsDto?> LoadAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.LoadGraphAsync<StoreSettingsDto>(id, cancellationToken);
    }

    public async Task SaveAsync(StoreSettingsDto settings, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.SynchronizeGraphAsync(settings, cancellationToken);
    }
}
