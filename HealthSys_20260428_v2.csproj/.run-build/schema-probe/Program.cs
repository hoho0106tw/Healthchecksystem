using HealthSys_20260428_v2.csproj.Data;

var factory = new OracleConnectionFactory();
var sql = "SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH, DATA_PRECISION, DATA_SCALE, NULLABLE FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'HEALTHCHECKUPLIST' AND COLUMN_NAME = 'SELFCOSTNO'";
var table = factory.GETDataTable(sql, string.Empty, true);

Console.WriteLine($"Rows={table.Rows.Count}");
foreach (System.Data.DataRow row in table.Rows)
{
    Console.WriteLine(string.Join("|", row.ItemArray.Select(value => Convert.ToString(value))));
}

var codeLengths = factory.GETDataTable(
    "SELECT MAX(LENGTH(CHDCTTYPE)) AS MAX_LENGTH, SUM(CASE WHEN LENGTH(CHDCTTYPE) > 10 THEN 1 ELSE 0 END) AS OVER_LENGTH FROM GENDCTTYPETBL",
    string.Empty,
    false);
Console.WriteLine($"CodeLength={codeLengths.Rows[0]["MAX_LENGTH"]}|Over10={codeLengths.Rows[0]["OVER_LENGTH"]}");

var childRows = factory.GETDataTable(
    @"SELECT COUNT(*) AS TOTAL,
SUM(CASE WHEN EXISTS (SELECT 1 FROM HealthCheckupNOTE n WHERE n.BARCODESTR = h.BARCODESTR AND n.PROJECTNO = h.PROJECTNO) THEN 0 ELSE 1 END) AS MISSING_NOTE,
SUM(CASE WHEN EXISTS (SELECT 1 FROM HealthCheckupReserInfo r WHERE r.BARCODESTR = h.BARCODESTR AND r.PROJECTNO = h.PROJECTNO) THEN 0 ELSE 1 END) AS MISSING_RESER,
SUM(CASE WHEN EXISTS (SELECT 1 FROM HealthCheckupPayment p WHERE p.BARCODESTR = h.BARCODESTR AND p.PROJECTNO = h.PROJECTNO) THEN 0 ELSE 1 END) AS MISSING_PAYMENT
FROM HealthCheckupList h",
    string.Empty,
    true);
Console.WriteLine($"Appointments={childRows.Rows[0]["TOTAL"]}|MissingNote={childRows.Rows[0]["MISSING_NOTE"]}|MissingReser={childRows.Rows[0]["MISSING_RESER"]}|MissingPayment={childRows.Rows[0]["MISSING_PAYMENT"]}");

var auditColumns = factory.GETDataTable(
    @"SELECT TABLE_NAME, COLUMN_NAME FROM USER_TAB_COLUMNS
WHERE TABLE_NAME IN ('HEALTHCHECKUPNOTE', 'HEALTHCHECKUPRESERINFO', 'HEALTHCHECKUPPAYMENT')
  AND COLUMN_NAME IN ('CREATEDAT', 'UPDATEDAT', 'UPDATED_AT')
ORDER BY TABLE_NAME, COLUMN_NAME",
    string.Empty,
    true);
foreach (System.Data.DataRow row in auditColumns.Rows)
{
    Console.WriteLine($"AuditColumn={row["TABLE_NAME"]}.{row["COLUMN_NAME"]}");
}
