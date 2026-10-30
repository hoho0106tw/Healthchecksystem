using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Models;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Globalization;

namespace HealthSys_20260428_v2.csproj.Controllers;

public sealed class StatisticsController : Controller
{
    public IActionResult AccountQuery()
    {
        return View();
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult AccountQueryList(DateTime? startDate, DateTime? endDate, string? keyword,
        CancellationToken requestAborted)
    {
        if (startDate is null || endDate is null || endDate < startDate)
        {
            return BadRequest(new { message = "請選擇有效的預約日期範圍。" });
        }

        const int timeoutSeconds = 30;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var query = OracleSql.SELECT.BuildAccountQueryList(startDate.Value, endDate.Value, keyword);
        try
        {
            var table = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDataTable(query.Sql, query.Parameters,
                "", true, timeoutSeconds, timeout.Token);
            return Json(table.Rows.Cast<DataRow>().Select(row => new StatisticsAccountListItemViewModel
            {
                Barcode = GetValue(row, "BARCODESTR"),
                ProjectNo = GetValue(row, "PROJECTNO"),
                ProjectName = GetValue(row, "PROJECTNAME"),
                ReserveDate = DateTime.TryParse(GetValue(row, "RESERVEDATE"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var reserveDate)
                    ? reserveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : GetValue(row, "RESERVEDATE"),
                ReserveTime = GetValue(row, "RESERVETIME"),
                Status = GetValue(row, "STATUS_NAME"),
                PatNo = GetValue(row, "PATNO"),
                PatientName = GetValue(row, "PATIENTNAME"),
                CustomerName = GetValue(row, "CUSTOMERNAME"),
                Chop1Date = GetValue(row, "CHOP1DATE"),
                Chop1Time = GetValue(row, "CHOP1TIME"),
                Chop1Room = GetValue(row, "CHOP1ROOM"),
                Intop1No = GetValue(row, "INTOP1NO")
            }).ToArray());
        }
        catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, new { message = "查詢逾時，請縮小日期範圍。" });
        }
        catch (OracleException exception)
        {
            Console.Error.WriteLine($"AccountQueryList Oracle {exception.Number}: {exception.Message}");
            return StatusCode(500, new { message = "帳目清單暫時無法載入。" });
        }
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult AccountQuerySuites(string? chop1Date, string? chop1Time,
        string? chop1Room, string? intop1No, CancellationToken requestAborted)
    {
        chop1Date = chop1Date?.Trim();
        chop1Time = chop1Time?.Trim();
        chop1Room = chop1Room?.Trim();
        if (string.IsNullOrWhiteSpace(chop1Date) || string.IsNullOrWhiteSpace(chop1Time)
            || string.IsNullOrWhiteSpace(chop1Room)
            || !int.TryParse(intop1No, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderNo))
        {
            return BadRequest(new { message = "缺少組套查詢所需的掛號資料。" });
        }

        const int timeoutSeconds = 30;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var query = OracleSql.SELECT.BuildAccountSuiteQuery(chop1Date, chop1Time, chop1Room, orderNo);
        try
        {
            var table = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData().GETDbGenDataTable(
                query.Sql, query.Parameters, timeoutSeconds, timeout.Token);
            decimal Amount(DataRow row, string column) => row[column] is DBNull
                ? 0m : Convert.ToDecimal(row[column], CultureInfo.InvariantCulture);
            return Json(table.Rows.Cast<DataRow>().Select((row, index) => new StatisticsAccountSuiteViewModel
            {
                Id = index.ToString(CultureInfo.InvariantCulture),
                Seq = GetValue(row, "ORDSEQ"),
                Name = GetValue(row, "SUITENAME"),
                Total = Amount(row, "TOTAL"),
                Receivable = Amount(row, "RECEIVABLE"),
                Discount = -Math.Abs(Amount(row, "DISCOUNT")),
                Booked = -Math.Abs(Amount(row, "BOOKED")),
                Arrears = -Math.Abs(Amount(row, "ARREARS"))
            }).ToArray());
        }
        catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (OracleException) when (requestAborted.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, new { message = "組套查詢逾時，請稍後再試。" });
        }
        catch (OracleException exception)
        {
            Console.Error.WriteLine($"AccountQuerySuites Oracle {exception.Number}: {exception.Message}");
            return StatusCode(500, new { message = "組套資料暫時無法載入。" });
        }
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult AccountQueryOrders(string? chop1Date, string? chop1Time,
        string? chop1Room, string? intop1No, CancellationToken requestAborted)
    {
        chop1Date = chop1Date?.Trim();
        chop1Time = chop1Time?.Trim();
        chop1Room = chop1Room?.Trim();
        if (string.IsNullOrWhiteSpace(chop1Date) || string.IsNullOrWhiteSpace(chop1Time)
            || string.IsNullOrWhiteSpace(chop1Room)
            || !int.TryParse(intop1No, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderNo))
        {
            return BadRequest(new { message = "缺少醫令查詢所需的掛號資料。" });
        }

        const int timeoutSeconds = 30;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var connectionFactory = /* 原資料庫連線：new OracleConnectionFactory() */ new ControllerDemoData();

        try
        {
            var orderNumberQuery = OracleSql.SELECT.BuildAccountOrderNumberQuery(chop1Date, chop1Time, chop1Room, intop1No!);
            var orderNumbers = connectionFactory.GETDataTable(orderNumberQuery.Sql, orderNumberQuery.Parameters,
                "", true, timeoutSeconds, timeout.Token);
            var excludedChop4OrdNo = orderNumbers.Rows.Cast<DataRow>()
                .Select(row => GetValue(row, "CHOP4ORDNO"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (excludedChop4OrdNo is null)
            {
                return Json(Array.Empty<StatisticsAccountOrderViewModel>());
            }

            var orderNamesQuery = OracleSql.SELECT.BuildAccountOrderNamesQuery(
                chop1Date, chop1Time, chop1Room, orderNo, excludedChop4OrdNo);
            var orderNames = connectionFactory.GETDbGenDataTable(orderNamesQuery.Sql, orderNamesQuery.Parameters,
                timeoutSeconds, timeout.Token);
            return Json(orderNames.Rows.Cast<DataRow>().Select(row => new StatisticsAccountOrderViewModel
            {
                Name = GetValue(row, "CHOP4ORDNAME"),
                Code = GetValue(row, "CHOP4ORDNO"),
                Sequence = GetValue(row, "INTOP1ORDSEQ")
            }).ToArray());
        }
        catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (OracleException) when (requestAborted.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, new { message = "醫令查詢逾時，請稍後再試。" });
        }
        catch (OracleException exception)
        {
            Console.Error.WriteLine($"AccountQueryOrders Oracle {exception.Number}: {exception.Message}");
            return StatusCode(500, new { message = "醫令資料暫時無法載入。" });
        }
    }

    private static string GetValue(DataRow row, string columnName)
    {
        if (!row.Table.Columns.Contains(columnName) || row[columnName] is null || row[columnName] == DBNull.Value)
        {
            return string.Empty;
        }

        return Convert.ToString(row[columnName], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }
}
