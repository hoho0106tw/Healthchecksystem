using HealthSys_20260428_v2.csproj.Data;
using Oracle.ManagedDataAccess.Client;
using System.Globalization;

namespace HealthSys_20260428_v2.csproj.Services;

public interface IOPDBasicTbl
{
    Task<OPDBasicTblResult> InsertAsync(
        AppointmentRegistrationKeys keys,
        CancellationToken requestAborted = default);
}

public sealed record OPDBasicTblResult(bool Success, string Message = "");

public sealed class OPDBasicTbl : IOPDBasicTbl
{
    private const string InsertSql = @"
INSERT INTO OPDBasicTbl
(
    CHOP1DATE, CHOP1TIME, CHOP1ROOM, INTOP1NO,
    CHOP1ROOMTYPE, CHOP1MRNO, CHOP1SEC, CHOP1PNAME,
    CHOP1PBRDT, CHOP1PID, CHOP1PSEX, CHOP1PFIN1,
    CHOP1PFIN2, CHOP1INSEQ, CHOP1DRID, CHOP1DRNAME,
    CHOP1OORBTIME, CHOP1EOUTDATE
)
SELECT
    CHOP0DATE, CHOP0TIME, CHOP0ROOM, INTOP0NO,
    CHOP0ROOMTYPE, CHOP0PMRNO, CHOP0SECNO, CHOP0PNAME,
    CHOP0BIRDATE, CHOP0PTID, CHOP0SEX, TRIM(CHOP0FIN1),
    TRIM(CHOP0FIN2), TRIM(CHOP0SEQ), CHOP0DRNO, CHOP0DOCNAME,
    CHOP0CDATE, CHOP0CDATE
FROM OPDREGPTNTBL
WHERE CHOP0DATE = :CHOP0DATE
  AND CHOP0TIME = :CHOP0TIME
  AND CHOP0ROOM = :CHOP0ROOM
  AND INTOP0NO = :INTOP0NO";
    private readonly ILogger<OPDBasicTbl> _logger;

    public OPDBasicTbl(ILogger<OPDBasicTbl> logger)
    {
        _logger = logger;
    }

    public Task<OPDBasicTblResult> InsertAsync(
        AppointmentRegistrationKeys keys,
        CancellationToken requestAborted = default) =>
        ValidateAndInsertAsync(keys, requestAborted);

    private Task<OPDBasicTblResult> ValidateAndInsertAsync(
        AppointmentRegistrationKeys keys, CancellationToken token)
    {
        if (!int.TryParse(keys.No, out var number))
            return Task.FromResult(new OPDBasicTblResult(
                false,
                "OPDBasicTbl 寫入失敗：就診號不是有效數字，未新增醫令。"));
        return ExecuteInsertAsync(keys, number, token);
    }

    private async Task<OPDBasicTblResult> ExecuteInsertCoreAsync(
        AppointmentRegistrationKeys keys, int number, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await Task.CompletedTask;
        return new(true, "示範門診資料已建立");
#if false // Original hospital integration retained for reference.

        using var connection = new OracleConnectionFactory().CreateDbGenConnection();
        await connection.OpenAsync(token);
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = InsertSql;
        command.BindByName = true;
        AddParameters(command, keys, number);
        var affectedRows = await command.ExecuteNonQueryAsync(token);
        if (affectedRows != 1)
        {
            transaction.Rollback();
            _logger.LogWarning("OPDBasicTbl 寫入筆數錯誤：{AffectedRows}", affectedRows);
            return new(false, $"OPDBasicTbl 寫入失敗（實際 {affectedRows} 筆），未新增醫令。");
        }
        transaction.Commit();
        return new(true);
    
#endif
    }

    private async Task<OPDBasicTblResult> ExecuteInsertAsync(
        AppointmentRegistrationKeys keys, int number, CancellationToken token)
    {
        try
        {
            return await ExecuteInsertCoreAsync(keys, number, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "OPDBasicTbl 寫入失敗。");
            return new(false, $"OPDBasicTbl 寫入失敗：{exception.Message}，未新增醫令。");
        }
    }

    private static void AddParameters(
        OracleCommand command, AppointmentRegistrationKeys keys, int number)
    {
        command.Parameters.Add("CHOP0DATE", OracleDbType.Char, 7).Value = DbValue(keys.Date);
        command.Parameters.Add("CHOP0TIME", OracleDbType.Char, 1).Value = DbValue(keys.Time);
        command.Parameters.Add("CHOP0ROOM", OracleDbType.Char, 6).Value = DbValue(keys.Room);
        command.Parameters.Add("INTOP0NO", OracleDbType.Int32).Value = number;
    }

    private static object DbValue(string value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
}
