using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HealthSys_20260428_v2.csproj.Services;

public sealed class RegHospitalApiClient(HttpClient httpClient)
{
    private static int demoRegistrationNo = 1000;
    public const string ProductionBaseUrl = "https://webap2.femh.org.tw:9389/RegHospitalApi/";

    public Task<RegNoResponse> GetRegNoAsync(GetRegNoRequest request) =>
        PostAsync("api/RegPtn/GetRegNo", request);

    // Serialize the common fields only: AgainGetRegNo does not accept the
    // section, control level, doctor name or specified registration number.
    public Task<RegNoResponse> AgainGetRegNoAsync(RegHospitalRegistrationRequest request) =>
        PostAsync("api/RegPtn/AgainGetRegNo", request);

    private async Task<RegNoResponse> PostAsync<T>(string endpoint, T request)
    {
        _ = httpClient; // Preserve the integration constructor while running offline.
        await Task.CompletedTask;
        return new RegNoResponse { Success = true, ReturnCode = 0, RegNo = Interlocked.Increment(ref demoRegistrationNo).ToString(), Message = "示範掛號成功" };
#if false // Original hospital integration retained for reference.

        // Do not retry a registration: a lost response can still mean it succeeded.
        using var response = await httpClient.PostAsJsonAsync(endpoint, request).ConfigureAwait(false);
        RegNoResponse? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<RegNoResponse>().ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"掛號 API 回傳格式錯誤（HTTP {(int)response.StatusCode}），請確認掛號結果後再操作。", exception);
        }

        if (result is null)
            throw new InvalidOperationException("掛號 API 回傳空白，請確認掛號結果後再操作。");

        // HTTP 400 contains the business error and validation field names.
        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.BadRequest)
            throw new InvalidOperationException($"掛號 API HTTP {(int)response.StatusCode}：{result.FailureMessage}");

        if (response.StatusCode != HttpStatusCode.OK && result.Success)
            throw new InvalidOperationException("掛號 API 的 HTTP 狀態與成功結果不一致，請確認掛號結果後再操作。");

        return result;
    
#endif
    }
}

public class RegHospitalRegistrationRequest
{
    public string RegDate { get; init; } = "";
    public string RegTime { get; init; } = "";
    public string RegRoom { get; init; } = "";
    public string RegDocNo { get; init; } = "";
    public int RegSource { get; init; }
    public string MrNo { get; init; } = "";
    public string PFin1 { get; init; } = "";
    public string PFin2 { get; init; } = "";
    public string PRegStat { get; init; } = "";
    public string PPayt { get; init; } = "";
    public string PCardNo { get; init; } = "";
    public string PPay { get; init; } = "";
    public string CUserNo { get; init; } = "";
    public bool BorPaper { get; init; }
    public short? Op0RecNo { get; init; }
    public string Op0RegOrd { get; init; } = "";
    public short? Op0RegAmt { get; init; }
    public short? Op0PayAmt { get; init; }
    public short? Op0HPayAmt { get; init; }
    public string TransHospNo { get; init; } = "";
    public bool BorwX { get; init; }
}

public sealed class GetRegNoRequest : RegHospitalRegistrationRequest
{
    public int? RegNo { get; init; }
    public string RegSecNo { get; init; } = "";
    public int ContrlStat { get; init; }
    public string RegDocName { get; init; } = "";
    public string RegSecName { get; init; } = "";
}

public sealed class RegNoResponse
{
    public bool Success { get; init; }
    public int ReturnCode { get; init; } = int.MinValue;
    public string? Message { get; init; }
    public List<string>? Errors { get; init; }
    public string? RegNo { get; init; }

    public bool HasRegistration => Success && ReturnCode == 0 && !string.IsNullOrWhiteSpace(RegNo);

    public string FailureMessage =>
        $"{(string.IsNullOrWhiteSpace(Message) ? "掛號 API 未回傳有效就診號" : Message)}（代碼：{ReturnCode}）"
        + (Errors is { Count: > 0 } ? $"；欄位：{string.Join("、", Errors)}" : "");
}
