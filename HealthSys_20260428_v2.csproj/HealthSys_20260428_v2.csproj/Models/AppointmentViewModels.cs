using System.Collections.Generic;
using System.Globalization;

namespace HealthSys_20260428_v2.csproj.Models;

public sealed class AppointmentManagementViewModel
{
    public IReadOnlyList<AppointmentListItemViewModel> Appointments { get; init; } = [];

    public AppointmentListItemViewModel? SelectedAppointment { get; init; }

    public string PageDateText { get; init; } = "";

    public string FilterStartDate { get; init; } = "";

    public string FilterEndDate { get; init; } = "";

    public string FilterKeyword { get; init; } = "";

    public string QueryErrorMessage { get; init; } = "";

    public IReadOnlyList<SelectOptionViewModel> ReservationAreaOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> AppointmentStatusOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> PlanOptions { get; init; } = [];

    public IReadOnlyList<AppointmentSummaryCardViewModel> SummaryCards { get; init; } = [];

    public IReadOnlyList<AppointmentStatusSummaryCardViewModel> StatusSummaryCards { get; init; } = [];

    public IReadOnlyList<CapacitySlotViewModel> CapacitySlots { get; init; } = [];

    public IReadOnlyList<AppointmentReminderViewModel> Reminders { get; init; } = [];

    public IReadOnlyList<int> PageSizeOptions { get; init; } = [];
}

public sealed class AppointmentListItemViewModel
{
    public bool Selected { get; init; }

    public string AppointmentDate { get; init; } = "";

    public string Time { get; init; } = "";

    public string CheckInDate { get; init; } = "";

    public string CheckInTime { get; init; } = "";

    public string WorkArea { get; init; } = "";

    public string Type { get; init; } = "";

    public string TypeTone { get; init; } = "";

    public string DoctorName { get; init; } = "";

    public string DoctorId { get; init; } = "";

    public string RoomNo { get; init; } = "";

    public string ProjectNumber { get; init; } = "";

    public string SelfPayAccountingUnit { get; init; } = "";

    public string Barcode { get; init; } = "";

    public string PatNo { get; init; } = "";

    public string IdNumber { get; init; } = "";

    public string Gender { get; init; } = "";

    public string Name { get; init; } = "";

    public string Company { get; init; } = "";

    public string Plan { get; init; } = "";

    public string AppointmentStatus { get; init; } = "";

    public string AppointmentTone { get; init; } = "";

    public string ReserveStatus { get; init; } = "";

    public string DataStatus { get; init; } = "";

    public string DataTone { get; init; } = "";

    public string Phone { get; init; } = "";

    public string Note { get; init; } = "";

    public bool HasSelectedAppointmentItem { get; init; }

    public bool HasMissingAccountingUnit { get; init; }
}

public sealed class AppointmentCheckInPostModel
{
    public string? ProjectNumber { get; set; }

    public string? Barcode { get; set; }

    public string NormalizedProjectNumber => (ProjectNumber ?? string.Empty).Trim();

    public string NormalizedBarcode => (Barcode ?? string.Empty).Trim();

    public bool HasRequiredKey =>
        !string.IsNullOrWhiteSpace(NormalizedProjectNumber)
        && !string.IsNullOrWhiteSpace(NormalizedBarcode);
}

public sealed class AppointmentDeletePostModel
{
    public string? ProjectNumber { get; set; }

    public string? Barcode { get; set; }

    public string? PatNo { get; set; }

    public string? Name { get; set; }

    public string? AppointmentDate { get; set; }

    public string? Time { get; set; }

    public string? DoctorName { get; set; }

    public string? RoomNo { get; set; }

    public string NormalizedProjectNumber => Normalize(ProjectNumber);

    public string NormalizedBarcode => Normalize(Barcode);

    public string NormalizedPatNo => Normalize(PatNo);

    public string NormalizedName => Normalize(Name);

    public bool HasRequiredBarcode =>
        !string.IsNullOrWhiteSpace(NormalizedBarcode);

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim();
}

public sealed class AppointmentDeleteResultViewModel
{
    public bool Deleted { get; init; }

    public int DeletedCount { get; init; }

