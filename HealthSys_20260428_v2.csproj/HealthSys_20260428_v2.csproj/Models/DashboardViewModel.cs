namespace HealthSys_20260428_v2.csproj.Models;

public sealed class DashboardViewModel
{
    public DateTime ReservationDateStart { get; init; } = DateTime.Today;
    public DateTime ReservationDateEnd { get; init; } = DateTime.Today;
    public string ReserveStatus { get; init; } = "";
    public string WorkArea { get; init; } = "";
    public string Keyword { get; init; } = "";
    public List<SelectOptionViewModel> WorkAreaOptions { get; } = [];
    public string WorkAreaError { get; set; } = "";
    public string QueryError { get; set; } = "";
    public string StatisticsError => QueryError;
    public int? QueryTotal => string.IsNullOrEmpty(QueryError) ? Examinees.Count : null;
    public int? QueryReserved => CountStatus("1");
    public int? QuerySlots => CountStatus("2");
    public int? QueryCancelled => CountStatus("0");
    public int? QueryRegistered => CountStatus("4");
    public int? QueryCheckedIn => CountStatus("5");
    public int? QueryChecking => CountStatus("6");
    public int? QueryOther => QueryTotal - QueryReserved - QueryRegistered - QueryCheckedIn - QueryChecking;

    private int? CountStatus(string status) => string.IsNullOrEmpty(QueryError)
        ? Examinees.Count(person => person.StatusCode == status) : null;
    public List<DashboardExaminee> Examinees { get; } = [];
}

public sealed class DashboardExaminee
{
    public string Barcode { get; init; } = "";
    public string PatientNumber { get; init; } = "";
    public string Name { get; init; } = "";
    public string IdNumber { get; init; } = "";
    public string Gender { get; init; } = "";
    public string Age { get; init; } = "";
    public string RawStatus { get; init; } = "";
    public string StatusCode => NormalizeStatus(RawStatus);
    public string Status => StatusCode switch
    {
        "0" => "已取消", "1" => "已預約", "2" => "佔名額",
        "4" => "已掛號", "5" => "已報到", "6" => "檢查中",
        _ => "未知狀態"
    };

    public static string NormalizeStatus(string? value)
    {
        var text = value?.Trim() ?? "";
        var code = text switch
        {
            "預約取消" or "已取消" => "0", "已預約" => "1", "佔名額" => "2",
            "已掛號" => "4", "已報到" => "5", "檢查中" => "6",
            _ => text
        };
        return decimal.TryParse(code, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var number)
            && number is 0m or 1m or 2m or 4m or 5m or 6m
            ? number.ToString("0", System.Globalization.CultureInfo.InvariantCulture) : code;
    }

    public string Station { get; init; } = "";
    public string ReservationDate { get; init; } = "";
    public string ReservationTime { get; init; } = "";
    public string CheckInTime { get; init; } = "";
    public string CheckInDate { get; init; } = "";
    public string BookingTime { get; init; } = "";
}
