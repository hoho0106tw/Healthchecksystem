using HealthSys_20260428_v2.csproj.Data;

namespace HealthSys_20260428_v2.csproj.Services;

public sealed record InaiLogResult(bool Success, bool Persisted, string Status, string? SessionId = null);
public interface IInaiLogService
{
    Task<InaiLogResult> StartSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<InaiLogResult> WriteMessageAsync(string sessionId, InaiMessage message, CancellationToken cancellationToken = default);
    Task<InaiLogResult> CompleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<InaiLogResult> WriteErrorAsync(string sessionId, string status, CancellationToken cancellationToken = default);
    Task<InaiLogResult> CheckLogTablesReadyAsync(CancellationToken cancellationToken = default);
}

// No database writes until table readiness and explicit log recording authorization are established.
public sealed class DisabledInaiLogService : IInaiLogService
{
    private static Task<InaiLogResult> Disabled(string sessionId) => Task.FromResult(new InaiLogResult(true, false, "Disabled", sessionId));
    public Task<InaiLogResult> StartSessionAsync(string sessionId, CancellationToken cancellationToken = default) => Disabled(sessionId);
    public Task<InaiLogResult> WriteMessageAsync(string sessionId, InaiMessage message, CancellationToken cancellationToken = default) => Disabled(sessionId);
    public Task<InaiLogResult> CompleteSessionAsync(string sessionId, CancellationToken cancellationToken = default) => Disabled(sessionId);
    public Task<InaiLogResult> WriteErrorAsync(string sessionId, string status, CancellationToken cancellationToken = default) => Disabled(sessionId);

    public async Task<InaiLogResult> CheckLogTablesReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new OracleConnectionFactory().CreateDbGenConnection();
            await connection.OpenAsync(cancellationToken);
            foreach (var table in new[] { "INAI_CHAT_SESSION_TBL", "INAI_CHAT_MESSAGE_TBL" })
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT 1 FROM GUID_AP01.{table} WHERE 1 = 0";
                command.CommandTimeout = 5;
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
            }
            return new(true, false, "TablesReadyLoggingDisabled");
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            return new(false, false, $"TablePreflightFailed:ORA-{ex.Number:D5}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(false, false, $"TablePreflightUnavailable:{ex.GetType().Name}");
        }
    }
}
