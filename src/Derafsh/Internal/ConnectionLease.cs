using System.Data;
using System.Data.Common;

namespace Derafsh.Internal;

internal sealed class ConnectionLease : IAsyncDisposable
{
    private readonly DbConnection _connection;
    private readonly bool _openedHere;

    private ConnectionLease(DbConnection connection, bool openedHere)
    {
        _connection = connection;
        _openedHere = openedHere;
    }

    public static async Task<ConnectionLease> AcquireAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.State == ConnectionState.Broken)
            await connection.CloseAsync();

        var openedHere = connection.State == ConnectionState.Closed;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        return new ConnectionLease(connection, openedHere);
    }

    public async ValueTask DisposeAsync()
    {
        if (_openedHere)
            await _connection.CloseAsync();
    }
}
