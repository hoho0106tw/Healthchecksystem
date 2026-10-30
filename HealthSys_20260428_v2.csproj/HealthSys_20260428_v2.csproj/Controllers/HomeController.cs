
using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace HealthSys_20260428_v2.csproj.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index()
        {
            return BuildDashboardIndex(null, null, null, null, null, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            [FromForm] string? reservationDateStart,
            [FromForm] string? reservationDateEnd,
            [FromForm] string? reservationDate,
            [FromForm] string? reserveStatus,
            [FromForm] string? workArea,
            [FromForm] string? keyword)
        {
            return BuildDashboardIndex(reservationDateStart, reservationDateEnd, reservationDate, reserveStatus, workArea, keyword);
        }

        private IActionResult BuildDashboardIndex(string? reservationDateStart, string? reservationDateEnd, string? reservationDate, string? reserveStatus, string? workArea, string? keyword)
        {
            var startDate = ParseInspectionDate(reservationDateStart) ?? ParseInspectionDate(reservationDate) ?? DateTime.Today;
            var endDate = ParseInspectionDate(reservationDateEnd) ?? startDate;
            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }
            var model = new DashboardViewModel
            {
                ReservationDateStart = startDate,
                ReservationDateEnd = endDate,
                WorkArea = workArea?.Trim() ?? "",
                Keyword = keyword?.Trim() ?? "",
                ReserveStatus = DashboardExaminee.NormalizeStatus(reserveStatus) is "0" or "1" or "2" or "4" or "5" or "6" ? DashboardExaminee.NormalizeStatus(reserveStatus) : ""
            };
            try
            {
                var table = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                    OracleSql.SELECT.GetDashboardExaminees(),
                    new Dictionary<string, object?>
                    {
                        ["RESERVEDATE_START"] = startDate,
                        ["RESERVEDATE_END"] = endDate.AddDays(1),
                        ["KEYWORD"] = string.IsNullOrEmpty(model.Keyword) ? DBNull.Value : model.Keyword.ToUpperInvariant(),
                        ["WORKAREA"] = string.IsNullOrEmpty(model.WorkArea) ? DBNull.Value : model.WorkArea
                    }, "", true, 30, HttpContext.RequestAborted);
                foreach (DataRow row in table.Rows)
                {
                    var rawStatus = GetInspectionValue(row, "ISRESERVE");
                    if (!string.IsNullOrEmpty(model.ReserveStatus)
                        && DashboardExaminee.NormalizeStatus(rawStatus) != model.ReserveStatus)
                    {
                        continue;
                    }
                    var age = "";
                    if (DateTime.TryParse(GetInspectionValue(row, "BIRTHDAY"), CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var birthday) && birthday.Date <= DateTime.Today)
                    {
                        var years = DateTime.Today.Year - birthday.Year;
                        if (birthday.Date.AddYears(years) > DateTime.Today) years--;
                        age = years.ToString(CultureInfo.InvariantCulture);
                    }
                    var gender = GetInspectionValue(row, "GENDER");
                    model.Examinees.Add(new DashboardExaminee
                    {
                        Barcode = GetInspectionValue(row, "BARCODESTR"),
                        PatientNumber = GetInspectionValue(row, "PATNO"),
                        Name = GetInspectionValue(row, "PATIENTNAME"),
                        IdNumber = GetInspectionValue(row, "IDNUMBER"),
                        Gender = gender switch { "M" or "1" => "男", "F" or "2" => "女", _ => gender },
                        Age = age,
                        RawStatus = rawStatus,
                        Station = GetInspectionValue(row, "WORKAREA"),
                        ReservationDate = GetInspectionValue(row, "RESERVEDATE"),
                        ReservationTime = GetInspectionValue(row, "RESERVETIME"),
                        CheckInDate = GetInspectionValue(row, "CHECKINDATE"),
                        CheckInTime = GetInspectionValue(row, "CHECKINTIME"),
                        BookingTime = GetInspectionValue(row, "RESERVEDATE") + " " + GetInspectionValue(row, "RESERVETIME")
                    });
                }
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OracleException exception)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<HomeController>>()
                    .LogError(exception, "Dashboard query failed for {StartDate} through {EndDate}", startDate, endDate);
                model.QueryError = $"受檢者清單查詢失敗（ORA-{exception.Number:D5}），請提供此代碼協助查明原因。";
            }
            try
            {
                var areas = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                    OracleSql.SELECT.GetDashboardWorkAreas(),
                    new Dictionary<string, object?>(), "", true, 30, HttpContext.RequestAborted);
                foreach (DataRow area in areas.Rows)
                {
                    model.WorkAreaOptions.Add(new SelectOptionViewModel
                    {
                        Value = GetInspectionValue(area, "AREA_CODE"),
                        Text = GetInspectionValue(area, "AREA_NAME")
                    });
                }
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OracleException exception)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<HomeController>>()
                    .LogError(exception, "Dashboard work area options query failed");
                model.WorkAreaError = $"工作區域載入失敗（ORA-{exception.Number:D5}）";
            }
            return View("Index", model);
        }

        public IActionResult CheckInManagement()
        {
            return RedirectToAction("TodayCheckIn", "Appointment");
        }

        public IActionResult AppointmentManagement()
        {
            return RedirectToAction("AppointmentManagement", "Appointment");
        }

        public IActionResult GroupAppointmentManagement()
        {
            return RedirectToAction("GroupAppointmentManagement", "Appointment");
        }

        public IActionResult CreateGroupAppointment()
        {
            return RedirectToAction("CreateGroupAppointment", "Appointment");
        }

        public IActionResult DownloadGroupRosterTemplate()
        {
            return RedirectToAction("DownloadGroupRosterTemplate", "Appointment");
        }

        public IActionResult CreateAppointment()
        {
            return RedirectToAction("CreateAppointment", "Appointment");
        }

        public IActionResult PatientLookupFrame(string? keyword, string? filter)
        {
            return RedirectToAction("PatientLookupFrame", "Appointment", new { keyword, filter });
        }

        public IActionResult AddRoleFrame()
        {
            return RedirectToAction("AddRoleFrame", "UserRole");
        }

        public IActionResult AddEmployeeFrame()
        {
            return RedirectToAction("AddEmployeeFrame", "UserRole");
        }

        public async Task<IActionResult> MyHealthPlusWebLink([FromQuery] string? patientId)
        {
            await Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(patientId)) return BadRequest("請選取示範受檢者。");
            return Content("<html lang='zh-Hant'><meta charset='utf-8'><body><h2>示範健康資料</h2><p>病歷號：" + HtmlEncoder.Default.Encode(patientId) + "</p><p>此為虛構健康資料，檢查結果無明顯異常。</p></body></html>", "text/html; charset=utf-8");
