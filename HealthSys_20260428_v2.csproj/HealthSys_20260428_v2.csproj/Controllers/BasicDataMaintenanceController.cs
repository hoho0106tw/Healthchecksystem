using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace HealthSys_20260428_v2.csproj.Controllers
{
    public class BasicDataMaintenanceController : Controller
    {
        public IActionResult BasicDataMaintenance()
        {
            var model = new BasicDataMaintenanceViewModel
            {
                PhraseNotes = GetPhraseNotes(),
                BookingAreas = GetBookingAreas(),
                FlowSheets = GetFlowSheets(),
                StatusOptions =
                [
                    new() { Value = "", Text = "全部", IsSelected = true },
                    new() { Value = "啟用", Text = "啟用" },
                    new() { Value = "停用", Text = "停用" }
                ],
                BookingAreaStatusOptions =
                [
                    new() { Value = "", Text = "全部", IsSelected = true },
                    new() { Value = "啟動", Text = "啟動" },
                    new() { Value = "停用", Text = "停用" }
                ],
                PageSizeOptions = [10, 20, 50]
            };

            return View(model);
        }

        private static IReadOnlyList<PhraseNoteItemViewModel> GetPhraseNotes()
        {
      
            var sql = OracleSql.SELECT.getHealthPHRASENOTETBL();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select(row => new PhraseNoteItemViewModel
                {
                    Category = GetString(row, "CATEGORY"),
                    Content = GetString(row, "PHRASE_CONTENT"),
                    Status = "啟用",
                    StatusTone = "enabled"
                })
                .ToArray();
        }

        private static IReadOnlyList<BookingAreaItemViewModel> GetBookingAreas()
        {
            var sql = OracleSql.SELECT.getHealthRESERVEAREATBL();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select(row =>
                {
                    var status = NormalizeBookingAreaStatus(GetString(row, "STATUS"));

                    return new BookingAreaItemViewModel
                    {
                        Code = GetString(row, "AREA_CODE"),
                        Name = GetString(row, "AREA_NAME"),
                        Status = status,
                        StatusTone = string.Equals(status, "停用", StringComparison.OrdinalIgnoreCase) ? "disabled" : "enabled"
                    };
                })
                .ToArray();
        }

        private static IReadOnlyList<FlowSheetItemViewModel> GetFlowSheets()
        {
            var sql = OracleSql.SELECT.getFlowListTBL();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select(row => new FlowSheetItemViewModel
                {
                    No = GetString(row, "NO"),
                    Name = GetString(row, "NAME")
                })
                .ToArray();
        }

        private static string GetString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return "";
            }

            return Convert.ToString(row[columnName])?.Trim() ?? "";
        }

        private static string NormalizeBookingAreaStatus(string status)
        {
            var value = (status ?? "").Trim();

            return value.ToUpperInvariant() switch
            {
                "Y" or "YES" or "TRUE" or "1" or "ON" => "啟動",
                "N" or "NO" or "FALSE" or "0" or "OFF" => "停用",
                "啟用" => "啟動",
                _ => value
            };
        }
    }
}
