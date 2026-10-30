using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Shared.Data;

// Every time EF opens a database connection, tell SQL Server who is signed in (SESSION_CONTEXT).
// The audit triggers read it, so each AuditLog row records who made the change.
public class AppUserSessionInterceptor(ICurrentUser currentUser) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetAppUser(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await SetAppUserAsync(connection, cancellationToken);
    }

    private DbCommand BuildCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "EXEC sp_set_session_context @key = N'AppUser', @value = @email";

        // Always set it (null when nobody is signed in) so a pooled connection never keeps a previous user
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@email";
        parameter.Value = (object?)currentUser.Email ?? DBNull.Value;
        command.Parameters.Add(parameter);
        return command;
    }

    private void SetAppUser(DbConnection connection)
    {
        using var command = BuildCommand(connection);
        command.ExecuteNonQuery();
    }

    private async Task SetAppUserAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = BuildCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
