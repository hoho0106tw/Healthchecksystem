using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using HealthSys_20260428_v2.csproj.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Globalization;
using System.Linq;


namespace HealthSys_20260428_v2.csproj.Controllers
{
    public class AppointmentController : Controller
    {
        private const string GroupRosterTemplateFileName = "團體匯入名單格式.xlsx";
        private const int AppointmentQueryTimeoutSeconds = 30;
        private const string PersistedPackageIdPrefix = "__persisted_package__";
        private const string PersistedQuestionnaireIdPrefix = "__persisted_questionnaire__";
        private const string DefaultSelfPayAccountingUnit = "00";
        private const string DefaultConsultingDoctorId = "D001";
        private const string DefaultConsultingDoctorName = "示範醫師甲";
        private const string GroupRosterCacheKeyPrefix = "group-roster-import:";
        private static readonly TimeSpan GroupRosterCacheSlidingExpiration = TimeSpan.FromHours(2);
        private static readonly TimeSpan GroupRosterCacheAbsoluteExpiration = TimeSpan.FromHours(8);
        private readonly IWebHostEnvironment _environment;
        private readonly IMemoryCache _cache;
        private readonly IOpdOrderService _opdOrderService;
        private readonly IAppointmentRegistrationService _appointmentRegistrationService;
        private readonly IOPDBasicTbl _opdBasicTbl;

