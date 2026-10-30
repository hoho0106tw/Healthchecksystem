using System.Net;
using System.Text;
using System.Text.Json;

namespace HealthSys_20260428_v2.csproj.Services;

public interface IOpdOrderService
{
    Task<OpdOrderResult> PostOPDPriOrd05Async(
        AppointmentRegistrationKeys keys,
        IReadOnlyList<OpdPackageOrder> packageOrders,
        CancellationToken cancellationToken = default);
}

public sealed record OpdOrderResult(
    bool Success,
    HttpStatusCode? StatusCode = null,
    string ResponseBody = "",
    string Message = "");

public sealed class OpdOrderService : IOpdOrderService
{
    // private const string BaseUrl = "https://webap.femh.org.tw:9007/OPDOrdDrgApi";

    private const string BaseUrl = "https://webap.femh.org.tw:9543/OPDOrdDrgApi";

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpdOrderService> _logger;

    public OpdOrderService(HttpClient httpClient, ILogger<OpdOrderService> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public async Task<OpdOrderResult> PostOPDPriOrd05Async(
        AppointmentRegistrationKeys keys,
        IReadOnlyList<OpdPackageOrder> packageOrders,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask;
        return new OpdOrderResult(true, HttpStatusCode.OK, "{\"demo\":true}", "示範醫令已建立");
#if false // Original hospital integration retained for reference.

        var normalizedPackageOrders = packageOrders
            .Select(order => new OpdPackageOrder(
                (order.OrderNumber ?? string.Empty).Trim(),
                Math.Max(0, order.OrderTotal),
                (order.PFin2 ?? string.Empty).Trim(),
                order.IsOtherSuite))
            .Where(order => !string.IsNullOrWhiteSpace(order.OrderNumber))
            .ToArray();

        if (!HasRequiredRegistrationKeys(keys) || normalizedPackageOrders.Length == 0)
        {
            _logger.LogWarning("新增醫令缺少掛號 keys 或組套號碼。");
            return new OpdOrderResult(false, Message: "缺少掛號資料或組套號碼。");
        }

        var inputDate = FormatRocDate(DateTime.Today);
        OpdOrderResult? lastResult = null;
        OpdOrderResult? firstFailure = null;

        foreach (var packageOrder in normalizedPackageOrders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastResult = await PostSingleOrderAsync(
                keys,
                inputDate,
                packageOrder,
                cancellationToken);

            if (!lastResult.Success)
            {
                firstFailure ??= lastResult;
            }
        }

        return firstFailure
            ?? lastResult
            ?? new OpdOrderResult(false, Message: "沒有可新增的醫令。");
    
#endif
    }

    private async Task<OpdOrderResult> PostSingleOrderAsync(
        AppointmentRegistrationKeys keys,
        string inputDate,
        OpdPackageOrder packageOrder,
        CancellationToken cancellationToken)
    {
        var orderNumber = packageOrder.OrderNumber;
        var pFin2 = string.IsNullOrWhiteSpace(packageOrder.PFin2)
            ? keys.PFin2
            : packageOrder.PFin2;
        //  var orderTotal = packageOrder.OrderTotal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var orderTotal = "1";
        var request = new
        {
            DryRun = false,

            ArrData = new[] { "0", "" },
            UserId = keys.UserId,
            PlaName = "OPD",
            IDate = inputDate,
            Edit = "oAdd",
            BOrdSeq0 = "0",
            Date = keys.Date,
            Time = keys.Time,
            Room = keys.Room,
            No = keys.No,
            PFin1 = keys.PFin1,
            PFin2 = pFin2,
            PSec = keys.Section,
            DrId = keys.DoctorId,
            ExeDrId = keys.DoctorId,

            OrdNo = orderNumber,
            ExtNo = "",
            ExtName = "",
            SPay = "1",
            Dose = "",
            Freq = "",
            Spect = "",
            Day = "",
            Day2 = "",
            OrdTot = orderTotal,
            OrdTot2 = "",
            Stat = "",
            Sys = "",
            ReplayLiveSeq = "",
            ReplayLiveSeq0 = ""


        };
        var json = JsonSerializer.Serialize(request);

        try
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(
                BaseUrl + "/api/OPDOrd/OPDPriOrd05",
                content,
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "新增醫令 API 失敗，OrdNo {OrderNumber}，HTTP {StatusCode}，Response: {ResponseBody}",
                    orderNumber,
                    (int)response.StatusCode,
                    TruncateForLog(responseBody));
            }

            var errorMessage = response.IsSuccessStatusCode
                ? ""
                : ExtractErrorMessage(responseBody, response.StatusCode);
            return new OpdOrderResult(
                response.IsSuccessStatusCode,
                response.StatusCode,
                responseBody,
                errorMessage);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("新增醫令 API 呼叫逾時，OrdNo {OrderNumber}。", orderNumber);
            return new OpdOrderResult(false, Message: "新增醫令 API 呼叫逾時。");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "新增醫令 API 連線失敗，OrdNo {OrderNumber}。", orderNumber);
            return new OpdOrderResult(false, Message: $"新增醫令 API 連線失敗：{exception.Message}");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "新增醫令 API 發生未預期錯誤，OrdNo {OrderNumber}。", orderNumber);
            return new OpdOrderResult(false, Message: $"新增醫令發生錯誤：{exception.Message}");
        }
    }

    private static bool HasRequiredRegistrationKeys(AppointmentRegistrationKeys keys) =>
        !string.IsNullOrWhiteSpace(keys.Date)
        && !string.IsNullOrWhiteSpace(keys.Time)
        && !string.IsNullOrWhiteSpace(keys.Room)
        && !string.IsNullOrWhiteSpace(keys.No)
        && !string.IsNullOrWhiteSpace(keys.PFin1)
        && !string.IsNullOrWhiteSpace(keys.PFin2)
        && !string.IsNullOrWhiteSpace(keys.UserId)
        && !string.IsNullOrWhiteSpace(keys.DoctorId);

    private static string FormatRocDate(DateTime date)
    {
        var rocYear = date.Year - 1911;
        return $"{rocYear:000}{date.Month:00}{date.Day:00}";
    }

    private static string ExtractErrorMessage(string responseBody, HttpStatusCode statusCode)
    {
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var messageProperty = document.RootElement.ValueKind == JsonValueKind.Object
                    ? document.RootElement
                        .EnumerateObject()
                        .FirstOrDefault(property =>
                            property.Name.Equals("Message", StringComparison.OrdinalIgnoreCase))
                    : default;

                if (messageProperty.Value.ValueKind == JsonValueKind.String)
                {
                    var message = messageProperty.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return message.Trim();
                    }
                }
            }
            catch (JsonException)
            {
            }

            return TruncateForDisplay(responseBody.Trim());
        }

        return $"HTTP {(int)statusCode} ({statusCode})";
    }

    private static string TruncateForDisplay(string value) =>
        value.Length <= 1000 ? value : value[..1000];

    private static string TruncateForLog(string value) =>
        value.Length <= 2000 ? value : value[..2000];
}
