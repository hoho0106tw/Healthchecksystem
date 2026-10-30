using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using System.Globalization;

namespace HealthSys_20260428_v2.csproj.Services;

public interface IAppointmentRegistrationService
{
    Task<AppointmentRegistrationExecutionResult> RegisterAsync(
        BathRegisteredItemPostModel row,
        string? selfPayAccountingUnit = null,
        bool useAgainGetRegNo = false);

    int CancelRegistration(
        string registrationNo,
        string registrationDate,
        string registrationTime,
        string registrationRoom);

    AppointmentRegistrationPreparationResult PrepareSupplementOrderKeys(
        BathRegisteredItemPostModel row,
        string registrationNo,
        string? registrationDate,
        string? registrationTime,
        string? registrationRoom);

    BathRegisteredRowResultViewModel CompleteRegistrations(
        BathRegisteredItemPostModel row,
        IReadOnlyList<AppointmentRegistrationExecutionResult> registrationResults);
}

public sealed record AppointmentRegistrationKeys(
    string Date,
    string Time,
    string Room,
    string No,
    string MedicalRecordNumber,
    string Section,
    string PFin1,
    string PFin2,
    string RegistrationStatus,
    string PaymentType,
    string PaymentCode,
    string UserId,
    string DoctorId,
    string DoctorName,
    int RecordNumber);

public sealed record AppointmentRegistrationExecutionResult(
    BathRegisteredRowResultViewModel RowResult,
    AppointmentRegistrationKeys? Keys,
    string RegistrationOrderSequence = "",
    string OrderNumber = "")
{
    public bool Success => RowResult.Success && Keys is not null;
}

public sealed record AppointmentRegistrationPreparationResult(
    bool Success,
    AppointmentRegistrationKeys? Keys,
    string Message = "");

public sealed class AppointmentRegistrationService(RegHospitalApiClient registrationApi) : IAppointmentRegistrationService
{
    public int CancelRegistration(
        string registrationNo,
        string registrationDate,
        string registrationTime,
        string registrationRoom)
    {
        return 0; // 示範取消掛號成功，不呼叫院內 COM。
#if false // Original hospital integration retained for reference.

        var connection = new RegForFEH.ClsRegForFEH();
        connection.SetOpdConnect(null);
        connection.SetRDB_System_Name = "ORC8";

        const string dcUserId = "90472";
        const bool createNewTrans = true;

        var dcRegNoResult = connection.DcRegNo(
            registrationNo,
            registrationDate,
            registrationTime,
            registrationRoom,
            dcUserId,
            createNewTrans);

        return dcRegNoResult;
    
#endif
    }

    public AppointmentRegistrationPreparationResult PrepareSupplementOrderKeys(
        BathRegisteredItemPostModel row,
        string registrationNo,
        string? registrationDate,
        string? registrationTime,
        string? registrationRoom)
    {
        const string regSecNo = "0294C";
        const string pFin1 = "01";
        const string pRegStat = "2";
        const string pPayt = "";
        const string pPay = "A12";
        const string cUserNo = "90472";

        var regDate = string.IsNullOrWhiteSpace(registrationDate)
            ? NormalizeRocAppointmentDate(row.AppointmentDate)
            : registrationDate.Trim();
        var regTime = string.IsNullOrWhiteSpace(registrationTime)
            ? NormalizeAppointmentRegTime(row.Time)
            : registrationTime.Trim();
        var room = string.IsNullOrWhiteSpace(registrationRoom)
            ? row.NormalizedRoomNo
            : registrationRoom.Trim();
        var regNo = (registrationNo ?? string.Empty).Trim();
        var mrno = row.NormalizedPatNo;
        var pFin2 = string.IsNullOrWhiteSpace(row.NormalizedSelfPayAccountingUnit)
            ? "00"
            : row.NormalizedSelfPayAccountingUnit;
        var regDocNo = row.NormalizedDoctorId;
        var regDocName = row.NormalizedDoctorName;

        if (string.IsNullOrWhiteSpace(regDate)
            || string.IsNullOrWhiteSpace(regTime)
            || string.IsNullOrWhiteSpace(room)
            || string.IsNullOrWhiteSpace(regNo)
            || string.IsNullOrWhiteSpace(mrno)
            || string.IsNullOrWhiteSpace(regDocNo)
            || string.IsNullOrWhiteSpace(pFin2))
        {
            return new AppointmentRegistrationPreparationResult(
                false,
                null,
                "缺少已掛號日期、時段、診間、就診號、病歷號、醫師或優待身分，無法補下醫令。");
        }

        return new AppointmentRegistrationPreparationResult(
            true,
            new AppointmentRegistrationKeys(
                regDate,
                regTime,
                room,
                regNo,
                mrno,
                regSecNo,
                pFin1,
                pFin2,
                pRegStat,
                pPayt,
                pPay,
                cUserNo,
                regDocNo,
                regDocName,
                0));
    }

