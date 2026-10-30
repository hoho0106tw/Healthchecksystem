using System.Data;
using System.Globalization;

namespace HealthSys_20260428_v2.csproj.Services;

public sealed record OpdPackageOrder(
    string OrderNumber,
    int OrderTotal,
    string PFin2,
    bool IsOtherSuite = false);

public static class AppointmentPackageOrderResolver
{
    // Saved appointment choices remain valid even when the current project
    // catalog has changed. ITEMNO is a row sequence, never an order code.
    public static IReadOnlyList<OpdPackageOrder> ResolvePersisted(
        DataTable packages, DataTable otherSuites, string? defaultAccountingUnit)
    {
        var orders = new List<OpdPackageOrder>();
        AddRows(packages, "LISITEMNAME", false);
        AddRows(otherSuites, "SETCODE", true);
        return orders;

        void AddRows(DataTable table, string codeColumn, bool isOtherSuite)
        {
            foreach (DataRow row in table.Rows)
            {
                var code = Text(row, codeColumn);
                if (string.IsNullOrWhiteSpace(code))
                    throw new InvalidOperationException("已選組套缺少醫令代碼，請確認組套設定；未執行掛號。");

                var accountingUnit = Text(row, "CHDCTTYPE");
                if (string.IsNullOrWhiteSpace(accountingUnit))
                    accountingUnit = string.IsNullOrWhiteSpace(defaultAccountingUnit) ? "00" : defaultAccountingUnit.Trim();

                var total = int.TryParse(Text(row, "ORDTOT"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var parsedTotal) ? Math.Max(0, parsedTotal) : 0;
                orders.Add(new OpdPackageOrder(code, isOtherSuite ? 1 : total, accountingUnit, isOtherSuite));
            }
        }
    }

    private static string Text(DataRow row, string column) =>
        row.Table.Columns.Contains(column)
            ? Convert.ToString(row[column], CultureInfo.InvariantCulture)?.Trim() ?? ""
            : "";
}
