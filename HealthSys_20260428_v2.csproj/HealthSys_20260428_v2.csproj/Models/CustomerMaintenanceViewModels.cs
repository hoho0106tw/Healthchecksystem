using System.Collections.Generic;

namespace HealthSys_20260428_v2.csproj.Models;

public sealed class CustomerMaintenanceViewModel
{
    public IReadOnlyList<CustomerListItemViewModel> Customers { get; init; } = [];

    public CustomerListItemViewModel? SelectedCustomer { get; init; }

    public IReadOnlyList<int> PageSizeOptions { get; init; } = [];

    public int TotalCustomerCount { get; init; }
}

public sealed class AddCustomerFrameViewModel
{
    public bool IsEdit { get; init; }

    public string FrameTitle { get; init; } = "新增公司";

    public string FormActionName { get; init; } = "CreateCustomer";

    public string SubmitButtonText { get; init; } = "儲存公司";

    public bool ShowSaveNext { get; init; } = true;

    public string SuggestedCustomerNo { get; init; } = "";

    public string CustomerNo { get; init; } = "";

    public string EffectiveCustomerNo =>
        string.IsNullOrWhiteSpace(CustomerNo) ? SuggestedCustomerNo : CustomerNo;

    public string CustomerName { get; init; } = "";

    public string EnglishName { get; init; } = "";

    public string UniformNo { get; init; } = "";

    public string Address { get; init; } = "";

    public string Tel { get; init; } = "";

    public string Fax { get; init; } = "";

    public string Email { get; init; } = "";

    public string CustomerNote { get; init; } = "";

    public string ContactPerson { get; init; } = "";

    public string BusinessRegAddress { get; init; } = "";

    public string TaxOfficeCode { get; init; } = "";

    public string BusinessUnion { get; init; } = "";

    public string IndustryCategory { get; init; } = "";

    public string BusinessUnitName { get; init; } = "";

    public string BusinessUnitAddress { get; init; } = "";

    public string MedicalManageCode { get; init; } = "";

    public string AccountingUnit { get; init; } = "";
}

public sealed class CustomerCreatePostModel
{
    public string? CustomerNo { get; init; }

    public string? CustomerName { get; init; }

    public string? Address { get; init; }

    public string? Tel { get; init; }

    public string? Fax { get; init; }

    public string? Email { get; init; }

    public string? CustomerNote { get; init; }

    public string? EnglishName { get; init; }

    public string? UniformNo { get; init; }

    public string? ContactPerson { get; init; }

    public string? BusinessRegAddress { get; init; }

    public string? TaxOfficeCode { get; init; }

    public string? BusinessUnion { get; init; }

    public string? IndustryCategory { get; init; }

    public string? BusinessUnitName { get; init; }

    public string? BusinessUnitAddress { get; init; }

    public string? MedicalManageCode { get; init; }

    public string? AccountingUnit { get; init; }
}

public sealed class CustomerDeletePostModel
{
    public string[] CustomerNos { get; init; } = [];
}

public sealed class CustomerListItemViewModel
{
    public bool Selected { get; init; }

    public string CustomerNo { get; init; } = "";

    public string CustomerName { get; init; } = "";

    public string EnglishName { get; init; } = "";

    public string UniformNo { get; init; } = "";

    public string Address { get; init; } = "";

    public string Tel { get; init; } = "";

    public string Fax { get; init; } = "";

    public string Email { get; init; } = "";

    public string ContactPerson { get; init; } = "";

    public string BusinessRegAddress { get; init; } = "";

    public string TaxOfficeCode { get; init; } = "";

    public string BusinessUnion { get; init; } = "";

    public string IndustryCategory { get; init; } = "";

    public string BusinessUnitName { get; init; } = "";

    public string BusinessUnitAddress { get; init; } = "";

    public string MedicalManageCode { get; init; } = "";

    public string AccountingUnit { get; init; } = "";

    public string CustomerNote { get; init; } = "";

    public IReadOnlyList<CustomerProjectListItemViewModel> Projects { get; init; } = [];
}

public sealed class CustomerProjectListItemViewModel
{
    public string CustomerNo { get; init; } = "";

    public string ProjectNo { get; init; } = "";

    public string CheckYear { get; init; } = "";

    public string ProjectName { get; init; } = "";

    public string ReserveCount { get; init; } = "";

    public string StartDate { get; init; } = "";

    public string EndDate { get; init; } = "";

    public string Status { get; init; } = "";

    public IReadOnlyList<CustomerProjectAppointmentDetailViewModel> AppointmentDetails { get; init; } = [];
}

public sealed class CustomerProjectAppointmentDetailViewModel
{
    public string ReserveCount { get; init; } = "";

    public string StartDate { get; init; } = "";

    public string EndDate { get; init; } = "";

    public string Status { get; init; } = "";
}