    public bool IsMock { get; init; }

    public string ProjectNumber { get; init; } = "";

    public string Barcode { get; init; } = "";

    public string PatNo { get; init; } = "";

    public string Name { get; init; } = "";
}

public sealed class AppointmentCancelPostModel
{
    public string? ProjectNumber { get; set; }

    public string? Barcode { get; set; }

    public string? PatNo { get; set; }

    public string? Name { get; set; }

    public string? CurrentReserveStatus { get; init; }

    public string? ReserveStatus { get; init; }

    public string NormalizedProjectNumber => Normalize(ProjectNumber);

    public string NormalizedBarcode => Normalize(Barcode);

    public string NormalizedPatNo => Normalize(PatNo);

    public string NormalizedName => Normalize(Name);

    public string NormalizedCurrentReserveStatus => Normalize(CurrentReserveStatus);

    public string NormalizedReserveStatus => Normalize(ReserveStatus);

    public bool HasRequiredBarcode =>
        !string.IsNullOrWhiteSpace(NormalizedBarcode);

    public bool HasValidReserveStatus =>
        NormalizedReserveStatus is "0" or "1" or "2";

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim();
}

public sealed class AppointmentCancelResultViewModel
{
    public bool Canceled { get; init; }

    public int UpdatedCount { get; init; }

    public bool IsMock { get; init; }

    public string ProjectNumber { get; init; } = "";

    public string Barcode { get; init; } = "";

    public string PatNo { get; init; } = "";

    public string Name { get; init; } = "";

    public string Status { get; init; } = "";

    public string StatusTone { get; init; } = "";

    public string ReserveStatus { get; init; } = "";

    public string CurrentReserveStatus { get; init; } = "";
}

public sealed class BathRegisteredPostModel
{
    public List<BathRegisteredItemPostModel> Rows { get; set; } = [];
}

public sealed class SupplementOrderPostModel
{
    public string? ProjectNumber { get; init; }

    public string? Barcode { get; init; }

    public string? PackageProjectNo { get; init; }

    public List<string> SelectedPackageIds { get; init; } = [];

    public List<string> SelectedPackageNames { get; init; } = [];

    public List<string> SelectedPackageAmounts { get; init; } = [];

    public List<string> SelectedPackageSelfPayAccountingUnits { get; init; } = [];

    public List<string> SelectedOtherSuiteIds { get; init; } = [];

    public List<string> SelectedOtherSuiteSelfPayAccountingUnits { get; init; } = [];
}

public sealed class BathRegisteredItemPostModel
{
    public string? ReserveStatus { get; set; }

    public string? ProjectNumber { get; set; }

    public string? Barcode { get; set; }

    public string? PatNo { get; set; }

    public string? Name { get; set; }

    public string? AppointmentDate { get; set; }

    public string? Time { get; set; }

    public string? DoctorName { get; set; }

    public string? DoctorId { get; set; }

    public string? RoomNo { get; set; }

    public string? SelfPayAccountingUnit { get; set; }

    public List<string> SelectedPackageIds { get; set; } = [];

    public List<string> SelectedPackageOrderTotals { get; set; } = [];

    public List<string> SelectedPackageSelfPayAccountingUnits { get; set; } = [];

    public List<string> SelectedPackageTypes { get; set; } = [];

    public string NormalizedProjectNumber => Normalize(ProjectNumber);

    public string NormalizedBarcode => Normalize(Barcode);

    public string NormalizedPatNo => Normalize(PatNo);

    public string NormalizedName => Normalize(Name);

    public string NormalizedReserveStatus => Normalize(ReserveStatus);

    public string NormalizedDoctorId => Normalize(DoctorId);

    public string NormalizedDoctorName => Normalize(DoctorName);

    public string NormalizedRoomNo => Normalize(RoomNo);

    public string NormalizedSelfPayAccountingUnit => Normalize(SelfPayAccountingUnit);

    public IReadOnlyList<string> NormalizedSelectedPackageIds => (SelectedPackageIds ?? [])
        .Select(Normalize)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToArray();

    public bool HasRequiredBarcode =>
        !string.IsNullOrWhiteSpace(NormalizedBarcode);

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim();
}

