namespace HealthSys_20260428_v2.csproj.Models;

public sealed class InspectionTasksViewModel
{
    public IReadOnlyList<InspectionTaskListItemViewModel> Tasks { get; init; } = [];

    public string FilterStartDate { get; init; } = "";

    public string FilterEndDate { get; init; } = "";

    public string QueryErrorMessage { get; init; } = "";

    public IReadOnlyList<string> ProjectNames { get; init; } = [];
}

public sealed class InspectionTaskListItemViewModel
{
    public bool Selected { get; init; }

    public string Barcode { get; init; } = "";

    public string ReserveStatus { get; init; } = "";

    public string Status { get; init; } = "";

    public string StatusTone { get; init; } = "waiting";

    public string Name { get; init; } = "";

    public string ProjectNumber { get; init; } = "";

    public string ProjectName { get; init; } = "";

    public IReadOnlyList<InspectionFlowStepViewModel> FlowSteps { get; init; } = [];

    public int ProgressPercent => FlowSteps.Count == 0 ? 0 : (int)Math.Round(100d * FlowSteps.Count(step => step.State == "done") / FlowSteps.Count);

    public string Progress => $"{ProgressPercent}%";

    public string FlowStatus => FlowSteps.Count > 0 && FlowSteps.All(step => step.State == "done") ? "complete" : "progress";

    public string Abnormal { get; init; } = "--";

    public int CriticalCount { get; init; }

    public string Chop1Date { get; init; } = "";

    public string Chop1Time { get; init; } = "";

    public string Chop1Room { get; init; } = "";

    public string Intop1No { get; init; } = "";

    public string Chop0Dc { get; init; } = "";
}

public sealed class InspectionFlowStepViewModel
{
    public string Name { get; init; } = "";
    public string State { get; init; } = "pending";
    public string Time { get; init; } = "";
    public string Status => State switch { "done" => "已完成", "active" => "進行中", _ => "待檢查" };
}

public sealed class InspectionImageReportViewModel
{
    public string HisGroupRequestNumber { get; init; } = "";

    public string HisRequestNumber { get; init; } = "";

    public string AccessionNumber { get; init; } = "";

    public string StatusCode { get; init; } = "";

    public string StatusName { get; init; } = "";

    public string ReportId { get; init; } = "";

    public string ModifyDoctor { get; init; } = "";

    public string ModifyDate { get; init; } = "";

    public string Findings { get; init; } = "";

    public string Impression { get; init; } = "";
}
