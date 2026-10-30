namespace HealthSys_20260428_v2.csproj.Models;

public sealed class MedicalOrderMaintenanceViewModel
{
    public string Category { get; init; } = "";
    public string Keyword { get; init; } = "";
    public IReadOnlyList<ProjectPackageCategoryViewModel> Categories { get; init; } = [];
    public IReadOnlyList<string> ItemCategories { get; init; } = [];
    public IReadOnlyList<MedicalOrderMaintenanceItemViewModel> Items { get; init; } = [];
    public IReadOnlyList<int> PageSizeOptions { get; init; } = [10, 20, 30, 50];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public string ErrorMessage { get; init; } = "";
}

public sealed class MedicalOrderMaintenanceItemViewModel
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string EnglishName { get; init; } = "";
    public string Category { get; init; } = "";
    public string CategoryTone { get; init; } = "";
    public string CategoryIcon { get; init; } = "";
    public string ItemCategory { get; init; } = "";
    public string Format { get; init; } = "";
    public string Unit { get; init; } = "";
}

public sealed class MedicalOrderMaintenanceInputModel
{
    public string OriginalCheckNo { get; set; } = "";
    public string CheckNo { get; set; } = "";
    public string Name { get; set; } = "";
    public string CheckCategory { get; set; } = "";
    public string ItemCategory { get; set; } = "";
    public string EName { get; set; } = "";
    public string Format { get; set; } = "";
    public string Unit { get; set; } = "";
}