#if false // Original hospital integration retained for reference.

            if (string.IsNullOrWhiteSpace(patientId))
            {
                return BadRequest("請先選取具有病歷號的受檢者。");
            }

            var json = JsonSerializer.Serialize(new
            {
                v = 1,
                patientId = patientId.Trim(),
                employeeId = "98648"
            });
            var ticket = GeneratePatientProfileTicket(json);
            var profileUri = new Uri("https://webap2.femh.org.tw:9048/PatientProfile/health-profile?ticket="
                + Uri.EscapeDataString(ticket));
            var httpClient = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, profileUri);

            request.Headers.Accept.ParseAdd("text/html");

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                HttpContext.RequestAborted);
            var html = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

            Response.StatusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                return Content(html, GetHtmlContentType(response));
            }

            return Content(PrepareIframeHtml(html, profileUri), GetHtmlContentType(response));
        
#endif
    }

        public IActionResult HealthCheckProcess()
        {
            return View();
        }

        public IActionResult SystemSettings()
        {
            return RedirectToAction("SystemSettings", "UserRole");
        }

        public IActionResult BasicDataMaintenance()
        {
            return RedirectToAction("BasicDataMaintenance", "BasicDataMaintenance");
        }

        [Route("Home/Error/{statusCode?}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(
            int? statusCode = null,
            string? title = null,
            string? reason = null,
            string? detail = null,
            string? requestPath = null)
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var statusCodeFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var resolvedStatusCode = statusCode ?? HttpContext.Response.StatusCode;

            if (resolvedStatusCode < 400)
            {
                resolvedStatusCode = 500;
            }

            HttpContext.Response.StatusCode = resolvedStatusCode;

            var model = new ErrorViewModel
            {
                StatusCode = resolvedStatusCode,
                Title = string.IsNullOrWhiteSpace(title) ? GetErrorTitle(resolvedStatusCode) : title.Trim(),
                Reason = string.IsNullOrWhiteSpace(reason) ? GetErrorReason(resolvedStatusCode, exceptionFeature?.Error) : reason.Trim(),
                Detail = string.IsNullOrWhiteSpace(detail) ? exceptionFeature?.Error.Message ?? "" : detail.Trim(),
                RequestPath = string.IsNullOrWhiteSpace(requestPath)
                    ? exceptionFeature?.Path ?? statusCodeFeature?.OriginalPath ?? HttpContext.Request.Path.ToString()
                    : requestPath.Trim(),
                RequestId = HttpContext.TraceIdentifier,
                OccurredAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture)
            };

            return View(model);
        }

        private static string GetErrorTitle(int statusCode)
        {
            return statusCode switch
            {
                400 => "請求內容有誤",
                401 => "需要登入或授權",
                403 => "沒有權限存取",
                404 => "找不到頁面",
                408 => "請求逾時",
                500 => "系統發生錯誤",
                502 => "上游服務暫時無法回應",
                503 => "服務暫時不可用",
                _ => statusCode >= 500 ? "系統發生錯誤" : "無法完成這次請求"
            };
        }

        private static string GetErrorReason(int statusCode, Exception? exception)
        {
            if (exception is not null)
            {
                return exception.Message;
            }

            return statusCode switch
            {
                400 => "系統無法理解目前送出的資料，請確認輸入內容後再試一次。",
                401 => "目前工作階段尚未完成身分驗證，請重新登入後再操作。",
                403 => "此帳號沒有執行此操作或檢視此頁面的權限。",
                404 => "網址不存在，或頁面已被移除、重新命名。",
                408 => "系統等待回應的時間過長，請稍後重新送出。",
                500 => "伺服器處理請求時發生未預期錯誤。",
                502 => "系統呼叫外部服務或資料來源時沒有取得有效回應。",
                503 => "服務可能正在維護或暫時忙碌，請稍後再試。",
                _ => "目前請求無法完成，請記錄錯誤代碼後通知系統管理員。"
            };
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

        private static string GeneratePatientProfileTicket(string json)
        {
            var securityLibraryType = GetSecurityLibraryType();
            var securityLibrary = Activator.CreateInstance(securityLibraryType);
            // GenerateTicket uses .NET Framework's JavaScriptSerializer, which is unavailable
            // on .NET 10. Preserve its UTC /Date(milliseconds)/ envelope and use the same
            // DLL encoder so the receiving system can still verify the original format.
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            var envelope = "{\"Datetime\":\"\\/Date(" + timestamp + ")\\/\",\"Name\":\"PatientProfile\",\"Content\":"
                + JsonSerializer.Serialize(json) + "}";
            var encodeString = securityLibraryType.GetMethod(
                "EncodeString",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string)],
                modifiers: null);

            if (encodeString is null)
            {
                throw new MissingMethodException("FemhSecurityLibrary.SecurityLibrary", "EncodeString");
            }

            string? ticket;
            try
            {
                ticket = encodeString.Invoke(securityLibrary, [envelope])?.ToString();
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw new InvalidOperationException("My Health+ ticket 加密失敗。", exception.InnerException);
            }

            if (string.IsNullOrWhiteSpace(ticket))
            {
                throw new InvalidOperationException("FemhSecurityLibrary.SecurityLibrary.EncodeString 未回傳 ticket。");
            }

            return ticket;
        }

        private static string AddBaseHref(string html, Uri pageUri)
        {
            if (html.Contains("<base", StringComparison.OrdinalIgnoreCase))
            {
                return html;
            }

            var baseElement = "<base href=\"" + HtmlEncoder.Default.Encode(pageUri.ToString()) + "\">";

            return InsertIntoHead(html, baseElement);
        }

        private static string PrepareIframeHtml(string html, Uri pageUri)
        {
            return AddIframeScrollStyle(AddBaseHref(html, pageUri));
        }

        private static string AddIframeScrollStyle(string html)
        {
            const string styleElement = "<style id=\"my-health-iframe-scroll-style\">"
                + "html{overflow-y:scroll!important;overflow-x:hidden!important;height:auto!important;min-height:100%!important;}"
                + "body{overflow-y:visible!important;overflow-x:hidden!important;height:auto!important;min-height:100%!important;}"
                + "</style>";

            if (html.Contains("my-health-iframe-scroll-style", StringComparison.OrdinalIgnoreCase))
            {
                return html;
            }

            return InsertIntoHead(html, styleElement);
        }

        private static string InsertIntoHead(string html, string element)
        {
            var headStart = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);

            if (headStart < 0)
            {
                return element + html;
            }

            var headEnd = html.IndexOf('>', headStart);

            return headEnd < 0 ? element + html : html.Insert(headEnd + 1, element);
        }

        private static string GetHtmlContentType(HttpResponseMessage response)
        {
            var contentType = response.Content.Headers.ContentType?.ToString();

            return string.IsNullOrWhiteSpace(contentType) ? "text/html; charset=utf-8" : contentType;
        }

        private static Type GetSecurityLibraryType()
        {
            var loadedType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("FemhSecurityLibrary.SecurityLibrary", throwOnError: false))
                .FirstOrDefault(type => type is not null);

            if (loadedType is not null)
            {
                return loadedType;
            }

            try
            {
                return Assembly.Load("FemhSecurityLibrary")
                    .GetType("FemhSecurityLibrary.SecurityLibrary", throwOnError: true)!;
            }
            catch (FileNotFoundException)
            {
            }

            var candidatePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "FemhSecurityLibrary.dll")
            };

            foreach (var candidatePath in candidatePaths)
            {
                if (!System.IO.File.Exists(candidatePath))
                {
                    continue;
                }

                return Assembly.LoadFrom(candidatePath)
                    .GetType("FemhSecurityLibrary.SecurityLibrary", throwOnError: true)!;
            }

            throw new FileNotFoundException("找不到 FemhSecurityLibrary.dll，無法產生 My Health+ ticket。");
        }
    }
}
