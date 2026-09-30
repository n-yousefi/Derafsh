using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Derafsh.SettingsComparison;

public sealed class ManualDapperSettingsStore(string connectionString)
{
    public async Task<StoreSettingsDto?> LoadAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var settings = await connection.QuerySingleOrDefaultAsync<StoreSettingsDto>(new CommandDefinition(
            "SELECT Id, StoreName, Currency, TimeZone FROM dbo.StoreSettings WHERE Id = @id;",
            new { id }, cancellationToken: cancellationToken));

        if (settings is null)
            return null;

        settings.Contacts = (await connection.QueryAsync<StoreContactDto>(new CommandDefinition(
            "SELECT Id, StoreSettingsId, Type, Value FROM dbo.StoreContact WHERE StoreSettingsId = @id ORDER BY Id;",
            new { id }, cancellationToken: cancellationToken))).AsList();

        settings.ShippingZones = (await connection.QueryAsync<ShippingZoneDto>(new CommandDefinition(
            "SELECT Id, StoreSettingsId, CountryCode, Fee FROM dbo.ShippingZone WHERE StoreSettingsId = @id ORDER BY Id;",
            new { id }, cancellationToken: cancellationToken))).AsList();

        settings.NotificationRecipients = (await connection.QueryAsync<NotificationRecipientDto>(new CommandDefinition(
            "SELECT Id, StoreSettingsId, Email FROM dbo.NotificationRecipient WHERE StoreSettingsId = @id ORDER BY Id;",
            new { id }, cancellationToken: cancellationToken))).AsList();

        return settings;
    }

    public async Task SaveAsync(StoreSettingsDto settings, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.StoreSettings
                SET StoreName = @StoreName, Currency = @Currency, TimeZone = @TimeZone
                WHERE Id = @Id;
                """,
                settings, transaction, cancellationToken: cancellationToken));

            if (affected != 1)
                throw new InvalidOperationException($"Store settings row {settings.Id} was not found.");

            if (settings.Contacts is not null)
                await SynchronizeContactsAsync(connection, transaction, settings, cancellationToken);

            if (settings.ShippingZones is not null)
                await SynchronizeShippingZonesAsync(connection, transaction, settings, cancellationToken);

            if (settings.NotificationRecipients is not null)
                await SynchronizeRecipientsAsync(connection, transaction, settings, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task SynchronizeContactsAsync(
        SqlConnection connection,
        DbTransaction transaction,
        StoreSettingsDto settings,
        CancellationToken cancellationToken)
    {
        foreach (var contact in settings.Contacts!)
        {
            contact.StoreSettingsId = settings.Id;
            if (contact.Id == 0)
            {
                contact.Id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    INSERT dbo.StoreContact (StoreSettingsId, Type, Value)
                    VALUES (@StoreSettingsId, @Type, @Value);
                    SELECT CONVERT(int, SCOPE_IDENTITY());
                    """,
                    contact, transaction, cancellationToken: cancellationToken));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE dbo.StoreContact
                    SET Type = @Type, Value = @Value
                    WHERE Id = @Id AND StoreSettingsId = @StoreSettingsId;
                    """,
                    contact, transaction, cancellationToken: cancellationToken));
            }
        }

        await DeleteMissingAsync(
            connection,
            transaction,
            "StoreContact",
            settings.Id,
            settings.Contacts!.Select(x => x.Id),
            cancellationToken);
    }

    private static async Task SynchronizeShippingZonesAsync(
        SqlConnection connection,
        DbTransaction transaction,
        StoreSettingsDto settings,
        CancellationToken cancellationToken)
    {
        foreach (var zone in settings.ShippingZones!)
        {
            zone.StoreSettingsId = settings.Id;
            if (zone.Id == 0)
            {
                zone.Id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    INSERT dbo.ShippingZone (StoreSettingsId, CountryCode, Fee)
                    VALUES (@StoreSettingsId, @CountryCode, @Fee);
                    SELECT CONVERT(int, SCOPE_IDENTITY());
                    """,
                    zone, transaction, cancellationToken: cancellationToken));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE dbo.ShippingZone
                    SET CountryCode = @CountryCode, Fee = @Fee
                    WHERE Id = @Id AND StoreSettingsId = @StoreSettingsId;
                    """,
                    zone, transaction, cancellationToken: cancellationToken));
            }
        }

        await DeleteMissingAsync(
            connection,
            transaction,
            "ShippingZone",
            settings.Id,
            settings.ShippingZones!.Select(x => x.Id),
            cancellationToken);
    }

    private static async Task SynchronizeRecipientsAsync(
        SqlConnection connection,
        DbTransaction transaction,
        StoreSettingsDto settings,
        CancellationToken cancellationToken)
    {
        foreach (var recipient in settings.NotificationRecipients!)
        {
            recipient.StoreSettingsId = settings.Id;
            if (recipient.Id == 0)
            {
                recipient.Id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    INSERT dbo.NotificationRecipient (StoreSettingsId, Email)
                    VALUES (@StoreSettingsId, @Email);
                    SELECT CONVERT(int, SCOPE_IDENTITY());
                    """,
                    recipient, transaction, cancellationToken: cancellationToken));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE dbo.NotificationRecipient
                    SET Email = @Email
                    WHERE Id = @Id AND StoreSettingsId = @StoreSettingsId;
                    """,
                    recipient, transaction, cancellationToken: cancellationToken));
            }
        }

        await DeleteMissingAsync(
            connection,
            transaction,
            "NotificationRecipient",
            settings.Id,
            settings.NotificationRecipients!.Select(x => x.Id),
            cancellationToken);
    }

    private static Task DeleteMissingAsync(
        SqlConnection connection,
        DbTransaction transaction,
        string table,
        int settingsId,
        IEnumerable<int> submittedIds,
        CancellationToken cancellationToken)
    {
        var ids = submittedIds.Where(id => id != 0).ToArray();
        var sql = ids.Length == 0
            ? $"DELETE dbo.[{table}] WHERE StoreSettingsId = @settingsId;"
            : $"DELETE dbo.[{table}] WHERE StoreSettingsId = @settingsId AND Id NOT IN @ids;";

        return connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { settingsId, ids },
            transaction,
            cancellationToken: cancellationToken));
    }
}