public sealed class BathRegisteredRowResultViewModel
{
    public bool Success { get; init; }

    public string ProjectNumber { get; init; } = "";

    public string Barcode { get; init; } = "";

    public string PatNo { get; init; } = "";

    public string Name { get; init; } = "";

    public string Message { get; init; } = "";

    public string ProcessedAt { get; init; } = "";
}

public sealed class BathRegisteredResultViewModel
{
    public bool Registered { get; init; }

    public bool IsMock { get; init; }

    public int TotalCount { get; init; }

    public int SuccessCount { get; init; }

    public int FailedCount { get; init; }

    public string Message { get; init; } = "";

    public IReadOnlyList<BathRegisteredRowResultViewModel> Results { get; init; } = [];
}

public sealed class GroupAppointmentManagementViewModel
{
    public string Title { get; init; } = "團體預約管理";

    public string FilterStartDate { get; init; } = "";

    public string FilterEndDate { get; init; } = "";

    public string ProjectNameKeyword { get; init; } = "";

    public IReadOnlyList<SelectOptionViewModel> CustomerOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> HospitalOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> CheckInStatusOptions { get; init; } = [];

    public IReadOnlyList<GroupSummaryCardViewModel> SummaryCards { get; init; } = [];

    public IReadOnlyList<GroupAppointmentListItemViewModel> Groups { get; init; } = [];

    public GroupAppointmentListItemViewModel? SelectedGroup { get; init; }

    public IReadOnlyList<GroupActivityViewModel> Activities { get; init; } = [];

    public IReadOnlyList<int> PageSizeOptions { get; init; } = [];

    public int TotalGroupCount { get; init; }
}

public sealed class GroupSummaryCardViewModel
{
    public string Label { get; init; } = "";

    public string Value { get; init; } = "";

    public string Unit { get; init; } = "";

    public string Tone { get; init; } = "";

    public string Icon { get; init; } = "";
}

public sealed class GroupAppointmentListItemViewModel
{
    public bool Selected { get; init; }

    public string CustomerNo { get; init; } = "";

    public string CustomerName { get; init; } = "";

    public string ProjectNo { get; init; } = "";

    public string ProjectName { get; init; } = "";

    public string CheckDate { get; init; } = "";

    public string EndDate { get; init; } = "";

    public int EstimatedCount { get; init; }

    public int CancelledCount { get; init; }

    public int ReservedCount { get; init; }

    public int OccupiedCount { get; init; }

    public int RegisteredCount { get; init; }

    public string Code { get; init; } = "";

    public string Company { get; init; } = "";

    public string Project { get; init; } = "";

    public string Date { get; init; } = "";

    public string Period { get; init; } = "";

    public int Estimate { get; init; }

    public int Imported { get; init; }

    public int Registered { get; init; }

    public string Status { get; init; } = "";

    public string StatusTone { get; init; } = "";

    public bool CanRegister { get; init; }

    public string ContactName { get; init; } = "";

    public string ContactPhone { get; init; } = "";

    public string Email { get; init; } = "";

    public string Hospital { get; init; } = "";

    public string RegistrationRateText =>
        Imported > 0
            ? Math.Round((decimal)Registered / Imported * 100, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%"
            : "0%";
}

public sealed class CreateGroupAppointmentViewModel
{
    public GroupAppointmentBasicInfoViewModel BasicInfo { get; init; } = new();

    public IReadOnlyList<GroupRosterMemberViewModel> Members { get; init; } = [];