    public async Task<AppointmentRegistrationExecutionResult> RegisterAsync(
        BathRegisteredItemPostModel row,
        string? selfPayAccountingUnit = null,
        bool useAgainGetRegNo = false)
    {
        var processedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        var regDate = NormalizeRocAppointmentDate(row.AppointmentDate);
        var regTime = NormalizeAppointmentRegTime(row.Time);
        var regRoom = row.NormalizedRoomNo;
        var mrno = row.NormalizedPatNo;
        var pFin2 = "00";

        if (!string.IsNullOrWhiteSpace(row.NormalizedSelfPayAccountingUnit))
        {
            pFin2 = row.NormalizedSelfPayAccountingUnit;
        }

        if (!string.IsNullOrWhiteSpace(selfPayAccountingUnit))
        {
            pFin2 = selfPayAccountingUnit.Trim();
        }

        if (string.IsNullOrWhiteSpace(regDate)
            || string.IsNullOrWhiteSpace(regTime)
            || string.IsNullOrWhiteSpace(regRoom)
            || string.IsNullOrWhiteSpace(mrno)
            || string.IsNullOrWhiteSpace(row.NormalizedDoctorId)
            || string.IsNullOrWhiteSpace(pFin2))
        {
            return CreateExecutionResult(row, false, "缺少掛號日期、時段、病歷號、看診醫師員編或優待身分代碼，無法掛號。", processedAt);
        }

        try
        {
            var regDocNo = row.NormalizedDoctorId;
            const string regSecNo = "0294C";
            const string pFin1 = "01";
            const string pRegStat = "2";
            const string pPayt = "";
            const string pPay = "A12";
            const string cUserNo = "90472";
            var regDocName = row.NormalizedDoctorName;
            const short op0RecNo = 0;
            const string op0RegOrd = "";

            var request = new GetRegNoRequest
            {
                RegNo = null,
                RegDate = regDate,
                RegTime = regTime,
                RegRoom = regRoom,
                RegDocNo = regDocNo,
                RegSecNo = regSecNo,
                RegSource = useAgainGetRegNo ? 20 : 77,
                MrNo = mrno,
                PFin1 = pFin1,
                PFin2 = pFin2,
                PRegStat = pRegStat,
                PPayt = pPayt,
                PCardNo = "",
                PPay = pPay,
                CUserNo = cUserNo,
                BorPaper = false,
                ContrlStat = 1,
                Op0RecNo = op0RecNo,
                Op0RegOrd = op0RegOrd,
                Op0RegAmt = 0,
                Op0PayAmt = 0,
                Op0HPayAmt = 0,
                TransHospNo = "",
                BorwX = false,
                RegDocName = regDocName,
                RegSecName = "高檢"
            };
            var response = useAgainGetRegNo
                ? await registrationApi.AgainGetRegNoAsync(request)
                : await registrationApi.GetRegNoAsync(request);

            if (!response.HasRegistration)
            {
                return CreateExecutionResult(row, false, response.FailureMessage, processedAt);
            }

            var regNo = response.RegNo!.Trim();

            return new AppointmentRegistrationExecutionResult(
                CreateResult(row, true, $"掛號成功，就診號：{regNo}", processedAt),
                new AppointmentRegistrationKeys(
                    regDate,
                    regTime,
                    regRoom,
                    regNo,
                    mrno,
                    regSecNo,
                    pFin1,
                    pFin2,
                    pRegStat,
                    pPayt,
                    pPay,
                    cUserNo,
                    regDocNo,
                    regDocName,
                    op0RecNo),
                op0RegOrd);
        }
        catch (Exception exception)
        {
            return CreateExecutionResult(row, false, $"掛號失敗：{exception.Message}", processedAt);
        }
    }

