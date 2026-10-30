using System.Collections.Generic;
using System.Linq;

namespace HealthSys_20260428_v2.csproj.Models;

public sealed class ProjectPackageSettingsViewModel
{
    public ProjectPackageInfoViewModel Project { get; init; } = new();

    public IReadOnlyList<ProjectPackageCategoryViewModel> Categories { get; init; } = [];

    public IReadOnlyList<ProjectPackageItemViewModel> AvailableItems { get; init; } = [];

    public IReadOnlyList<ProjectPackageItemViewModel> SelectedItems { get; init; } = [];

    public int DiscountAmount { get; init; }

    public decimal DiscountRate { get; init; }

    public int CostEstimate { get; init; }
}

public sealed class HealthCheckProjectManagementViewModel
{
    public IReadOnlyList<HealthCheckProjectListItemViewModel> Projects { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> HospitalOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> ClientUnitOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> CustomerNoOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> YearOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> CheckYearOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> StatusOptions { get; init; } = [];

    public IReadOnlyList<int> PageSizeOptions { get; init; } = [];

    public int TotalProjectCount { get; init; }
}

public sealed class HealthCheckProjectListItemViewModel
{
    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string Hospital { get; init; } = "";

    public string ClientUnit { get; init; } = "";

    public string SelfPayAccountingUnit { get; init; } = "";

    public string CustomerNo { get; init; } = "";

    public string CustomerName { get; init; } = "";

    public string Year { get; init; } = "";

    public string CheckYear { get; init; } = "";

    public string Status { get; init; } = "";

    public string StatusTone { get; init; } = "";

    public string CreatedAt { get; init; } = "";

    public string UpdatedAt { get; init; } = "";

    public string CheckDate { get; init; } = "";

    public string EndDate { get; init; } = "";

    public string CompareVal { get; init; } = "";

    public string FlowName { get; init; } = "";

    public string DefaultArea { get; init; } = "";

    public string Questionnaire { get; init; } = "";

    public string IsPastHistory { get; init; } = "";

    public string IsOralCheck { get; init; } = "";

    public string IsOnlineQuestion { get; init; } = "";

    public string Note { get; init; } = "";
}

public sealed class HealthCheckProjectDetailViewModel
{
    public ProjectPackageInfoViewModel Project { get; init; } = new();

    public IReadOnlyList<HealthCheckProjectPackageRowViewModel> Packages { get; init; } = [];

    public IReadOnlyList<HealthCheckProjectSpecialItemRowViewModel> SpecialItems { get; init; } = [];

    public HealthCheckProjectPackageRowViewModel? SelectedPackage { get; init; }

    public IReadOnlyList<HealthCheckProjectOrderRowViewModel> Orders { get; init; } = [];

    public int PackagePublicExpense => Packages.Sum(package => package.PublicExpense);

    public int SpecialPublicExpense => SpecialItems.Sum(item => item.PublicExpense);

    public int TotalPublicExpense => PackagePublicExpense + SpecialPublicExpense;
}

public sealed class HealthCheckProjectPackageRowViewModel
{
    public string SchemeNo { get; init; } = "";

    public string Name { get; init; } = "";

    public string Note { get; init; } = "";

    public string SelfPayAccountingUnit { get; init; } = "";

    public string SelfPayAccountingUnitDisplay { get; init; } = "";

    public int PublicExpense { get; init; }

    public string FixItems { get; init; } = "";

    public bool Active { get; init; }
}

public sealed class HealthCheckProjectOrderRowViewModel
{
    public string Code { get; init; } = "";

    public string Name { get; init; } = "";
}

public sealed class HealthCheckProjectSpecialItemRowViewModel
{
    public int SortOrder { get; init; }

    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public int PublicExpense { get; init; }
}

public sealed class InternalMedicalOrderSuiteViewModel
{
    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public int Price { get; init; }

    public string PriceDate { get; init; } = "";

    public int OrderCount { get; init; }
}

public sealed class InternalMedicalOrderSuiteOrderViewModel
{
    public string SuiteCode { get; init; } = "";

    public string Code { get; init; } = "";

    public string Name { get; init; } = "";
}

public sealed class HealthProjectWizardViewModel
{
    public int CurrentStep { get; init; }

    public bool IsEditMode { get; init; }

    public ProjectPackageInfoViewModel Project { get; init; } = new();