    public IReadOnlyList<RosterStatViewModel> RosterStats { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> ProjectOptions { get; init; } = [];

    public IReadOnlyList<ProjectLookupItemViewModel> ProjectLookupItems { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> WorkAreaOptions { get; init; } = [];

    public IReadOnlyList<PackageOptionViewModel> PackageOptions { get; init; } = [];

    public string UploadedFileName { get; init; } = "";

    public string UploadedFileSizeText { get; init; } = "";

    public string ImportMessage { get; init; } = "";

    public string RosterKey { get; init; } = "";

    public int PreviewRowLimit { get; init; } = 20;

    public IReadOnlyList<int> PageSizeOptions { get; init; } = [10, 20, 30, 40, 50];

    public int TotalCount => Members.Count;

    public int SuccessCount => Members.Count(member => member.Tone == "success");

    public int PendingCount => Members.Count(member => member.Tone == "pending");

    public int ErrorCount => Members.Count(member => member.Tone == "error");

    public int VisibleCount => Math.Min(PreviewRowLimit, Members.Count);
}

public sealed class GroupAppointmentBasicInfoViewModel
{
    public string CompanyName { get; init; } = "";

    public string ContactName { get; init; } = "";

    public string ContactPhone { get; init; } = "";

    public string Email { get; init; } = "";

    public string ReservationDateText { get; init; } = "";

    public string Period { get; init; } = "";

    public string Hospital { get; init; } = "";

    public int EstimateCount { get; init; }

    public string Note { get; init; } = "";
}

public sealed class GroupRosterMemberViewModel
{
    public int RowNumber { get; init; }

    public string IdNo { get; init; } = "";

    public string Name { get; init; } = "";

    public string Sex { get; init; } = "";

    public string StaffNo { get; init; } = "";

    public string CompanyBranch { get; init; } = "";

    public string Department { get; init; } = "";

    public string UnitClass { get; init; } = "";

    public string JobTitleSeatNo { get; init; } = "";

    public string Birthday { get; init; } = "";

    public string Phone { get; init; } = "";

    public string Mobile { get; init; } = "";

    public string Address { get; init; } = "";

    public string Email { get; init; } = "";

    public string CheckDate { get; init; } = "";

    public string IsDependent { get; init; } = "";

    public string DependentEmployeeId { get; init; } = "";

    public string ChartNo { get; init; } = "";

    public string NativePlace { get; init; } = "";

    public string Note { get; init; } = "";

    public string OccupationalAccidentBarcode { get; init; } = "";

    public string WorkArea { get; init; } = "";

    public string LabNo { get; init; } = "";

    public string DisplayPassword { get; init; } = "";

    public string SequenceNo { get; init; } = "";

    public string ReserveDate { get; init; } = "";

    public string EnvironmentTest { get; init; } = "";

    public string EnvironmentTestDescription { get; init; } = "";

    public string CheckNo { get; init; } = "";

    public string PublicPackageName { get; init; } = "";

    public string SelfPayAddOns { get; init; } = "";

    public string Status { get; init; } = "";

    public string Tone { get; init; } = "";

    public string ValidationMessage { get; init; } = "";
}

public sealed class RosterStatViewModel
{
    public string Label { get; init; } = "";

    public int Value { get; init; }

    public string Unit { get; init; } = "人";

    public string Tone { get; init; } = "";

    public string Icon { get; init; } = "";
}

public sealed class GroupActivityViewModel
{
    public string Tone { get; init; } = "";

    public string Title { get; init; } = "";

    public string Actor { get; init; } = "";

    public string Time { get; init; } = "";
}

public sealed class CreateAppointmentViewModel
{
    public string SelectedSelfPayAccountingUnit { get; init; } = string.Empty;

    public string SelectedSelfPayAccountingUnitDisplay { get; init; } = string.Empty;

    public IReadOnlyList<SelfPayAccountingUnitViewModel> SelfPayAccountingUnits { get; init; } = [];

    public bool IsEditMode { get; init; }

    public bool IsReadOnly { get; init; }

    public string ReturnUrl { get; init; } = "";

    public CreateAppointmentFormViewModel Form { get; init; } = new();

    public string? SelectedPackageId { get; init; }

    public IReadOnlyList<string> SelectedAddOnIds { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> ProjectOptions { get; init; } = [];

    public IReadOnlyList<ProjectLookupItemViewModel> ProjectLookupItems { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> WorkAreaOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> ConsultingDoctorOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> PhraseOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> ParkingOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> LaxativeOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> MealOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> AllergyOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> DietOptions { get; init; } = [];

    public IReadOnlyList<PackageOptionViewModel> PackageOptions { get; init; } = [];

    public IReadOnlyList<QuestionnaireOptionViewModel> QuestionnaireOptions { get; init; } = [];

    public IReadOnlyList<AddOnOptionViewModel> AddOnOptions { get; init; } = [];

    public IReadOnlyList<BillingItemViewModel> BillingItems { get; init; } = [];

    public IReadOnlyList<AppointmentFlowStepViewModel> FlowSteps { get; init; } = [];
}

public sealed class CreateAppointmentFormViewModel
{
    public string SelfPayAccountingUnit { get; init; } = string.Empty;

    public string PatientHealthCode { get; init; } = "";

    public string ProjectNo { get; init; } = "";

    public string ReservationDate { get; init; } = "";

    public string ReservationTime { get; init; } = "";

    public string ConsultingDoctorId { get; init; } = "";

    public string ConsultingDoctorName { get; init; } = "";

    public string ClinicRoom { get; init; } = "";

    public string PatientName { get; init; } = "";

    public string PatientIdNo { get; init; } = "";

    public string PatientBirthDate { get; init; } = "";

    public string PatientAge { get; init; } = "";

    public string PatientSex { get; init; } = "";

    public string PatientPhone { get; init; } = "";

    public string PatientMobile { get; init; } = "";

    public string PatientEmail { get; init; } = "";

    public string ReserveStatus { get; set; } = "";

    public string PatientChartNo { get; init; } = "";

    public string ReservationArea { get; init; } = "";

    public string ParkingType { get; init; } = "";

    public string Laxative { get; init; } = "";

    public string MealType { get; init; } = "";

    public string ContactName { get; init; } = "";

    public string ContactPhone { get; init; } = "";

    public string ContactRelationship { get; init; } = "";

    public string MailAddress { get; init; } = "";

    public string LiveAddress { get; init; } = "";

    public bool IsSameAddress { get; init; } = true;

    public bool IsVip { get; init; }

    public bool IsEmployeeFamily { get; init; }

    public string BranchFactory { get; init; } = "";

    public string EmployeeStudentNo { get; init; } = "";

    public string CustomerNo { get; init; } = "";

    public string FamilyNote { get; init; } = "";

    public string NotificationNote { get; init; } = "";

    public string CurrentNote { get; init; } = "";

    public string PermanentNote { get; init; } = "";

    public string PackageAmount { get; init; } = "";

    public string ExtraAmount { get; init; } = "";

    public string RemitAmount { get; init; } = "";

    public string DiscountAmount { get; init; } = "";

    public string AccountAmount { get; init; } = "";

    public string ReceivedAmount { get; init; } = "";

    public string PaymentDate { get; init; } = "";

    public string PaymentMethod { get; init; } = "";

    public string PaymentNote { get; init; } = "";

    public IReadOnlyList<PackageOptionViewModel> Packages { get; init; } = [];

    public IReadOnlyList<OtherSuiteLookupItemViewModel> OtherSuites { get; init; } = [];

    public IReadOnlyList<QuestionnaireOptionViewModel> Questionnaires { get; init; } = [];
}

public sealed class CreateAppointmentPostModel
{
    public string? SelfPayAccountingUnit { get; init; }

    public bool IsEditMode { get; init; }

    public string? OriginalPatientHealthCode { get; init; }

    public string? OriginalProjectNo { get; init; }

    public string? PatientHealthCode { get; init; }

    public string? ProjectNo { get; init; }

    public DateTime? ReservationDate { get; init; }

    public string? ReservationTime { get; init; }

    public string? ConsultingDoctorId { get; init; }

    public string? ConsultingDoctorName { get; init; }

    public string? ClinicRoom { get; init; }

    public string? PatientName { get; init; }

    public string? PatientIdNo { get; init; }

    public string? PatientBirthDate { get; init; }

    public string? PatientAge { get; init; }

    public string? PatientSex { get; init; }

    public string? PatientPhone { get; init; }

    public string? PatientMobile { get; init; }

    public string? PatientEmail { get; init; }

    public string? ReserveStatus { get; init; }

    public List<string> SelectedPackageIds { get; init; } = [];

    public List<string> SelectedPackageNames { get; init; } = [];

    public List<string> SelectedPackageAmounts { get; init; } = [];

    public List<string> SelectedPackageSelfPayAccountingUnits { get; init; } = [];

    public List<string> SelectedOtherSuiteIds { get; init; } = [];

    public List<string> SelectedOtherSuiteSelfPayAccountingUnits { get; init; } = [];

    public List<string> SelectedQuestionnaireIds { get; init; } = [];

    public List<string> SelectedQuestionnaireNames { get; init; } = [];

    public List<string> SelectedQuestionnaireAmounts { get; init; } = [];

    public string? PatientChartNo { get; init; }

    public string? ReservationArea { get; init; }

    public string? ContactName { get; init; }

    public string? ContactPhone { get; init; }

    public string? ContactRelationship { get; init; }

    public string? MailAddress { get; init; }

    public string? LiveAddress { get; init; }

    public bool IsSameAddress { get; init; }

    public bool IsVip { get; init; }

    public bool IsEmployeeFamily { get; init; }

    public string? BranchFactory { get; init; }

    public string? EmployeeStudentNo { get; init; }

    public string? CustomerNo { get; init; }

    public string? FamilyNote { get; init; }

    public string? NotificationNote { get; init; }

    public string? CurrentNote { get; init; }

    public string? PermanentNote { get; init; }

    public string? ParkingType { get; init; }

    public string? Laxative { get; init; }

    public string? MealType { get; init; }

    public string? PackageAmount { get; init; }

    public string? ExtraAmount { get; init; }

    public string? RemitAmount { get; init; }

    public string? DiscountAmount { get; init; }

    public string? AccountAmount { get; init; }

    public string? ReceivedAmount { get; init; }

    public DateTime? PaymentDate { get; init; }

    public string? PaymentMethod { get; init; }

    public string? PaymentNote { get; init; }
}

public sealed class SelectOptionViewModel
{
    public string Value { get; init; } = "";

    public string Text { get; init; } = "";

    public bool IsSelected { get; init; }
}

public sealed class ProjectLookupItemViewModel
{
    public string SelfPayAccountingUnit { get; init; } = string.Empty;

    public string ProjectNo { get; init; } = "";

    public string CustomerNo { get; init; } = "";

    public string CustomerName { get; init; } = "";

    public string CheckYear { get; init; } = "";

    public string ProjectName { get; init; } = "";

    public string CheckDate { get; init; } = "";

    public string EndDate { get; init; } = "";

    public string Note { get; init; } = "";

}

public sealed class AppointmentSummaryCardViewModel
{
    public string Label { get; init; } = "";

    public int Value { get; init; }

    public string Tone { get; init; } = "";

    public string Icon { get; init; } = "";

    public string Key { get; init; } = "";
}

public sealed class AppointmentStatusSummaryCardViewModel
{
    public string Key { get; init; } = "";

    public string Label { get; init; } = "";

    public string Tone { get; init; } = "";

    public int TotalCount { get; init; }

    public int GroupCount { get; init; }

    public int IndividualCount { get; init; }
}

public sealed class CapacitySlotViewModel
{
    public string Time { get; init; } = "";

    public string Used { get; init; } = "";

    public string Detail { get; init; } = "";

    public string Hint { get; init; } = "";

    public bool Active { get; init; }
}

public sealed class AppointmentReminderViewModel
{
    public string Label { get; init; } = "";

    public string Value { get; init; } = "";

    public string Unit { get; init; } = "";

    public string Tone { get; init; } = "";

    public string Icon { get; init; } = "";
}

public sealed class AppointmentFlowStepViewModel
{
    public string Label { get; init; } = "";

    public string Description { get; init; } = "";

    public string Icon { get; init; } = "";

    public bool IsActive { get; init; }
}

public sealed class PatientLookupFrameViewModel
{
    public string Title { get; init; } = "查詢受檢者";

    public string InitialKeyword { get; init; } = "";

    public string SelectedFilter { get; init; } = "chartNo";

    public string SearchPlaceholder { get; init; } = "請輸入病歷號/身分證/姓名/手機";

    public IReadOnlyList<PatientLookupFilterViewModel> Filters { get; init; } = [];

    public IReadOnlyList<PatientLookupPatientViewModel> Patients { get; init; } = [];
}

public sealed class PatientLookupFilterViewModel
{
    public string Key { get; init; } = "";

    public string Label { get; init; } = "";

    public bool IsActive { get; init; }
}

public sealed class PatientLookupPatientViewModel
{
    public string ChartNo { get; init; } = "";

    public string IdNo { get; init; } = "";

    public string MaskedId { get; init; } = "";

    public string Name { get; init; } = "";

    public string Sex { get; init; } = "";

    public string Age { get; init; } = "";

    public string BirthDate { get; init; } = "";

    public string BloodType { get; init; } = "";

    public string Phone { get; init; } = "";

    public string Mobile { get; init; } = "";

    public string MemberNo { get; init; } = "";

    public string HealthCode { get; init; } = "";

    public string Email { get; init; } = "";

    public string ContactName { get; init; } = "";

    public string ContactPhone { get; init; } = "";

    public string ContactRelationship { get; init; } = "";

    public bool IsVip { get; init; }

    public bool IsEmployeeFamily { get; init; }

    public string FamilyNote { get; init; } = "";

    public string MailingAddress { get; init; } = "";

    public string Address { get; init; } = "";

    public string NotificationNote { get; init; } = "";

    public string Allergy { get; init; } = "";

    public string Diet { get; init; } = "";

    public string SpecialNeed { get; init; } = "";
}

public sealed class OtherSuiteLookupFrameViewModel
{
    public string Title { get; init; } = "新增組套";

    public string InitialKeyword { get; init; } = "";

    public string SelectedProjectNo { get; init; } = "";

    public string SelectedProjectName { get; init; } = "";

    public IReadOnlyList<OtherSuiteProjectViewModel> Projects { get; init; } = [];

    public IReadOnlyList<OtherSuiteLookupItemViewModel> Suites { get; init; } = [];
}

public sealed class OtherSuiteProjectViewModel
{
    public string ProjectNo { get; init; } = "";

    public string Name { get; init; } = "";

    public bool IsSelected { get; init; }
}

public sealed class OtherSuiteLookupItemViewModel
{
    public string Id { get; init; } = "";

    public string ProjectNo { get; init; } = "";

    public string ProjectName { get; init; } = "";

    public string Name { get; init; } = "";

    public string Code { get; init; } = "";

    public string SelfPayAccountingUnit { get; init; } = "00";

    public string SelfPayAccountingUnitDisplay { get; init; } = "[00] 無優待";

    public decimal Price { get; init; }

    public string PriceText => Price.ToString("N0", CultureInfo.InvariantCulture);
}

public sealed class PackageOrderOptionViewModel
{
    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public string Category { get; init; } = "";
}

public sealed class PackageOptionViewModel
{
    public string Id { get; init; } = "";

    public string LisItemName { get; init; } = "";

    public int OrderTotal { get; init; }

    public string Level { get; init; } = "";

    public string Name { get; init; } = "";

    public string SelfPayAccountingUnit { get; init; } = "";

    public string SelfPayAccountingUnitDisplay { get; init; } = "";

    public decimal Price { get; init; }

    public bool IsSelected { get; init; }

    public IReadOnlyList<PackageOrderOptionViewModel> Orders { get; init; } = [];

    public string PriceText => FormatCurrency(Price);

    private static string FormatCurrency(decimal value) =>
        "$" + value.ToString("N0", CultureInfo.InvariantCulture);
}

public sealed class QuestionnaireOptionViewModel
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string FirstChar { get; init; } = "";

    public decimal Price { get; init; }

    public bool IsSelected { get; init; }

    public string PriceText => Price.ToString("N0", CultureInfo.InvariantCulture);
}

public sealed class AddOnOptionViewModel
{
    public string Id { get; init; } = "";

    public string Category { get; init; } = "";

    public string Name { get; init; } = "";

    public decimal Price { get; init; }

    public bool IsSelected { get; init; }

    public string PriceText => FormatCurrency(Price);

    private static string FormatCurrency(decimal value) =>
        "$" + value.ToString("N0", CultureInfo.InvariantCulture);
}

public sealed class BillingItemViewModel
{
    public string Label { get; init; } = "";

    public string Value { get; init; } = "";

    public string Tone { get; init; } = "";
}
