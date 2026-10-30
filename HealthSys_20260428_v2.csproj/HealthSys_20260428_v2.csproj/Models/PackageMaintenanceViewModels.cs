namespace HealthSys_20260428_v2.csproj.Models;

public sealed class PackageMaintenanceViewModel
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string ProjectName { get; init; } = "";
    public string ProjectNo { get; init; } = "";
    public string PackageName { get; init; } = "";
    public string SchemeCode { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public IReadOnlyList<PackageMaintenanceItemViewModel> Items { get; init; } = [];
    public IReadOnlyList<int> PageSizeOptions { get; init; } = [10, 20, 30, 50];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public string ErrorMessage { get; init; } = "";
}

public sealed class PackageMaintenanceItemViewModel
{
    public string ProjectNo { get; init; } = "";
    public string LisItemName { get; init; } = "";
    public string ProjectName { get; init; } = "";
    public DateTime? CheckDate { get; init; }
    public string PackageName { get; init; } = "";
    public string SchemeCode { get; init; } = "";
    public string CustomerName { get; init; } = "";
}