        public AppointmentController(
            IWebHostEnvironment environment,
            IMemoryCache cache,
            IOpdOrderService opdOrderService,
            IAppointmentRegistrationService appointmentRegistrationService,
            IOPDBasicTbl opdBasicTbl)
        {
            _environment = environment;
            _cache = cache;
            _opdOrderService = opdOrderService;
            _appointmentRegistrationService = appointmentRegistrationService;
            _opdBasicTbl = opdBasicTbl;
        }
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult AppointmentManagement(
            string? startDate,
            string? endDate,
            string? checkDateStart,
            string? checkDateEnd,
            string? reservationArea,
            string? workArea,
            string? reserveStatus,
            string? appointmentStatus,
            string? keyword,
            string? barcode,
            string? healthCode,
            string? projectNo,
            string? projectNumber,
            string? customerName,
            string? company,
            CancellationToken requestAborted)
        {
            using var queryTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            queryTimeoutSource.CancelAfter(TimeSpan.FromSeconds(AppointmentQueryTimeoutSeconds));
            var queryCancellationToken = queryTimeoutSource.Token;
            var hasExplicitDateQuery = Request.Query.ContainsKey("startDate")
                || Request.Query.ContainsKey("endDate")
                || Request.Query.ContainsKey("checkDateStart")
                || Request.Query.ContainsKey("checkDateEnd");
            var selectedStartDate = ParseAppointmentQueryDate(FirstNonEmpty(checkDateStart, startDate));
            var selectedEndDate = ParseAppointmentQueryDate(FirstNonEmpty(checkDateEnd, endDate));
            var selectedArea = NormalizeText(FirstNonEmpty(reservationArea, workArea));
            var selectedReserveStatus = NormalizeAppointmentQueryReserveStatus(FirstNonEmpty(reserveStatus, appointmentStatus));
            var filterKeyword = NormalizeText(FirstNonEmpty(keyword, barcode, healthCode, projectNo, projectNumber, customerName, company));
            var hasFilterQuery = hasExplicitDateQuery
                || !string.IsNullOrWhiteSpace(selectedArea)
                || !string.IsNullOrWhiteSpace(selectedReserveStatus)
                || !string.IsNullOrWhiteSpace(filterKeyword);

            if (!hasFilterQuery)
            {
                selectedStartDate = DateTime.Today;
                selectedEndDate = DateTime.Today;
            }

            if (selectedStartDate.HasValue && selectedEndDate.HasValue && selectedEndDate < selectedStartDate)
            {
                (selectedStartDate, selectedEndDate) = (selectedEndDate, selectedStartDate);
            }

            var appointmentQuery = OracleSql.SELECT.BuildAppointmentManagementQuery(
                selectedStartDate,
                selectedEndDate,
                selectedArea,
                selectedReserveStatus,
                filterKeyword);
            AppointmentListItemViewModel[] appointments;
            var queryErrorMessage = string.Empty;

            try
            {
                var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                    appointmentQuery.Sql,
                    appointmentQuery.Parameters,
                    "",
                    true,
                    AppointmentQueryTimeoutSeconds,
                    queryCancellationToken);
                appointments = dataTable.Rows
                    .Cast<DataRow>()
                    .Select((row, index) =>
                    {
                        queryCancellationToken.ThrowIfCancellationRequested();

                        return new AppointmentListItemViewModel
                        {
                            Selected = index == 0,
                            AppointmentDate = GetDateString(row, "RESERVEDATE"),
                            Time = GetTimeString(row, "RESERVETIME"),
                            WorkArea = GetString(row, "WORKAREA"),
                            Type = GetString(row, "CATEGORY"),
                            DoctorName = GetString(row, "DOCTORNAME"),
                            DoctorId = GetString(row, "DOCTOREMPNO"),
                            RoomNo = GetString(row, "ROOMNO"),
                            ProjectNumber = GetString(row, "PROJECTNO"),
                            SelfPayAccountingUnit = GetString(row, "SELFCOSTNO"),
                            Barcode = GetString(row, "BARCODESTR"),
                            PatNo = GetString(row, "PATNO"),
                            IdNumber = GetString(row, "IDNUMBER"),
                            Gender = GetString(row, "GENDER"),
                            Name = GetString(row, "PATIENTNAME"),
                            Company = GetString(row, "CUSTOMERNAME"),
                            Plan = GetString(row, "PROJECTNAME"),
                            AppointmentStatus = ResolveAppointmentStatus(GetString(row, "ISRESERVE")),
                            AppointmentTone = ResolveAppointmentTone(GetString(row, "ISRESERVE")),
                            ReserveStatus = NormalizeReserveStatus(GetString(row, "ISRESERVE")),
                            Phone = GetString(row, "MOBILE"),
                            HasSelectedAppointmentItem = GetString(row, "HAS_SELECTED_APPOINTMENT_ITEM") == "1",
                            HasMissingAccountingUnit = GetString(row, "HAS_MISSING_ACCOUNTING_UNIT") == "1"
                        };
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
            catch (OperationCanceledException) when (queryCancellationToken.IsCancellationRequested)
            {
                appointments = [];
                queryErrorMessage = $"查詢超過 {AppointmentQueryTimeoutSeconds} 秒，已自動逾時，請縮小查詢範圍後再試。";
                Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }
            catch (OracleException exception) when (exception.Number == 1013)
            {
                appointments = [];
                queryErrorMessage = $"查詢超過 {AppointmentQueryTimeoutSeconds} 秒，已自動逾時，請縮小查詢範圍後再試。";
                Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }

            IReadOnlyList<SelectOptionViewModel> reservationAreaOptions =
                [
                    new SelectOptionViewModel { Value = "", Text = "全部工作區域", IsSelected = string.IsNullOrWhiteSpace(selectedArea) }
                ];

            if (string.IsNullOrEmpty(queryErrorMessage))
            {
                try
                {
                    reservationAreaOptions = GetDashboardReservationAreaOptions(queryCancellationToken);
                }
                catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
                {
                    return new EmptyResult();
                }
                catch (OracleException) when (requestAborted.IsCancellationRequested)
                {
                    return new EmptyResult();
                }
                catch (OperationCanceledException) when (queryCancellationToken.IsCancellationRequested)
                {
                    appointments = [];
                    queryErrorMessage = $"查詢超過 {AppointmentQueryTimeoutSeconds} 秒，已自動逾時，請縮小查詢範圍後再試。";
                    Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                }
                catch (OracleException exception) when (exception.Number == 1013)
                {
                    appointments = [];
                    queryErrorMessage = $"查詢超過 {AppointmentQueryTimeoutSeconds} 秒，已自動逾時，請縮小查詢範圍後再試。";
                    Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                }
            }

            var model = new AppointmentManagementViewModel
            {
                Appointments = appointments,
                SelectedAppointment = appointments.FirstOrDefault(appointment => appointment.Selected) ?? appointments.FirstOrDefault(),
                PageDateText = FormatTaiwanDate(DateTime.Today),
                FilterStartDate = FormatAppointmentQueryDate(selectedStartDate),
                FilterEndDate = FormatAppointmentQueryDate(selectedEndDate),
                FilterKeyword = filterKeyword,
                QueryErrorMessage = queryErrorMessage,
                ReservationAreaOptions = WithSelectedOption(reservationAreaOptions, selectedArea),
                AppointmentStatusOptions = BuildAppointmentStatusFilterOptions(selectedReserveStatus),
                PlanOptions =
                [
                    new SelectOptionViewModel { Value = "", Text = "全部方案", IsSelected = true }
                ],
                SummaryCards = BuildSummaryCards(appointments),
                StatusSummaryCards = BuildStatusSummaryCards(appointments),
                CapacitySlots = BuildCapacitySlots(),
                Reminders = BuildReminders(),
                PageSizeOptions = [10, 20, 30, 40, 50]
            };

            return View(model);
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult TodayCheckIn(
            string? reservationArea,
            string? reserveStatus,
            string? keyword,
            CancellationToken requestAborted)
        {
            using var queryTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            queryTimeoutSource.CancelAfter(TimeSpan.FromSeconds(AppointmentQueryTimeoutSeconds));
            var selectedArea = NormalizeText(reservationArea);
            var normalizedReserveStatus = NormalizeReserveStatus(reserveStatus ?? "");
            var selectedReserveStatus = normalizedReserveStatus is "4" or "5"
                ? normalizedReserveStatus
                : string.Empty;
            var normalizedKeyword = NormalizeText(keyword);
            var query = OracleSql.SELECT.BuildTodayCheckInQuery(
                selectedArea,
                selectedReserveStatus,
                normalizedKeyword);
            AppointmentListItemViewModel[] appointments;
            var queryErrorMessage = string.Empty;
            IReadOnlyList<SelectOptionViewModel> reservationAreaOptions =
            [
                new SelectOptionViewModel { Value = "", Text = "全部區域", IsSelected = string.IsNullOrWhiteSpace(selectedArea) }
            ];

            try
            {
                var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                    query.Sql,
                    query.Parameters,
                    "",
                    true,
                    AppointmentQueryTimeoutSeconds,
                    queryTimeoutSource.Token);
                appointments = dataTable.Rows
                    .Cast<DataRow>()
                    .Select((row, index) => new AppointmentListItemViewModel
                    {
                        Selected = index == 0,
                        AppointmentDate = GetDateString(row, "RESERVEDATE"),
                        Time = GetTimeString(row, "RESERVETIME"),
                        CheckInDate = GetString(row, "CHECKINDATE"),
                        CheckInTime = GetString(row, "CHECKINTIME"),
                        WorkArea = GetString(row, "WORKAREA"),
                        Type = GetString(row, "CATEGORY"),
                        DoctorName = GetString(row, "DOCTORNAME"),
                        DoctorId = GetString(row, "DOCTOREMPNO"),
                        RoomNo = GetString(row, "ROOMNO"),
                        ProjectNumber = GetString(row, "PROJECTNO"),
                        SelfPayAccountingUnit = GetString(row, "SELFCOSTNO"),
                        Barcode = GetString(row, "BARCODESTR"),
                        PatNo = GetString(row, "PATNO"),
                        IdNumber = GetString(row, "IDNUMBER"),
                        Name = GetString(row, "PATIENTNAME"),
                        Company = GetString(row, "CUSTOMERNAME"),
                        Plan = GetString(row, "PROJECTNAME"),
                        AppointmentStatus = ResolveAppointmentStatus(GetString(row, "ISRESERVE")),
                        AppointmentTone = ResolveAppointmentTone(GetString(row, "ISRESERVE")),
                        ReserveStatus = NormalizeReserveStatus(GetString(row, "ISRESERVE")),
                        Phone = FirstNonEmpty(GetString(row, "MOBILE"), GetString(row, "TEL"))
                    })
                    .ToArray();
                reservationAreaOptions = GetReservationAreaFilterOptions(queryTimeoutSource.Token);
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
                appointments = [];
                queryErrorMessage = $"查詢超過 {AppointmentQueryTimeoutSeconds} 秒，已自動逾時，請稍後再試。";
                Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }
            catch (OracleException exception) when (exception.Number == 1013)
            {
                appointments = [];
                queryErrorMessage = $"查詢超過 {AppointmentQueryTimeoutSeconds} 秒，已自動逾時，請稍後再試。";
                Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }

            ViewData["IsTodayCheckIn"] = true;
            return View("CheckInOperation", new AppointmentManagementViewModel
            {
                Appointments = appointments,
                SelectedAppointment = appointments.FirstOrDefault(),
                PageDateText = FormatTaiwanDate(DateTime.Today),
                FilterKeyword = normalizedKeyword,
                QueryErrorMessage = queryErrorMessage,
                ReservationAreaOptions = WithSelectedOption(reservationAreaOptions, selectedArea),
                AppointmentStatusOptions =
                [
                    new SelectOptionViewModel { Value = "", Text = "全部", IsSelected = string.IsNullOrWhiteSpace(selectedReserveStatus) },
                    new SelectOptionViewModel { Value = "4", Text = "已掛號", IsSelected = selectedReserveStatus == "4" },
                    new SelectOptionViewModel { Value = "5", Text = "已報到", IsSelected = selectedReserveStatus == "5" }
                ],
                PageSizeOptions = [10, 20, 30, 40, 50]
            });
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CheckInOperation(
            string? startDate,
            string? endDate,
            string? reservationArea,
            string? reserveStatus,
            string? keyword,
            CancellationToken requestAborted)
        {
            using var queryTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            queryTimeoutSource.CancelAfter(TimeSpan.FromSeconds(AppointmentQueryTimeoutSeconds));
            var defaultStartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var defaultEndDate = defaultStartDate.AddMonths(1).AddDays(-1);
            var selectedStartDate = ParseAppointmentQueryDate(startDate) ?? defaultStartDate;
            var selectedEndDate = ParseAppointmentQueryDate(endDate) ?? defaultEndDate;
            var selectedArea = NormalizeText(reservationArea);
            var normalizedReserveStatus = NormalizeReserveStatus(reserveStatus ?? "");
            var selectedReserveStatus = normalizedReserveStatus is "4" or "5"
                ? normalizedReserveStatus
                : string.Empty;
            var normalizedKeyword = NormalizeText(keyword);

            if (selectedEndDate < selectedStartDate)
            {
                (selectedStartDate, selectedEndDate) = (selectedEndDate, selectedStartDate);
            }

            var query = OracleSql.SELECT.BuildCheckInOperationQuery(
                selectedStartDate,
                selectedEndDate,
                selectedArea,
                selectedReserveStatus,
                normalizedKeyword);
            AppointmentListItemViewModel[] appointments;
            var queryErrorMessage = string.Empty;
            IReadOnlyList<SelectOptionViewModel> reservationAreaOptions =
            [
                new SelectOptionViewModel { Value = "", Text = "全部區域", IsSelected = string.IsNullOrWhiteSpace(selectedArea) }
            ];

            try
            {
                var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                    query.Sql,
                    query.Parameters,
                    "",
                    true,
                    AppointmentQueryTimeoutSeconds,
                    queryTimeoutSource.Token);
                appointments = dataTable.Rows
                    .Cast<DataRow>()
                    .Select((row, index) => new AppointmentListItemViewModel
                    {
                        Selected = index == 0,
                        AppointmentDate = GetDateString(row, "RESERVEDATE"),
                        Time = GetTimeString(row, "RESERVETIME"),
                        CheckInDate = GetString(row, "CHECKINDATE"),
                        CheckInTime = GetString(row, "CHECKINTIME"),
                        WorkArea = GetString(row, "WORKAREA"),
                        Type = GetString(row, "CATEGORY"),
                        DoctorName = GetString(row, "DOCTORNAME"),
                        DoctorId = GetString(row, "DOCTOREMPNO"),
                        RoomNo = GetString(row, "ROOMNO"),
                        ProjectNumber = GetString(row, "PROJECTNO"),
                        SelfPayAccountingUnit = GetString(row, "SELFCOSTNO"),
                        Barcode = GetString(row, "BARCODESTR"),
                        PatNo = GetString(row, "PATNO"),
                        IdNumber = GetString(row, "IDNUMBER"),
                        Name = GetString(row, "PATIENTNAME"),
                        Company = GetString(row, "CUSTOMERNAME"),
                        Plan = GetString(row, "PROJECTNAME"),
                        AppointmentStatus = ResolveAppointmentStatus(GetString(row, "ISRESERVE")),
                        AppointmentTone = ResolveAppointmentTone(GetString(row, "ISRESERVE")),
                        ReserveStatus = NormalizeReserveStatus(GetString(row, "ISRESERVE")),
                        Phone = FirstNonEmpty(GetString(row, "MOBILE"), GetString(row, "TEL"))
                    })
                    .ToArray();
                reservationAreaOptions = GetReservationAreaFilterOptions(queryTimeoutSource.Token);
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
                appointments = [];
                queryErrorMessage = $"查詢超過 {AppointmentQueryTimeoutSeconds} 秒，已自動逾時，請縮小查詢條件後再試。";
                Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }
            catch (OracleException exception) when (exception.Number == 1013)
            {
                appointments = [];
                queryErrorMessage = $"查詢超過 {AppointmentQueryTimeoutSeconds} 秒，已自動逾時，請縮小查詢條件後再試。";
                Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }

            return View(new AppointmentManagementViewModel
            {
                Appointments = appointments,
                SelectedAppointment = appointments.FirstOrDefault(),
                PageDateText = FormatTaiwanDate(DateTime.Today),
                FilterStartDate = FormatAppointmentQueryDate(selectedStartDate),
                FilterEndDate = FormatAppointmentQueryDate(selectedEndDate),
                FilterKeyword = normalizedKeyword,
                QueryErrorMessage = queryErrorMessage,
                ReservationAreaOptions = WithSelectedOption(reservationAreaOptions, selectedArea),
                AppointmentStatusOptions =
                [
                    new SelectOptionViewModel { Value = "", Text = "全部", IsSelected = string.IsNullOrWhiteSpace(selectedReserveStatus) },
                    new SelectOptionViewModel { Value = "4", Text = "已掛號", IsSelected = selectedReserveStatus == "4" },
                    new SelectOptionViewModel { Value = "5", Text = "已報到", IsSelected = selectedReserveStatus == "5" }
                ],
                PageSizeOptions = [10, 20, 30, 40, 50]
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckInAppointment([FromForm] AppointmentCheckInPostModel input)
        {
            if (!input.HasRequiredKey)
            {
                return BadRequest(new
                {
                    updated = false,
                    message = "缺少 Barcode 或專案編號，無法完成報到。"
                });
            }

            try
            {
                var updatedCount = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQuery(
                    OracleSql.UPDATE.CheckInRegisteredAppointment(),
                    new Dictionary<string, object?>
                    {
                        ["BARCODESTR"] = input.NormalizedBarcode,
                        ["PROJECTNO"] = input.NormalizedProjectNumber
                    },
                    "",
                    true);

                if (updatedCount < 1)
                {
                    return StatusCode(StatusCodes.Status409Conflict, new
                    {
                        updated = false,
                        updatedCount,
                        message = "此筆資料已不是已掛號狀態，無法執行報到。"
                    });
                }

                return Json(new
                {
                    updated = true,
                    updatedCount,
                    reserveStatus = "5",
                    status = "已報到",
                    message = "報到完成。"
                });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    updated = false,
                    message = "報到資料更新失敗，請稍後再試。"
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelCheckInAppointment([FromForm] AppointmentCheckInPostModel input)
        {
            if (!input.HasRequiredKey)
            {
                return BadRequest(new
                {
                    updated = false,
                    message = "缺少 Barcode 或專案編號，無法取消報到。"
                });
            }

            try
            {
                var updatedCount = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQuery(
                    OracleSql.UPDATE.CancelCheckedInAppointment(),
                    new Dictionary<string, object?>
                    {
                        ["BARCODESTR"] = input.NormalizedBarcode,
                        ["PROJECTNO"] = input.NormalizedProjectNumber
                    },
                    "",
                    true);

                if (updatedCount < 1)
                {
                    return StatusCode(StatusCodes.Status409Conflict, new
                    {
                        updated = false,
                        updatedCount,
                        message = "此筆資料已不是已報到狀態，無法取消報到。"
                    });
                }

                return Json(new
                {
                    updated = true,
                    updatedCount,
                    reserveStatus = "4",
                    status = "已掛號",
                    message = "已取消報到。"
                });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    updated = false,
                    message = "取消報到更新失敗，請稍後再試。"
                });
            }
        }

        private static string NormalizeAppointmentQueryReserveStatus(string? reserveStatus)
        {
            var normalizedReserveStatus = NormalizeReserveStatus(reserveStatus ?? "");

            return normalizedReserveStatus is "0" or "1" or "2" or "4" or "5" or "6"
                ? normalizedReserveStatus
                : string.Empty;
        }

        private static DateTime? ParseAppointmentQueryDate(string? value) =>
            ParseGroupQueryDate(value);

        private static string FormatAppointmentQueryDate(DateTime? value) =>
            value.HasValue
                ? value.Value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
                : string.Empty;

        private static IReadOnlyList<SelectOptionViewModel> BuildAppointmentStatusFilterOptions(string selectedReserveStatus) =>
            WithSelectedOption(
            [
                new SelectOptionViewModel { Value = "", Text = "全部狀態", IsSelected = true },
                new SelectOptionViewModel { Value = "0", Text = "已取消" },
                new SelectOptionViewModel { Value = "1", Text = "已預約" },
                new SelectOptionViewModel { Value = "2", Text = "佔名額" },
                new SelectOptionViewModel { Value = "4", Text = "已掛號" },
                new SelectOptionViewModel { Value = "5", Text = "已報到" },
                new SelectOptionViewModel { Value = "6", Text = "檢查中" }
            ], selectedReserveStatus);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelAppointment([FromForm] AppointmentCancelPostModel input)
        {
            if (!input.HasRequiredBarcode)
            {
                return BadRequest(new { updated = false, message = "缺少 Barcode 編號，無法更新預約狀態。" });
            }

            if (!input.HasValidReserveStatus)
            {
                return BadRequest(new { updated = false, message = "預約狀態只能是已取消、已預約、佔名額。" });
            }

            try
            {
                var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();
                var keyParameters = new Dictionary<string, object?>
                {
                    ["BARCODESTR"] = DbString(input.NormalizedBarcode),
                    ["PROJECTNO"] = DbString(input.NormalizedProjectNumber)
                };

                if (input.NormalizedReserveStatus == "1")
                {
                    var missingFields = new List<string>();

                    if (string.IsNullOrWhiteSpace(input.NormalizedProjectNumber))
                    {
                        missingFields.Add("健檢專案");
                    }
                    else
                    {
                        var appointmentTable = connectionFactory.GETDataTable(
                            OracleSql.SELECT.GetHealthCheckupListByBarcodeProject(),
                            keyParameters,
                            "",
                            true);

                        if (appointmentTable.Rows.Count == 0)
                        {
                            return NotFound(new { updated = false, message = "找不到要更新的預約資料。" });
                        }

                        var appointmentRow = appointmentTable.Rows[0];
                        var requiredColumns = new (string Column, string Label)[]
                        {
                            ("PATNO", "病歷號"),
                            ("PATIENTNAME", "姓名"),
                            ("IDNUMBER", "身分證 / 居留證號"),
                            ("GENDER", "性別"),
                            ("MOBILE", "手機"),
                            ("WORKAREA", "預約區域"),
                            ("RESERVEDATE", "預約日期"),
                            ("RESERVETIME", "預約時間"),
                            ("DOCTOREMPNO", "看診醫師"),
                            ("ROOMNO", "診間號"),
                            ("PROJECTNO", "健檢專案")
                        };

                        foreach (var requiredColumn in requiredColumns)
                        {
                            if (appointmentRow.IsNull(requiredColumn.Column)
                                || string.IsNullOrWhiteSpace(Convert.ToString(
                                    appointmentRow[requiredColumn.Column],
                                    CultureInfo.InvariantCulture)))
                            {
                                missingFields.Add(requiredColumn.Label);
                            }
                        }

                        var packageTable = connectionFactory.GETDataTable(
                            OracleSql.SELECT.GetHealthCheckupSetInfoByBarcodeProject(),
                            keyParameters,
                            "",
                            true);
                        var otherSuiteTable = connectionFactory.GETDataTable(
                            OracleSql.SELECT.GetHealthCheckupOtherSetByBarcodeProject(),
                            keyParameters,
                            "",
                            true);
                        var selectedItemRows = packageTable.Rows.Cast<DataRow>()
                            .Concat(otherSuiteTable.Rows.Cast<DataRow>())
                            .ToArray();

                        if (selectedItemRows.Length == 0)
                        {
                            missingFields.Add("套餐 / 套組");
                        }
                        else if (selectedItemRows.Any(row => string.IsNullOrWhiteSpace(GetString(row, "CHDCTTYPE"))))
                        {
                            missingFields.Add("優待身分");
                        }
                    }

                    if (missingFields.Count > 0)
                    {
                        return BadRequest(new
                        {
                            updated = false,
                            rejected = true,
                            title = "拒絕預約",
                            message = "必要欄位尚未填寫完整，無法將狀態改為已預約。",
                            missingFields = missingFields.Distinct(StringComparer.Ordinal).ToArray()
                        });
                    }
                }

                if (input.NormalizedCurrentReserveStatus == "4"
                    && input.NormalizedReserveStatus == "1")
                {
                    var registrationTable = connectionFactory.GETDataTable(
                        OracleSql.SELECT.GetFEMHCheckupRegistrationByBarcodeProject(),
                        keyParameters,
                        "",
                        true);
                    var registrationRows = registrationTable.Rows.Cast<DataRow>().ToArray();

                    if (registrationRows.Length == 0)
                    {
                        return NotFound(new
                        {
                            updated = false,
                            message = "查無掛號資料，無法取消掛號。"
                        });
                    }

                    var incompleteRegistration = registrationRows.FirstOrDefault(registrationRow =>
                        string.IsNullOrWhiteSpace(GetString(registrationRow, "INTOP1NO"))
                        || string.IsNullOrWhiteSpace(GetString(registrationRow, "CHOP1TIME"))
                        || string.IsNullOrWhiteSpace(GetString(registrationRow, "CHOP1DATE"))
                        || string.IsNullOrWhiteSpace(GetString(registrationRow, "CHOP1ROOM")));

                    if (incompleteRegistration is not null)
                    {
                        return Conflict(new
                        {
                            updated = false,
                            message = "掛號資料不完整，無法取消掛號。"
                        });
                    }

                    foreach (var registrationRow in registrationRows)
                    {
                        var regNo = GetString(registrationRow, "INTOP1NO");
                        var dcResult = _appointmentRegistrationService.CancelRegistration(
                            regNo,
                            GetString(registrationRow, "CHOP1DATE"),
                            GetString(registrationRow, "CHOP1TIME"),
                            GetString(registrationRow, "CHOP1ROOM"));

                        // DcRegNo 回傳 1 代表取消掛號成功。
                        if (dcResult != 1)
                        {
                            return Conflict(new
                            {
                                updated = false,
                                registrationNo = regNo,
                                resultCode = dcResult,
                                message = $"就診號 {regNo} 取消掛號失敗（回傳值：{dcResult}），預約狀態未變更。"
                            });
                        }
                    }

                    var cancellationWriteCounts = connectionFactory.ExecuteNonQueryTransaction(
                        [
                            (
                                OracleSql.UPDATE.UpdateHealthCheckupListReserveStatus(),
                                (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(keyParameters)
                                {
                                    ["ISRESERVE"] = "1"
                                }),
                            (
                                OracleSql.UPDATE.UpdateFEMHCheckupCancellationStatus(),
                                keyParameters)
                        ],
                        "",
                        true);
                    var appointmentUpdatedCount = cancellationWriteCounts.ElementAtOrDefault(0);
                    var registrationUpdatedCount = cancellationWriteCounts.ElementAtOrDefault(1);

                    if (appointmentUpdatedCount < 1 || registrationUpdatedCount != registrationRows.Length)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, new
                        {
                            updated = false,
                            appointmentUpdatedCount,
                            registrationUpdatedCount,
                            message = "取消掛號成功，但更新已預約狀態或 CHOP0DC 失敗。"
                        });
                    }

                    return Json(CreateAppointmentCancelResult(input, appointmentUpdatedCount, isMock: false));
                }

                var updatedCount = connectionFactory.ExecuteNonQuery(
                    OracleSql.UPDATE.UpdateHealthCheckupListReserveStatus(),
                    new Dictionary<string, object?>(keyParameters)
                    {
                        ["ISRESERVE"] = input.NormalizedReserveStatus
                    },
                    "",
                    true);

                if (updatedCount < 1)
                {
                    return NotFound(new { updated = false, updatedCount, message = "找不到要更新的預約資料。" });
                }

                return Json(CreateAppointmentCancelResult(input, updatedCount, isMock: false));
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    updated = false,
                    message = "更新預約狀態失敗，請稍後再試。"
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BathRegistered(
            [FromBody] BathRegisteredPostModel? input,
            CancellationToken requestAborted)
        {
            var rows = input?.Rows ?? [];

            if (rows.Count == 0)
            {
                return Json(new BathRegisteredResultViewModel
                {
                    Registered = false,
                    IsMock = true,
                    TotalCount = 0,
                    SuccessCount = 0,
                    FailedCount = 0,
                    Message = "請先選擇要批次掛號的預約。",
                    Results = []
                });
            }

            var resultRows = new List<BathRegisteredRowResultViewModel>();

            foreach (var row in rows)
            {
                // 尚未開始下一筆掛號前可停止；一旦開始，就不能讓瀏覽器斷線取消 Oracle 寫入。
                if (requestAborted.IsCancellationRequested)
                {
                    break;
                }

                var processedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);

                if (row is null || !row.HasRequiredBarcode)
                {
                    resultRows.Add(new BathRegisteredRowResultViewModel
                    {
                        Success = false,
                        ProjectNumber = row?.NormalizedProjectNumber ?? "",
                        Barcode = row?.NormalizedBarcode ?? "",
                        PatNo = row?.NormalizedPatNo ?? "",
                        Name = row?.NormalizedName ?? "",
                        Message = "缺少 Barcode 編號，無法掛號。",
                        ProcessedAt = processedAt
                    });
                    continue;
                }

                try
                {
                    var reserveStatusTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                        OracleSql.SELECT.GetHealthCheckupListReserveStatus(),
                        new Dictionary<string, object?>
                        {
                            ["BARCODESTR"] = DbString(row.NormalizedBarcode),
                            ["PROJECTNO"] = DbString(row.NormalizedProjectNumber)
                        },
                        "",
                        true);
                    var currentReserveStatus = reserveStatusTable.Rows.Count > 0
                        ? NormalizeReserveStatus(GetString(reserveStatusTable.Rows[0], "ISRESERVE"))
                        : string.Empty;

                    if (currentReserveStatus != "1")
                    {
                        resultRows.Add(new BathRegisteredRowResultViewModel
                        {
                            Success = false,
                            ProjectNumber = row.NormalizedProjectNumber,
                            Barcode = row.NormalizedBarcode,
                            PatNo = row.NormalizedPatNo,
                            Name = row.NormalizedName,
                            Message = currentReserveStatus == "2"
                                ? "佔名額資料不可執行批次掛號，請先完成必要欄位並改為已預約。"
                                : "只有已預約資料可以執行批次掛號。",
                            ProcessedAt = processedAt
                        });
                        continue;
                    }
                }
                catch (Exception)
                {
                    resultRows.Add(new BathRegisteredRowResultViewModel
                    {
                        Success = false,
                        ProjectNumber = row.NormalizedProjectNumber,
                        Barcode = row.NormalizedBarcode,
                        PatNo = row.NormalizedPatNo,
                        Name = row.NormalizedName,
                        Message = "無法確認目前預約狀態，未執行掛號。",
                        ProcessedAt = processedAt
                    });
                    continue;
                }

                IReadOnlyList<OpdPackageOrder> packageOrders;
                try
                {
                    packageOrders = ResolveRegistrationPackageOrders(row);
                }
                catch (Exception exception)
                {
                    resultRows.Add(new BathRegisteredRowResultViewModel
                    {
                        Success = false,
                        ProjectNumber = row.NormalizedProjectNumber,
                        Barcode = row.NormalizedBarcode,
                        PatNo = row.NormalizedPatNo,
                        Name = row.NormalizedName,
                        Message = exception is InvalidOperationException
                            ? exception.Message
                            : "組套資料查詢失敗，無法確認已選組套，未執行掛號。",
                        ProcessedAt = processedAt
                    });
                    continue;
                }
                if (packageOrders.Count == 0)
                {
                    resultRows.Add(new BathRegisteredRowResultViewModel
                    {
                        Success = false,
                        ProjectNumber = row.NormalizedProjectNumber,
                        Barcode = row.NormalizedBarcode,
                        PatNo = row.NormalizedPatNo,
                        Name = row.NormalizedName,
                        Message = "此預約尚未儲存組套或其他組套，請先確認預約內容。",
                        ProcessedAt = processedAt
                    });
                    continue;
                }

                var registrationResults = new List<AppointmentRegistrationExecutionResult>();
                var hasRegistrationFailure = false;
                var registrationPlans = packageOrders
                    .Select((packageOrder, index) => (
                        PackageOrder: (OpdPackageOrder?)packageOrder,
                        PFin2: FirstNonEmpty(
                            packageOrder.PFin2,
                            row.NormalizedSelfPayAccountingUnit,
                            DefaultSelfPayAccountingUnit),
                        UseAgainGetRegNo: index > 0,
                        Label: index == 0
                            ? $"第一筆組套 {packageOrder.OrderNumber}"
                            : $"第 {index + 1} 筆組套 {packageOrder.OrderNumber}"
                    ))
                    .ToArray();

                foreach (var registrationPlan in registrationPlans)
                {
                    var registrationResult = await _appointmentRegistrationService.RegisterAsync(
                        row,
                        registrationPlan.PFin2,
                        registrationPlan.UseAgainGetRegNo);

                    if (!registrationResult.Success || registrationResult.Keys is null)
                    {
                        resultRows.Add(new BathRegisteredRowResultViewModel
                        {
                            Success = false,
                            ProjectNumber = row.NormalizedProjectNumber,
                            Barcode = row.NormalizedBarcode,
                            PatNo = row.NormalizedPatNo,
                            Name = row.NormalizedName,
                            Message = $"{registrationPlan.Label}失敗：{registrationResult.RowResult.Message}",
                            ProcessedAt = registrationResult.RowResult.ProcessedAt
                        });
                        hasRegistrationFailure = true;
                        break;
                    }

                    var basicTableResult = await _opdBasicTbl.InsertAsync(
                        registrationResult.Keys,
                        CancellationToken.None);

                    if (!basicTableResult.Success)
                    {
                        resultRows.Add(new BathRegisteredRowResultViewModel
                        {
                            Success = false,
                            ProjectNumber = row.NormalizedProjectNumber,
                            Barcode = row.NormalizedBarcode,
                            PatNo = row.NormalizedPatNo,
                            Name = row.NormalizedName,
                            Message = $"{registrationPlan.Label}：{basicTableResult.Message}",
                            ProcessedAt = registrationResult.RowResult.ProcessedAt
                        });
                        hasRegistrationFailure = true;
                        break;
                    }

                    var orderResult = registrationPlan.PackageOrder is null
                        ? new OpdOrderResult(true)
                        : await _opdOrderService.PostOPDPriOrd05Async(
                            registrationResult.Keys,
                            [registrationPlan.PackageOrder],
                            CancellationToken.None);

                    if (!orderResult.Success)
                    {
                        resultRows.Add(new BathRegisteredRowResultViewModel
                        {
                            Success = false,
                            ProjectNumber = row.NormalizedProjectNumber,
                            Barcode = row.NormalizedBarcode,
                            PatNo = row.NormalizedPatNo,
                            Name = row.NormalizedName,
                            Message = string.IsNullOrWhiteSpace(orderResult.Message)
                                ? $"{registrationPlan.Label}新增醫令失敗。"
                                : $"{registrationPlan.Label}新增醫令失敗：{orderResult.Message}",
                            ProcessedAt = registrationResult.RowResult.ProcessedAt
                        });
                        hasRegistrationFailure = true;
                        break;
                    }

                    registrationResults.Add(registrationResult with
                    {
                        OrderNumber = registrationPlan.PackageOrder!.OrderNumber
                    });
                }

                if (hasRegistrationFailure)
                {
                    continue;
                }

                resultRows.Add(_appointmentRegistrationService.CompleteRegistrations(row, registrationResults));
            }

            var successCount = resultRows.Count(result => result.Success);
            var failedCount = resultRows.Count - successCount;

            return Json(new BathRegisteredResultViewModel
            {
                Registered = successCount > 0,
                IsMock = true,
                TotalCount = rows.Count,
                SuccessCount = successCount,
                FailedCount = failedCount,
                Message = $"{successCount} 筆成功，{failedCount} 筆失敗。",
                Results = resultRows
            });
        }
        private static IReadOnlyList<OpdPackageOrder> ResolveRegistrationPackageOrders(BathRegisteredItemPostModel row)
        {
            if (row.NormalizedSelectedPackageIds.Count > 0)
            {
                IReadOnlyDictionary<string, PackageOptionViewModel> projectPackages =
                    new Dictionary<string, PackageOptionViewModel>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    if (!string.IsNullOrWhiteSpace(row.NormalizedProjectNumber))
                    {
                        projectPackages = GetPersonalProjectPackagesByProjectNo(row.NormalizedProjectNumber)
                            .GroupBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch
                {
                }

                return row.NormalizedSelectedPackageIds
                    .Select((packageId, index) =>
                    {
                        var postedValue = row.SelectedPackageOrderTotals.ElementAtOrDefault(index);
                        var postedPFin2 = NormalizeText(
                            row.SelectedPackageSelfPayAccountingUnits.ElementAtOrDefault(index));
                        var hasPostedTotal = int.TryParse(
                            postedValue,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var postedTotal);
                        projectPackages.TryGetValue(packageId, out var package);

                        return new OpdPackageOrder(
                            packageId,
                            hasPostedTotal ? Math.Max(0, postedTotal) : package?.OrderTotal ?? 0,
                            FirstNonEmpty(
                                postedPFin2,
                                package?.SelfPayAccountingUnit,
                                row.NormalizedSelfPayAccountingUnit),
                            string.Equals(
                                NormalizeText(row.SelectedPackageTypes.ElementAtOrDefault(index)),
                                "other-suite",
                                StringComparison.OrdinalIgnoreCase));
                    })
                    .ToArray();
            }

            if (string.IsNullOrWhiteSpace(row.NormalizedProjectNumber)
                || string.IsNullOrWhiteSpace(row.NormalizedBarcode))
            {
                return [];
            }

            var parameters = new Dictionary<string, object?>
            {
                ["BARCODESTR"] = row.NormalizedBarcode,
                ["PROJECTNO"] = row.NormalizedProjectNumber
            };
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetHealthCheckupSetInfoByBarcodeProject(),
                parameters,
                "",
                true);
            var otherSetDataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetHealthCheckupOtherSetByBarcodeProject(),
                parameters,
                "",
                true);

            return AppointmentPackageOrderResolver.ResolvePersisted(
                dataTable, otherSetDataTable, row.NormalizedSelfPayAccountingUnit);
        }
        public IActionResult GroupAppointmentManagement(
            string? customerNo,
            string? projectName,
            string? checkDateStart,
            string? checkDateEnd,
            string? reserveStatus,
            string? selectedCustomerNo,
            string? selectedProjectNo)
        {
            var defaultStartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var defaultEndDate = defaultStartDate.AddMonths(1).AddDays(-1);
            var selectedStartDate = ParseGroupQueryDate(checkDateStart) ?? defaultStartDate;
            var selectedEndDate = ParseGroupQueryDate(checkDateEnd) ?? defaultEndDate;

            if (selectedEndDate < selectedStartDate)
            {
                (selectedStartDate, selectedEndDate) = (selectedEndDate, selectedStartDate);
            }

            var selectedFilterCustomerNo = (customerNo ?? string.Empty).Trim();
            var projectNameKeyword = (projectName ?? string.Empty).Trim();
            var selectedReserveStatus = NormalizeGroupReserveStatus(reserveStatus);
            var selectedDetailCustomerNo = (selectedCustomerNo ?? string.Empty).Trim();
            var selectedDetailProjectNo = (selectedProjectNo ?? string.Empty).Trim();
            var groups = GetGroupAppointments(
                selectedFilterCustomerNo,
                projectNameKeyword,
                selectedStartDate,
                selectedEndDate,
                selectedReserveStatus,
                selectedDetailCustomerNo,
                selectedDetailProjectNo);
            var model = new GroupAppointmentManagementViewModel
            {
                FilterStartDate = FormatGroupQueryDate(selectedStartDate),
                FilterEndDate = FormatGroupQueryDate(selectedEndDate),
                ProjectNameKeyword = projectNameKeyword,
                CustomerOptions = GetGroupCustomerOptions(selectedFilterCustomerNo),
                HospitalOptions =
                [
                    new SelectOptionViewModel { Value = "", Text = "全部院區", IsSelected = true }
                ],
                CheckInStatusOptions = BuildGroupReserveStatusOptions(selectedReserveStatus),
                SummaryCards = BuildGroupSummaryCards(groups),
                Groups = groups,
                SelectedGroup = groups.FirstOrDefault(group => group.Selected),
                Activities = [],
                PageSizeOptions = [10, 20, 30, 40, 50],
                TotalGroupCount = groups.Count
            };

            return View(model);
        }

        public IActionResult CreateGroupAppointment(string? rosterKey)
        {
            var normalizedRosterKey = NormalizeText(rosterKey);

            if (TryGetGroupRosterImport(normalizedRosterKey, out var cachedImport) && cachedImport is not null)
            {
                return View(BuildCreateGroupAppointmentModel(
                    cachedImport.Members,
                    cachedImport.UploadedFileName,
                    cachedImport.UploadedFileSize,
                    cachedImport.ImportMessage,
                    normalizedRosterKey));
            }

            return View(BuildCreateGroupAppointmentModel(
                [],
                "",
                0,
                string.IsNullOrWhiteSpace(normalizedRosterKey) ? "" : "匯入名單已過期，請重新上傳。"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateGroupAppointment(IFormFile? rosterFile)
        {
            if (rosterFile is null || rosterFile.Length == 0)
            {
                return View(BuildCreateGroupAppointmentModel(
                    [],
                    "",
                    0,
                    "請先選擇要匯入的團體預約名單。"));
            }

            try
            {
                using var stream = rosterFile.OpenReadStream();
                var members = GroupRosterImportReader.Read(stream, rosterFile.FileName);
                var successCount = members.Count(member => member.Tone == "success");
                var pendingCount = members.Count(member => member.Tone == "pending");
                var errorCount = members.Count(member => member.Tone == "error");
                var importMessage = $"已讀取 {members.Count} 筆：匯入成功 {successCount} 筆、待確認 {pendingCount} 筆、格式錯誤 {errorCount} 筆。";
                var rosterKey = SaveGroupRosterImport(members, rosterFile.FileName, rosterFile.Length, importMessage);

                return View(BuildCreateGroupAppointmentModel(
                    members,
                    rosterFile.FileName,
                    rosterFile.Length,
                    importMessage,
                    rosterKey));
            }
            catch (InvalidOperationException exception)
            {
                return View(BuildCreateGroupAppointmentModel(
                    [],
                    rosterFile.FileName,
                    rosterFile.Length,
                    exception.Message));
            }
        }

        public IActionResult DownloadGroupRosterTemplate()
        {
            var templatePath = ResolveGroupRosterTemplatePath();

            if (!System.IO.File.Exists(templatePath))
            {
                return NotFound();
            }

            return PhysicalFile(
                templatePath,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                GroupRosterTemplateFileName);
        }

        [HttpGet]
        public IActionResult GetSupplementOrderOptions(string? projectNumber, string? barcode)
        {
            var normalizedProjectNumber = NormalizeText(projectNumber);
            var normalizedBarcode = NormalizeText(barcode);

            if (string.IsNullOrWhiteSpace(normalizedProjectNumber)
                || string.IsNullOrWhiteSpace(normalizedBarcode))
            {
                return BadRequest(new { message = "缺少 PROJECTNO 或 BARCODESTR，無法載入補下醫令資料。" });
            }

            try
            {
                IReadOnlyList<SelfPayAccountingUnitViewModel> accountingUnits;

                try
                {
                    accountingUnits = GetPersonalSelfPayAccountingUnits();
                }
                catch
                {
                    accountingUnits = [];
                }

                var accountingUnitDisplayCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                string AccountingUnitDisplay(string? code)
                {
                    var normalizedCode = NormalizeText(code);

                    if (string.IsNullOrWhiteSpace(normalizedCode))
                    {
                        normalizedCode = DefaultSelfPayAccountingUnit;
                    }

                    if (!accountingUnitDisplayCache.TryGetValue(normalizedCode, out var display))
                    {
                        display = GetPersonalSelfPayAccountingUnitDisplay(normalizedCode);
                        accountingUnitDisplayCache[normalizedCode] = display;
                    }

                    return display;
                }

                var parameters = new Dictionary<string, object?>
                {
                    ["BARCODESTR"] = normalizedBarcode,
                    ["PROJECTNO"] = normalizedProjectNumber
                };
                var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();
                var setInfoDataTable = connectionFactory.GETDataTable(
                    OracleSql.SELECT.GetHealthCheckupSetInfoByBarcodeProject(),
                    parameters,
                    "",
                    true);
                var otherSetDataTable = connectionFactory.GETDataTable(
                    OracleSql.SELECT.GetHealthCheckupOtherSetByBarcodeProject(),
                    parameters,
                    "",
                    true);

                var packages = setInfoDataTable.Rows
                    .Cast<DataRow>()
                    .Select((row, index) =>
                    {
                        var accountingUnit = FirstNonEmpty(
                            NormalizeText(GetString(row, "CHDCTTYPE")),
                            DefaultSelfPayAccountingUnit);

                        return new
                        {
                            orderNumber = FirstNonEmpty(
                                GetString(row, "LISITEMNAME"),
                                GetString(row, "ITEMNO"),
                                (index + 1).ToString(CultureInfo.InvariantCulture)),
                            name = FirstNonEmpty(
                                GetString(row, "SETNAME"),
                                GetString(row, "LISITEMNAME"),
                                GetString(row, "ITEMNO")),
                            selfPayAccountingUnit = accountingUnit,
                            selfPayAccountingUnitDisplay = AccountingUnitDisplay(accountingUnit)
                        };
                    })
                    .Where(package => !string.IsNullOrWhiteSpace(package.name))
                    .ToArray();

                var otherSuites = otherSetDataTable.Rows
                    .Cast<DataRow>()
                    .Select((row, index) =>
                    {
                        var accountingUnit = FirstNonEmpty(
                            NormalizeText(GetString(row, "CHDCTTYPE")),
                            DefaultSelfPayAccountingUnit);

                        return new
                        {
                            orderNumber = FirstNonEmpty(
                                GetString(row, "SETCODE"),
                                GetString(row, "ITEMNO"),
                                (index + 1).ToString(CultureInfo.InvariantCulture)),
                            name = FirstNonEmpty(
                                GetString(row, "SETNAME"),
                                GetString(row, "SETCODE"),
                                GetString(row, "ITEMNO")),
                            selfPayAccountingUnit = accountingUnit,
                            selfPayAccountingUnitDisplay = AccountingUnitDisplay(accountingUnit)
                        };
                    })
                    .Where(suite => !string.IsNullOrWhiteSpace(suite.name))
                    .ToArray();

                object[] availablePackages;

                try
                {
                    availablePackages = GetPersonalProjectPackagesByProjectNo(normalizedProjectNumber)
                        .Select(package => (object)new
                        {
                            id = package.Id,
                            orderNumber = FirstNonEmpty(package.Id, package.LisItemName),
                            package.Name,
                            selfPayAccountingUnit = FirstNonEmpty(
                                NormalizeText(package.SelfPayAccountingUnit),
                                DefaultSelfPayAccountingUnit),
                            selfPayAccountingUnitDisplay = FirstNonEmpty(
                                package.SelfPayAccountingUnitDisplay,
                                AccountingUnitDisplay(package.SelfPayAccountingUnit)),
                            package.Price,
                            package.PriceText,
                            package.OrderTotal,
                            orders = package.Orders.Select(order => new
                            {
                                order.Code,
                                order.Name,
                                order.Category
                            }).ToArray()
                        })
                        .ToArray();
                }
                catch
                {
                    availablePackages = [];
                }

                return Json(new
                {
                    packages,
                    otherSuites,
                    availablePackages,
                    selfPayAccountingUnits = accountingUnits.Select(unit => new
                    {
                        unit.Code,
                        unit.Name,
                        unit.DisplayText
                    }).ToArray()
                });
            }
            catch
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = "載入當初的組套與優待身分時發生錯誤，請稍後再試。" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSupplementOrders(
            [FromBody] SupplementOrderPostModel input,
            CancellationToken requestAborted)
        {
            var targetProjectNo = NormalizeText(input.ProjectNumber);
            var barcode = NormalizeText(input.Barcode);
            var packageProjectNo = NormalizeText(input.PackageProjectNo);
            var packageIds = input.SelectedPackageIds.Select(NormalizeText).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            var otherSuiteIds = input.SelectedOtherSuiteIds.Select(NormalizeText).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

            if (string.IsNullOrWhiteSpace(targetProjectNo) || string.IsNullOrWhiteSpace(barcode))
            {
                return BadRequest(new { message = "缺少 PROJECTNO 或 BARCODESTR，無法補下醫令。" });
            }

            if (packageIds.Length == 0 && otherSuiteIds.Length == 0)
            {
                return BadRequest(new { message = "請至少勾選一個健檢專案 / 方案套組，或新增一個其他組套。" });
            }

            if (packageIds.Length > 0 && string.IsNullOrWhiteSpace(packageProjectNo))
            {
                return BadRequest(new { message = "請先選擇健檢專案 / 方案。" });
            }

            try
            {
                var selectedPackages = ResolveSelectedPackages(
                    packageProjectNo,
                    packageIds,
                    input.SelectedPackageNames,
                    input.SelectedPackageAmounts,
                    input.SelectedPackageSelfPayAccountingUnits);
                var selectedOtherSuites = ResolveSelectedOtherSuites(
                    otherSuiteIds,
                    input.SelectedOtherSuiteSelfPayAccountingUnits);

                if (selectedPackages.Count != packageIds.Length)
                {
                    return BadRequest(new { message = "送出的套組代碼無法對應目前健檢專案 / 方案資料。" });
                }

                if (selectedOtherSuites.Count != otherSuiteIds.Length)
                {
                    return BadRequest(new { message = "送出的其他組套代碼無法對應目前其他組套資料。" });
                }

                var keyParameters = new Dictionary<string, object?>
                {
                    ["PROJECTNO"] = DbString(targetProjectNo),
                    ["BARCODESTR"] = DbString(barcode)
                };
                var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();
                var appointmentTable = connectionFactory.GETDataTable(
                    OracleSql.SELECT.GetHealthCheckupListByBarcodeProject(),
                    keyParameters,
                    "",
                    true);
                var registrationTable = connectionFactory.GETDataTable(
                    OracleSql.SELECT.GetFEMHCheckupRegistrationByBarcodeProject(),
                    keyParameters,
                    "",
                    true);
                var existingPackageTable = connectionFactory.GETDataTable(
                    OracleSql.SELECT.GetHealthCheckupSetInfoByBarcodeProject(),
                    keyParameters,
                    "",
                    true);
                var existingOtherSuiteTable = connectionFactory.GETDataTable(
                    OracleSql.SELECT.GetHealthCheckupOtherSetByBarcodeProject(),
                    keyParameters,
                    "",
                    true);
                var existingPackageCodes = existingPackageTable.Rows.Cast<DataRow>()
                    .Select(row => NormalizeText(FirstNonEmpty(GetString(row, "LISITEMNAME"), GetString(row, "ITEMNO"))))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var existingOtherSuiteCodes = existingOtherSuiteTable.Rows.Cast<DataRow>()
                    .Select(row => NormalizeText(FirstNonEmpty(GetString(row, "SETCODE"), GetString(row, "ITEMNO"))))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (selectedPackages.Any(package => existingPackageCodes.Contains(FirstNonEmpty(package.LisItemName, package.Id)))
                    || selectedOtherSuites.Any(suite => existingOtherSuiteCodes.Contains(GetOtherSuiteSetCode(suite))))
                {
                    return Conflict(new { message = "部分套組已存在，請重新開啟補下醫令後再選擇。" });
                }

                var appointmentRow = appointmentTable.Rows.Cast<DataRow>().FirstOrDefault();
                var registrationRow = registrationTable.Rows.Cast<DataRow>().FirstOrDefault();
                if (appointmentRow is null || registrationRow is null)
                {
                    return Conflict(new { message = "找不到已掛號預約或就診號資料，未執行補下醫令。" });
                }

                var registrationInput = new BathRegisteredItemPostModel
                {
                    ProjectNumber = targetProjectNo,
                    Barcode = barcode,
                    PatNo = GetString(appointmentRow, "PATNO"),
                    Name = GetString(appointmentRow, "PATIENTNAME"),
                    AppointmentDate = GetDateString(appointmentRow, "RESERVEDATE"),
                    Time = GetString(appointmentRow, "RESERVETIME"),
                    DoctorId = GetString(appointmentRow, "DOCTOREMPNO"),
                    DoctorName = GetString(appointmentRow, "DOCTORNAME"),
                    RoomNo = GetString(appointmentRow, "ROOMNO"),
                    SelfPayAccountingUnit = FirstNonEmpty(
                        GetString(appointmentRow, "SELFCOSTNO"),
                        selectedPackages.FirstOrDefault()?.SelfPayAccountingUnit,
                        selectedOtherSuites.FirstOrDefault()?.SelfPayAccountingUnit,
                        DefaultSelfPayAccountingUnit)
                };
                var opdOrders = selectedPackages
                    .Select(package => new OpdPackageOrder(
                        FirstNonEmpty(package.Id, package.LisItemName),
                        package.OrderTotal,
                        FirstNonEmpty(package.SelfPayAccountingUnit, DefaultSelfPayAccountingUnit)))
                    .Concat(selectedOtherSuites.Select(suite => new OpdPackageOrder(
                        GetOtherSuiteSetCode(suite),
                        1,
                        FirstNonEmpty(suite.SelfPayAccountingUnit, DefaultSelfPayAccountingUnit),
                        IsOtherSuite: true)))
                    .Where(order => !string.IsNullOrWhiteSpace(order.OrderNumber))
                    .ToArray();

                if (opdOrders.Length != selectedPackages.Count + selectedOtherSuites.Count)
                {
                    return BadRequest(new { message = "部分組套缺少 OrdNo，未執行補下醫令。" });
                }

                var registrationResults = new List<AppointmentRegistrationExecutionResult>();
                foreach (var opdOrder in opdOrders)
                {
                    requestAborted.ThrowIfCancellationRequested();

                    var registrationResult = await _appointmentRegistrationService.RegisterAsync(
                        registrationInput,
                        opdOrder.PFin2,
                        useAgainGetRegNo: true);
                    if (!registrationResult.Success || registrationResult.Keys is null)
                    {
                        return StatusCode(StatusCodes.Status502BadGateway, new
                        {
                            message = $"組套 {opdOrder.OrderNumber} 再次掛號失敗：{registrationResult.RowResult.Message}；醫令資料未新增。"
                        });
                    }

                    var basicTableResult = await _opdBasicTbl.InsertAsync(
                        registrationResult.Keys,
                        requestAborted);
                    if (!basicTableResult.Success)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, new
                        {
                            message = $"組套 {opdOrder.OrderNumber}：{basicTableResult.Message}"
                        });
                    }

                    var opdOrderResult = await _opdOrderService.PostOPDPriOrd05Async(
                        registrationResult.Keys,
                        [opdOrder],
                        requestAborted);
                    if (!opdOrderResult.Success)
                    {
                        return StatusCode(StatusCodes.Status502BadGateway, new
                        {
                            message = string.IsNullOrWhiteSpace(opdOrderResult.Message)
                                ? $"組套 {opdOrder.OrderNumber} 新增醫令 API 執行失敗，套組資料未新增。"
                                : $"組套 {opdOrder.OrderNumber} 新增醫令 API 執行失敗：{opdOrderResult.Message}；套組資料未新增。"
                        });
                    }

                    registrationResults.Add(registrationResult with
                    {
                        OrderNumber = opdOrder.OrderNumber
                    });
                }

                var completedRegistration = _appointmentRegistrationService.CompleteRegistrations(
                    registrationInput,
                    registrationResults);
                if (!completedRegistration.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        message = $"再次掛號資料儲存失敗：{completedRegistration.Message}；套組資料未新增。"
                    });
                }

                requestAborted.ThrowIfCancellationRequested();

                var commands = new List<OracleTransactionCommand>();
                var packageItemNo = existingPackageTable.Rows.Count;
                commands.AddRange(selectedPackages.Select((package, index) => new OracleTransactionCommand(
                    OracleSql.INSERT.InsertHealthCheckupSetInfo(),
                    new Dictionary<string, object?>
                    {
                        ["PROJECTNO"] = DbString(targetProjectNo),
                        ["BARCODESTR"] = DbString(barcode),
                        ["ITEMNO"] = packageItemNo + index + 1,
                        ["LISITEMNAME"] = DbString(package.LisItemName),
                        ["ORDTOT"] = package.OrderTotal,
                        ["SETNAME"] = DbString(package.Name),
                        ["AMOUNT"] = package.Price,
                        ["CHDCTTYPE"] = DbString(package.SelfPayAccountingUnit)
                    })));
                var otherSuiteItemNo = existingOtherSuiteTable.Rows.Count;
                commands.AddRange(selectedOtherSuites.Select((suite, index) => new OracleTransactionCommand(
                    OracleSql.INSERT.InsertHealthCheckupOtherSet(),
                    new Dictionary<string, object?>
                    {
                        ["PROJECTNO"] = DbString(targetProjectNo),
                        ["BARCODESTR"] = DbString(barcode),
                        ["ITEMNO"] = (otherSuiteItemNo + index + 1).ToString(CultureInfo.InvariantCulture),
                        ["SETCODE"] = DbString(GetOtherSuiteSetCode(suite)),
                        ["SETNAME"] = DbString(suite.Name),
                        ["AMOUNT"] = DbString(suite.Price.ToString("0.##", CultureInfo.InvariantCulture)),
                        ["CHDCTTYPE"] = DbString(FirstNonEmpty(suite.SelfPayAccountingUnit, DefaultSelfPayAccountingUnit))
                    })));

                var affectedCounts = connectionFactory.ExecuteNonQueryTransaction(commands, "", true);
                if (affectedCounts.Length != commands.Count || affectedCounts.Any(count => count <= 0))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = "補下醫令未完整寫入，已取消本次交易。" });
                }

                return Json(new
                {
                    success = true,
                    addedCount = affectedCounts.Sum(),
                    message = $"已新增 {affectedCounts.Sum()} 筆醫令。"
                });
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                return StatusCode(499, new { message = "補下醫令已取消，HealthCheckupSetInfo 未新增。" });
            }
            catch
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = "補下醫令寫入失敗，已取消本次交易，請稍後再試。" });
            }
        }
        public IActionResult CreateAppointment(
            string? projectNumber,
            string? barcode,
            string? reserveStatus,
            bool readOnly,
            string? returnUrl,
            string? source,
            string? name,
            string? idNo,
            string? chartNo,
            string? sex,
            string? birthDate,
            string? phone,
            string? mobile,
            string? email,
            string? checkDate,
            string? workArea,
            string? address,
            string? company,
            string? note,
            string? packageName,
            string? addOns)
        {
            var form = GetCreateAppointmentEditForm(projectNumber, barcode);
            var isGroupRosterSource = string.Equals(NormalizeText(source), "groupRoster", StringComparison.OrdinalIgnoreCase);

            if (isGroupRosterSource)
            {
                var patient = FindGroupRosterPatient(chartNo, idNo);

                if (patient is not null)
                {
                    form = ApplyPatientLookupPrefill(form, patient);
                }
            }

            if (HasGroupRosterAppointmentPrefill(source, name, idNo, chartNo, sex, birthDate, phone, mobile, email, checkDate, workArea, address, company, note, packageName, addOns))
            {
                form = ApplyGroupRosterAppointmentPrefill(
                    form,
                    name,
                    idNo,
                    chartNo,
                    sex,
                    birthDate,
                    phone,
                    mobile,
                    email,
                    checkDate,
                    workArea,
                    address,
                    company,
                    note,
                    packageName,
                    addOns);
            }

            var normalizedReserveStatus = NormalizeReserveStatus(reserveStatus ?? "");

            if (!string.IsNullOrWhiteSpace(normalizedReserveStatus))
            {
                form.ReserveStatus = normalizedReserveStatus;
            }

            var model = BuildCreateAppointmentModel(
                form,
                ResolveCreateAppointmentReturnUrl(returnUrl),
                readOnly && normalizedReserveStatus == "4");

            return View(model);
        }

        private static PatientLookupPatientViewModel? FindGroupRosterPatient(string? chartNo, string? idNo)
        {
            var normalizedChartNo = NormalizePatientLookupKeyword(chartNo, "chartNo");

            if (!string.IsNullOrWhiteSpace(normalizedChartNo))
            {
                var patientByChartNo = GetPatientLookupPatients(normalizedChartNo, "chartNo")
                    .FirstOrDefault(patient => string.Equals(
                        NormalizePatientLookupKeyword(patient.ChartNo, "chartNo"),
                        normalizedChartNo,
                        StringComparison.Ordinal));

                if (patientByChartNo is not null)
                {
                    return patientByChartNo;
                }
            }

            var normalizedIdNo = NormalizePatientLookupKeyword(idNo, "idNo");

            return string.IsNullOrWhiteSpace(normalizedIdNo)
                ? null
                : GetPatientLookupPatients(normalizedIdNo, "idNo")
                    .FirstOrDefault(patient => string.Equals(
                        NormalizePatientLookupKeyword(patient.IdNo, "idNo"),
                        normalizedIdNo,
                        StringComparison.Ordinal));
        }

        private static CreateAppointmentFormViewModel ApplyPatientLookupPrefill(
            CreateAppointmentFormViewModel form,
            PatientLookupPatientViewModel patient)
        {
            return new CreateAppointmentFormViewModel
            {
                SelfPayAccountingUnit = form.SelfPayAccountingUnit,
                PatientHealthCode = FirstNonEmpty(form.PatientHealthCode, patient.HealthCode),
                ProjectNo = form.ProjectNo,
                ReservationDate = form.ReservationDate,
                ReservationTime = form.ReservationTime,
                ConsultingDoctorId = form.ConsultingDoctorId,
                ConsultingDoctorName = form.ConsultingDoctorName,
                ClinicRoom = form.ClinicRoom,
                PatientName = FirstNonEmpty(patient.Name, form.PatientName),
                PatientIdNo = FirstNonEmpty(patient.IdNo, form.PatientIdNo),
                PatientBirthDate = FirstNonEmpty(NormalizeAppointmentDateInput(patient.BirthDate), form.PatientBirthDate),
                PatientAge = FirstNonEmpty(patient.Age, form.PatientAge),
                PatientSex = FirstNonEmpty(NormalizeRosterSex(patient.Sex), form.PatientSex),
                PatientPhone = FirstNonEmpty(patient.Phone, form.PatientPhone),
                PatientMobile = FirstNonEmpty(patient.Mobile, form.PatientMobile),
                PatientEmail = FirstNonEmpty(patient.Email, form.PatientEmail),
                ReserveStatus = form.ReserveStatus,
                PatientChartNo = FirstNonEmpty(patient.ChartNo, form.PatientChartNo),
                ReservationArea = form.ReservationArea,
                ParkingType = form.ParkingType,
                Laxative = form.Laxative,
                MealType = form.MealType,
                ContactName = FirstNonEmpty(patient.ContactName, form.ContactName),
                ContactPhone = FirstNonEmpty(patient.ContactPhone, form.ContactPhone),
                ContactRelationship = FirstNonEmpty(patient.ContactRelationship, form.ContactRelationship),
                MailAddress = FirstNonEmpty(patient.MailingAddress, form.MailAddress),
                LiveAddress = FirstNonEmpty(patient.Address, form.LiveAddress),
                IsSameAddress = form.IsSameAddress,
                IsVip = patient.IsVip || form.IsVip,
                IsEmployeeFamily = patient.IsEmployeeFamily || form.IsEmployeeFamily,
                BranchFactory = form.BranchFactory,
                EmployeeStudentNo = form.EmployeeStudentNo,
                CustomerNo = form.CustomerNo,
                FamilyNote = FirstNonEmpty(patient.FamilyNote, form.FamilyNote),
                NotificationNote = FirstNonEmpty(patient.NotificationNote, form.NotificationNote),
                CurrentNote = form.CurrentNote,
                PermanentNote = form.PermanentNote,
                PackageAmount = form.PackageAmount,
                ExtraAmount = form.ExtraAmount,
                RemitAmount = form.RemitAmount,
                DiscountAmount = form.DiscountAmount,
                AccountAmount = form.AccountAmount,
                ReceivedAmount = form.ReceivedAmount,
                PaymentDate = form.PaymentDate,
                PaymentMethod = form.PaymentMethod,
                PaymentNote = form.PaymentNote,
                Packages = form.Packages,
                OtherSuites = form.OtherSuites,
                Questionnaires = form.Questionnaires
            };
        }

        private static bool HasGroupRosterAppointmentPrefill(
            string? source,
            params string?[] values) =>
            string.Equals(NormalizeText(source), "groupRoster", StringComparison.OrdinalIgnoreCase)
            || values.Any(value => !string.IsNullOrWhiteSpace(value));

        private static CreateAppointmentFormViewModel ApplyGroupRosterAppointmentPrefill(
            CreateAppointmentFormViewModel form,
            string? name,
            string? idNo,
            string? chartNo,
            string? sex,
            string? birthDate,
            string? phone,
            string? mobile,
            string? email,
            string? checkDate,
            string? workArea,
            string? address,
            string? company,
            string? note,
            string? packageName,
            string? addOns)
        {
            var normalizedBirthDate = NormalizeAppointmentDateInput(birthDate);
            var normalizedCheckDate = NormalizeAppointmentDateInput(checkDate);
            var normalizedAddress = NormalizeText(address);
            var groupRosterNote = NormalizeText(note);

            return new CreateAppointmentFormViewModel
            {
                SelfPayAccountingUnit = form.SelfPayAccountingUnit,
                PatientHealthCode = form.PatientHealthCode,
                ProjectNo = form.ProjectNo,
                ReservationDate = FirstNonEmpty(form.ReservationDate, normalizedCheckDate),
                ReservationTime = form.ReservationTime,
                ConsultingDoctorId = form.ConsultingDoctorId,
                ConsultingDoctorName = form.ConsultingDoctorName,
                ClinicRoom = form.ClinicRoom,
                PatientName = FirstNonEmpty(form.PatientName, NormalizeText(name)),
                PatientIdNo = FirstNonEmpty(form.PatientIdNo, NormalizeText(idNo)),
                PatientBirthDate = FirstNonEmpty(form.PatientBirthDate, normalizedBirthDate),
                PatientAge = form.PatientAge,
                PatientSex = FirstNonEmpty(form.PatientSex, NormalizeRosterSex(sex)),
                PatientPhone = FirstNonEmpty(form.PatientPhone, NormalizeText(phone)),
                PatientMobile = FirstNonEmpty(form.PatientMobile, NormalizeText(mobile), NormalizeText(phone)),
                PatientEmail = FirstNonEmpty(form.PatientEmail, NormalizeText(email)),
                ReserveStatus = form.ReserveStatus,
                PatientChartNo = FirstNonEmpty(form.PatientChartNo, NormalizeText(chartNo)),
                ReservationArea = FirstNonEmpty(form.ReservationArea, NormalizeText(workArea)),
                ParkingType = form.ParkingType,
                Laxative = form.Laxative,
                MealType = form.MealType,
                ContactName = form.ContactName,
                ContactPhone = form.ContactPhone,
                ContactRelationship = form.ContactRelationship,
                MailAddress = FirstNonEmpty(form.MailAddress, normalizedAddress),
                LiveAddress = FirstNonEmpty(form.LiveAddress, normalizedAddress),
                IsSameAddress = form.IsSameAddress,
                IsVip = form.IsVip,
                IsEmployeeFamily = form.IsEmployeeFamily,
                BranchFactory = form.BranchFactory,
                EmployeeStudentNo = form.EmployeeStudentNo,
                CustomerNo = form.CustomerNo,
                FamilyNote = form.FamilyNote,
                NotificationNote = form.NotificationNote,
                CurrentNote = FirstNonEmpty(form.CurrentNote, groupRosterNote),
                PermanentNote = form.PermanentNote,
                PackageAmount = form.PackageAmount,
                ExtraAmount = form.ExtraAmount,
                RemitAmount = form.RemitAmount,
                DiscountAmount = form.DiscountAmount,
                AccountAmount = form.AccountAmount,
                ReceivedAmount = form.ReceivedAmount,
                PaymentDate = form.PaymentDate,
                PaymentMethod = form.PaymentMethod,
                PaymentNote = form.PaymentNote,
                Packages = form.Packages,
                OtherSuites = form.OtherSuites,
                Questionnaires = form.Questionnaires
            };
        }
        private static string NormalizeRosterSex(string? value)
        {
            var normalizedValue = NormalizeText(value);

            return normalizedValue switch
            {
                "M" or "m" or "男生" or "男性" => "男",
                "F" or "f" or "女生" or "女性" => "女",
                _ => normalizedValue
            };
        }

        private static string NormalizeAppointmentDateInput(string? value)
        {
            var normalizedValue = NormalizeText(value);

            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return "";
            }

            string[] formats = ["yyyy/M/d", "yyyy/MM/dd", "yyyy-M-d", "yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy"];

            return DateTime.TryParseExact(normalizedValue, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate)
                || DateTime.TryParse(normalizedValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out exactDate)
                || DateTime.TryParse(normalizedValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out exactDate)
                ? exactDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : normalizedValue;
        }
        private string ResolveCreateAppointmentReturnUrl(string? returnUrl)
        {
            var normalizedReturnUrl = NormalizeText(returnUrl);

            if (!string.IsNullOrWhiteSpace(normalizedReturnUrl) && Url.IsLocalUrl(normalizedReturnUrl))
            {
                return normalizedReturnUrl;
            }

            var fallbackUrl = Url.Action("AppointmentManagement", "Appointment") ?? "/Appointment/AppointmentManagement";

            return fallbackUrl + "#individual-appointment";
        }

        private static CreateAppointmentViewModel BuildCreateAppointmentModel(
            CreateAppointmentFormViewModel form,
            string returnUrl = "",
            bool isReadOnly = false)
        {
            var selectedProjectNo = NormalizeText(form.ProjectNo);
            var selectedWorkArea = NormalizeText(form.ReservationArea);
            var isEditMode = !string.IsNullOrWhiteSpace(form.PatientHealthCode)
                && !string.IsNullOrWhiteSpace(selectedProjectNo);
            var selectedConsultingDoctorId = NormalizeText(form.ConsultingDoctorId);
            var selectedConsultingDoctorName = NormalizeText(form.ConsultingDoctorName);

            if (!isEditMode && string.IsNullOrWhiteSpace(selectedConsultingDoctorId))
            {
                selectedConsultingDoctorId = DefaultConsultingDoctorId;
                selectedConsultingDoctorName = DefaultConsultingDoctorName;
            }

            var selectedParking = NormalizeText(form.ParkingType);
            var selectedLaxative = NormalizeText(form.Laxative);
            var selectedMealType = NormalizeText(form.MealType);
            var projectLookupItems = GetAllProjectLookupItems();
            var selectedSelfPayAccountingUnit = NormalizeText(form.SelfPayAccountingUnit);
            var selfPayAccountingUnits = GetPersonalSelfPayAccountingUnits();

            return new CreateAppointmentViewModel
            {
                SelectedSelfPayAccountingUnit = selectedSelfPayAccountingUnit,
                SelectedSelfPayAccountingUnitDisplay = GetPersonalSelfPayAccountingUnitDisplay(selectedSelfPayAccountingUnit),
                SelfPayAccountingUnits = selfPayAccountingUnits,
                IsEditMode = isEditMode,
                IsReadOnly = isReadOnly,
                ReturnUrl = returnUrl,
                Form = form,
                SelectedPackageId = null,
                SelectedAddOnIds = [],
                ProjectOptions = WithSelectedOption(GetPersonalAppointmentProjectOptions(), selectedProjectNo),
                ProjectLookupItems = projectLookupItems,
                WorkAreaOptions = WithSelectedOption(GetWorkAreaOptions(), selectedWorkArea),
                ConsultingDoctorOptions = WithSelectedOption(
                    GetConsultingDoctorOptions(),
                    selectedConsultingDoctorId,
                    string.IsNullOrWhiteSpace(selectedConsultingDoctorName)
                        ? null
                        : $"({selectedConsultingDoctorId}) {selectedConsultingDoctorName}"),
                PhraseOptions = GetPhraseOptions(),
                ParkingOptions = WithSelectedOption(
                [
                    new SelectOptionViewModel { Value = "", Text = "請選擇", IsSelected = true },
                    new SelectOptionViewModel { Value = "無", Text = "無" },
                    new SelectOptionViewModel { Value = "汽車", Text = "汽車" },
                    new SelectOptionViewModel { Value = "機車", Text = "機車" }
                ], selectedParking),
                LaxativeOptions = WithSelectedOption(
                [
                    new SelectOptionViewModel { Value = "", Text = "請選擇", IsSelected = true },
                    new SelectOptionViewModel { Value = "腸見淨", Text = "腸見淨" },
                    new SelectOptionViewModel { Value = "保可淨", Text = "保可淨" }
                ], selectedLaxative),
                MealOptions = WithSelectedOption(
                [
                    new SelectOptionViewModel { Value = "", Text = "請選擇", IsSelected = true },
                    new SelectOptionViewModel { Value = "葷", Text = "葷" },
                    new SelectOptionViewModel { Value = "素", Text = "素" },
                    new SelectOptionViewModel { Value = "無", Text = "無" }
                ], selectedMealType),
                AllergyOptions =
                [
                    new SelectOptionViewModel { Value = "否", Text = "否", IsSelected = true },
                    new SelectOptionViewModel { Value = "是", Text = "是" }
                ],
                DietOptions =
                [
                    new SelectOptionViewModel { Value = "無", Text = "無", IsSelected = true },
                    new SelectOptionViewModel { Value = "素食", Text = "素食" },
                    new SelectOptionViewModel { Value = "清真", Text = "清真" }
                ],
                PackageOptions = [],
                QuestionnaireOptions = GetQuestionnaireOptions(form.Questionnaires),
                AddOnOptions = [],
                BillingItems =
                [
                    new BillingItemViewModel { Label = "套餐費用", Value = "$0" },
                    new BillingItemViewModel { Label = "加做費用", Value = "$0" },
                    new BillingItemViewModel { Label = "其他費用", Value = "$0" },
                    new BillingItemViewModel { Label = "預估總價", Value = "$0", Tone = "red" }
                ],
                FlowSteps =
                [
                    new AppointmentFlowStepViewModel { Label = "預約", Description = "建立預約資料", Icon = "person-small-icon", IsActive = true },
                    new AppointmentFlowStepViewModel { Label = "報到", Description = "受檢人員報到", Icon = "person-small-icon" },
                    new AppointmentFlowStepViewModel { Label = "繳費", Description = "完成繳費", Icon = "money-icon" },
                    new AppointmentFlowStepViewModel { Label = "檢查", Description = "進行各項檢查", Icon = "stethoscope-icon" },
                    new AppointmentFlowStepViewModel { Label = "完成", Description = "報告完成", Icon = "check-mini-icon" }
                ]
            };
        }

        private static CreateAppointmentFormViewModel GetCreateAppointmentEditForm(string? projectNumber, string? barcode)
        {
            var normalizedProjectNo = NormalizeText(projectNumber);
            var normalizedBarcode = NormalizeText(barcode);

            if (string.IsNullOrWhiteSpace(normalizedProjectNo) || string.IsNullOrWhiteSpace(normalizedBarcode))
            {
                return new CreateAppointmentFormViewModel();
            }

            var parameters = new Dictionary<string, object?>
            {
                ["BARCODESTR"] = normalizedBarcode,
                ["PROJECTNO"] = normalizedProjectNo
            };
            var sql = OracleSql.SELECT.GetHealthCheckupListByBarcodeProject();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                sql,
                parameters,
                "",
                true);

            if (dataTable.Rows.Count == 0)
            {
                return new CreateAppointmentFormViewModel();
            }

            var noteDataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetHealthCheckupNoteByBarcodeProject(),
                parameters,
                "",
                true);
            var noteRow = noteDataTable.Rows.Count > 0 ? noteDataTable.Rows[0] : null;

            var reserInfoDataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetHealthCheckupReserInfoByBarcodeProject(),
                parameters,
                "",
                true);
            var reserInfoRow = reserInfoDataTable.Rows.Count > 0 ? reserInfoDataTable.Rows[0] : null;

            var paymentDataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetHealthCheckupPaymentByBarcodeProject(),
                parameters,
                "",
                true);
            var paymentRow = paymentDataTable.Rows.Count > 0 ? paymentDataTable.Rows[0] : null;

            var setInfoDataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetHealthCheckupSetInfoByBarcodeProject(),
                parameters,
                "",
                true);

            var otherSetDataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetHealthCheckupOtherSetByBarcodeProject(),
                parameters,
                "",
                true);

            var questInfoDataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetHealthCheckupQuestInfoByBarcodeProject(),
                parameters,
                "",
                true);

            return CreateAppointmentForm(
                dataTable.Rows[0],
                noteRow,
                reserInfoRow,
                paymentRow,
                CreatePackageItemsFromSetInfo(setInfoDataTable, normalizedProjectNo),
                CreateOtherSuiteItemsFromOtherSet(otherSetDataTable, normalizedProjectNo),
                CreateQuestionnaireItemsFromQuestInfo(questInfoDataTable));
        }

        private static CreateAppointmentFormViewModel CreateAppointmentForm(
            DataRow row,
            DataRow? noteRow,
            DataRow? reserInfoRow,
            DataRow? paymentRow,
            IReadOnlyList<PackageOptionViewModel> packages,
            IReadOnlyList<OtherSuiteLookupItemViewModel> otherSuites,
            IReadOnlyList<QuestionnaireOptionViewModel> questionnaires)
        {
            return new CreateAppointmentFormViewModel
            {
                PatientHealthCode = GetString(row, "BARCODESTR"),
                SelfPayAccountingUnit = GetString(row, "SELFCOSTNO"),
                ProjectNo = GetString(row, "PROJECTNO"),
                ReservationDate = FirstNonEmpty(
                    GetDateInputString(row, "RESERVEDATE"),
                    reserInfoRow is null ? "" : GetDateInputString(reserInfoRow, "RESERVE_DATE")),
                ReservationTime = FirstNonEmpty(
                    GetTimeInputString(row, "RESERVETIME"),
                    reserInfoRow is null ? "" : GetTimeInputString(reserInfoRow, "RESERVE_TIME")),
                ConsultingDoctorId = GetString(row, "DOCTOREMPNO"),
                ConsultingDoctorName = GetString(row, "DOCTORNAME"),
                ClinicRoom = GetString(row, "ROOMNO"),
                PatientName = GetString(row, "PATIENTNAME"),
                PatientIdNo = GetString(row, "IDNUMBER"),
                PatientBirthDate = GetDateInputString(row, "BIRTHDAY"),
                PatientAge = GetString(row, "AGE"),
                PatientSex = GetString(row, "GENDER"),
                PatientPhone = GetString(row, "TEL"),
                PatientMobile = GetString(row, "MOBILE"),
                PatientEmail = GetString(row, "EMAIL"),
                ReserveStatus = NormalizeReserveStatus(GetString(row, "ISRESERVE")),
                PatientChartNo = GetString(row, "PATNO"),
                ReservationArea = FirstNonEmpty(
                    GetString(row, "WORKAREA"),
                    reserInfoRow is null ? "" : GetString(reserInfoRow, "RESERVE_AREA")),
                ParkingType = reserInfoRow is null ? "" : GetString(reserInfoRow, "PARKING"),
                Laxative = reserInfoRow is null ? "" : GetString(reserInfoRow, "LAXATIVE"),
                MealType = reserInfoRow is null ? "" : GetString(reserInfoRow, "MEAL"),
                ContactName = GetString(row, "CONTACTNAME"),
                ContactPhone = GetString(row, "CONTACTTEL"),
                ContactRelationship = GetString(row, "CONTACTRELATION"),
                MailAddress = GetString(row, "MAILADDRESS"),
                LiveAddress = GetString(row, "LIVEADDRESS"),
                IsSameAddress = IsTruthy(GetString(row, "ISSAMEADDRESS")),
                IsVip = IsTruthy(GetString(row, "IS_VIP")),
                IsEmployeeFamily = IsTruthy(GetString(row, "IS_FAMILY")),
                BranchFactory = GetString(row, "BranchFactory"),
                EmployeeStudentNo = GetString(row, "EmployeeStudentNo"),
                CustomerNo = GetString(row, "CUSTOMERNO"),
                FamilyNote = noteRow is null ? "" : GetString(noteRow, "FAMILYNOTE"),
                NotificationNote = noteRow is null ? "" : GetString(noteRow, "NOTICENOTE"),
                CurrentNote = noteRow is null ? "" : GetString(noteRow, "CURRENTNOTE"),
                PermanentNote = noteRow is null ? "" : GetString(noteRow, "PERMANENTNOTE"),
                PackageAmount = paymentRow is null ? "" : GetAmountInputString(paymentRow, "PACKAGE_AMOUNT"),
                ExtraAmount = paymentRow is null ? "" : GetAmountInputString(paymentRow, "EXTRA_AMOUNT"),
                RemitAmount = paymentRow is null ? "" : GetAmountInputString(paymentRow, "REMIT_AMOUNT"),
                DiscountAmount = paymentRow is null ? "" : GetAmountInputString(paymentRow, "DISCOUNT_AMOUNT"),
                AccountAmount = paymentRow is null ? "" : GetAmountInputString(paymentRow, "ACCOUNT_AMOUNT"),
                ReceivedAmount = paymentRow is null ? "" : GetAmountInputString(paymentRow, "RECEIVED_AMOUNT"),
                PaymentDate = paymentRow is null ? "" : GetDateInputString(paymentRow, "PAYMENT_DATE"),
                PaymentMethod = paymentRow is null ? "" : GetString(paymentRow, "PAYMENT_METHOD"),
                PaymentNote = paymentRow is null ? "" : GetString(paymentRow, "NOTE"),
                Packages = packages,
                OtherSuites = otherSuites,
                Questionnaires = questionnaires
            };
        }

        private static IReadOnlyList<PackageOptionViewModel> CreatePackageItemsFromSetInfo(
            DataTable dataTable,
            string projectNo)
        {
            var projectPackages = GetPersonalProjectPackagesByProjectNo(projectNo).ToList();

            return dataTable.Rows
                .Cast<DataRow>()
                .Select((row, index) =>
                {
                    var itemNo = GetString(row, "ITEMNO");
                    var lisItemName = GetString(row, "LISITEMNAME");
                    var orderTotal = GetInt32(row, "ORDTOT");
                    var setName = GetString(row, "SETNAME");
                    var amount = GetDecimal(row, 0m, "AMOUNT");
                    var persistedAccountingUnit = NormalizeText(GetString(row, "CHDCTTYPE"));
                    var persistedAccountingUnitDisplay = GetPersonalSelfPayAccountingUnitDisplay(persistedAccountingUnit);
                    var matchedPackage = FindMatchingPackage(projectPackages, lisItemName, setName, amount);

                    if (matchedPackage is not null)
                    {
                        projectPackages.Remove(matchedPackage);

                        return new PackageOptionViewModel
                        {
                            Id = matchedPackage.Id,
                            LisItemName = matchedPackage.LisItemName,
                            OrderTotal = orderTotal,
                            Level = matchedPackage.Level,
                            Name = matchedPackage.Name,
                            SelfPayAccountingUnit = FirstNonEmpty(persistedAccountingUnit, matchedPackage.SelfPayAccountingUnit),
                            SelfPayAccountingUnitDisplay = FirstNonEmpty(persistedAccountingUnitDisplay, matchedPackage.SelfPayAccountingUnitDisplay),
                            Price = matchedPackage.Price,
                            Orders = matchedPackage.Orders,
                            IsSelected = true
                        };
                    }

                    return new PackageOptionViewModel
                    {
                        Id = PersistedPackageIdPrefix + (index + 1).ToString(CultureInfo.InvariantCulture),
                        LisItemName = lisItemName,
                        OrderTotal = orderTotal,
                        Name = FirstNonEmpty(setName, itemNo),
                        SelfPayAccountingUnit = persistedAccountingUnit,
                        SelfPayAccountingUnitDisplay = persistedAccountingUnitDisplay,
                        Price = amount,
                        IsSelected = true
                    };
                })
                .Where(package => !string.IsNullOrWhiteSpace(package.Name))
                .ToArray();
        }

        private static PackageOptionViewModel? FindMatchingPackage(
            IReadOnlyList<PackageOptionViewModel> packages,
            string lisItemName,
            string setName,
            decimal amount)
        {
            var normalizedLisItemName = NormalizeText(lisItemName);

            if (!string.IsNullOrWhiteSpace(normalizedLisItemName))
            {
                var codeMatch = packages.FirstOrDefault(package =>
                    string.Equals(
                        NormalizeText(package.LisItemName),
                        normalizedLisItemName,
                        StringComparison.OrdinalIgnoreCase));

                if (codeMatch is not null)
                {
                    return codeMatch;
                }
            }

            var normalizedSetName = NormalizeText(setName);

            if (string.IsNullOrWhiteSpace(normalizedSetName))
            {
                return null;
            }

            return packages.FirstOrDefault(package =>
                    NormalizeText(package.Name) == normalizedSetName
                    && package.Price == amount)
                ?? packages.FirstOrDefault(package => NormalizeText(package.Name) == normalizedSetName);
        }

        private static IReadOnlyList<OtherSuiteLookupItemViewModel> CreateOtherSuiteItemsFromOtherSet(
            DataTable dataTable,
            string projectNo)
        {
            return dataTable.Rows
                .Cast<DataRow>()
                .Select((row, index) =>
                {
                    var itemNo = GetString(row, "ITEMNO");
                    var setCode = GetString(row, "SETCODE");
                    var setName = GetString(row, "SETNAME");
                    var accountingUnit = FirstNonEmpty(
                        NormalizeText(GetString(row, "CHDCTTYPE")),
                        DefaultSelfPayAccountingUnit);
                    var itemKey = FirstNonEmpty(
                        setCode,
                        itemNo,
                        setName,
                        (index + 1).ToString(CultureInfo.InvariantCulture));

                    return new OtherSuiteLookupItemViewModel
                    {
                        Id = projectNo + "|" + itemKey,
                        ProjectNo = projectNo,
                        Name = FirstNonEmpty(setName, setCode, itemNo),
                        Code = setCode,
                        SelfPayAccountingUnit = accountingUnit,
                        SelfPayAccountingUnitDisplay = GetPersonalSelfPayAccountingUnitDisplay(accountingUnit),
                        Price = GetDecimal(row, 0m, "AMOUNT")
                    };
                })
                .Where(suite => !string.IsNullOrWhiteSpace(suite.Name))
                .ToArray();
        }

        private static IReadOnlyList<QuestionnaireOptionViewModel> CreateQuestionnaireItemsFromQuestInfo(
            DataTable dataTable)
        {
            var questionnaires = GetQuestionnaireOptions().ToList();

            return dataTable.Rows
                .Cast<DataRow>()
                .Select((row, index) =>
                {
                    var questionnaireName = GetString(row, "QUESTIONNAIRE");
                    var amount = GetDecimal(row, 0m, "AMOUNT");
                    var matchedQuestionnaire = FindMatchingQuestionnaire(questionnaires, questionnaireName, amount);

                    if (matchedQuestionnaire is not null)
                    {
                        return new QuestionnaireOptionViewModel
                        {
                            Id = matchedQuestionnaire.Id,
                            Name = matchedQuestionnaire.Name,
                            FirstChar = matchedQuestionnaire.FirstChar,
                            Price = matchedQuestionnaire.Price,
                            IsSelected = true
                        };
                    }

                    return new QuestionnaireOptionViewModel
                    {
                        Id = PersistedQuestionnaireIdPrefix + (index + 1).ToString(CultureInfo.InvariantCulture),
                        Name = questionnaireName,
                        FirstChar = GetQuestionnaireFirstChar(questionnaireName),
                        Price = amount,
                        IsSelected = true
                    };
                })
                .Where(questionnaire => !string.IsNullOrWhiteSpace(questionnaire.Name))
                .ToArray();
        }

        private static QuestionnaireOptionViewModel? FindMatchingQuestionnaire(
            IReadOnlyList<QuestionnaireOptionViewModel> questionnaires,
            string questionnaireName,
            decimal amount)
        {
            var normalizedQuestionnaireName = NormalizeText(questionnaireName);

            if (string.IsNullOrWhiteSpace(normalizedQuestionnaireName))
            {
                return null;
            }

            return questionnaires.FirstOrDefault(questionnaire =>
                    NormalizeText(questionnaire.Name) == normalizedQuestionnaireName
                    && questionnaire.Price == amount)
                ?? questionnaires.FirstOrDefault(questionnaire =>
                    NormalizeText(questionnaire.Name) == normalizedQuestionnaireName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAppointment([FromForm] CreateAppointmentPostModel input)
        {
            var normalizedReserveStatus = NormalizeCreateAppointmentReserveStatus(input.ReserveStatus);

            if (string.IsNullOrWhiteSpace(normalizedReserveStatus))
            {
                return CreateAppointmentErrorPage(
                    StatusCodes.Status400BadRequest,
                    "預約狀態只能是確認預約或佔名額。");
            }

            var requiresCompleteAppointment = normalizedReserveStatus != "2";

            if (requiresCompleteAppointment
                && (string.IsNullOrWhiteSpace(input.PatientHealthCode)
                    || string.IsNullOrWhiteSpace(input.ProjectNo)
                    || string.IsNullOrWhiteSpace(input.PatientName)
                    || string.IsNullOrWhiteSpace(input.ReservationArea)
                    || input.ReservationDate is null))
            {
                return CreateAppointmentErrorPage(
                    StatusCodes.Status400BadRequest,
                    "請確認健檢碼、健檢專案、姓名、預約區域與預約日期。");
            }

            if (input.IsEditMode)
            {
                if (string.IsNullOrWhiteSpace(input.OriginalPatientHealthCode)
                    || string.IsNullOrWhiteSpace(input.OriginalProjectNo))
                {
                    return CreateAppointmentErrorPage(
                        StatusCodes.Status400BadRequest,
                        "缺少原預約健檢碼或專案編號，無法更新預約資料。");
                }

                return UpdateCreateAppointment(input, normalizedReserveStatus);
            }

            var insertSql = OracleSql.INSERT.InsertHealthCheckupList();
            var insertNoteSql = OracleSql.INSERT.InsertHealthCheckupNote();
            var insertReserInfoSql = OracleSql.INSERT.InsertHealthCheckupReserInfo();
            var insertSetInfoSql = OracleSql.INSERT.InsertHealthCheckupSetInfo();
            var insertOtherSetSql = OracleSql.INSERT.InsertHealthCheckupOtherSet();
            var insertQuestInfoSql = OracleSql.INSERT.InsertHealthCheckupQuestInfo();
            var insertPaymentSql = OracleSql.INSERT.InsertHealthCheckupPayment();
            var upsertPatientSql = OracleSql.MERGE.UpsertHealthCheckupPatient();
            var selectedPackageIds = GetSelectedPackageIds(input);
            var selectedOtherSuiteIds = GetSelectedOtherSuiteIds(input);
            var selectedQuestionnaireIds = GetSelectedQuestionnaireIds(input);

            if (requiresCompleteAppointment
                && selectedPackageIds.Count == 0
                && selectedOtherSuiteIds.Count == 0)
            {
                return CreateAppointmentErrorPage(
                    StatusCodes.Status400BadRequest,
                    "請至少選擇一個有效的套餐 / 套組。");
            }

            try
            {
                var selectedPackages = ResolveSelectedPackages(
                    input.ProjectNo,
                    selectedPackageIds,
                    input.SelectedPackageNames,
                    input.SelectedPackageAmounts,
                    input.SelectedPackageSelfPayAccountingUnits);
                var selectedOtherSuites = ResolveSelectedOtherSuites(
                    selectedOtherSuiteIds,
                    input.SelectedOtherSuiteSelfPayAccountingUnits);
                var selectedQuestionnaires = ResolveSelectedQuestionnaires(
                    selectedQuestionnaireIds,
                    input.SelectedQuestionnaireNames,
                    input.SelectedQuestionnaireAmounts);

                if (requiresCompleteAppointment
                    && selectedPackages.Count != selectedPackageIds.Count)
                {
                    return CreateAppointmentErrorPage(
                        StatusCodes.Status400BadRequest,
                        "請至少選擇一個有效的套餐 / 套組。",
                        "送出的套組代碼無法對應目前專案套組資料。");
                }

                if (requiresCompleteAppointment
                    && selectedOtherSuites.Count != selectedOtherSuiteIds.Count)
                {
                    return CreateAppointmentErrorPage(
                        StatusCodes.Status400BadRequest,
                        "請確認其他組套選項。",
                        "送出的其他組套代碼無法對應目前其他組套資料。");
                }

                if (requiresCompleteAppointment
                    && selectedQuestionnaires.Count != selectedQuestionnaireIds.Count)
                {
                    return CreateAppointmentErrorPage(
                        StatusCodes.Status400BadRequest,
                        "請確認問卷選項。",
                        "送出的問卷代碼無法對應目前問卷資料。");
                }

                var parameters = CreateHealthCheckupListParameters(input, normalizedReserveStatus);
                var noteParameters = CreateHealthCheckupNoteParameters(input);
                var reserInfoParameters = CreateHealthCheckupReserInfoParameters(input);
                var paymentParameters = CreateHealthCheckupPaymentParameters(
                    input,
                    selectedPackages,
                    selectedOtherSuites);
                var commands = new List<OracleTransactionCommand>
                {
                    new(insertSql, parameters),
                    new(insertNoteSql, noteParameters),
                    new(insertReserInfoSql, reserInfoParameters),
                    new(insertPaymentSql, paymentParameters)
                };

                commands.AddRange(selectedPackages.Select((package, index) => new OracleTransactionCommand(
                    insertSetInfoSql,
                    CreateHealthCheckupSetInfoParameters(input, package, index + 1))));

                commands.AddRange(selectedOtherSuites.Select((otherSuite, index) => new OracleTransactionCommand(
                    insertOtherSetSql,
                    CreateHealthCheckupOtherSetParameters(input, otherSuite, index + 1))));

                commands.AddRange(selectedQuestionnaires.Select((questionnaire, index) => new OracleTransactionCommand(
                    insertQuestInfoSql,
                    CreateHealthCheckupQuestInfoParameters(input, questionnaire, index + 1))));

                commands.Add(new OracleTransactionCommand(
                    upsertPatientSql,
                    CreateHealthCheckupPatientParameters(input),
                    allowZeroAffectedRows: true));

                var insertedCounts = string.IsNullOrWhiteSpace(insertSql)
                        || string.IsNullOrWhiteSpace(insertNoteSql)
                        || string.IsNullOrWhiteSpace(insertReserInfoSql)
                        || string.IsNullOrWhiteSpace(insertSetInfoSql)
                        || string.IsNullOrWhiteSpace(insertOtherSetSql)
                        || (selectedQuestionnaires.Count > 0 && string.IsNullOrWhiteSpace(insertQuestInfoSql))
                        || string.IsNullOrWhiteSpace(insertPaymentSql)
                        || string.IsNullOrWhiteSpace(upsertPatientSql)
                    ? Array.Empty<int>()
                    : /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQueryTransaction(
                        commands,
                        "",
                        true);
                var insertedCount = insertedCounts.Length > 0 ? insertedCounts[0] : 0;
                var noteInsertedCount = insertedCounts.Length > 1 ? insertedCounts[1] : 0;
                var reserInfoInsertedCount = insertedCounts.Length > 2 ? insertedCounts[2] : 0;
                var paymentInsertedCount = insertedCounts.Length > 3 ? insertedCounts[3] : 0;
                var setInfoInsertedCount = insertedCounts.Skip(4).Take(selectedPackages.Count).Sum();
                var otherSetInsertedCount = insertedCounts
                    .Skip(4 + selectedPackages.Count)
                    .Take(selectedOtherSuites.Count)
                    .Sum();
                var questInfoInsertedCount = insertedCounts
                    .Skip(4 + selectedPackages.Count + selectedOtherSuites.Count)
                    .Take(selectedQuestionnaires.Count)
                    .Sum();
                var patientCommandIndex = 4
                    + selectedPackages.Count
                    + selectedOtherSuites.Count
                    + selectedQuestionnaires.Count;
                var patientSavedCount = insertedCounts.Length > patientCommandIndex
                    ? insertedCounts[patientCommandIndex]
                    : 0;
                var saved = insertedCount >= 1
                    && noteInsertedCount >= 1
                    && reserInfoInsertedCount >= 1
                    && paymentInsertedCount >= 1
                    && setInfoInsertedCount == selectedPackages.Count
                    && otherSetInsertedCount == selectedOtherSuites.Count
                    && questInfoInsertedCount == selectedQuestionnaires.Count;

                if (!saved)
                {
                    return CreateAppointmentErrorPage(
                        StatusCodes.Status500InternalServerError,
                        "預約資料、備註、預約資訊、費用、套組、其他組套或問卷資料未寫入，已取消本次交易。",
                        $"HealthCheckupList={insertedCount}, HealthCheckupNOTE={noteInsertedCount}, HealthCheckupReserInfo={reserInfoInsertedCount}, HealthCheckupPayment={paymentInsertedCount}, HealthCheckupSetInfo={setInfoInsertedCount}, HealthCheckupOtherSet={otherSetInsertedCount}, HealthCheckupQuestInfo={questInfoInsertedCount}, HealthCheckupPatient={patientSavedCount}");
                }

                return Json(new
                {
                    saved,
                    insertedCount,
                    noteInsertedCount,
                    reserInfoInsertedCount,
                    paymentInsertedCount,
                    setInfoInsertedCount,
                    otherSetInsertedCount,
                    questInfoInsertedCount,
                    patientSavedCount,
                    message = "預約資料、病人資料、備註、預約資訊、費用、套組、其他組套與問卷資料已建立。",
                    reserveStatus = normalizedReserveStatus,
                    status = ResolveAppointmentStatus(normalizedReserveStatus),
                    barcode = NormalizeText(input.PatientHealthCode),
                    projectNo = NormalizeText(input.ProjectNo),
                    patientName = NormalizeText(input.PatientName)
                });
            }
            catch (Exception ex)
            {
                return CreateAppointmentErrorPage(
                    StatusCodes.Status500InternalServerError,
                    "預約資料、備註、預約資訊、費用、套組、其他組套或問卷資料寫入失敗，已取消本次交易。",
                    ex.Message);
            }
        }

        private IActionResult UpdateCreateAppointment(
            CreateAppointmentPostModel input,
            string normalizedReserveStatus)
        {
            var requiresCompleteAppointment = normalizedReserveStatus != "2";
            var updateSql = OracleSql.UPDATE.UPDATEHealthCheckupList();
            var updateParameters = CreateHealthCheckupListUpdateParameters(input, normalizedReserveStatus);
            var updateNoteSql = OracleSql.MERGE.UPDATEHealthCheckupNote();
            var updateNoteParameters = CreateHealthCheckupNoteParameters(input);
            var updateReserInfoSql = OracleSql.MERGE.UPDATEHealthCheckupReserInfo();
            var updateReserInfoParameters = CreateHealthCheckupReserInfoParameters(input);
            var updatePaymentSql = OracleSql.MERGE.UPDATEHealthCheckupPayment();
            var deleteSetInfoSql = OracleSql.DELETE.DELETEHealthCheckupSetInfo();
            var deleteOtherSetSql = OracleSql.DELETE.DELETEHealthCheckupOtherSet();
            var deleteQuestInfoSql = OracleSql.DELETE.DELETEHealthCheckupQuestInfo();
            var insertSetInfoSql = OracleSql.INSERT.InsertHealthCheckupSetInfo();
            var insertOtherSetSql = OracleSql.INSERT.InsertHealthCheckupOtherSet();
            var insertQuestInfoSql = OracleSql.INSERT.InsertHealthCheckupQuestInfo();
            var upsertPatientSql = OracleSql.MERGE.UpsertHealthCheckupPatient();
            var selectedPackageIds = GetSelectedPackageIds(input);
            var selectedOtherSuiteIds = GetSelectedOtherSuiteIds(input);
            var selectedQuestionnaireIds = GetSelectedQuestionnaireIds(input);

            if (requiresCompleteAppointment
                && selectedPackageIds.Count == 0
                && selectedOtherSuiteIds.Count == 0)
            {
                return CreateAppointmentErrorPage(
                    StatusCodes.Status400BadRequest,
                    "請至少選擇一個有效的套餐 / 套組。");
            }

            try
            {
                var selectedPackages = ResolveSelectedPackages(
                    input.ProjectNo,
                    selectedPackageIds,
                    input.SelectedPackageNames,
                    input.SelectedPackageAmounts,
                    input.SelectedPackageSelfPayAccountingUnits);
                var persistedOtherSuitesById = GetPersistedOtherSuiteFallbackMap(input);
                var persistedQuestionnairesById = GetPersistedQuestionnaireFallbackMap(input);
                var selectedOtherSuites = ResolveSelectedOtherSuites(
                    selectedOtherSuiteIds,
                    input.SelectedOtherSuiteSelfPayAccountingUnits,
                    persistedOtherSuitesById);
                var selectedQuestionnaires = ResolveSelectedQuestionnaires(
                    selectedQuestionnaireIds,
                    input.SelectedQuestionnaireNames,
                    input.SelectedQuestionnaireAmounts,
                    persistedQuestionnairesById);

                if (requiresCompleteAppointment
                    && selectedPackages.Count != selectedPackageIds.Count)
                {
                    return CreateAppointmentErrorPage(
                        StatusCodes.Status400BadRequest,
                        "請至少選擇一個有效的套餐 / 套組。",
                        "送出的套組代碼無法對應目前專案套組資料。");
                }

                if (requiresCompleteAppointment
                    && selectedOtherSuites.Count != selectedOtherSuiteIds.Count)
                {
                    return CreateAppointmentErrorPage(
                        StatusCodes.Status400BadRequest,
                        "請確認其他組套選項。",
                        "送出的其他組套代碼無法對應目前其他組套資料。");
                }

                if (requiresCompleteAppointment
                    && selectedQuestionnaires.Count != selectedQuestionnaireIds.Count)
                {
                    return CreateAppointmentErrorPage(
                        StatusCodes.Status400BadRequest,
                        "請確認問卷選項。",
                        "送出的問卷代碼無法對應目前問卷資料。");
                }

                var updatePaymentParameters = CreateHealthCheckupPaymentParameters(input, selectedPackages, selectedOtherSuites);
                var appointmentKeyParameters = CreateHealthCheckupAppointmentKeyParameters(input);

                var updateCommands = new List<OracleTransactionCommand>
                {
                    new(updateSql, updateParameters),
                    new(updateNoteSql, updateNoteParameters),
                    new(updateReserInfoSql, updateReserInfoParameters),
                    new(updatePaymentSql, updatePaymentParameters),
                    new(deleteSetInfoSql, appointmentKeyParameters, allowZeroAffectedRows: true),
                    new(deleteOtherSetSql, appointmentKeyParameters, allowZeroAffectedRows: true),
                    new(deleteQuestInfoSql, appointmentKeyParameters, allowZeroAffectedRows: true)
                };

                updateCommands.AddRange(selectedPackages.Select((package, index) => new OracleTransactionCommand(
                    insertSetInfoSql,
                    CreateHealthCheckupSetInfoParameters(input, package, index + 1))));

                updateCommands.AddRange(selectedOtherSuites.Select((otherSuite, index) => new OracleTransactionCommand(
                    insertOtherSetSql,
                    CreateHealthCheckupOtherSetParameters(input, otherSuite, index + 1))));

                updateCommands.AddRange(selectedQuestionnaires.Select((questionnaire, index) => new OracleTransactionCommand(
                    insertQuestInfoSql,
                    CreateHealthCheckupQuestInfoParameters(input, questionnaire, index + 1))));

                updateCommands.Add(new OracleTransactionCommand(
                    upsertPatientSql,
                    CreateHealthCheckupPatientParameters(input),
                    allowZeroAffectedRows: true));

                var commands = updateCommands.ToArray();

                var updatedCounts = commands.Any(command => string.IsNullOrWhiteSpace(command.Sql))
                    ? Array.Empty<int>()
                    : /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().ExecuteNonQueryTransaction(
                        commands,
                        "",
                        true);
                var updatedCount = updatedCounts.Length > 0 ? updatedCounts[0] : 0;
                var noteUpdatedCount = updatedCounts.Length > 1 ? updatedCounts[1] : 0;
                var reserInfoUpdatedCount = updatedCounts.Length > 2 ? updatedCounts[2] : 0;
                var paymentUpdatedCount = updatedCounts.Length > 3 ? updatedCounts[3] : 0;
                var setInfoDeletedCount = updatedCounts.Length > 4 ? updatedCounts[4] : 0;
                var otherSetDeletedCount = updatedCounts.Length > 5 ? updatedCounts[5] : 0;
                var questInfoDeletedCount = updatedCounts.Length > 6 ? updatedCounts[6] : 0;
                var setInfoInsertedCount = updatedCounts
                    .Skip(7)
                    .Take(selectedPackages.Count)
                    .Sum();
                var otherSetInsertedCount = updatedCounts
                    .Skip(7 + selectedPackages.Count)
                    .Take(selectedOtherSuites.Count)
                    .Sum();
                var questInfoInsertedCount = updatedCounts
                    .Skip(7 + selectedPackages.Count + selectedOtherSuites.Count)
                    .Take(selectedQuestionnaires.Count)
                    .Sum();
                var patientCommandIndex = 7
                    + selectedPackages.Count
                    + selectedOtherSuites.Count
                    + selectedQuestionnaires.Count;
                var patientSavedCount = updatedCounts.Length > patientCommandIndex
                    ? updatedCounts[patientCommandIndex]
                    : 0;

                if (updatedCount < 1
                    || noteUpdatedCount < 1
                    || reserInfoUpdatedCount < 1
                    || paymentUpdatedCount < 1
                    || setInfoInsertedCount != selectedPackages.Count
                    || otherSetInsertedCount != selectedOtherSuites.Count
                    || questInfoInsertedCount != selectedQuestionnaires.Count)
                {
                    return CreateAppointmentErrorPage(
                        StatusCodes.Status500InternalServerError,
                        "預約資料、備註、預約資訊、費用、套組、其他組套或問卷資料未更新，已取消本次交易。",
                        $"HealthCheckupList={updatedCount}, HealthCheckupNOTE={noteUpdatedCount}, HealthCheckupReserInfo={reserInfoUpdatedCount}, HealthCheckupPayment={paymentUpdatedCount}, HealthCheckupSetInfoDelete={setInfoDeletedCount}, HealthCheckupSetInfo={setInfoInsertedCount}, HealthCheckupOtherSetDelete={otherSetDeletedCount}, HealthCheckupOtherSet={otherSetInsertedCount}, HealthCheckupQuestInfoDelete={questInfoDeletedCount}, HealthCheckupQuestInfo={questInfoInsertedCount}");
                }

                return Json(new
                {
                    saved = true,
                    updated = true,
                    updatedCount,
                    noteUpdatedCount,
                    reserInfoUpdatedCount,
                    paymentUpdatedCount,
                    setInfoDeletedCount,
                    setInfoInsertedCount,
                    otherSetDeletedCount,
                    otherSetInsertedCount,
                    questInfoDeletedCount,
                    questInfoInsertedCount,
                    patientSavedCount,
                    message = "預約資料、病人資料、套組、其他組套與問卷資料已更新。",
                    reserveStatus = normalizedReserveStatus,
                    status = ResolveAppointmentStatus(normalizedReserveStatus),
                    barcode = NormalizeText(input.PatientHealthCode),
                    projectNo = NormalizeText(input.ProjectNo),
                    patientName = NormalizeText(input.PatientName)
                });
            }
            catch (Exception ex)
            {
                return CreateAppointmentErrorPage(
                    StatusCodes.Status500InternalServerError,
                    "預約資料更新失敗，已取消本次交易。",
                    ex.Message);
            }
        }

        [HttpGet]
        public IActionResult GetPersonalProjects(string? checkDateStart, string? checkDateEnd, CancellationToken cancellationToken)
        {
            var today = DateTime.Today;
            var selectedStartDate = ParseAppointmentQueryDate(checkDateStart) ?? today.AddYears(-2);
            var selectedEndDate = ParseAppointmentQueryDate(checkDateEnd) ?? today.AddYears(2);

            if (selectedEndDate < selectedStartDate)
            {
                (selectedStartDate, selectedEndDate) = (selectedEndDate, selectedStartDate);
            }

            try
            {
                var projects = GetProjectLookupItems(
                    OracleSql.SELECT.getHealthProjectList(selectedStartDate, selectedEndDate),
                    new Dictionary<string, object?>
                    {
                        ["QUERY_START_DATE"] = selectedStartDate.Date,
                        ["QUERY_END_DATE"] = selectedEndDate.Date
                    },
                    cancellationToken);

                return Json(new
                {
                    checkDateStart = selectedStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    checkDateEnd = selectedEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    projects = projects.Select(project => new
                    {
                        projectNo = project.ProjectNo,
                        customerNo = project.CustomerNo,
                        customerName = project.CustomerName,
                        projectName = project.ProjectName,
                        selfPayAccountingUnit = project.SelfPayAccountingUnit,
                        checkDate = project.CheckDate,
                        endDate = project.EndDate
                    }).ToArray()
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    projects = Array.Empty<object>(),
                    message = "專案資料查詢失敗。"
                });
            }
        }

        [HttpGet]
        public IActionResult GetPersonalProjectPackages(string? projectNo)
        {
            var normalizedProjectNo = (projectNo ?? "").Trim();

            if (string.IsNullOrWhiteSpace(normalizedProjectNo))
            {
                return Json(new
                {
                    packages = Array.Empty<object>(),
                    questionnaires = Array.Empty<object>()
                });
            }

            try
            {
                var packages = GetPersonalProjectPackagesByProjectNo(normalizedProjectNo)
                    .Select(package => new
                    {
                        id = package.Id,
                        lisItemName = package.LisItemName,
                        orderTotal = package.OrderTotal,
                        name = package.Name,
                        selfPayAccountingUnit = package.SelfPayAccountingUnit,
                        selfPayAccountingUnitDisplay = package.SelfPayAccountingUnitDisplay,
                        price = package.Price,
                        priceText = package.PriceText,
                        orders = package.Orders.Select(order => new
                        {
                            code = order.Code,
                            name = order.Name,
                            category = order.Category
                        }).ToArray()
                    })
                    .ToArray();
                var questionnaires = GetPersonalProjectQuestionnairesByProjectNo(normalizedProjectNo)
                    .Select(questionnaire => new
                    {
                        id = questionnaire.Id,
                        name = questionnaire.Name,
                        firstChar = questionnaire.FirstChar,
                        price = questionnaire.Price,
                        priceText = questionnaire.PriceText
                    })
                    .ToArray();

                return Json(new { packages, questionnaires });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    packages = Array.Empty<object>(),
                    questionnaires = Array.Empty<object>(),
                    message = "套組與問卷資料載入失敗。"
                });
            }
        }

        [HttpGet]
        public IActionResult GetPersonalHealthCode()
        {
            try
            {
                var sql = OracleSql.SELECT.getSYSTIMESTAMP();
                var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);
                var timestamp = dataTable.Rows.Count > 0
                    ? FirstNonEmpty(
                        GetString(dataTable.Rows[0], "dt_ms"),
                        GetString(dataTable.Rows[0], "DT_MS"))
                    : "";

                if (string.IsNullOrWhiteSpace(timestamp))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        healthCode = "",
                        message = "健檢碼產生失敗。"
                    });
                }

                return Json(new { healthCode = "BR" + timestamp });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    healthCode = "",
                    message = "健檢碼產生失敗。"
                });
            }
        }

        public IActionResult OtherSuiteLookupFrame(string? keyword, string? projectNo, string? projectName)
        {
            var projects = GetOtherSuiteProjects();
            var selectedProject = projects.FirstOrDefault(project =>
                string.Equals(project.ProjectNo, projectNo?.Trim(), StringComparison.Ordinal));

            if (selectedProject is null && !string.IsNullOrWhiteSpace(projectName))
            {
                selectedProject = projects.FirstOrDefault(project =>
                    string.Equals(project.Name, projectName.Trim(), StringComparison.Ordinal));
            }

            selectedProject ??= projects.FirstOrDefault();
            var selectedProjectNo = selectedProject?.ProjectNo ?? "";
            var selectedProjectName = selectedProject?.Name ?? "";
            var suites = GetOtherSuiteLookupItemsByProjectNo(selectedProjectNo, selectedProjectName);

            var model = new OtherSuiteLookupFrameViewModel
            {
                InitialKeyword = keyword?.Trim() ?? "",
                SelectedProjectNo = selectedProjectNo,
                SelectedProjectName = selectedProjectName,
                Projects = projects
                    .Select(project => new OtherSuiteProjectViewModel
                    {
                        ProjectNo = project.ProjectNo,
                        Name = project.Name,
                        IsSelected = string.Equals(project.ProjectNo, selectedProjectNo, StringComparison.Ordinal)
                    })
                    .ToArray(),
                Suites = suites
            };

            return View(model);
        }

        private string SaveGroupRosterImport(
            IReadOnlyList<GroupRosterMemberViewModel> members,
            string uploadedFileName,
            long uploadedFileSize,
            string importMessage)
        {
            var rosterKey = Guid.NewGuid().ToString("N");
            var cacheEntry = new CachedGroupRosterImport
            {
                Members = members.ToArray(),
                UploadedFileName = uploadedFileName,
                UploadedFileSize = uploadedFileSize,
                ImportMessage = importMessage
            };

            _cache.Set(
                GetGroupRosterCacheKey(rosterKey),
                cacheEntry,
                new MemoryCacheEntryOptions
                {
                    SlidingExpiration = GroupRosterCacheSlidingExpiration,
                    AbsoluteExpirationRelativeToNow = GroupRosterCacheAbsoluteExpiration
                });

            return rosterKey;
        }

        private bool TryGetGroupRosterImport(string? rosterKey, out CachedGroupRosterImport? cachedImport)
        {
            cachedImport = null;
            var normalizedRosterKey = NormalizeText(rosterKey);

            if (string.IsNullOrWhiteSpace(normalizedRosterKey))
            {
                return false;
            }

            return _cache.TryGetValue(GetGroupRosterCacheKey(normalizedRosterKey), out cachedImport) && cachedImport is not null;
        }

        private static string GetGroupRosterCacheKey(string rosterKey) =>
            GroupRosterCacheKeyPrefix + rosterKey;

        private sealed class CachedGroupRosterImport
        {
            public IReadOnlyList<GroupRosterMemberViewModel> Members { get; init; } = [];

            public string UploadedFileName { get; init; } = "";

            public long UploadedFileSize { get; init; }

            public string ImportMessage { get; init; } = "";
        }

        private CreateGroupAppointmentViewModel BuildCreateGroupAppointmentModel(
            IReadOnlyList<GroupRosterMemberViewModel> members,
            string uploadedFileName,
            long uploadedFileSize,
            string importMessage,
            string rosterKey = "")
        {
            var memberArray = members.ToArray();

            return new CreateGroupAppointmentViewModel
            {
                BasicInfo = BuildGroupBasicInfo(memberArray),
                Members = memberArray,
                RosterStats = BuildRosterStats(memberArray),
                ProjectOptions = GetGroupProjectOptions(),
                ProjectLookupItems = GetAllProjectLookupItems(),
                WorkAreaOptions = GetWorkAreaOptions(),
                PackageOptions = GetGroupPackageOptions(),
                UploadedFileName = uploadedFileName,
                UploadedFileSizeText = uploadedFileSize > 0 ? FormatFileSize(uploadedFileSize) : "",
                ImportMessage = importMessage,
                RosterKey = rosterKey
            };
        }

        private static IReadOnlyList<SelectOptionViewModel> GetGroupProjectOptions()
        {
            return
            [
                new SelectOptionViewModel { Value = "", Text = "請選擇健檢專案 / 方案", IsSelected = true }
            ];
        }

        private static GroupAppointmentBasicInfoViewModel BuildGroupBasicInfo(IReadOnlyList<GroupRosterMemberViewModel> members)
        {
            var companyBranch = members.FirstOrDefault()?.CompanyBranch.Trim() ?? "";
            var workAreas = members
                .Select(member => member.WorkArea)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .Take(3)
                .ToArray();

            return new GroupAppointmentBasicInfoViewModel
            {
                CompanyName = companyBranch,
                ContactName = "",
                ContactPhone = "",
                Email = "",
                ReservationDateText = FormatDateRange(members.Select(member => member.CheckDate)),
                Period = "",
                Hospital = workAreas.FirstOrDefault() ?? "",
                EstimateCount = members.Count,
                Note = "依匯入名單建立團體預約與套檢資料。"
            };
        }

        private static IReadOnlyList<RosterStatViewModel> BuildRosterStats(IReadOnlyList<GroupRosterMemberViewModel> members)
        {
            return
            [
                new RosterStatViewModel { Label = "總名單數", Value = members.Count, Tone = "blue", Icon = "file" },
                new RosterStatViewModel { Label = "匯入成功", Value = members.Count(member => member.Tone == "success"), Tone = "green", Icon = "check" },
                new RosterStatViewModel { Label = "待確認", Value = members.Count(member => member.Tone == "pending"), Tone = "orange", Icon = "clock" },
                new RosterStatViewModel { Label = "格式錯誤", Value = members.Count(member => member.Tone == "error"), Tone = "red", Icon = "alert" }
            ];
        }

        private static IReadOnlyList<PackageOptionViewModel> GetGroupPackageOptions()
        {
            return
            [
                new PackageOptionViewModel { Id = "PHC-HI-001", Level = "高階", Name = "頂級安心肺 + 腸舒子彈 + 肝功能全套檢查", Price = 66000, IsSelected = true },
                new PackageOptionViewModel { Id = "PHC-HI-002", Level = "高階", Name = "尊榮全身標準健檢套餐", Price = 48000 },
                new PackageOptionViewModel { Id = "PHC-STP-001", Level = "一般", Name = "標準健檢套餐", Price = 12000 }
            ];
        }

        private string ResolveGroupRosterTemplatePath()
        {
            var contentRootPath = Path.Combine(_environment.ContentRootPath, GroupRosterTemplateFileName);

            if (System.IO.File.Exists(contentRootPath))
            {
                return contentRootPath;
            }

            var parentDirectory = Directory.GetParent(_environment.ContentRootPath)?.FullName ?? _environment.ContentRootPath;
            return Path.Combine(parentDirectory, GroupRosterTemplateFileName);
        }

        private static string FormatDateRange(IEnumerable<string> dateValues)
        {
            var dates = dateValues
                .Select(value => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    ? date
                    : (DateTime?)null)
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .Distinct()
                .OrderBy(date => date)
                .ToArray();

            return dates.Length switch
            {
                0 => "",
                1 => dates[0].ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                _ => dates[0].ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) + " - " + dates[^1].ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
            };
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes.ToString("N0", CultureInfo.InvariantCulture) + " B";
            }

            return (bytes / 1024m).ToString("N1", CultureInfo.InvariantCulture) + " KB";
        }

        private static IReadOnlyList<SelectOptionViewModel> GetPersonalAppointmentProjectOptions()
        {
            var projects = GetProjectLookupItems(OracleSql.SELECT.getHealthProjectListForPersonalAppointment());
            var options = projects
                .Where(project => !string.IsNullOrWhiteSpace(project.ProjectNo))
                .Select(project => new SelectOptionViewModel
                {
                    Value = project.ProjectNo,
                    Text = FirstNonEmpty(project.ProjectName, project.ProjectNo)
                })
                .ToArray();

            return
            [
                new SelectOptionViewModel { Value = "", Text = "請選擇健檢專案 / 方案", IsSelected = true },
                .. options
            ];
        }

        private static IReadOnlyList<PackageOptionViewModel> GetPersonalProjectPackagesByProjectNo(string projectNo)
        {
            if (string.IsNullOrWhiteSpace(projectNo))
            {
                return [];
            }

            var sql = OracleSql.SELECT.getCheckProjectScheme(projectNo);
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);
            var accountingUnitDisplayCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string AccountingUnitDisplay(string code)
            {
                code = NormalizeText(code);

                if (string.IsNullOrWhiteSpace(code))
                {
                    return string.Empty;
                }

                if (!accountingUnitDisplayCache.TryGetValue(code, out var display))
                {
                    display = GetPersonalSelfPayAccountingUnitDisplay(code);
                    accountingUnitDisplayCache[code] = display;
                }

                return display;
            }

            return dataTable.Rows
                .Cast<DataRow>()
                .Select((row, index) =>
                {
                    var selfPayAccountingUnit = NormalizeText(GetString(row, "CHDCTTYPE"));
                    var lisItemName = NormalizeText(GetString(row, "LISITEMNAME"));
                    var schemeNo = NormalizeText(GetString(row, "SCHEMENO"));
                    var packageName = FirstNonEmpty(GetString(row, "NAME"), schemeNo);
                    var fixItems = GetString(row, "FIXITEMS");

                    return new PackageOptionViewModel
                    {
                        Id = FirstNonEmpty(lisItemName, schemeNo, packageName),
                        LisItemName = lisItemName,
                        OrderTotal = CountFixItems(fixItems),
                        Name = packageName,
                        SelfPayAccountingUnit = selfPayAccountingUnit,
                        SelfPayAccountingUnitDisplay = AccountingUnitDisplay(selfPayAccountingUnit),
                        Price = GetDecimal(row, 0m, "PUBLICEXPENSE"),
                        Orders = GetPersonalProjectPackageOrders(fixItems)
                    };
                })
                .Where(package => !string.IsNullOrWhiteSpace(package.Name))
                .ToArray();
        }

        private static IReadOnlyList<SelfPayAccountingUnitViewModel> GetPersonalSelfPayAccountingUnits()
        {
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.getSelfPayAccountingUnits(),
                "",
                false);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select(row => new SelfPayAccountingUnitViewModel
                {
                    Code = NormalizeText(GetString(row, "CHDCTTYPE")),
                    Name = NormalizeText(GetString(row, "CHDCTTYPENAME")),
                    EndDateValue = NormalizeText(GetString(row, "VCHEDATE"))
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Code))
                .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }

        private static string GetPersonalSelfPayAccountingUnitDisplay(string? code)
        {
            code = NormalizeText(code);

            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.getSelfPayAccountingUnitDisplay(),
                new Dictionary<string, object?> { ["SELFCOSTNO"] = code },
                "",
                false);

            var display = dataTable.Rows
                .Cast<DataRow>()
                .Select(row => GetString(row, "DCTTYPE_DISPLAY"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            return FirstNonEmpty(
                display,
                string.Equals(code, DefaultSelfPayAccountingUnit, StringComparison.OrdinalIgnoreCase)
                    ? "[00] 無優待"
                    : code);
        }

        private static IReadOnlyList<PackageOrderOptionViewModel> GetPersonalProjectPackageOrders(string fixItems)
        {
            var checkNos = ParseCheckNoList(fixItems);

            if (checkNos.Count == 0)
            {
                return [];
            }

            var orderLookup = GetPackageOrderLookup(checkNos);

            return checkNos
                .Select(checkNo =>
                {
                    orderLookup.TryGetValue(checkNo, out var order);

                    return new PackageOrderOptionViewModel
                    {
                        Code = checkNo,
                        Name = FirstNonEmpty(order?.Name, checkNo),
                        Category = order?.Category ?? ""
                    };
                })
                .ToArray();
        }

        private static int CountFixItems(string? fixItems) =>
            NormalizeText(fixItems)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Count(item => !string.IsNullOrWhiteSpace(item));

        private static IReadOnlyDictionary<string, PackageOrderOptionViewModel> GetPackageOrderLookup(IReadOnlyList<string> checkNos)
        {
            var normalizedCheckNos = checkNos
                .Select(NormalizeText)
                .Where(checkNo => !string.IsNullOrWhiteSpace(checkNo))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var ordersByCode = new Dictionary<string, PackageOrderOptionViewModel>(StringComparer.OrdinalIgnoreCase);

            AddPackageOrderLookupItems(ordersByCode, GetPackageOrderItemsByCodes(normalizedCheckNos, internalOrders: false));

            var missingCheckNos = normalizedCheckNos
                .Where(checkNo => !ordersByCode.ContainsKey(checkNo))
                .ToArray();

            if (missingCheckNos.Length > 0)
            {
                AddPackageOrderLookupItems(ordersByCode, GetPackageOrderItemsByCodes(missingCheckNos, internalOrders: true));
            }

            return ordersByCode;
        }

        private static IReadOnlyList<PackageOrderOptionViewModel> GetPackageOrderItemsByCodes(
            IReadOnlyList<string> checkNos,
            bool internalOrders)
        {
            if (checkNos.Count == 0)
            {
                return [];
            }

            try
            {
                var sql = internalOrders
                    ? OracleSql.SELECT.getInternalMedicalOrderItemsByCodes(string.Join(",", checkNos))
                    : OracleSql.SELECT.getCheckItemsByCodes(string.Join(",", checkNos));
                var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", !internalOrders);

                return dataTable.Rows
                    .Cast<DataRow>()
                    .Select(row => new PackageOrderOptionViewModel
                    {
                        Code = FirstNonEmpty(GetString(row, "CHECKNO"), GetString(row, "CODE")),
                        Name = FirstNonEmpty(GetString(row, "NAME"), GetString(row, "REPORTNAME"), GetString(row, "CHECKNO")),
                        Category = FirstNonEmpty(GetString(row, "ITEMCATEGORY"), GetString(row, "CHECKCATEGORY"), internalOrders ? "院內組套" : "")
                    })
                    .Where(order => !string.IsNullOrWhiteSpace(order.Code))
                    .ToArray();
            }
            catch
            {
                return [];
            }
        }

        private static void AddPackageOrderLookupItems(
            IDictionary<string, PackageOrderOptionViewModel> ordersByCode,
            IEnumerable<PackageOrderOptionViewModel> orders)
        {
            foreach (var order in orders)
            {
                if (!string.IsNullOrWhiteSpace(order.Code) && !ordersByCode.ContainsKey(order.Code))
                {
                    ordersByCode.Add(order.Code, order);
                }
            }
        }

        private static IReadOnlyList<string> ParseCheckNoList(string? value) =>
            NormalizeText(value)
                .Split([',', ';', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token => NormalizeText(token.Trim('\'', '"', '(', ')')))
                .Where(checkNo => !string.IsNullOrWhiteSpace(checkNo))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        private static IReadOnlyList<QuestionnaireOptionViewModel> GetPersonalProjectQuestionnairesByProjectNo(string projectNo)
        {
            if (string.IsNullOrWhiteSpace(projectNo))
            {
                return [];
            }

            var sql = OracleSql.SELECT.getCheckProjectSpecialItemTbl(projectNo);
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select((row, index) =>
                {
                    var id = FirstNonEmpty(
                        GetString(row, "SPECIALITEMNO"),
                        GetString(row, "CATEGORYNO"),
                        GetString(row, "NAME"),
                        (index + 1).ToString(CultureInfo.InvariantCulture));
                    var name = FirstNonEmpty(GetString(row, "NAME"), id);

                    return new QuestionnaireOptionViewModel
                    {
                        Id = id,
                        Name = name,
                        FirstChar = GetQuestionnaireFirstChar(name),
                        Price = GetDecimal(row, 0m, "PUBLICEXPENSE", "PRICE", "AMOUNT", "FEE"),
                        IsSelected = true
                    };
                })
                .Where(questionnaire => !string.IsNullOrWhiteSpace(questionnaire.Name))
                .GroupBy(questionnaire => NormalizeText(questionnaire.Id), StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
        }
        private static IReadOnlyList<QuestionnaireOptionViewModel> GetQuestionnaireOptions(
            IReadOnlyList<QuestionnaireOptionViewModel>? selectedQuestionnaires = null)
        {
            var selectedItems = selectedQuestionnaires ?? Array.Empty<QuestionnaireOptionViewModel>();
            var selectedIds = selectedItems
                .Select(questionnaire => NormalizeText(questionnaire.Id))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
            var sql = OracleSql.SELECT.getSpecialCategoryTBL();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);
            var options = dataTable.Rows
                .Cast<DataRow>()
                .Select((row, index) =>
                {
                    var name = GetString(row, "NAME");
                    var firstChar = FirstNonEmpty(GetString(row, "FIRSTCHAR"), GetQuestionnaireFirstChar(name));
                    var id = FirstNonEmpty(
                        GetString(row, "CATEGORYNO"),
                        name,
                        (index + 1).ToString(CultureInfo.InvariantCulture));
                    var price = GetDecimal(row, 0m, "PUBLICEXPENSE");

                    return new QuestionnaireOptionViewModel
                    {
                        Id = id,
                        Name = name,
                        FirstChar = firstChar,
                        Price = price,
                        IsSelected = selectedIds.Contains(NormalizeText(id))
                            || selectedItems.Any(selected => IsSameQuestionnaire(selected, id, name, price))
                    };
                })
                .Where(questionnaire => !string.IsNullOrWhiteSpace(questionnaire.Name))
                .ToList();

            foreach (var selectedQuestionnaire in selectedItems)
            {
                if (options.Any(option => IsSameQuestionnaire(selectedQuestionnaire, option.Id, option.Name, option.Price)))
                {
                    continue;
                }

                options.Insert(0, new QuestionnaireOptionViewModel
                {
                    Id = selectedQuestionnaire.Id,
                    Name = selectedQuestionnaire.Name,
                    FirstChar = FirstNonEmpty(selectedQuestionnaire.FirstChar, GetQuestionnaireFirstChar(selectedQuestionnaire.Name)),
                    Price = selectedQuestionnaire.Price,
                    IsSelected = true
                });
            }

            return options;
        }

        private static bool IsSameQuestionnaire(
            QuestionnaireOptionViewModel selectedQuestionnaire,
            string optionId,
            string optionName,
            decimal optionPrice)
        {
            var selectedId = NormalizeText(selectedQuestionnaire.Id);
            var normalizedOptionId = NormalizeText(optionId);
            var selectedName = NormalizeText(selectedQuestionnaire.Name);
            var normalizedOptionName = NormalizeText(optionName);

            return (!string.IsNullOrWhiteSpace(selectedId)
                    && !string.IsNullOrWhiteSpace(normalizedOptionId)
                    && selectedId == normalizedOptionId)
                || (!string.IsNullOrWhiteSpace(selectedName)
                    && selectedName == normalizedOptionName
                    && selectedQuestionnaire.Price == optionPrice)
                || (!string.IsNullOrWhiteSpace(selectedName)
                    && selectedName == normalizedOptionName);
        }

        private static string GetQuestionnaireFirstChar(string name)
        {
            var normalizedName = NormalizeText(name);

            return string.IsNullOrWhiteSpace(normalizedName)
                ? ""
                : new string(normalizedName.Take(1).ToArray());
        }

        private static IReadOnlyList<ProjectLookupItemViewModel> GetAllProjectLookupItems()
        {
            var startDate = DateTime.Today.AddYears(-2);
            var endDate = DateTime.Today.AddYears(2);

            return GetProjectLookupItems(
                OracleSql.SELECT.getHealthProjectList(startDate, endDate),
                new Dictionary<string, object?>
                {
                    ["QUERY_START_DATE"] = startDate,
                    ["QUERY_END_DATE"] = endDate
                });
        }

        private static IReadOnlyList<ProjectLookupItemViewModel> GetProjectLookupItems(
            string sql,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();
            var dataTable = parameters is null
                ? connectionFactory.GETDataTable(sql, "", true)
                : connectionFactory.GETDataTable(
                    sql,
                    parameters,
                    "",
                    true,
                    AppointmentQueryTimeoutSeconds,
                    cancellationToken);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select(row => new ProjectLookupItemViewModel
                {
                    ProjectNo = GetString(row, "PROJECTNO"),
                    CustomerNo = GetString(row, "CUSTOMERNO"),
                    CustomerName = GetString(row, "CUSTOMERNAME"),
                    SelfPayAccountingUnit = GetString(row, "SELFCOSTNO"),
                    CheckYear = GetString(row, "CHECKYEAR"),
                    ProjectName = GetString(row, "PROJECTNAME"),
                    CheckDate = GetDateString(row, "CHECKDATE"),
                    EndDate = GetDateString(row, "ENDDATE"),
                    Note = GetString(row, "NOTE")
                })
                .ToArray();
        }

        private static IReadOnlyList<SelectOptionViewModel> GetWorkAreaOptions(CancellationToken cancellationToken = default)
        {
            var sql = OracleSql.SELECT.getHealthRESERVEAREATBL();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                sql,
                new Dictionary<string, object?>(),
                "",
                true,
                AppointmentQueryTimeoutSeconds,
                cancellationToken);
            var options = dataTable.Rows
                .Cast<DataRow>()
                .Select(row => GetString(row, "AREA_NAME"))
                .Where(areaName => !string.IsNullOrWhiteSpace(areaName))
                .Select(areaName => new SelectOptionViewModel
                {
                    Value = areaName,
                    Text = areaName
                })
                .ToArray();

            return
            [
                new SelectOptionViewModel { Value = "", Text = "請選擇", IsSelected = true },
                .. options
            ];
        }

        private static IReadOnlyList<SelectOptionViewModel> GetConsultingDoctorOptions()
        {
            var sql = OracleSql.SELECT.GetConsultingDoctors();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql);
            var options = dataTable.Rows
                .Cast<DataRow>()
                .Select(row => new SelectOptionViewModel
                {
                    Value = GetString(row, "CHUSERID"),
                    Text = GetString(row, "DOCTOREMP")
                })
                .ToArray();

            return
            [
                new SelectOptionViewModel { Value = "", Text = "請選擇", IsSelected = true },
                .. options
            ];
        }

        private static IReadOnlyList<SelectOptionViewModel> GetDashboardReservationAreaOptions(CancellationToken cancellationToken)
        {
            var table = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetDashboardWorkAreas(), new Dictionary<string, object?>(),
                "", true, AppointmentQueryTimeoutSeconds, cancellationToken);
            return
            [
                new SelectOptionViewModel { Value = "", Text = "全部工作區域", IsSelected = true },
                .. table.Rows.Cast<DataRow>().Select(row => new SelectOptionViewModel
                {
                    Value = GetString(row, "AREA_NAME"),
                    Text = GetString(row, "AREA_NAME")
                })
            ];
        }

        private static IReadOnlyList<SelectOptionViewModel> GetReservationAreaFilterOptions(CancellationToken cancellationToken = default)
        {
            var areaOptions = GetWorkAreaOptions(cancellationToken)
                .Where(option => !string.IsNullOrWhiteSpace(option.Value))
                .Select(option => new SelectOptionViewModel
                {
                    Value = option.Value,
                    Text = option.Text
                })
                .ToArray();

            return
            [
                new SelectOptionViewModel { Value = "", Text = "全部", IsSelected = true },
                .. areaOptions
            ];
        }

        private static IReadOnlyList<SelectOptionViewModel> GetPhraseOptions()
        {
            var sql = OracleSql.SELECT.getHealthPHRASENOTETBL();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select(row => GetString(row, "PHRASE_CONTENT"))
                .Where(phraseContent => !string.IsNullOrWhiteSpace(phraseContent))
                .Select(phraseContent => new SelectOptionViewModel
                {
                    Value = phraseContent,
                    Text = phraseContent
                })
                .ToArray();
        }

        private static IReadOnlyList<OtherSuiteProjectViewModel> GetOtherSuiteProjects()
        {
            var sql = OracleSql.SELECT.getCheckGlobalScheme();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select(row => new OtherSuiteProjectViewModel
                {
                    ProjectNo = GetString(row, "PROJECTNO"),
                    Name = FirstNonEmpty(GetString(row, "PROJECTNAME"), GetString(row, "PROJECTNO"))
                })
                .Where(project => !string.IsNullOrWhiteSpace(project.ProjectNo))
                .GroupBy(project => project.ProjectNo)
                .Select(group => group.First())
                .ToArray();
        }

        private static IReadOnlyList<OtherSuiteLookupItemViewModel> GetOtherSuiteLookupItemsByProjectNo(string projectNo, string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectNo))
            {
                return [];
            }

            var sql = OracleSql.SELECT.getCheckProjectScheme(projectNo);
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select((row, index) =>
                {
                    var schemeNo = GetString(row, "SCHEMENO");
                    var code = GetString(row, "LISITEMNAME");
                    var name = GetString(row, "NAME");
                    var itemKey = FirstNonEmpty(code, schemeNo, name, (index + 1).ToString(CultureInfo.InvariantCulture));

                    return new OtherSuiteLookupItemViewModel
                    {
                        Id = projectNo + "|" + itemKey,
                        ProjectNo = projectNo,
                        ProjectName = projectName,
                        Name = name,
                        Code = code,
                        SelfPayAccountingUnit = DefaultSelfPayAccountingUnit,
                        SelfPayAccountingUnitDisplay = "[00] 無優待",
                        Price = GetDecimal(row, 0m, "PUBLICEXPENSE")
                    };
                })
                .Where(suite => !string.IsNullOrWhiteSpace(suite.Name))
                .ToArray();
        }

        public IActionResult PatientLookupFrame(string? keyword, string? filter)
        {
            var selectedFilter = NormalizePatientLookupFilter(filter);
            var normalizedKeyword = NormalizePatientLookupKeyword(keyword, selectedFilter);
            var model = new PatientLookupFrameViewModel
            {
                InitialKeyword = normalizedKeyword,
                SelectedFilter = selectedFilter,
                Filters = BuildPatientLookupFilters(selectedFilter),
                Patients = GetPatientLookupPatients(normalizedKeyword, selectedFilter)
                    .Where(patient => MatchesPatientLookupKeyword(patient, normalizedKeyword, selectedFilter))
                    .ToArray()
            };

            return View(model);
        }

        private IActionResult CreateAppointmentErrorPage(int statusCode, string reason, string detail = "")
        {
            return RedirectToAction("Error", "Home", new
            {
                statusCode,
                title = GetCreateAppointmentErrorTitle(statusCode),
                reason,
                detail,
                requestPath = Request.Path.ToString()
            });
        }

        private static string GetCreateAppointmentErrorTitle(int statusCode)
        {
            return statusCode switch
            {
                400 => "請求內容有誤",
                500 => "系統發生錯誤",
                _ => statusCode >= 500 ? "系統發生錯誤" : "無法完成這次請求"
            };
        }

        private static IReadOnlyDictionary<string, object?> CreateHealthCheckupListParameters(
            CreateAppointmentPostModel input,
            string reserveStatus)
        {
            var projectNo = NormalizeText(input.ProjectNo);
            var mailAddress = NormalizeText(input.MailAddress);

            return new Dictionary<string, object?>
            {
                ["BARCODESTR"] = DbString(input.PatientHealthCode),
                ["PROJECTNO"] = DbString(projectNo),
                ["RESERVEDATE"] = input.ReservationDate?.Date,
                ["RESERVETIME"] = DbString(input.ReservationTime),
                ["REGTIME"] = DBNull.Value,
                ["DOCTOREMPNO"] = DbString(input.ConsultingDoctorId),
                ["DOCTORNAME"] = DbString(input.ConsultingDoctorName),
                ["ROOMNO"] = DbString(input.ClinicRoom),
                ["PATIENTNAME"] = DbString(input.PatientName),
                ["IDNUMBER"] = DbString(input.PatientIdNo),
                ["BIRTHDAY"] = DbString(input.PatientBirthDate),
                ["AGE"] = DbString(input.PatientAge),
                ["GENDER"] = DbString(input.PatientSex),
                ["TEL"] = DbString(input.PatientPhone),
                ["MOBILE"] = DbString(input.PatientMobile),
                ["EMAIL"] = DbString(input.PatientEmail),
                ["CATEGORY"] = "高檢",
                ["ISRESERVE"] = Convert.ToInt32(reserveStatus, CultureInfo.InvariantCulture),
                ["PATNO"] = DbString(input.PatientChartNo),
                ["WORKAREA"] = DbString(input.ReservationArea),
                ["CONTACTNAME"] = DbString(input.ContactName),
                ["CONTACTTEL"] = DbString(input.ContactPhone),
                ["CONTACTRELATION"] = DbString(input.ContactRelationship),
                ["MAILADDRESS"] = DbString(mailAddress),
                ["SENDINGADDRESS"] = DbString(mailAddress),
                ["LIVEADDRESS"] = DbString(input.LiveAddress),
                ["ISSAMEADDRESS"] = input.IsSameAddress ? 1 : 0,
                ["IS_VIP"] = input.IsVip ? 1 : 0,
                ["IS_FAMILY"] = input.IsEmployeeFamily ? 1 : 0,
                ["BranchFactory"] = DbString(input.BranchFactory),
                ["EmployeeStudentNo"] = DbString(input.EmployeeStudentNo),
                ["CUSTOMERNO"] = DbString(input.CustomerNo),
                ["SELFCOSTNO"] = DbString(input.SelfPayAccountingUnit)
            };
        }

        private static IReadOnlyDictionary<string, object?> CreateHealthCheckupListUpdateParameters(
            CreateAppointmentPostModel input,
            string reserveStatus)
        {
            var parameters = new Dictionary<string, object?>(
                CreateHealthCheckupListParameters(input, reserveStatus),
                StringComparer.Ordinal);

            parameters["ORIGINAL_BARCODESTR"] = DbString(input.OriginalPatientHealthCode);
            parameters["ORIGINAL_PROJECTNO"] = DbString(input.OriginalProjectNo);

            return parameters;
        }

        private static IReadOnlyDictionary<string, object?> CreateHealthCheckupPatientParameters(
            CreateAppointmentPostModel input)
        {
            return new Dictionary<string, object?>
            {
                ["PATNO"] = DbString(input.PatientChartNo),
                ["PATIENTNAME"] = DbString(input.PatientName),
                ["IDNUMBER"] = DbString(input.PatientIdNo),
                ["BIRTHDAY"] = DbString(input.PatientBirthDate),
                ["AGE"] = DbString(input.PatientAge),
                ["MOBILE"] = DbString(input.PatientMobile),
                ["TEL"] = DbString(input.PatientPhone),
                ["GENDER"] = DbString(input.PatientSex),
                ["CONTACTNAME"] = DbString(input.ContactName),
                ["CONTACTTEL"] = DbString(input.ContactPhone),
                ["CONTACTRELATION"] = DbString(input.ContactRelationship),
                ["IS_VIP"] = input.IsVip ? "1" : "0",
                ["FAMILYNOTE"] = DbString(input.FamilyNote),
                ["EMAIL"] = DbString(input.PatientEmail),
                ["SENDINGADDRESS"] = DbString(input.MailAddress),
                ["LIVEADDRESS"] = DbString(input.LiveAddress),
                ["NOTICENOTE"] = DbString(input.NotificationNote)
            };
        }

        private static IReadOnlyDictionary<string, object?> CreateHealthCheckupAppointmentKeyParameters(
            CreateAppointmentPostModel input)
        {
            return new Dictionary<string, object?>
            {
                ["PROJECTNO"] = DbString(input.ProjectNo),
                ["BARCODESTR"] = DbString(input.PatientHealthCode)
            };
        }

        private static IReadOnlyDictionary<string, object?> CreateHealthCheckupNoteParameters(
            CreateAppointmentPostModel input)
        {
            return new Dictionary<string, object?>
            {
                ["BARCODESTR"] = DbString(input.PatientHealthCode),
                ["PROJECTNO"] = DbString(input.ProjectNo),
                ["FAMILYNOTE"] = DbString(input.FamilyNote),
                ["NOTICENOTE"] = DbString(input.NotificationNote),
                ["CURRENTNOTE"] = DbString(input.CurrentNote),
                ["PERMANENTNOTE"] = DbString(input.PermanentNote)
            };
        }

        private static IReadOnlyDictionary<string, object?> CreateHealthCheckupReserInfoParameters(
            CreateAppointmentPostModel input)
        {
            return new Dictionary<string, object?>
            {
                ["PROJECTNO"] = DbString(input.ProjectNo),
                ["BARCODESTR"] = DbString(input.PatientHealthCode),
                ["RESERVE_AREA"] = DbString(input.ReservationArea),
                ["RESERVE_DATE"] = input.ReservationDate?.Date,
                ["RESERVE_TIME"] = DbString(input.ReservationTime),
                ["PARKING"] = DbString(input.ParkingType),
                ["LAXATIVE"] = DbString(input.Laxative),
                ["MEAL"] = DbString(input.MealType)
            };
        }

        private static IReadOnlyDictionary<string, object?> CreateHealthCheckupSetInfoParameters(
            CreateAppointmentPostModel input,
            PackageOptionViewModel package,
            int itemNo)
        {
            return new Dictionary<string, object?>
            {
                ["PROJECTNO"] = DbString(input.ProjectNo),
                ["BARCODESTR"] = DbString(input.PatientHealthCode),
                ["ITEMNO"] = itemNo,
                ["LISITEMNAME"] = DbString(package.LisItemName),
                ["ORDTOT"] = package.OrderTotal,
                ["SETNAME"] = DbString(package.Name),
                ["AMOUNT"] = package.Price,
                ["CHDCTTYPE"] = DbString(package.SelfPayAccountingUnit)
            };
        }

        private static IReadOnlyDictionary<string, object?> CreateHealthCheckupOtherSetParameters(
            CreateAppointmentPostModel input,
            OtherSuiteLookupItemViewModel otherSuite,
            int itemNo)
        {
            return new Dictionary<string, object?>
            {
                ["PROJECTNO"] = DbString(input.ProjectNo),
                ["BARCODESTR"] = DbString(input.PatientHealthCode),
                ["ITEMNO"] = itemNo.ToString(CultureInfo.InvariantCulture),
                ["SETCODE"] = DbString(GetOtherSuiteSetCode(otherSuite)),
                ["SETNAME"] = DbString(otherSuite.Name),
                ["AMOUNT"] = DbString(otherSuite.Price.ToString("0.##", CultureInfo.InvariantCulture)),
                ["CHDCTTYPE"] = DbString(FirstNonEmpty(
                    otherSuite.SelfPayAccountingUnit,
                    DefaultSelfPayAccountingUnit))
            };
        }

        private static IReadOnlyDictionary<string, object?> CreateHealthCheckupQuestInfoParameters(
            CreateAppointmentPostModel input,
            QuestionnaireOptionViewModel questionnaire,
            int itemNo)
        {
            return new Dictionary<string, object?>
            {
                ["PROJECTNO"] = DbString(input.ProjectNo),
                ["BARCODESTR"] = DbString(input.PatientHealthCode),
                ["ITEMNO"] = itemNo.ToString(CultureInfo.InvariantCulture),
                ["QUESTIONNAIRE"] = DbString(questionnaire.Name),
                ["AMOUNT"] = questionnaire.Price
            };
        }

        private static IReadOnlyDictionary<string, object?> CreateHealthCheckupPaymentParameters(
            CreateAppointmentPostModel input,
            IReadOnlyList<PackageOptionViewModel> selectedPackages,
            IReadOnlyList<OtherSuiteLookupItemViewModel> selectedOtherSuites)
        {
            var calculatedPackageAmount = selectedPackages.Sum(package => package.Price);
            var calculatedExtraAmount = selectedOtherSuites.Sum(otherSuite => otherSuite.Price);
            var packageAmount = ParsePostedAmount(input.PackageAmount) ?? calculatedPackageAmount;
            var extraAmount = ParsePostedAmount(input.ExtraAmount) ?? calculatedExtraAmount;
            var remitAmount = ParsePostedAmount(input.RemitAmount) ?? 0m;
            var discountAmount = ParsePostedAmount(input.DiscountAmount) ?? 0m;
            var accountAmount = ParsePostedAmount(input.AccountAmount) ?? 0m;
            var calculatedReceivedAmount = Math.Max(0m, packageAmount + extraAmount - remitAmount - discountAmount - accountAmount);
            var receivedAmount = ParsePostedAmount(input.ReceivedAmount) ?? calculatedReceivedAmount;

            return new Dictionary<string, object?>
            {
                ["PROJECTNO"] = DbString(input.ProjectNo),
                ["BARCODESTR"] = DbString(input.PatientHealthCode),
                ["PACKAGE_AMOUNT"] = packageAmount,
                ["EXTRA_AMOUNT"] = extraAmount,
                ["REMIT_AMOUNT"] = remitAmount,
                ["DISCOUNT_AMOUNT"] = discountAmount,
                ["ACCOUNT_AMOUNT"] = accountAmount,
                ["RECEIVED_AMOUNT"] = receivedAmount,
                ["PAYMENT_DATE"] = input.PaymentDate?.Date,
                ["PAYMENT_METHOD"] = DbString(input.PaymentMethod),
                ["NOTE"] = DbString(input.PaymentNote)
            };
        }

        private static IReadOnlyList<string> GetSelectedPackageIds(CreateAppointmentPostModel input)
        {
            return input.SelectedPackageIds
                .Select(NormalizeText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<string> GetSelectedOtherSuiteIds(CreateAppointmentPostModel input)
        {
            return input.SelectedOtherSuiteIds
                .Select(NormalizeText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<string> GetSelectedQuestionnaireIds(CreateAppointmentPostModel input)
        {
            return input.SelectedQuestionnaireIds
                .Select(NormalizeText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<PackageOptionViewModel> ResolveSelectedPackages(
            string? projectNo,
            IReadOnlyList<string> selectedPackageIds,
            IReadOnlyList<string> selectedPackageNames,
            IReadOnlyList<string> selectedPackageAmounts,
            IReadOnlyList<string> selectedPackageSelfPayAccountingUnits)
        {
            var normalizedProjectNo = NormalizeText(projectNo);

            if (string.IsNullOrWhiteSpace(normalizedProjectNo) || selectedPackageIds.Count == 0)
            {
                return [];
            }

            var projectPackages = GetPersonalProjectPackagesByProjectNo(normalizedProjectNo);
            var packageMap = projectPackages
                .GroupBy(package => NormalizeText(package.Id), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var accountingUnits = GetPersonalSelfPayAccountingUnits()
                .ToDictionary(unit => unit.Code, StringComparer.OrdinalIgnoreCase);
            var selectedPackages = new List<PackageOptionViewModel>();

            for (var index = 0; index < selectedPackageIds.Count; index++)
            {
                var selectedPackageId = selectedPackageIds[index];
                var postedPackageName = NormalizeText(selectedPackageNames.ElementAtOrDefault(index));
                var postedPackageAmount = ParsePostedAmount(selectedPackageAmounts.ElementAtOrDefault(index)) ?? 0m;
                var postedAccountingUnit = FirstNonEmpty(
                    NormalizeText(selectedPackageSelfPayAccountingUnits.ElementAtOrDefault(index)),
                    DefaultSelfPayAccountingUnit);

                accountingUnits.TryGetValue(postedAccountingUnit, out var accountingUnit);

                packageMap.TryGetValue(selectedPackageId, out var package);
                package ??= FindMatchingPackage(
                    projectPackages,
                    string.Empty,
                    postedPackageName,
                    postedPackageAmount);

                if (package is null)
                {
                    if (!selectedPackageId.StartsWith(PersistedPackageIdPrefix, StringComparison.Ordinal)
                        || accountingUnit is null)
                    {
                        return [];
                    }

                    if (string.IsNullOrWhiteSpace(postedPackageName))
                    {
                        return [];
                    }

                    selectedPackages.Add(new PackageOptionViewModel
                    {
                        Id = selectedPackageId,
                        LisItemName = selectedPackageId.StartsWith(PersistedPackageIdPrefix, StringComparison.Ordinal)
                            ? string.Empty
                            : selectedPackageId,
                        OrderTotal = 0,
                        Name = postedPackageName,
                        SelfPayAccountingUnit = postedAccountingUnit,
                        SelfPayAccountingUnitDisplay = accountingUnit.DisplayText,
                        Price = postedPackageAmount,
                        IsSelected = true
                    });

                    continue;
                }

                var usesOriginalAccountingUnit = string.Equals(
                    postedAccountingUnit,
                    package.SelfPayAccountingUnit,
                    StringComparison.OrdinalIgnoreCase);

                if (accountingUnit is null && !usesOriginalAccountingUnit)
                {
                    return [];
                }

                selectedPackages.Add(new PackageOptionViewModel
                {
                    Id = package.Id,
                    LisItemName = package.LisItemName,
                    OrderTotal = package.OrderTotal,
                    Level = package.Level,
                    Name = package.Name,
                    SelfPayAccountingUnit = postedAccountingUnit,
                    SelfPayAccountingUnitDisplay = accountingUnit?.DisplayText ?? package.SelfPayAccountingUnitDisplay,
                    Price = package.Price,
                    IsSelected = true,
                    Orders = package.Orders
                });
            }

            return selectedPackages;
        }

        private static IReadOnlyDictionary<string, QuestionnaireOptionViewModel> GetPersistedQuestionnaireFallbackMap(
            CreateAppointmentPostModel input)
        {
            var projectNo = FirstNonEmpty(input.OriginalProjectNo, input.ProjectNo);
            var barcode = FirstNonEmpty(input.OriginalPatientHealthCode, input.PatientHealthCode);

            if (string.IsNullOrWhiteSpace(projectNo) || string.IsNullOrWhiteSpace(barcode))
            {
                return new Dictionary<string, QuestionnaireOptionViewModel>(StringComparer.Ordinal);
            }

            var parameters = new Dictionary<string, object?>
            {
                ["PROJECTNO"] = DbString(projectNo),
                ["BARCODESTR"] = DbString(barcode)
            };
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetHealthCheckupQuestInfoByBarcodeProject(),
                parameters,
                "",
                true);

            return CreateQuestionnaireItemsFromQuestInfo(dataTable)
                .GroupBy(questionnaire => NormalizeText(questionnaire.Id), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        private static IReadOnlyList<QuestionnaireOptionViewModel> ResolveSelectedQuestionnaires(
            IReadOnlyList<string> selectedQuestionnaireIds,
            IReadOnlyList<string> selectedQuestionnaireNames,
            IReadOnlyList<string> selectedQuestionnaireAmounts,
            IReadOnlyDictionary<string, QuestionnaireOptionViewModel>? fallbackQuestionnairesById = null)
        {
            if (selectedQuestionnaireIds.Count == 0)
            {
                return [];
            }

            var questionnaireMap = GetQuestionnaireOptions()
                .GroupBy(questionnaire => NormalizeText(questionnaire.Id), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var selectedQuestionnaires = new List<QuestionnaireOptionViewModel>();

            for (var index = 0; index < selectedQuestionnaireIds.Count; index++)
            {
                var selectedQuestionnaireId = selectedQuestionnaireIds[index];

                if (fallbackQuestionnairesById is not null
                    && fallbackQuestionnairesById.TryGetValue(selectedQuestionnaireId, out var fallbackQuestionnaire))
                {
                    selectedQuestionnaires.Add(fallbackQuestionnaire);
                    continue;
                }

                if (questionnaireMap.TryGetValue(selectedQuestionnaireId, out var questionnaire))
                {
                    selectedQuestionnaires.Add(questionnaire);
                    continue;
                }

                if (!selectedQuestionnaireId.StartsWith(PersistedQuestionnaireIdPrefix, StringComparison.Ordinal))
                {
                    return [];
                }

                var questionnaireName = NormalizeText(selectedQuestionnaireNames.ElementAtOrDefault(index));
                var questionnaireAmount = ParsePostedAmount(selectedQuestionnaireAmounts.ElementAtOrDefault(index)) ?? 0m;

                if (string.IsNullOrWhiteSpace(questionnaireName))
                {
                    return [];
                }

                selectedQuestionnaires.Add(new QuestionnaireOptionViewModel
                {
                    Id = selectedQuestionnaireId,
                    Name = questionnaireName,
                    FirstChar = GetQuestionnaireFirstChar(questionnaireName),
                    Price = questionnaireAmount,
                    IsSelected = true
                });
            }

            return selectedQuestionnaires;
        }

        private static IReadOnlyDictionary<string, OtherSuiteLookupItemViewModel> GetPersistedOtherSuiteFallbackMap(
            CreateAppointmentPostModel input)
        {
            var projectNo = FirstNonEmpty(input.OriginalProjectNo, input.ProjectNo);
            var barcode = FirstNonEmpty(input.OriginalPatientHealthCode, input.PatientHealthCode);

            if (string.IsNullOrWhiteSpace(projectNo) || string.IsNullOrWhiteSpace(barcode))
            {
                return new Dictionary<string, OtherSuiteLookupItemViewModel>(StringComparer.Ordinal);
            }

            var parameters = new Dictionary<string, object?>
            {
                ["PROJECTNO"] = DbString(projectNo),
                ["BARCODESTR"] = DbString(barcode)
            };
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(
                OracleSql.SELECT.GetHealthCheckupOtherSetByBarcodeProject(),
                parameters,
                "",
                true);

            return CreateOtherSuiteItemsFromOtherSet(dataTable, projectNo)
                .GroupBy(suite => NormalizeText(suite.Id), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        private static IReadOnlyList<OtherSuiteLookupItemViewModel> ResolveSelectedOtherSuites(
            IReadOnlyList<string> selectedOtherSuiteIds,
            IReadOnlyList<string> selectedOtherSuiteSelfPayAccountingUnits,
            IReadOnlyDictionary<string, OtherSuiteLookupItemViewModel>? fallbackOtherSuitesById = null)
        {
            if (selectedOtherSuiteIds.Count == 0)
            {
                return [];
            }

            var suiteMapsByProjectNo = new Dictionary<string, Dictionary<string, OtherSuiteLookupItemViewModel>>(StringComparer.Ordinal);
            var accountingUnits = GetPersonalSelfPayAccountingUnits()
                .ToDictionary(unit => unit.Code, StringComparer.OrdinalIgnoreCase);
            var selectedOtherSuites = new List<OtherSuiteLookupItemViewModel>();

            for (var index = 0; index < selectedOtherSuiteIds.Count; index++)
            {
                var selectedOtherSuiteId = selectedOtherSuiteIds[index];
                OtherSuiteLookupItemViewModel otherSuite;

                if (fallbackOtherSuitesById is not null
                    && fallbackOtherSuitesById.TryGetValue(selectedOtherSuiteId, out var fallbackOtherSuite))
                {
                    otherSuite = fallbackOtherSuite;
                }
                else
                {
                    var projectNo = GetOtherSuiteProjectNo(selectedOtherSuiteId);

                    if (string.IsNullOrWhiteSpace(projectNo))
                    {
                        return [];
                    }

                    if (!suiteMapsByProjectNo.TryGetValue(projectNo, out var suiteMap))
                    {
                        suiteMap = GetOtherSuiteLookupItemsByProjectNo(projectNo, "")
                            .GroupBy(suite => NormalizeText(suite.Id), StringComparer.Ordinal)
                            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
                        suiteMapsByProjectNo[projectNo] = suiteMap;
                    }

                    if (!suiteMap.TryGetValue(selectedOtherSuiteId, out var resolvedOtherSuite))
                    {
                        return [];
                    }

                    otherSuite = resolvedOtherSuite;
                }

                var postedAccountingUnit = FirstNonEmpty(
                    NormalizeText(selectedOtherSuiteSelfPayAccountingUnits.ElementAtOrDefault(index)),
                    otherSuite.SelfPayAccountingUnit,
                    DefaultSelfPayAccountingUnit);
                accountingUnits.TryGetValue(postedAccountingUnit, out var accountingUnit);
                var usesOriginalAccountingUnit = string.Equals(
                    postedAccountingUnit,
                    otherSuite.SelfPayAccountingUnit,
                    StringComparison.OrdinalIgnoreCase);

                if (accountingUnit is null && !usesOriginalAccountingUnit)
                {
                    return [];
                }

                selectedOtherSuites.Add(new OtherSuiteLookupItemViewModel
                {
                    Id = otherSuite.Id,
                    ProjectNo = otherSuite.ProjectNo,
                    ProjectName = otherSuite.ProjectName,
                    Name = otherSuite.Name,
                    Code = otherSuite.Code,
                    SelfPayAccountingUnit = postedAccountingUnit,
                    SelfPayAccountingUnitDisplay = accountingUnit?.DisplayText
                        ?? FirstNonEmpty(
                            otherSuite.SelfPayAccountingUnitDisplay,
                            postedAccountingUnit == DefaultSelfPayAccountingUnit ? "[00] 無優待" : postedAccountingUnit),
                    Price = otherSuite.Price
                });
            }

            return selectedOtherSuites;
        }

        private static string GetOtherSuiteProjectNo(string otherSuiteId)
        {
            var normalizedOtherSuiteId = NormalizeText(otherSuiteId);
            var separatorIndex = normalizedOtherSuiteId.IndexOf('|', StringComparison.Ordinal);

            return separatorIndex > 0
                ? normalizedOtherSuiteId[..separatorIndex]
                : "";
        }

        private static string GetOtherSuiteSetCode(OtherSuiteLookupItemViewModel otherSuite)
        {
            var code = NormalizeText(otherSuite.Code);

            if (!string.IsNullOrWhiteSpace(code))
            {
                return code;
            }

            var id = NormalizeText(otherSuite.Id);
            var separatorIndex = id.LastIndexOf('|');

            return separatorIndex >= 0 && separatorIndex < id.Length - 1
                ? id[(separatorIndex + 1)..]
                : id;
        }

        private static decimal? ParsePostedAmount(string? value)
        {
            var normalizedValue = NormalizeText(value).Replace(",", "", StringComparison.Ordinal);

            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return null;
            }

            if (!decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                && !decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
            {
                return null;
            }

            return Math.Max(0m, amount);
        }

        private static string NormalizeCreateAppointmentReserveStatus(string? reserveStatus) =>
            NormalizeReserveStatus(reserveStatus ?? "") switch
            {
                "1" => "1",
                "2" => "2",
                _ => ""
            };

        private static object? DbString(string? value)
        {
            var normalizedValue = NormalizeText(value);
            return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
        }

        private static string NormalizeText(string? value) =>
            (value ?? "").Trim();

        private static AppointmentDeleteResultViewModel CreateMockAppointmentDeleteResult(AppointmentDeletePostModel input) =>
            CreateAppointmentDeleteResult(input, deletedCount: 1, isMock: true);

        private static AppointmentDeleteResultViewModel CreateAppointmentDeleteResult(AppointmentDeletePostModel input, int deletedCount, bool isMock) =>
            new()
            {
                Deleted = deletedCount >= 1,
                DeletedCount = deletedCount,
                IsMock = isMock,
                ProjectNumber = input.NormalizedProjectNumber,
                Barcode = input.NormalizedBarcode,
                PatNo = input.NormalizedPatNo,
                Name = input.NormalizedName
            };

        private static AppointmentCancelResultViewModel CreateMockAppointmentCancelResult(AppointmentCancelPostModel input) =>
            CreateAppointmentCancelResult(input, updatedCount: 1, isMock: true);

        private static AppointmentCancelResultViewModel CreateAppointmentCancelResult(AppointmentCancelPostModel input, int updatedCount, bool isMock) =>
            new()
            {
                Canceled = updatedCount >= 1,
                UpdatedCount = updatedCount,
                IsMock = isMock,
                ProjectNumber = input.NormalizedProjectNumber,
                Barcode = input.NormalizedBarcode,
                PatNo = input.NormalizedPatNo,
                Name = input.NormalizedName,
                Status = ResolveAppointmentStatus(input.NormalizedReserveStatus),
                StatusTone = ResolveAppointmentTone(input.NormalizedReserveStatus),
                ReserveStatus = input.NormalizedReserveStatus,
                CurrentReserveStatus = input.NormalizedCurrentReserveStatus
            };

        private static string ResolveAppointmentStatus(string isReserve) =>
            NormalizeReserveStatus(isReserve) switch
            {
                "1" => "已預約",
                "0" => "已取消",
                "2" => "佔名額",
                "4" => "已掛號",
                "5" => "已報到",
                "6" => "檢查中",
                _ => ""
            };



        private static string ResolveAppointmentTone(string isReserve) =>
            NormalizeReserveStatus(isReserve) switch
            {
                "1" => "confirmed",
                "0" => "cancelled",
                "2" => "reserved-slot",
                "4" => "registered",
                "5" => "checked-in",
                "6" => "checking",
                _ => "empty"
            };

        private static string NormalizeReserveStatus(string isReserve)
        {
            var normalizedReserveStatus = NormalizeText(isReserve);
            if (normalizedReserveStatus == "檢查中") return "6";
            if (normalizedReserveStatus == "已取消") return "0";

            if (string.Equals(normalizedReserveStatus, "已預約", StringComparison.Ordinal))
            {
                return "1";
            }

            if (string.Equals(normalizedReserveStatus, "預約取消", StringComparison.Ordinal))
            {
                return "0";
            }

            if (string.Equals(normalizedReserveStatus, "佔名額", StringComparison.Ordinal))
            {
                return "2";
            }

            if (string.Equals(normalizedReserveStatus, "已掛號", StringComparison.Ordinal))
            {
                return "4";
            }

            if (string.Equals(normalizedReserveStatus, "已報到", StringComparison.Ordinal))
            {
                return "5";
            }

            if (decimal.TryParse(normalizedReserveStatus, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericReserveStatus)
                || decimal.TryParse(normalizedReserveStatus, NumberStyles.Number, CultureInfo.CurrentCulture, out numericReserveStatus))
            {
                return numericReserveStatus switch
                {
                    0m => "0",
                    1m => "1",
                    2m => "2",
                    4m => "4",
                    5m => "5",
                    6m => "6",
                    _ => normalizedReserveStatus
                };
            }

            return normalizedReserveStatus;
        }

        private static IReadOnlyList<AppointmentSummaryCardViewModel> BuildSummaryCards(IReadOnlyList<AppointmentListItemViewModel> appointments)
        {
            var totalAppointmentCount = appointments.Count;
            var individualAppointmentCount = appointments.Count(appointment => appointment.Type == "個人");
            var groupAppointmentCount = appointments.Count(appointment => appointment.Type == "團檢");

            return
            [
                new AppointmentSummaryCardViewModel { Label = "總預約", Value = totalAppointmentCount, Tone = "indigo", Icon = "calendar", Key = "total" },
                new AppointmentSummaryCardViewModel { Label = "團檢", Value = groupAppointmentCount, Tone = "teal", Icon = "people", Key = "group" },
                new AppointmentSummaryCardViewModel { Label = "個人", Value = individualAppointmentCount, Tone = "violet", Icon = "person", Key = "individual" }
            ];
        }

        private static IReadOnlyList<AppointmentStatusSummaryCardViewModel> BuildStatusSummaryCards(IReadOnlyList<AppointmentListItemViewModel> appointments) =>
            [
                CreateStatusSummaryCard(appointments, "total", "總預約數", "total", null),
                CreateStatusSummaryCard(appointments, "confirmed", "已預約", "confirmed", "1"),
                CreateStatusSummaryCard(appointments, "registered", "已掛號", "registered", "4"),
                CreateStatusSummaryCard(appointments, "reserved", "佔名額", "reserved", "2"),
                CreateStatusSummaryCard(appointments, "cancelled", "已取消", "cancelled", "0")
            ];

        private static AppointmentStatusSummaryCardViewModel CreateStatusSummaryCard(
            IReadOnlyList<AppointmentListItemViewModel> appointments,
            string key,
            string label,
            string tone,
            string? reserveStatus)
        {
            var matchedAppointments = appointments
                .Where(appointment => string.IsNullOrWhiteSpace(reserveStatus)
                    || NormalizeReserveStatus(appointment.ReserveStatus) == reserveStatus)
                .ToArray();

            return new AppointmentStatusSummaryCardViewModel
            {
                Key = key,
                Label = label,
                Tone = tone,
                TotalCount = matchedAppointments.Length,
                GroupCount = matchedAppointments.Count(appointment => appointment.Type == "團檢"),
                IndividualCount = matchedAppointments.Count(appointment => appointment.Type == "個人")
            };
        }

        private static IReadOnlyList<SelectOptionViewModel> GetGroupCustomerOptions(string selectedCustomerNo)
        {
            var normalizedSelectedCustomerNo = (selectedCustomerNo ?? string.Empty).Trim();
            var sql = OracleSql.SELECT.getHealthCustomerList();
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);
            var options = dataTable.Rows
                .Cast<DataRow>()
                .Select(row => new
                {
                    CustomerNo = GetString(row, "CUSTOMERNO"),
                    CustomerName = GetString(row, "CUSTOMERNAME")
                })
                .Where(customer => !string.IsNullOrWhiteSpace(customer.CustomerNo) || !string.IsNullOrWhiteSpace(customer.CustomerName))
                .Select(customer => new SelectOptionViewModel
                {
                    Value = customer.CustomerNo,
                    Text = string.IsNullOrWhiteSpace(customer.CustomerNo)
                        ? customer.CustomerName
                        : $"[{customer.CustomerNo}] {customer.CustomerName}",
                    IsSelected = string.Equals(customer.CustomerNo, normalizedSelectedCustomerNo, StringComparison.OrdinalIgnoreCase)
                })
                .ToArray();

            return
            [
                new SelectOptionViewModel
                {
                    Value = "",
                    Text = "全部企業",
                    IsSelected = string.IsNullOrWhiteSpace(normalizedSelectedCustomerNo)
                },
                .. options
            ];
        }

        private static IReadOnlyList<SelectOptionViewModel> BuildGroupReserveStatusOptions(string selectedReserveStatus)
        {
            var normalizedSelectedReserveStatus = NormalizeGroupReserveStatus(selectedReserveStatus);
            var options = new[]
            {
                new SelectOptionViewModel { Value = "1", Text = "已預約", IsSelected = normalizedSelectedReserveStatus == "1" },
                new SelectOptionViewModel { Value = "2", Text = "佔名額", IsSelected = normalizedSelectedReserveStatus == "2" },
                new SelectOptionViewModel { Value = "4", Text = "已掛號", IsSelected = normalizedSelectedReserveStatus == "4" },
                new SelectOptionViewModel { Value = "0", Text = "預約取消", IsSelected = normalizedSelectedReserveStatus == "0" }
            };

            return
            [
                new SelectOptionViewModel
                {
                    Value = "",
                    Text = "全部狀態",
                    IsSelected = string.IsNullOrWhiteSpace(normalizedSelectedReserveStatus)
                },
                .. options
            ];
        }

        private static string NormalizeGroupReserveStatus(string? reserveStatus)
        {
            var normalizedReserveStatus = (reserveStatus ?? string.Empty).Trim();

            return normalizedReserveStatus is "0" or "1" or "2" or "4"
                ? normalizedReserveStatus
                : string.Empty;
        }

        private static DateTime? ParseGroupQueryDate(string? value)
        {
            var dateText = (value ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(dateText))
            {
                return null;
            }

            string[] formats = ["yyyy/MM/dd", "yyyy-MM-dd", "yyyyMMdd"];

            if (DateTime.TryParseExact(dateText, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate))
            {
                return exactDate.Date;
            }

            return DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
                ? parsedDate.Date
                : null;
        }

        private static string FormatGroupQueryDate(DateTime value) =>
            value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static IReadOnlyList<GroupSummaryCardViewModel> BuildGroupSummaryCards(IReadOnlyList<GroupAppointmentListItemViewModel> groups)
        {
            var estimatedCount = groups.Sum(group => group.EstimatedCount);
            var cancelledCount = groups.Sum(group => group.CancelledCount);
            var reservedCount = groups.Sum(group => group.ReservedCount);
            var occupiedCount = groups.Sum(group => group.OccupiedCount);
            var registeredCount = groups.Sum(group => group.RegisteredCount);

            return
            [
                new GroupSummaryCardViewModel { Label = "團體預約", Value = groups.Count.ToString(CultureInfo.InvariantCulture), Unit = "組", Tone = "indigo", Icon = "people" },
                new GroupSummaryCardViewModel { Label = "預估人數", Value = estimatedCount.ToString(CultureInfo.InvariantCulture), Unit = "人", Tone = "teal", Icon = "file" },
                new GroupSummaryCardViewModel { Label = "預約取消", Value = cancelledCount.ToString(CultureInfo.InvariantCulture), Unit = "人", Tone = "rose", Icon = "cancel-ring" },
                new GroupSummaryCardViewModel { Label = "預約人數", Value = reservedCount.ToString(CultureInfo.InvariantCulture), Unit = "人", Tone = "cyan", Icon = "check-ring" },
                new GroupSummaryCardViewModel { Label = "佔名額", Value = occupiedCount.ToString(CultureInfo.InvariantCulture), Unit = "人", Tone = "amber", Icon = "reserved-slot" },
                new GroupSummaryCardViewModel { Label = "已掛號", Value = registeredCount.ToString(CultureInfo.InvariantCulture), Unit = "人", Tone = "green", Icon = "clock-ring" }
            ];
        }

        private static IReadOnlyList<GroupAppointmentListItemViewModel> GetGroupAppointments(
            string filterCustomerNo,
            string filterProjectName,
            DateTime? checkDateStart,
            DateTime? checkDateEnd,
            string reserveStatus,
            string selectedDetailCustomerNo,
            string selectedDetailProjectNo)
        {
            var sql = OracleSql.SELECT.getGroupAppointmentManagementList(filterCustomerNo, filterProjectName, checkDateStart, checkDateEnd, reserveStatus);
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select(row =>
                {
                    var customerNo = GetString(row, "CUSTOMERNO");
                    var customerName = GetString(row, "CUSTOMERNAME");
                    var projectNo = GetString(row, "PROJECTNO");
                    var projectName = GetString(row, "PROJECTNAME");
                    var checkDate = GetDateString(row, "CHECKDATE");
                    var endDate = GetDateString(row, "ENDDATE");
                    var estimatedCount = GetInt32(row, "預估人數");
                    var cancelledCount = GetInt32(row, "預約取消");
                    var reservedCount = GetInt32(row, "預約人數");
                    var occupiedCount = GetInt32(row, "佔名額");
                    var registeredCount = GetInt32(row, "已掛號");
                    var status = ResolveGroupAppointmentStatus(estimatedCount, cancelledCount, reservedCount, occupiedCount, registeredCount);
                    var selected = !string.IsNullOrWhiteSpace(selectedDetailCustomerNo)
                        && string.Equals(customerNo, selectedDetailCustomerNo, StringComparison.OrdinalIgnoreCase)
                        && (string.IsNullOrWhiteSpace(selectedDetailProjectNo)
                            || string.Equals(projectNo, selectedDetailProjectNo, StringComparison.OrdinalIgnoreCase));

                    return new GroupAppointmentListItemViewModel
                    {
                        Selected = selected,
                        CustomerNo = customerNo,
                        CustomerName = customerName,
                        ProjectNo = projectNo,
                        ProjectName = projectName,
                        CheckDate = checkDate,
                        EndDate = endDate,
                        EstimatedCount = estimatedCount,
                        CancelledCount = cancelledCount,
                        ReservedCount = reservedCount,
                        OccupiedCount = occupiedCount,
                        RegisteredCount = registeredCount,
                        Code = projectNo,
                        Company = customerName,
                        Project = projectName,
                        Date = checkDate,
                        Estimate = estimatedCount,
                        Imported = reservedCount,
                        Registered = registeredCount,
                        Status = status.Text,
                        StatusTone = status.Tone,
                        CanRegister = reservedCount + occupiedCount > 0
                    };
                })
                .ToArray();
        }

        private static (string Text, string Tone) ResolveGroupAppointmentStatus(
            int estimatedCount,
            int cancelledCount,
            int reservedCount,
            int occupiedCount,
            int registeredCount)
        {
            var activeCount = reservedCount + occupiedCount + registeredCount;

            if (estimatedCount <= 0)
            {
                return ("無名單", "waiting");
            }

            if (activeCount <= 0 && cancelledCount > 0)
            {
                return ("預約取消", "cancelled");
            }

            if (activeCount > 0 && registeredCount >= activeCount)
            {
                return ("已完成", "completed");
            }

            if (registeredCount > 0)
            {
                return ("掛號中", "in-progress");
            }

            if (reservedCount > 0 || occupiedCount > 0)
            {
                return ("待掛號", "waiting");
            }

            return ("未預約", "waiting");
        }

        private static IReadOnlyList<CapacitySlotViewModel> BuildCapacitySlots()
        {
            return
            [
                new CapacitySlotViewModel { Time = "08:00 - 09:00", Used = "26 / 32", Detail = "一般檢查區", Hint = "剩餘 6" },
                new CapacitySlotViewModel { Time = "09:00 - 10:00", Used = "28 / 32", Detail = "抽血、超音波", Hint = "剩餘 4", Active = true },
                new CapacitySlotViewModel { Time = "10:00 - 11:00", Used = "21 / 32", Detail = "高階影像檢查", Hint = "剩餘 11" },
                new CapacitySlotViewModel { Time = "11:00 - 12:00", Used = "15 / 28", Detail = "醫師諮詢", Hint = "剩餘 13" }
            ];
        }

        private static IReadOnlyList<AppointmentReminderViewModel> BuildReminders()
        {
            return
            [
                new AppointmentReminderViewModel { Label = "需延誤處理", Value = "18", Unit = "人", Tone = "red", Icon = "late" },
                new AppointmentReminderViewModel { Label = "尚未填問卷", Value = "6", Unit = "人", Tone = "amber", Icon = "edit" },
                new AppointmentReminderViewModel { Label = "高齡健檢（65 歲以上）", Value = "12", Unit = "人", Tone = "blue", Icon = "senior" },
                new AppointmentReminderViewModel { Label = "團體報到未齊", Value = "3", Unit = "組", Tone = "purple", Icon = "group" }
            ];
        }

        private static IReadOnlyList<PatientLookupFilterViewModel> BuildPatientLookupFilters(string selectedFilter)
        {
            return
            [
                CreateFilter("chartNo", "病歷號"),
                CreateFilter("idNo", "身分證"),
                CreateFilter("name", "姓名"),
                CreateFilter("mobile", "手機")
            ];

            PatientLookupFilterViewModel CreateFilter(string key, string label) =>
                new() { Key = key, Label = label, IsActive = key == selectedFilter };
        }

        private static IReadOnlyList<PatientLookupPatientViewModel> GetPatientLookupPatients(string? lookupValue, string lookupCode)
        {
            var normalizedLookupValue = lookupValue?.Trim() ?? "";
            var normalizedLookupCode = NormalizePatientLookupFilter(lookupCode);
            // lookupCode 代表使用者選的查詢類別：
            // chartNo = 病歷號, idNo = 身分證, name = 姓名, mobile = 手機

            if (normalizedLookupValue.Length == 0)
            {
                return [];
            }

            var sql = OracleSql.SELECT.getOPDMRBasicTbl(normalizedLookupValue, normalizedLookupCode);
            var dataTable = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(sql, "", true);

            return dataTable.Rows
                .Cast<DataRow>()
                .Select(row => new PatientLookupPatientViewModel
                {
                    ChartNo = GetString(row, "CHMRNO"),
                    Name = GetString(row, "病人姓名"),
                    IdNo = GetString(row, "病人身份證"),
                    Sex = GetString(row, "病人性別"),
                    Age = GetString(row, "年齡"),
                    BirthDate = GetString(row, "病人生日"),
                    Mobile = GetString(row, "CHMRCELLULAR"),
                    BloodType = GetString(row, "CHBLOOD"),
                    Phone = GetString(row, "CHTELH"),
                    Email = GetString(row, "EMAIL"),
                    ContactName = GetString(row, "CONTACTNAME"),
                    ContactPhone = GetString(row, "CONTACTTEL"),
                    ContactRelationship = GetString(row, "CONTACTRELATION"),
                    IsVip = IsTruthy(GetString(row, "IS_VIP")),
                    IsEmployeeFamily = !string.IsNullOrWhiteSpace(GetString(row, "FAMILYNOTE")),
                    FamilyNote = GetString(row, "FAMILYNOTE"),
                    MailingAddress = GetString(row, "SENDINGADDRESS"),
                    Address = GetString(row, "LIVEADDRESS"),
                    NotificationNote = GetString(row, "NOTICENOTE")
                })
                .ToArray();
        }

        private static bool MatchesPatientLookupKeyword(PatientLookupPatientViewModel patient, string? keyword, string filter)
        {
            var normalizedKeyword = NormalizeSearchValue(keyword);

            if (normalizedKeyword.Length == 0)
            {
                return true;
            }

            string[] filterValues = filter switch
            {
                "idNo" => [patient.IdNo, patient.MaskedId],
                "name" => [patient.Name],
                "mobile" => [patient.Mobile, patient.Phone],
                _ => [patient.ChartNo]
            };

            string[] fallbackValues =
            [
                patient.ChartNo,
                patient.IdNo,
                patient.MaskedId,
                patient.Name,
                patient.Mobile,
                patient.Phone
            ];

            return filterValues
                .Concat(fallbackValues)
                .Any(value => NormalizeSearchValue(value).Contains(normalizedKeyword));
        }

        private static string NormalizePatientLookupFilter(string? filter)
        {
            return filter switch
            {
                "idNo" or "name" or "mobile" => filter,
                _ => "chartNo"
            };
        }

        private static string NormalizePatientLookupKeyword(string? keyword, string filter)
        {
            var normalizedKeyword = keyword?.Trim() ?? "";

            return filter is "chartNo" or "idNo"
                ? normalizedKeyword.ToUpperInvariant()
                : normalizedKeyword;
        }

        private static string NormalizeSearchValue(string? value) =>
            (value ?? "").Trim().ToUpperInvariant();

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

        private static string FormatTaiwanDate(DateTime date)
        {
            string[] weekdays = ["日", "一", "二", "三", "四", "五", "六"];
            return $"{date:yyyy/MM/dd}（{weekdays[(int)date.DayOfWeek]}）";
        }

        private static IReadOnlyList<SelectOptionViewModel> WithSelectedOption(
            IReadOnlyList<SelectOptionViewModel> options,
            string? selectedValue,
            string? selectedText = null)
        {
            var normalizedSelectedValue = NormalizeText(selectedValue);

            if (string.IsNullOrWhiteSpace(normalizedSelectedValue))
            {
                return options;
            }

            var hasMatchedOption = false;
            var selectedOptions = options
                .Select(option =>
                {
                    var isSelected = string.Equals(option.Value, normalizedSelectedValue, StringComparison.Ordinal);
                    hasMatchedOption = hasMatchedOption || isSelected;

                    return new SelectOptionViewModel
                    {
                        Value = option.Value,
                        Text = option.Text,
                        IsSelected = isSelected
                    };
                })
                .ToList();

            if (!hasMatchedOption)
            {
                selectedOptions.Add(new SelectOptionViewModel
                {
                    Value = normalizedSelectedValue,
                    Text = FirstNonEmpty(NormalizeText(selectedText), normalizedSelectedValue),
                    IsSelected = true
                });
            }

            return selectedOptions;
        }

        private static string GetString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return "";
            }

            return TrimDataValue(row[columnName]);
        }

        private static string GetDateInputString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return "";
            }

            if (row[columnName] is DateTime date)
            {
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            var value = TrimDataValue(row[columnName]);

            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date)
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : value;
        }

        private static string GetAmountInputString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return "";
            }

            if (row[columnName] is IConvertible convertible)
            {
                try
                {
                    return Convert.ToDecimal(convertible, CultureInfo.InvariantCulture)
                        .ToString("0.##", CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                }
                catch (InvalidCastException)
                {
                }
                catch (OverflowException)
                {
                }
            }

            return TrimDataValue(row[columnName]).Replace(",", "", StringComparison.Ordinal);
        }

        private static int GetInt32(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(row[columnName], CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return 0;
            }
            catch (InvalidCastException)
            {
                return 0;
            }
            catch (OverflowException)
            {
                return 0;
            }
        }

        private static string GetDateString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return "";
            }

            return row[columnName] is DateTime date
                ? date.ToString("yyyy/MM/dd")
                : TrimDataValue(row[columnName]);
        }

        private static string GetTimeInputString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return "";
            }

            if (row[columnName] is DateTime time)
            {
                return time.ToString("HH:mm", CultureInfo.InvariantCulture);
            }

            var value = TrimDataValue(row[columnName]);

            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsedTime)
                || TimeSpan.TryParse(value, CultureInfo.CurrentCulture, out parsedTime))
            {
                return parsedTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
                || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out time))
            {
                return time.ToString("HH:mm", CultureInfo.InvariantCulture);
            }

            return value.Length == 4 && value.All(char.IsDigit)
                ? value[..2] + ":" + value[2..]
                : value;
        }

        private static string GetTimeString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return "";
            }

            return row[columnName] is DateTime time
                ? time.ToString("HH:mm")
                : TrimDataValue(row[columnName]);
        }

        private static decimal GetDecimal(DataRow row, decimal defaultValue, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                {
                    continue;
                }

                if (row[columnName] is IConvertible convertible)
                {
                    try
                    {
                        return Convert.ToDecimal(convertible, CultureInfo.InvariantCulture);
                    }
                    catch (FormatException)
                    {
                    }
                    catch (InvalidCastException)
                    {
                    }
                    catch (OverflowException)
                    {
                    }
                }

                var value = TrimDataValue(row[columnName]);

                if (decimal.TryParse(value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
                {
                    return parsedValue;
                }
            }

            return defaultValue;
        }

        private static bool IsTruthy(string? value)
        {
            return NormalizeText(value).ToUpperInvariant() switch
            {
                "1" or "Y" or "YES" or "TRUE" or "T" or "是" => true,
                _ => false
            };
        }

        private static string TrimDataValue(object value) =>
            Convert.ToString(value)?.Trim() ?? "";
    }
}