    public IReadOnlyList<HealthProjectWizardStepViewModel> Steps { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> ClientUnitOptions { get; init; } = [];

    public IReadOnlyList<SelfPayAccountingUnitViewModel> SelfPayAccountingUnits { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> DefaultAreaOptions { get; init; } = [];

    public IReadOnlyList<ProjectCustomerLookupItemViewModel> CustomerLookupItems { get; init; } = [];

    public IReadOnlyList<ProjectPackageCategoryViewModel> Categories { get; init; } = [];

    public IReadOnlyList<ProjectPackageItemViewModel> AvailableItems { get; init; } = [];

    public IReadOnlyList<ProjectPackageItemViewModel> SelectedItems { get; init; } = [];

    public IReadOnlyList<HealthProjectSelectedSuiteViewModel> SelectedSuites { get; init; } = [];

    public IReadOnlyList<ProjectSpecialOperationItemViewModel> AvailableSpecialOperations { get; init; } = [];

    public IReadOnlyList<ProjectSpecialOperationItemViewModel> SelectedSpecialOperations { get; init; } = [];

    public int MedicalOrderSubtotal { get; init; }

    public int SpecialOperationSubtotal { get; init; }

    public int Subtotal { get; init; }

    public int DiscountAmount { get; init; }

    public decimal DiscountRate { get; init; }

    public int FinalPrice { get; init; }

    public int CostEstimate { get; init; }
}

public sealed class HealthProjectSelectedSuiteViewModel
{
    public string Id { get; init; } = "";

    public string SchemeNo { get; init; } = "";

    public string Source { get; init; } = "";

    public string Name { get; init; } = "";

    public string Note { get; init; } = "";

    public string FeeType { get; init; } = "";

    public string SelfPayAccountingUnit { get; init; } = "";

    public string SelfPayAccountingUnitName { get; init; } = "";

    public int PublicExpense { get; init; }

    public IReadOnlyList<HealthProjectSelectedSuiteOrderViewModel> Orders { get; init; } = [];

    public IReadOnlyList<string> CheckNos { get; init; } = [];
}

public sealed class HealthProjectSelectedSuiteOrderViewModel
{
    public string Code { get; init; } = "";

    public string CheckNo { get; init; } = "";

    public string Source { get; init; } = "";

    public bool Locked { get; init; }

    public string Name { get; init; } = "";

    public string Category { get; init; } = "";

    public string CategoryName { get; init; } = "";

    public string ItemCategory { get; init; } = "";
}
public sealed class WizardProjectInput
{
    public string Mode { get; init; } = "";

    public string Code { get; init; } = "";

    public string ProjectNo { get; init; } = "";

    public string ProjectName { get; init; } = "";

    public string CustomerNo { get; init; } = "";

    public string CustomerName { get; init; } = "";

    public string ClientUnit { get; init; } = "";

    public string SelfPayAccountingUnit { get; init; } = "";

    public string Year { get; init; } = "";

    public string CheckDate { get; init; } = "";

    public string CheckStartDate { get; init; } = "";

    public string CheckEndDate { get; init; } = "";

    public string CompareVal { get; init; } = "";

    public string FlowName { get; init; } = "";

    public string DefaultArea { get; init; } = "";

    public string Notes { get; init; } = "";

    public string IsPastHistory { get; init; } = "";

    public string IsOralCheck { get; init; } = "";

    public string IsOnlineQuestion { get; init; } = "";

    public string Questionnaire { get; init; } = "";

    public string SelectedOrders { get; init; } = "";

    public string SelectedSuites { get; init; } = "";

    public string SelectedSpecialOperations { get; init; } = "";
}

public sealed class HealthProjectWizardStepViewModel
{
    public int Number { get; init; }

    public string Title { get; init; } = "";

    public string Subtitle { get; init; } = "";

    public string State { get; init; } = "";
}

public sealed class ProjectPackageInfoViewModel
{
    public string CustomerName { get; init; } = string.Empty;

    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public string Target { get; init; } = "";

    public string ShortName { get; init; } = "";

    public string Hospital { get; init; } = "";

    public string ClientUnit { get; init; } = "";

    public string SelfPayAccountingUnit { get; init; } = "";

    public string CustomerNo { get; init; } = "";

    public string Year { get; init; } = "";

    public string CheckDate { get; init; } = "";

    public string CheckStartDate { get; init; } = "";

    public string CheckEndDate { get; init; } = "";

    public string EndDate { get; init; } = "";

    public string Questionnaire { get; init; } = "";

    public string FlowSheet { get; init; } = "";

    public string DefaultArea { get; init; } = "";

    public bool IsPastHistoryEnabled { get; init; }

    public bool IsOralCheckEnabled { get; init; }

    public bool IsOnlineQuestionEnabled { get; init; }

    public string Notes { get; init; } = "";

    public string Category { get; init; } = "";

    public string Status { get; init; } = "";

    public string StatusTone { get; init; } = "";

    public int ItemCount { get; init; }

    public int Price { get; init; }

    public string CreatedAt { get; init; } = "";

    public string Owner { get; init; } = "";

    public string UpdatedAt { get; init; } = "";
}

public sealed class ProjectCustomerLookupItemViewModel
{
    public string CustomerNo { get; init; } = "";

    public string CustomerName { get; init; } = "";

    public string CustUnit { get; init; } = "";

    public string CheckYear { get; init; } = "";
}

public sealed class SelfPayAccountingUnitViewModel
{
    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public string EndDateValue { get; init; } = "";

    public string DisplayText => string.IsNullOrWhiteSpace(Name) ? Code : $"[{Code}] {Name}";
}

public sealed class ProjectPackageCategoryViewModel
{
    public string Name { get; init; } = "";

    public string Key { get; init; } = "";

    public string Tone { get; init; } = "";

    public string Icon { get; init; } = "";

    public bool Active { get; init; }
}

public sealed class ProjectPackageItemViewModel
{
    public int SortOrder { get; init; }

    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public string Category { get; init; } = "";

    public string CategoryKey { get; init; } = "";

    public string ItemCategory { get; init; } = "";

    public string ReportName { get; init; } = "";

    public string Icon { get; init; } = "";

    public int Price { get; init; }

    public bool Required { get; init; }

    public string Status { get; init; } = "";

    public string StatusTone { get; init; } = "";

    public bool Selected { get; init; }
}

public sealed class ProjectSpecialOperationItemViewModel
{
    public int SortOrder { get; init; }

    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public string Category { get; init; } = "";

    public string CategoryKey { get; init; } = "";

    public string Note { get; init; } = "";

    public string ManualText { get; init; } = "";

    public string BillingCategory { get; init; } = "";

    public int Price { get; init; }

    public bool Selected { get; init; }
}

internal readonly record struct SelectedProjectOrder(
    string SchemeNo,
    string CheckNo,
    string Name = "",
    string Category = "",
    string Source = "",
    bool Locked = false);

internal sealed class SelectedProjectSuite
{
    public string SchemeNo { get; init; } = "";

    public string Source { get; init; } = "";

    public string Name { get; init; } = "";

    public string Note { get; init; } = "";

    public string FeeType { get; init; } = "";

    public string SelfPayAccountingUnit { get; init; } = "";

    public string SelfPayAccountingUnitName { get; init; } = "";

    public int PublicExpense { get; init; }

    public IReadOnlyList<SelectedProjectOrder> Orders { get; init; } = [];

    public IReadOnlyList<string> CheckNos { get; init; } = [];
}

internal sealed class SelectedProjectSuitePayload
{
    public string Id { get; init; } = "";

    public string SchemeNo { get; init; } = "";

    public string Source { get; init; } = "";

    public string Name { get; init; } = "";

    public string Note { get; init; } = "";

    public string Category { get; init; } = "";

    public string CategoryName { get; init; } = "";

    public string FeeType { get; init; } = "";

    public string SelfPayAccountingUnit { get; init; } = "";

    public string SelfPayAccountingUnitName { get; init; } = "";

    public int Price { get; init; }

    public IReadOnlyList<SelectedProjectSuiteOrderPayload>? Orders { get; init; }

    public IReadOnlyList<string>? CheckNos { get; init; }
}

internal sealed class SelectedProjectSuiteOrderPayload
{
    public string Code { get; init; } = "";

    public string CheckNo { get; init; } = "";

    public string Source { get; init; } = "";

    public bool Locked { get; init; }

    public string Name { get; init; } = "";

    public string Category { get; init; } = "";

    public string CategoryName { get; init; } = "";

    public string ItemCategory { get; init; } = "";
}

internal sealed class SelectedSpecialOperationPayload
{
    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public string Category { get; init; } = "";

    public string CategoryKey { get; init; } = "";

    public string Note { get; init; } = "";

    public string ManualText { get; init; } = "";

    public string BillingCategory { get; init; } = "";

    public int Price { get; init; }
}


