namespace HealthSys_20260428_v2.csproj.Models;

public sealed class ErrorViewModel
{
    public int StatusCode { get; init; }

    public string Title { get; init; } = "";

    public string Reason { get; init; } = "";

    public string Detail { get; init; } = "";

    public string RequestPath { get; init; } = "";

    public string RequestId { get; init; } = "";

    public string OccurredAt { get; init; } = "";

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
}
