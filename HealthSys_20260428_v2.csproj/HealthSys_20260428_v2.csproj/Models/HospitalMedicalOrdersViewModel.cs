namespace HealthSys_20260428_v2.csproj.Models;

public sealed class HospitalMedicalOrdersViewModel
{
    public string OrderCode { get; init; } = "";
    public string OrderName { get; init; } = "";
    public string Initial { get; init; } = "";
    public IReadOnlyList<HospitalMedicalOrderItemViewModel> Items { get; init; } = [];
    public IReadOnlyList<int> PageSizeOptions { get; init; } = [10, 20, 30, 50];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public string ErrorMessage { get; init; } = "";
}

public sealed class HospitalMedicalOrderItemViewModel
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Unit { get; init; } = "";
}
