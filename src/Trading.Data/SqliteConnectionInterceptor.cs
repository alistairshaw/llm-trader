using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Trading.Data;

internal sealed class SqliteConnectionInterceptor(int busyTimeoutMilliseconds) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Configure(connection);

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) =>
        await ConfigureAsync(connection, cancellationToken).ConfigureAwait(false);

    private void Configure(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_keys=ON; PRAGMA busy_timeout={busyTimeoutMilliseconds}; PRAGMA journal_mode=WAL;";
        command.ExecuteNonQuery();
    }

    private async Task ConfigureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_keys=ON; PRAGMA busy_timeout={busyTimeoutMilliseconds}; PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
