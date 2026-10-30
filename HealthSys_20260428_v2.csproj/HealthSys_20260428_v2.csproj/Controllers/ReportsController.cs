using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Globalization;

namespace HealthSys_20260428_v2.csproj.Controllers
{
    public class ReportsController : Controller
    {
        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Reports));

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Reports(
            string? checkDateStart,
            string? checkDateEnd,
            CancellationToken requestAborted)
        {
            const int queryTimeoutSeconds = 30;
            var selectedEndDate = ParseInspectionDate(checkDateEnd) ?? DateTime.Today;
            var selectedStartDate = ParseInspectionDate(checkDateStart) ?? selectedEndDate.AddYears(-1);

            if (selectedEndDate < selectedStartDate)
            {
                (selectedStartDate, selectedEndDate) = (selectedEndDate, selectedStartDate);
            }

            using var queryTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            queryTimeoutSource.CancelAfter(TimeSpan.FromSeconds(queryTimeoutSeconds));
            var query = OracleSql.SELECT.BuildInspectionTasksQuery(selectedStartDate, selectedEndDate);
            InspectionTaskListItemViewModel[] tasks;
            var queryErrorMessage = string.Empty;

            try
            {
                var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                    query.Sql,
                    query.Parameters,
                    "",
                    true,
                    queryTimeoutSeconds,
                    queryTimeoutSource.Token);
                tasks = dataTable.Rows
                    .Cast<DataRow>()
                    .Select((row, index) => new InspectionTaskListItemViewModel
                    {
                        Selected = index == 0,
                        Barcode = GetInspectionValue(row, "BARCODESTR"),
                        ReserveStatus = GetInspectionValue(row, "RESERVE_STATUS"),
                        Status = GetInspectionValue(row, "STATUS_NAME"),
                        StatusTone = ResolveInspectionStatusTone(GetInspectionValue(row, "RESERVE_STATUS")),
                        Name = GetInspectionValue(row, "PATIENTNAME"),
                        ProjectNumber = GetInspectionValue(row, "PROJECTNO"),
                        ProjectName = GetInspectionValue(row, "PROJECTNAME"),
                        Chop1Date = GetInspectionValue(row, "CHOP1DATE"),
                        Chop1Time = GetInspectionValue(row, "CHOP1TIME"),
                        Chop1Room = GetInspectionValue(row, "CHOP1ROOM"),
                        Intop1No = GetInspectionValue(row, "INTOP1NO"),
                        Chop0Dc = GetInspectionValue(row, "CHOP0DC"),
                        FlowSteps = BuildDemoFlowSteps(row),
                        CriticalCount = int.TryParse(GetInspectionValue(row, "CRITICAL_COUNT"), out var criticalCount) ? criticalCount : 0,
                        Abnormal = GetInspectionValue(row, "ABNORMAL_COUNT") is { Length: > 0 } abnormalCount ? abnormalCount + " 項異常" : "--"
                    })
                    .ToArray();
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OracleException) when (requestAborted.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OperationCanceledException) when (queryTimeoutSource.IsCancellationRequested)
            {
                tasks = [];
                queryErrorMessage = $"查詢超過 {queryTimeoutSeconds} 秒仍未完成，請縮小日期範圍後再試。";
            }
            catch (OracleException exception)
            {
                tasks = [];
                queryErrorMessage = exception.Number == 1013
                    ? $"查詢超過 {queryTimeoutSeconds} 秒仍未完成，請縮小日期範圍後再試。"
                    : $"無法載入檢查報告資料（Oracle {exception.Number}）。";
                Console.Error.WriteLine($"InspectionTasks Oracle {exception.Number}: {exception.Message}");
            }

            return View(new InspectionTasksViewModel
            {
                Tasks = tasks,
                FilterStartDate = selectedStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                FilterEndDate = selectedEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                QueryErrorMessage = queryErrorMessage,
                ProjectNames = tasks
                    .Select(task => task.ProjectName)
                    .Where(projectName => !string.IsNullOrWhiteSpace(projectName))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(projectName => projectName, StringComparer.CurrentCulture)
                    .ToArray()
            });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult InspectionImageReports(
            string? chop1Date,
            string? chop1Time,
            string? chop1Room,
            string? intop1No,
            CancellationToken requestAborted)
        {
            const int queryTimeoutSeconds = 30;
            chop1Date = chop1Date?.Trim();
            chop1Time = chop1Time?.Trim();
            chop1Room = chop1Room?.Trim();

            if (string.IsNullOrWhiteSpace(chop1Date)
                || string.IsNullOrWhiteSpace(chop1Time)
                || string.IsNullOrWhiteSpace(chop1Room)
                || !int.TryParse(intop1No, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIntop1No))
            {
                return BadRequest(new { message = "選取的檢查報告缺少 CHOP1DATE、CHOP1TIME、CHOP1ROOM 或 INTOP1NO。" });
            }

            using var queryTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            queryTimeoutSource.CancelAfter(TimeSpan.FromSeconds(queryTimeoutSeconds));
            var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();

            try
            {
                var requestNumberQuery = OracleSql.SELECT.BuildInspectionImageRequestNumbersQuery(
                    chop1Date,
                    chop1Time,
                    chop1Room,
                    parsedIntop1No);
                var requestNumbers = connectionFactory.GETDbGenDataTable(
                    requestNumberQuery.Sql,
                    requestNumberQuery.Parameters,
                    queryTimeoutSeconds,
                    queryTimeoutSource.Token);

                var reports = new List<InspectionImageReportViewModel>();
                var reportTextCache = new Dictionary<string, (string Findings, string Impression)>(StringComparer.Ordinal);

                foreach (DataRow requestNumber in requestNumbers.Rows)
                {
                    var hisGroupRequestNumber = GetInspectionValue(requestNumber, "CHOP4GREQNO");
                    var hisRequestNumber = GetInspectionValue(requestNumber, "CHOP4REQNO");
                    var reportQuery = OracleSql.SELECT.BuildInspectionImageReportsQuery(hisGroupRequestNumber, hisRequestNumber);
                    var reportTable = connectionFactory.GETDbGenDataTable(
                        reportQuery.Sql,
                        reportQuery.Parameters,
                        queryTimeoutSeconds,
                        queryTimeoutSource.Token);

                    foreach (DataRow reportRow in reportTable.Rows)
                    {
                        var reportId = GetInspectionValue(reportRow, "REPORTID");
                        var findings = string.Empty;
                        var impression = string.Empty;

                        if (!string.IsNullOrWhiteSpace(reportId))
                        {
                            if (!reportTextCache.TryGetValue(reportId, out var reportText))
                            {
                                var textQuery = OracleSql.SELECT.BuildInspectionImageReportTextQuery(reportId);
                                var textTable = connectionFactory.GETDbGenDataTable(
                                    textQuery.Sql,
                                    textQuery.Parameters,
                                    queryTimeoutSeconds,
                                    queryTimeoutSource.Token);
                                reportText = (
                                    DecodeFirstReportText(textTable, "1"),
                                    DecodeFirstReportText(textTable, "2"));
                                reportTextCache[reportId] = reportText;
                            }

                            findings = reportText.Findings;
                            impression = reportText.Impression;
                        }

                        reports.Add(new InspectionImageReportViewModel
                        {
                            HisGroupRequestNumber = GetInspectionValue(reportRow, "HISGREQNO"),
                            HisRequestNumber = GetInspectionValue(reportRow, "HISREQNO"),
                            AccessionNumber = GetInspectionValue(reportRow, "ACCNO"),
                            StatusCode = GetInspectionValue(reportRow, "REPORT_STATUS_CODE"),
                            StatusName = GetInspectionValue(reportRow, "REPORT_STATUS_NAME"),
                            ReportId = reportId,
                            ModifyDoctor = GetInspectionValue(reportRow, "MODIFYDR"),
                            ModifyDate = FormatInspectionDate(reportRow, "MODIFYDATE"),
                            Findings = findings,
                            Impression = impression
                        });
                    }
                }

                return Json(new
                {
                    total = reports.Count,
                    complete = reports.Count(report => report.StatusCode == "RPTVS"),
                    alert = reports.Count(report => !string.IsNullOrWhiteSpace(report.Impression)),
                    rows = reports
                        .OrderByDescending(report => report.ModifyDate, StringComparer.Ordinal)
                        .Select(report => new
                        {
                            statusName = report.StatusName,
                            reportDate = report.ModifyDate,
                            modifyDoctor = report.ModifyDoctor,
                            findings = report.Findings,
                            impression = report.Impression
                        })
                });
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OracleException) when (requestAborted.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OperationCanceledException) when (queryTimeoutSource.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = $"查詢逾時 {queryTimeoutSeconds} 秒，請稍後再試。" });
            }
            catch (OracleException exception)
            {
                Console.Error.WriteLine($"InspectionImageReports Oracle {exception.Number}: {exception.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "無法載入影像報告資料。" });
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult InspectionSpecialReports(
            string? chop1Date,
            string? chop1Time,
            string? chop1Room,
            string? intop1No,
            CancellationToken requestAborted)
        {
            const int queryTimeoutSeconds = 30;
            chop1Date = chop1Date?.Trim();
            chop1Time = chop1Time?.Trim();
            chop1Room = chop1Room?.Trim();

            if (string.IsNullOrWhiteSpace(chop1Date)
                || string.IsNullOrWhiteSpace(chop1Time)
                || string.IsNullOrWhiteSpace(chop1Room)
                || !int.TryParse(intop1No, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIntop1No))
            {
                return BadRequest(new { message = "選取的檢查報告缺少 CHOP1DATE、CHOP1TIME、CHOP1ROOM 或 INTOP1NO。" });
            }

            using var queryTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            queryTimeoutSource.CancelAfter(TimeSpan.FromSeconds(queryTimeoutSeconds));
            var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();

            try
            {
                var requestNumberQuery = OracleSql.SELECT.BuildInspectionSpecialRequestNumbersQuery(
                    chop1Date,
                    chop1Time,
                    chop1Room,
                    parsedIntop1No);
                var requestNumbers = connectionFactory.GETDbGenDataTable(
                    requestNumberQuery.Sql,
                    requestNumberQuery.Parameters,
                    queryTimeoutSeconds,
                    queryTimeoutSource.Token);

                var reports = new List<(string StatusCode, string ReportTime, object Row)>();

                foreach (DataRow requestNumber in requestNumbers.Rows)
                {
                    var hisGroupRequestNumber = GetInspectionValue(requestNumber, "CHOP4GREQNO");
                    var hisRequestNumber = GetInspectionValue(requestNumber, "CHOP4REQNO");
                    var reportQuery = OracleSql.SELECT.BuildInspectionSpecialReportsQuery(hisGroupRequestNumber, hisRequestNumber);
                    var reportTable = connectionFactory.GETDbGenDataTable(
                        reportQuery.Sql,
                        reportQuery.Parameters,
                        queryTimeoutSeconds,
                        queryTimeoutSource.Token);

                    var textQuery = OracleSql.SELECT.BuildInspectionSpecialReportTextQuery(hisGroupRequestNumber, hisRequestNumber);
                    var textTable = connectionFactory.GETDbGenDataTable(
                        textQuery.Sql,
                        textQuery.Parameters,
                        queryTimeoutSeconds,
                        queryTimeoutSource.Token);
                    var content = string.Join(Environment.NewLine, textTable.Rows.Cast<DataRow>()
                        .Select(row => System.Net.WebUtility.HtmlDecode(GetInspectionValue(row, "CHTXT"))));

                    // Prefer status first, then newest time; emit one report per request pair.
                    var reportRow = reportTable.Rows.Cast<DataRow>().FirstOrDefault();
                    var statusCode = reportRow is null ? string.Empty : GetInspectionValue(reportRow, "CHSTAT");
                    var reportTime = reportRow is null ? string.Empty
                        : reportRow["CHMODDTM"] is DateTime timestamp
                            ? timestamp.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture)
                            : GetInspectionValue(reportRow, "CHMODDTM");
                    reports.Add((statusCode, reportTime, new
                    {
                        groupRequestNumber = hisGroupRequestNumber,
                        requestNumber = hisRequestNumber,
                        statusName = reportRow is null ? string.Empty : GetInspectionValue(reportRow, "CHSTAT_NAME"),
                        modifyDoctor = reportRow is null ? string.Empty : GetInspectionValue(reportRow, "CHMODNM"),
                        reportDate = reportRow is null ? string.Empty : FormatInspectionDate(reportRow, "CHMODDTM", convertRocDate: true),
                        content
                    }));
                }

                return Json(new
                {
                    total = reports.Count,
                    rows = reports
                        .OrderByDescending(report => report.StatusCode, StringComparer.Ordinal)
                        .ThenByDescending(report => report.ReportTime, StringComparer.Ordinal)
                        .Select(report => report.Row)
                });
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OracleException) when (requestAborted.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OperationCanceledException) when (queryTimeoutSource.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = $"查詢逾時 {queryTimeoutSeconds} 秒，請稍後再試。" });
            }
            catch (OracleException exception)
            {
                Console.Error.WriteLine($"InspectionSpecialReports Oracle {exception.Number}: {exception.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "無法載入特檢報告資料。" });
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult InspectionEcgReports(
            string? chop1Date,
            string? chop1Time,
            string? chop1Room,
            string? intop1No,
            CancellationToken requestAborted)
        {
            const int queryTimeoutSeconds = 30;
            chop1Date = chop1Date?.Trim();
            chop1Time = chop1Time?.Trim();
            chop1Room = chop1Room?.Trim();

            if (string.IsNullOrWhiteSpace(chop1Date)
                || string.IsNullOrWhiteSpace(chop1Time)
                || string.IsNullOrWhiteSpace(chop1Room)
                || !int.TryParse(intop1No, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIntop1No))
            {
                return BadRequest(new { message = "選取的檢查報告缺少 CHOP1DATE、CHOP1TIME、CHOP1ROOM 或 INTOP1NO。" });
            }

            using var queryTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            queryTimeoutSource.CancelAfter(TimeSpan.FromSeconds(queryTimeoutSeconds));
            var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            var queryStage = "K001 request numbers";

            try
            {
                var requestNumberQuery = OracleSql.SELECT.BuildInspectionEcgRequestNumbersQuery(
                    chop1Date,
                    chop1Time,
                    chop1Room,
                    parsedIntop1No);
                var requestNumbers = connectionFactory.GETDbGenDataTable(
                    requestNumberQuery.Sql,
                    requestNumberQuery.Parameters,
                    queryTimeoutSeconds,
                    queryTimeoutSource.Token);

                var reports = new List<object>();
                var seenRequests = new HashSet<(string, string)>();

                foreach (DataRow requestNumber in requestNumbers.Rows)
                {
                    var groupRequestNumber = GetInspectionValue(requestNumber, "CHOP4GREQNO");
                    var individualRequestNumber = GetInspectionValue(requestNumber, "CHOP4REQNO");
                    if (string.IsNullOrWhiteSpace(groupRequestNumber)
                        || string.IsNullOrWhiteSpace(individualRequestNumber)
                        || !seenRequests.Add((groupRequestNumber, individualRequestNumber)))
                    {
                        continue;
                    }

                    queryTimeoutSource.Token.ThrowIfCancellationRequested();
                    queryStage = "EKGReportTbl";
                    // OpdOrdTbl.CHOP4GREQNO -> EKGReportTbl.VCHGREQNO;
                    // OpdOrdTbl.CHOP4REQNO -> EKGReportTbl.VCHREQNO (keep leading zeroes).
                    var reportQuery = OracleSql.SELECT.BuildInspectionEcgReportsQuery(
                        groupRequestNumber: groupRequestNumber,
                        requestNumber: individualRequestNumber);
                    var reportTable = connectionFactory.GETDbGenDataTable(
                        reportQuery.Sql,
                        reportQuery.Parameters,
                        queryTimeoutSeconds,
                        queryTimeoutSource.Token);

                    foreach (DataRow reportRow in reportTable.Rows)
                    {
                        reports.Add(new
                        {
                            groupRequestNumber,
                            requestNumber = individualRequestNumber,
                            reportStatus = GetInspectionValue(reportRow, "REPORTSTATUS"),
                            lastModifyDate = FormatInspectionDate(reportRow, "LASTMODIFYDATE"),
                            ownerUserId = GetInspectionValue(reportRow, "OWNERUSERID"),
                            content = System.Net.WebUtility.HtmlDecode(GetInspectionValue(reportRow, "AUTOSTATEMENT_NEW"))
                        });
                    }
                }

                return Json(new
                {
                    total = reports.Count,
                    rows = reports
                });
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                Console.WriteLine($"InspectionEcgReports request canceled at {queryStage} after {elapsed.ElapsedMilliseconds} ms.");
                return new EmptyResult();
            }
            catch (OracleException exception) when (exception.Number == 1013 && requestAborted.IsCancellationRequested)
            {
                Console.WriteLine($"InspectionEcgReports request canceled at {queryStage} after {elapsed.ElapsedMilliseconds} ms.");
                return new EmptyResult();
            }
            catch (OperationCanceledException) when (queryTimeoutSource.IsCancellationRequested)
            {
                Console.Error.WriteLine($"InspectionEcgReports timeout at {queryStage} after {elapsed.ElapsedMilliseconds} ms.");
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = $"查詢逾時 {queryTimeoutSeconds} 秒，請稍後再試。" });
            }
            catch (OracleException exception) when (exception.Number == 1013)
            {
                Console.Error.WriteLine($"InspectionEcgReports command timeout at {queryStage} after {elapsed.ElapsedMilliseconds} ms: ORA-01013.");
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = $"查詢逾時 {queryTimeoutSeconds} 秒，請稍後再試。" });
            }
            catch (OracleException exception)
            {
                Console.Error.WriteLine($"InspectionEcgReports Oracle {exception.Number}: {exception.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "無法載入心電圖報告資料。" });
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult InspectionLabResults(
            string? chop1Date,
            string? chop1Time,
            string? chop1Room,
            string? intop1No,
            CancellationToken requestAborted)
        {
            const int queryTimeoutSeconds = 30;
            chop1Date = chop1Date?.Trim();
            chop1Time = chop1Time?.Trim();
            chop1Room = chop1Room?.Trim();

            if (string.IsNullOrWhiteSpace(chop1Date)
                || string.IsNullOrWhiteSpace(chop1Time)
                || string.IsNullOrWhiteSpace(chop1Room)
                || !int.TryParse(intop1No, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIntop1No))
            {
                return BadRequest(new { message = "選取的檢查報告缺少 CHOP1DATE、CHOP1TIME、CHOP1ROOM 或 INTOP1NO。" });
            }

            using var queryTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            queryTimeoutSource.CancelAfter(TimeSpan.FromSeconds(queryTimeoutSeconds));
            var query = OracleSql.SELECT.BuildInspectionLabResultsQuery(
                chop1Date,
                chop1Time,
                chop1Room,
                parsedIntop1No);

            try
            {
                var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDbGenDataTable(
                    query.Sql,
                    query.Parameters,
                    queryTimeoutSeconds,
                    queryTimeoutSource.Token);
                var results = dataTable.Rows.Cast<DataRow>().Select(row =>
                {
                    var value = GetInspectionValue(row, "CHVAL");
                    var low = GetInspectionValue(row, "CHNL");
                    var high = GetInspectionValue(row, "CHNH");
                    var statusCode = GetInspectionValue(row, "CHSTAT");
                    var isCritical = GetInspectionValue(row, "IS_CRITICAL") == "1";

                    return new
                    {
                        receiptDate = FormatRocDateTime(GetInspectionValue(row, "CHRCPDTM"), includeTime: false),
                        reportDate = FormatRocDateTime(GetInspectionValue(row, "CHMDDTM"), includeTime: false),
                        labName = GetInspectionValue(row, "LAB_NAME"),
                        value,
                        referenceValue = FormatReferenceValue(low, high),
                        statusName = GetInspectionValue(row, "CHSTAT_NAME"),
                        isCritical,
                        severity = isCritical ? "危急" : IsOutsideReferenceRange(value, low, high) ? "異常" : "正常",
                        isAbnormal = isCritical || IsOutsideReferenceRange(value, low, high),
                        isComplete = statusCode is "60" or "70"
                    };
                }).ToArray();

                return Json(new
                {
                    total = results.Length,
                    complete = results.Count(result => result.isComplete),
                    alert = results.Count(result => result.isAbnormal),
                    critical = results.Count(result => result.isCritical),
                    rows = results
                });
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OracleException) when (requestAborted.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OperationCanceledException) when (queryTimeoutSource.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = $"查詢逾時 {queryTimeoutSeconds} 秒，請稍後再試。" });
            }
            catch (OracleException exception)
            {
                Console.Error.WriteLine($"InspectionLabResults Oracle {exception.Number}: {exception.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "無法載入 Lab 檢驗資料。" });
            }
        }

        private static IReadOnlyList<InspectionFlowStepViewModel> BuildDemoFlowSteps(DataRow row)
        {
            string[] names = ["報到", "抽血檢查", "身體測量", "X 光檢查", "醫師問診", "報告總評"];
            var completed = int.TryParse(GetInspectionValue(row, "FLOW_COMPLETED_STEPS"), out var count) ? Math.Clamp(count, 0, names.Length) : 0;
            var started = GetInspectionValue(row, "FLOW_STARTED") == "1";
            return names.Select((name, index) => new InspectionFlowStepViewModel
            {
                Name = name,
                State = index < completed ? "done" : started && index == completed ? "active" : "pending",
                Time = index < completed ? DateTime.Today.AddHours(8).AddMinutes(index * 20).ToString("HH:mm", CultureInfo.InvariantCulture) : ""
            }).ToArray();
        }

        private static DateTime? ParseInspectionDate(string? value)
        {
            return DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate)
                ? parsedDate.Date
                : null;
        }

        private static string GetInspectionValue(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] is null || row[columnName] == DBNull.Value)
            {
                return string.Empty;
            }

            return Convert.ToString(row[columnName], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        }

        private static string DecodeFirstReportText(DataTable textTable, string itemName)
        {
            var textRow = textTable.Rows.Cast<DataRow>()
                .FirstOrDefault(row => string.Equals(
                    GetInspectionValue(row, "ITEMNAME"),
                    itemName,
                    StringComparison.Ordinal));
            return textRow is null
                ? string.Empty
                : System.Net.WebUtility.HtmlDecode(GetInspectionValue(textRow, "TEXTVALUE"));
        }

        private static string FormatInspectionDate(DataRow row, string columnName, bool convertRocDate = false)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] is null || row[columnName] == DBNull.Value)
            {
                return string.Empty;
            }

            if (row[columnName] is DateTime dateTime)
            {
                return dateTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            }

            var value = GetInspectionValue(row, columnName);
            return convertRocDate && !string.IsNullOrWhiteSpace(value)
                ? FormatRocDateTime(value, includeTime: false)
                : value;
        }

        private static string FormatRocDateTime(string value, bool includeTime)
        {
            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length < 7
                || !int.TryParse(digits[..3], NumberStyles.None, CultureInfo.InvariantCulture, out var rocYear)
                || !int.TryParse(digits.Substring(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
                || !int.TryParse(digits.Substring(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var day))
            {
                return string.IsNullOrWhiteSpace(value) ? "—" : value;
            }

            try
            {
                var date = new DateTime(rocYear + 1911, month, day);
                if (!includeTime || digits.Length < 11)
                {
                    return date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                }

                if (!int.TryParse(digits.Substring(7, 2), out var hour)
                    || !int.TryParse(digits.Substring(9, 2), out var minute)
                    || hour is < 0 or > 23
                    || minute is < 0 or > 59)
                {
                    return date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                }

                return date.AddHours(hour).AddMinutes(minute)
                    .ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return value;
            }
        }

        private static string FormatReferenceValue(string low, string high)
        {
            if (string.IsNullOrWhiteSpace(low) && string.IsNullOrWhiteSpace(high)) return "—";
            return $"{low}~{high}";
        }

        private static bool IsOutsideReferenceRange(string value, string low, string high)
        {
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue))
            {
                return false;
            }

            var belowLow = decimal.TryParse(low, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericLow)
                && numericValue < numericLow;
            var aboveHigh = decimal.TryParse(high, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericHigh)
                && numericValue > numericHigh;
            return belowLow || aboveHigh;
        }

        private static string ResolveInspectionStatusTone(string reserveStatus)
        {
            return reserveStatus switch
            {
                "4" => "progress",
                "5" => "done",
                _ => "waiting"
            };
        }

    }
}
