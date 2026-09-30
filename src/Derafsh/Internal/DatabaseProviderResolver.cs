using System.Data.Common;

namespace Derafsh.Internal;

internal enum DatabaseProvider
{
    SqlServer,
    Sqlite
}

internal static class DatabaseProviderResolver
{
    public static DatabaseProvider Resolve(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        for (Type? type = connection.GetType(); type is not null; type = type.BaseType)
        {
            switch (type.FullName)
            {
                case "Microsoft.Data.SqlClient.SqlConnection":
                    return DatabaseProvider.SqlServer;
                case "Microsoft.Data.Sqlite.SqliteConnection":
                    return DatabaseProvider.Sqlite;
            }
        }

        throw new NotSupportedException(
            $"Derafsh does not support database provider '{connection.GetType().FullName ?? connection.GetType().Name}'. " +
            "Supported providers are Microsoft.Data.SqlClient (SQL Server) and Microsoft.Data.Sqlite (SQLite).");
    }
}
