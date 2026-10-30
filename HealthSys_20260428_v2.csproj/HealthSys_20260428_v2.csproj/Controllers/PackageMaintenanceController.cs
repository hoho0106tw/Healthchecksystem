using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Globalization;

namespace HealthSys_20260428_v2.csproj.Controllers;

public sealed class PackageMaintenanceController : Controller
{
    public IActionResult PackageMaintenance(
        string? startDate = null,
        string? endDate = null,
        string? projectName = null,
        string? projectNo = null,
        string? packageName = null,
        string? schemeCode = null,
        string? customerName = null,
        int page = 1,
        int pageSize = 20)
    {
        var today = DateTime.Today;
        var normalizedEndDate = ParseDate(endDate) ?? today;
        var normalizedStartDate = ParseDate(startDate) ?? normalizedEndDate.AddYears(-1);

        if (normalizedStartDate > normalizedEndDate)
        {
            (normalizedStartDate, normalizedEndDate) = (normalizedEndDate, normalizedStartDate);
        }

        var normalizedPageSize = new[] { 10, 20, 30, 50 }.Contains(pageSize) ? pageSize : 20;
        var normalizedPage = Math.Max(1, page);
        var allItems = Array.Empty<PackageMaintenanceItemViewModel>();
        var errorMessage = "";

        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["START_DATE"] = normalizedStartDate,
                ["END_DATE_EXCLUSIVE"] = normalizedEndDate.AddDays(1),
                ["PROJECT_NAME"] = LikeValue(projectName),
                ["PROJECT_NO"] = LikeValue(projectNo),
                ["PACKAGE_NAME"] = LikeValue(packageName),
                ["SCHEME_CODE"] = LikeValue(schemeCode),
                ["CUSTOMER_NAME"] = LikeValue(customerName)
            };

            var table = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetPackageMaintenanceList(),
                parameters,
                TestConnection: true);

            allItems = table.Rows.Cast<DataRow>().Select(CreateItem).ToArray();
        }
        catch
        {
            errorMessage = "組套資料讀取失敗，請稍後再試。";
        }

        var totalCount = allItems.Length;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)normalizedPageSize));
        normalizedPage = Math.Min(normalizedPage, totalPages);

        return View(new PackageMaintenanceViewModel
        {
            StartDate = normalizedStartDate,
            EndDate = normalizedEndDate,
            ProjectName = Normalize(projectName),
            ProjectNo = Normalize(projectNo),
            PackageName = Normalize(packageName),
            SchemeCode = Normalize(schemeCode),
            CustomerName = Normalize(customerName),
            Items = allItems
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToArray(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            ErrorMessage = errorMessage
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateSchemeCode(string? projectNo, string? lisItemName, string? schemeCode)
    {
        var normalizedProjectNo = Normalize(projectNo);
        var normalizedLisItemName = Normalize(lisItemName);
        var normalizedSchemeCode = Normalize(schemeCode);

        if (string.IsNullOrWhiteSpace(normalizedProjectNo) || string.IsNullOrWhiteSpace(normalizedLisItemName))
        {
            return BadRequest(new
            {
                saved = false,
                updatedCount = 0,
                message = "缺少專案編號或 LISITEMNAME，無法更新自訂碼。"
            });
        }

        try
        {
            var updatedCount = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQuery(
                OracleSql.UPDATE.UpdateCheckProjectSchemeCode(),
                new Dictionary<string, object?>
                {
                    ["SCHEME_CODE"] = normalizedSchemeCode,
                    ["PROJECT_NO"] = normalizedProjectNo,
                    ["LIS_ITEM_NAME"] = normalizedLisItemName
                });
            var saved = updatedCount > 0;

            return Json(new
            {
                saved,
                updatedCount,
                message = saved ? "自訂碼已更新。" : "查無可更新的專案組套資料。"
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                saved = false,
                updatedCount = 0,
                message = "自訂碼更新失敗，請稍後再試。"
            });
        }
    }

    private static PackageMaintenanceItemViewModel CreateItem(DataRow row) => new()
    {
        ProjectNo = GetString(row, "PROJECTNO"),
        LisItemName = GetString(row, "LISITEMNAME"),
        ProjectName = GetString(row, "PROJECTNAME"),
        CheckDate = GetDate(row, "CHECKDATE"),
        PackageName = GetString(row, "NAME"),
        SchemeCode = GetString(row, "SCHEMECODE"),
        CustomerName = GetString(row, "CUSTOMERNAME")
    };

    private static string LikeValue(string? value)
    {
        var normalized = Normalize(value)
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

        return $"%{normalized}%";
    }

    private static string Normalize(string? value) => value?.Trim() ?? "";

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParseExact(
            Normalize(value),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate)
            ? parsedDate.Date
            : null;

    private static string GetString(DataRow row, string columnName) =>
        row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
            ? Convert.ToString(row[columnName], CultureInfo.InvariantCulture)?.Trim() ?? ""
            : "";

    private static DateTime? GetDate(DataRow row, string columnName)
    {
        if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
        {
            return null;
        }

        if (row[columnName] is DateTime date)
        {
            return date;
        }

        return DateTime.TryParse(
            Convert.ToString(row[columnName], CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate)
            ? parsedDate
            : null;
    }
}
