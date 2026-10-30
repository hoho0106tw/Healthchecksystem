using System.Collections.Generic;

namespace HealthSys_20260428_v2.csproj.Models;

public sealed class BasicDataMaintenanceViewModel
{
    public IReadOnlyList<PhraseNoteItemViewModel> PhraseNotes { get; init; } = [];

    public IReadOnlyList<BookingAreaItemViewModel> BookingAreas { get; init; } = [];

    public IReadOnlyList<FlowSheetItemViewModel> FlowSheets { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> StatusOptions { get; init; } = [];

    public IReadOnlyList<SelectOptionViewModel> BookingAreaStatusOptions { get; init; } = [];

    public IReadOnlyList<int> PageSizeOptions { get; init; } = [];
}

public sealed class PhraseNoteItemViewModel
{
    public string Content { get; init; } = "";

    public string Category { get; init; } = "";

    public string Status { get; init; } = "";

    public string StatusTone { get; init; } = "";
}

public sealed class BookingAreaItemViewModel
{
    public string Code { get; init; } = "";

    public string Name { get; init; } = "";

    public string Status { get; init; } = "";

    public string StatusTone { get; init; } = "";
}

public sealed class FlowSheetItemViewModel
{
    public string No { get; init; } = "";

    public string Name { get; init; } = "";
}