    public BathRegisteredRowResultViewModel CompleteRegistrations(
        BathRegisteredItemPostModel row,
        IReadOnlyList<AppointmentRegistrationExecutionResult> registrationResults)
    {
        var completedRegistrations = registrationResults
            .Where(result => result.Success && result.Keys is not null)
            .ToArray();

        if (completedRegistrations.Length != registrationResults.Count || completedRegistrations.Length == 0)
        {
            var failedResult = registrationResults.FirstOrDefault(result => !result.Success || result.Keys is null);
            return failedResult?.RowResult
                ?? CreateResult(row, false, "沒有可寫入的掛號結果。", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        try
        {
            PersistRegistrations(row, completedRegistrations);
            var registrationNumbers = completedRegistrations
                .Select(result => result.Keys!.No)
                .Where(number => !string.IsNullOrWhiteSpace(number));
            return CreateResult(
                row,
                true,
                $"掛號成功，共完成 {completedRegistrations.Length} 次掛號，就診號：{string.Join("、", registrationNumbers)}",
                completedRegistrations[^1].RowResult.ProcessedAt);
        }
        catch (Exception exception)
        {
            return CreateResult(
                row,
                false,
                $"掛號完成，但預約狀態寫入失敗：{exception.Message}",
                completedRegistrations[^1].RowResult.ProcessedAt);
        }
    }
    private static void PersistRegistrations(
        BathRegisteredItemPostModel row,
        IReadOnlyList<AppointmentRegistrationExecutionResult> registrationResults)
    {
        var updateParameters = new Dictionary<string, object?>
        {
            ["BARCODESTR"] = row.NormalizedBarcode,
            ["PROJECTNO"] = row.NormalizedProjectNumber
        };
        var commands = new List<(string Sql, IReadOnlyDictionary<string, object?> Parameters)>
        {
            (OracleSql.UPDATE.UpdateHealthCheckupListRegistered(), updateParameters)
        };

        commands.AddRange(registrationResults.Select(registrationResult =>
        {
            var keys = registrationResult.Keys!;
            IReadOnlyDictionary<string, object?> parameters = new Dictionary<string, object?>
            {
                ["BARCODESTR"] = row.NormalizedBarcode,
                ["PROJECTNO"] = row.NormalizedProjectNumber,
                ["CHOP1DATE"] = keys.Date,
                ["CHOP1TIME"] = keys.Time,
                ["CHOP1ROOM"] = keys.Room,
                ["INTOP1NO"] = keys.No,
                ["INTOP1ORDSEQ"] = string.IsNullOrWhiteSpace(registrationResult.RegistrationOrderSequence)
                    ? null
                    : registrationResult.RegistrationOrderSequence,
                ["CHOP4ORDNO"] = registrationResult.OrderNumber,
                ["CHOP0FIN2"] = keys.PFin2
            };
            return (OracleSql.MERGE.UpsertFEMHCheckupList(), parameters);
        }));
        var writeCounts = /* new OracleConnectionFactory() */ new HealthSys_20260428_v2.csproj.Controllers.ControllerDemoData().ExecuteNonQueryTransaction(
            commands,
            "",
            true);
        var updatedCount = writeCounts.Length > 0 ? writeCounts[0] : 0;
        var upsertedCount = writeCounts.Skip(1).Sum();

        if (updatedCount < 1 || upsertedCount != registrationResults.Count)
        {
            throw new InvalidOperationException("掛號成功，但預約狀態或 FEMHCheckupList 掛號資料寫入失敗。");
        }
    }

    private static AppointmentRegistrationExecutionResult CreateExecutionResult(
        BathRegisteredItemPostModel row,
        bool success,
        string message,
        string processedAt) =>
        new(CreateResult(row, success, message, processedAt), null);
    private static BathRegisteredRowResultViewModel CreateResult(
        BathRegisteredItemPostModel row,
        bool success,
        string message,
        string processedAt) =>
        new()
        {
            Success = success,
            ProjectNumber = row.NormalizedProjectNumber,
            Barcode = row.NormalizedBarcode,
            PatNo = row.NormalizedPatNo,
            Name = row.NormalizedName,
            Message = message,
            ProcessedAt = processedAt
        };

    private static string NormalizeRocAppointmentDate(string? appointmentDate)
    {
        if (!TryParseAppointmentDate(appointmentDate, out var parsedDate))
        {
            return "";
        }

        var rocYear = parsedDate.Year - 1911;
        return $"{rocYear:000}{parsedDate.Month:00}{parsedDate.Day:00}";
    }

    private static string NormalizeAppointmentRegTime(string? appointmentTime)
    {
        if (string.IsNullOrWhiteSpace(appointmentTime))
        {
            return "";
        }

        var text = appointmentTime.Trim();

        if (text.Contains("上午", StringComparison.Ordinal))
        {
            return "1";
        }

        if (text.Contains("下午", StringComparison.Ordinal))
        {
            return "2";
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime)
            || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedTime))
        {
            return parsedTime.Hour < 12 ? "1" : "2";
        }

        var digitsOnly = text.Replace(":", "", StringComparison.Ordinal);
        return digitsOnly.Length >= 4 && int.TryParse(digitsOnly[..2], out var hour)
            ? hour < 12 ? "1" : "2"
            : "";
    }

    private static bool TryParseAppointmentDate(string? value, out DateTime date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] formats = ["yyyy/MM/dd", "yyyy-MM-dd", "yyyyMMdd", "yyy/MM/dd", "yyy-MM-dd", "yyyMMdd"];
        var text = value.Trim();
        return DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

}
