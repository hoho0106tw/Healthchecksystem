namespace HealthSys_20260428_v2.csproj.Models;

public sealed class StatisticsAccountListItemViewModel
{
    public string Barcode { get; init; } = "";
    public string ProjectNo { get; init; } = "";
    public string ProjectName { get; init; } = "";
    public string ReserveDate { get; init; } = "";
    public string ReserveTime { get; init; } = "";
    public string Status { get; init; } = "";
    public string PatNo { get; init; } = "";
    public string PatientName { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string Chop1Date { get; init; } = "";
    public string Chop1Time { get; init; } = "";
    public string Chop1Room { get; init; } = "";
    public string Intop1No { get; init; } = "";
}

public sealed class StatisticsAccountSuiteViewModel
{
    public string Id { get; init; } = "";
    public string Seq { get; init; } = "";
    public string Name { get; init; } = "";
    public decimal Total { get; init; }
    public decimal Receivable { get; init; }
    public decimal Discount { get; init; }
    public decimal Booked { get; init; }
    public decimal Arrears { get; init; }
}

public sealed class StatisticsAccountOrderViewModel
{
    public string Name { get; init; } = "";
    public string Code { get; init; } = "";
    public string Sequence { get; init; } = "";
}
