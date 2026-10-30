using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Globalization;

namespace HealthSys_20260428_v2.csproj.Controllers;

public sealed class MedicalOrderMaintenanceController : Controller
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public IActionResult HospitalMedicalOrders(
        string? orderCode = null,
        string? orderName = null,
        int page = 1,
        int pageSize = 20,
        string? initial = null)
    {
        var normalizedPageSize = new[] { 10, 20, 30, 50 }.Contains(pageSize) ? pageSize : 20;
        var normalizedCode = Normalize(orderCode);
        var normalizedName = Normalize(orderName);
        var normalizedInitial = Normalize(initial).ToUpperInvariant();
        if (normalizedInitial != "0-9" && normalizedInitial != "OTHER" &&
            !(normalizedInitial.Length == 1 && normalizedInitial[0] >= 'A' && normalizedInitial[0] <= 'Z'))
        {
            normalizedInitial = "";
        }
        var normalizedPage = Math.Max(1, page);
        var totalCount = 0;
        var items = Array.Empty<HospitalMedicalOrderItemViewModel>();
        var errorMessage = "";

        try
        {
            var query = OracleSql.SELECT.BuildHospitalMedicalOrdersPageQuery(
                normalizedCode, normalizedName, normalizedInitial, normalizedPage, normalizedPageSize);
            var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var table = connectionFactory.GETDbGenDataTable(
                query.Sql, query.Parameters, commandTimeoutSeconds: 30, cancellationToken: HttpContext.RequestAborted);
            totalCount = table.Rows.Count == 0 ? 0 : Convert.ToInt32(table.Rows[0]["TOTAL_COUNT"], CultureInfo.InvariantCulture);
            normalizedPage = table.Rows.Count == 0 ? 1 : Convert.ToInt32(table.Rows[0]["RESULT_PAGE"], CultureInfo.InvariantCulture);
            HttpContext.RequestServices.GetRequiredService<ILogger<MedicalOrderMaintenanceController>>()
                .LogInformation("院內醫令資料庫查詢耗時 {ElapsedMilliseconds} ms，符合 {TotalCount} 筆。", timer.ElapsedMilliseconds, totalCount);
            items = table.Rows.Cast<DataRow>().Select(row => new HospitalMedicalOrderItemViewModel
            {
                Code = GetString(row, "CHORDNO"),
                Name = GetString(row, "CHORDENAME"),
                Unit = GetString(row, "CHORDUNIT")
            }).ToArray();
        }
        catch (Exception) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<MedicalOrderMaintenanceController>>()
                .LogError(exception, "院內醫令查詢失敗。");
            errorMessage = "院內醫令資料載入失敗，請稍後再試或確認資料庫連線。";
            totalCount = 0;
            normalizedPage = 1;
        }

        var model = new HospitalMedicalOrdersViewModel
        {
            OrderCode = normalizedCode,
            OrderName = normalizedName,
            Initial = normalizedInitial,
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            ErrorMessage = errorMessage
        };
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
            ? PartialView(nameof(HospitalMedicalOrders), model)
            : View(model);
    }

    public IActionResult MedicalOrderMaintenance(
        string? category = null,
        string? keyword = null,
        int page = 1,
        int pageSize = 20)
    {
        var normalizedCategory = Normalize(category);
        var normalizedKeyword = Normalize(keyword);
        var normalizedPageSize = new[] { 10, 20, 30, 50 }.Contains(pageSize) ? pageSize : 20;
        var normalizedPage = Math.Max(1, page);
        var categories = Array.Empty<ProjectPackageCategoryViewModel>();
        var itemCategories = Array.Empty<string>();
        var allItems = Array.Empty<MedicalOrderMaintenanceItemViewModel>();
        var errorMessage = "";

        try
        {
            var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();
            var categoryTable = connectionFactory.GETDataTable(OracleSql.SELECT.getCheckItemCategories(), "", true);
            var itemCategoryTable = connectionFactory.GETDataTable(OracleSql.SELECT.getCheckItemItemCategories(), "", true);

            var categoryValues = categoryTable.Rows
                .Cast<DataRow>()
                .Select(row => GetString(row, "CHECKCATEGORY"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            categories = categoryValues
                .Select((value, index) => new ProjectPackageCategoryViewModel
                {
                    Name = value,
                    Key = value,
                    Tone = CategoryTone(index),
                    Icon = CategoryIcon(value),
                    Active = string.Equals(value, normalizedCategory, StringComparison.Ordinal)
                })
                .ToArray();

            itemCategories = itemCategoryTable.Rows
                .Cast<DataRow>()
                .Select(row => GetString(row, "ITEMCATEGORY"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var categoryStyles = categories.ToDictionary(category => category.Key, StringComparer.Ordinal);

            var itemTable = connectionFactory.GETDataTable(
                OracleSql.SELECT.getCheckItemsByCategory(normalizedCategory, normalizedKeyword),
                "",
                true);

            allItems = itemTable.Rows
                .Cast<DataRow>()
                .Select(row => CreateItem(row, categoryStyles))
                .Where(item => !string.IsNullOrWhiteSpace(item.Code) || !string.IsNullOrWhiteSpace(item.Name))
                .ToArray();
        }
        catch
        {
            errorMessage = "醫令資料載入失敗，請稍後再試或確認資料庫連線。";
        }

        var totalCount = allItems.Length;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)normalizedPageSize));
        normalizedPage = Math.Min(normalizedPage, totalPages);

        return View(new MedicalOrderMaintenanceViewModel
        {
            Category = normalizedCategory,
            Keyword = normalizedKeyword,
            Categories = categories,
            ItemCategories = itemCategories,
            Items = allItems.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).ToArray(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            ErrorMessage = errorMessage
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateMedicalOrder([FromForm] MedicalOrderMaintenanceInputModel input)
    {
        NormalizeInput(input);
        var validationMessage = ValidateInput(input, requireOriginalCheckNo: false);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { saved = false, message = validationMessage });
        }

        try
        {
            var insertedCount = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQuery(
                OracleSql.INSERT.InsertCheckItem(),
                BuildInputParameters(input));

            return Json(new
            {
                saved = insertedCount > 0,
                updatedCount = insertedCount,
                message = insertedCount > 0 ? "醫令已新增。" : "醫令未新增，請確認輸入資料。"
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                saved = false,
                message = "醫令新增失敗，請確認醫令代碼是否重複或資料格式是否正確。"
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateMedicalOrder([FromForm] MedicalOrderMaintenanceInputModel input)
    {
        NormalizeInput(input);
        var validationMessage = ValidateInput(input, requireOriginalCheckNo: true);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { saved = false, message = validationMessage });
        }

        try
        {
            var parameters = BuildInputParameters(input);
            parameters["ORIGINAL_CHECKNO"] = input.OriginalCheckNo;
            var updatedCount = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQuery(
                OracleSql.UPDATE.UpdateCheckItem(),
                parameters);

            if (updatedCount < 1)
            {
                return NotFound(new { saved = false, message = "找不到要更新的醫令資料。" });
            }

            return Json(new { saved = true, updatedCount, message = "醫令已更新。" });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                saved = false,
                message = "醫令更新失敗，請確認醫令代碼是否重複或資料格式是否正確。"
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteMedicalOrder([FromForm] string? checkNo)
    {
        var normalizedCheckNo = Normalize(checkNo);
        if (string.IsNullOrWhiteSpace(normalizedCheckNo))
        {
            return BadRequest(new { deleted = false, message = "缺少醫令代碼，無法刪除。" });
        }

        try
        {
            var deletedCount = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQuery(
                OracleSql.DELETE.DeleteCheckItem(),
                new Dictionary<string, object?> { ["CHECKNO"] = normalizedCheckNo });

            if (deletedCount < 1)
            {
                return NotFound(new { deleted = false, message = "找不到要刪除的醫令資料。" });
            }

            return Json(new { deleted = true, deletedCount, message = "醫令已刪除。" });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                deleted = false,
                message = "醫令刪除失敗，資料可能仍被其他功能使用。"
            });
        }
    }

    [HttpGet]
    public IActionResult ExportExcel(string? category = null, string? keyword = null)
    {
        var normalizedCategory = Normalize(category);
        var normalizedKeyword = Normalize(keyword);

        try
        {
            var table = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.getCheckItemsByCategory(normalizedCategory, normalizedKeyword),
                "",
                true);

            string[] headers = ["醫令代碼", "醫令名稱", "英文名稱", "檢驗分類", "細項類別", "格式", "單位"];
            var rows = table.Rows.Cast<DataRow>()
                .Select(row => (IReadOnlyList<string>)new[]
                {
                    GetString(row, "CHECKNO"),
                    GetString(row, "NAME"),
                    GetString(row, "ENAME"),
                    GetString(row, "CHECKCATEGORY"),
                    GetString(row, "ITEMCATEGORY"),
                    GetString(row, "FORMAT"),
                    GetString(row, "UNIT")
                })
                .ToArray();

            var workbook = SimpleExcelWorkbook.Create("醫令維護", headers, rows);
            var fileName = $"醫令維護_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(workbook, ExcelContentType, fileName);
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "醫令資料匯出失敗，請稍後再試或確認資料庫連線。");
        }
    }

    private static MedicalOrderMaintenanceItemViewModel CreateItem(
        DataRow row,
        IReadOnlyDictionary<string, ProjectPackageCategoryViewModel> categoryStyles)
    {
        var category = GetString(row, "CHECKCATEGORY");
        categoryStyles.TryGetValue(category, out var categoryStyle);

        return new()
        {
            Code = GetString(row, "CHECKNO"),
            Name = GetString(row, "NAME"),
            EnglishName = GetString(row, "ENAME"),
            Category = category,
            CategoryTone = categoryStyle?.Tone ?? CategoryTone(StableCategoryIndex(category)),
            CategoryIcon = categoryStyle?.Icon ?? CategoryIcon(category),
            ItemCategory = GetString(row, "ITEMCATEGORY"),
            Format = GetString(row, "FORMAT"),
            Unit = GetString(row, "UNIT")
        };
    }

    private static string Normalize(string? value) => value?.Trim() ?? "";

    private static void NormalizeInput(MedicalOrderMaintenanceInputModel input)
    {
        input.OriginalCheckNo = Normalize(input.OriginalCheckNo);
        input.CheckNo = Normalize(input.CheckNo);
        input.Name = Normalize(input.Name);
        input.CheckCategory = Normalize(input.CheckCategory);
        input.ItemCategory = Normalize(input.ItemCategory);
        input.EName = Normalize(input.EName);
        input.Format = Normalize(input.Format);
        input.Unit = Normalize(input.Unit);
    }

    private static string ValidateInput(MedicalOrderMaintenanceInputModel input, bool requireOriginalCheckNo)
    {
        if (requireOriginalCheckNo && string.IsNullOrWhiteSpace(input.OriginalCheckNo)) return "缺少原醫令代碼，無法更新。";
        if (string.IsNullOrWhiteSpace(input.CheckNo)) return "請輸入醫令代碼。";
        if (input.CheckNo.Length > 26) return "醫令代碼不可超過 26 個字元。";

        var longFields = new Dictionary<string, string>
        {
            ["醫令名稱"] = input.Name,
            ["檢查分類"] = input.CheckCategory,
            ["細項類別"] = input.ItemCategory,
            ["英文名稱"] = input.EName
        };

        var oversizedField = longFields.FirstOrDefault(field => field.Value.Length > 500);
        if (!string.IsNullOrWhiteSpace(oversizedField.Key)) return $"{oversizedField.Key}不可超過 500 個字元。";
        if (input.Format.Length > 100) return "格式不可超過 100 個字元。";
        if (input.Unit.Length > 100) return "單位不可超過 100 個字元。";
        return "";
    }

    private static Dictionary<string, object?> BuildInputParameters(MedicalOrderMaintenanceInputModel input) => new()
    {
        ["CHECKNO"] = input.CheckNo,
        ["NAME"] = input.Name,
        ["CHECKCATEGORY"] = input.CheckCategory,
        ["ITEMCATEGORY"] = input.ItemCategory,
        ["ENAME"] = input.EName,
        ["FORMAT"] = input.Format,
        ["UNIT"] = input.Unit
    };

    private static string GetString(DataRow row, string columnName) =>
        row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
            ? Convert.ToString(row[columnName], CultureInfo.InvariantCulture)?.Trim() ?? ""
            : "";

    private static string CategoryTone(int index)
    {
        string[] tones = [
            "red", "blue", "orange", "purple", "green", "pink", "cyan",
            "amber", "indigo", "teal", "rose", "violet", "lime", "brown"
        ];

        return tones[Math.Abs(index % tones.Length)];
    }

    private static int StableCategoryIndex(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = (hash * 31) + character;
            }

            return hash == int.MinValue ? 0 : Math.Abs(hash);
        }
    }

    private static string CategoryIcon(string value)
    {
        if (value.Contains("影像", StringComparison.Ordinal) || value.Contains("X", StringComparison.OrdinalIgnoreCase)) return "image";
        if (value.Contains("血", StringComparison.Ordinal)) return "drop";
        if (value.Contains("生化", StringComparison.Ordinal) || value.Contains("檢驗", StringComparison.Ordinal)) return "flask";
        if (value.Contains("尿", StringComparison.Ordinal)) return "beaker";
        if (value.Contains("心", StringComparison.Ordinal)) return "pulse";
        if (value.Contains("肺", StringComparison.Ordinal)) return "lung";
        if (value.Contains("眼", StringComparison.Ordinal)) return "eye";
        if (value.Contains("耳", StringComparison.Ordinal)) return "ear";
        if (value.Contains("超音波", StringComparison.Ordinal)) return "wave";
        if (value.Contains("肝", StringComparison.Ordinal)) return "liver";
        if (value.Contains("腎", StringComparison.Ordinal)) return "kidney";
        if (value.Contains("糞", StringComparison.Ordinal)) return "sample";
        if (value.Contains("婦", StringComparison.Ordinal)) return "female";
        if (value.Contains("特殊", StringComparison.Ordinal) || value.Contains("過敏", StringComparison.Ordinal)) return "spark";
        return "stethoscope";
    }
}
