using System.Data;
using System.Data.Common;
using Derafsh.Internal;

namespace Derafsh.Tests;

public sealed class DatabaseProviderTests
{
    [Fact]
    public void Unsupported_provider_fails_with_clear_message()
    {
        using var connection = new UnsupportedConnection();

        var error = Assert.Throws<NotSupportedException>(() => DatabaseProviderResolver.Resolve(connection));

        Assert.Contains(nameof(UnsupportedConnection), error.Message);
        Assert.Contains("Microsoft.Data.SqlClient", error.Message);
        Assert.Contains("Microsoft.Data.Sqlite", error.Message);
    }

    private sealed class UnsupportedConnection : DbConnection
    {
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Unsupported";
        public override string DataSource => "Unsupported";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Close() { }
        public override void Open() => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}
