using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using HealthSys_20260428_v2.csproj.Models;

namespace HealthSys_20260428_v2.csproj.Data;

public static class GroupRosterImportReader
{
    private const int HeaderScanRowLimit = 30;
    private const int MinimumHeaderMatchCount = 3;
    private static readonly IReadOnlyDictionary<RosterField, int> LegacyFieldIndexes = new Dictionary<RosterField, int>
    {
        [RosterField.IdNo] = 0,
        [RosterField.Name] = 1,
        [RosterField.Sex] = 2,
        [RosterField.StaffNo] = 3,
        [RosterField.CompanyBranch] = 4,
        [RosterField.Department] = 5,
        [RosterField.UnitClass] = 6,
        [RosterField.JobTitleSeatNo] = 7,
        [RosterField.Birthday] = 8,
        [RosterField.Phone] = 9,
        [RosterField.Mobile] = 10,
        [RosterField.Address] = 11,
        [RosterField.Email] = 12,
        [RosterField.CheckDate] = 13,
        [RosterField.IsDependent] = 14,
        [RosterField.DependentEmployeeId] = 15,
        [RosterField.ChartNo] = 16,
        [RosterField.NativePlace] = 17,
        [RosterField.Note] = 18,
        [RosterField.OccupationalAccidentBarcode] = 19,
        [RosterField.WorkArea] = 20,
        [RosterField.LabNo] = 21,
        [RosterField.DisplayPassword] = 22,
        [RosterField.SequenceNo] = 23,
        [RosterField.ReserveDate] = 24,
        [RosterField.EnvironmentTest] = 25,
        [RosterField.EnvironmentTestDescription] = 26,
        [RosterField.CheckNo] = 27,
        [RosterField.PublicPackageName] = 28,
        [RosterField.SelfPayAddOns] = 29
    };

    private static readonly RosterColumn[] RosterColumns =
    [
        new(RosterField.IdNo, ["身分證號", "身份證號", "身分證", "身份證", "身分證字號", "身份證字號", "IDNO", "ID"]),
        new(RosterField.ChartNo, ["病歷號", "病歷號碼", "病歷"]),
        new(RosterField.Name, ["姓名", "名字", "受檢者姓名", "病人姓名"]),
        new(RosterField.Sex, ["性別", "性", "生理性別"]),
        new(RosterField.Birthday, ["生日", "出生日期", "出生年月日", "出生日"]),
        new(RosterField.Phone, ["電話", "聯絡電話", "電話號碼", "聯絡電話號碼", "市話", "住家電話", "家電"]),
        new(RosterField.Mobile, ["手機", "手機號碼", "行動電話", "行動電話號碼", "手機號"]),
        new(RosterField.Address, ["地址", "居住地址", "住址", "戶籍地址", "通訊地址"]),
        new(RosterField.Email, ["E-MAIL", "EMAIL", "MAIL", "電子信箱", "信箱"]),
        new(RosterField.CheckDate, ["檢查日", "檢查日期", "預約日期", "健檢日期", "健檢日"]),
        new(RosterField.IsDependent, ["眷屬", "是否家屬", "是否眷屬", "家屬"]),
        new(RosterField.DependentEmployeeId, ["眷屬的員工ID", "眷屬員工ID", "家屬備註", "家屬員工ID", "員工ID"]),
        new(RosterField.Note, ["備註", "當次備註", "本次備註", "此次備註"]),
        new(RosterField.PublicPackageName, ["公費套檢名稱(記帳)", "公費套檢名稱", "健檢專案 / 方案", "健檢專案/方案", "健檢專案", "方案", "套檢名稱"]),
        new(RosterField.SelfPayAddOns, ["自費加項", "其他組套", "自費項目", "加項", "加選項目"]),
        new(RosterField.CompanyBranch, ["廠別/分公司", "廠別", "分公司", "公司名稱", "公司", "客戶名稱"]),
        new(RosterField.JobTitleSeatNo, ["職稱/座號", "職稱", "座號", "職位", "職務"]),
        new(RosterField.StaffNo, ["工/學號", "工號", "學號", "員工編號", "員編"]),
        new(RosterField.Department, ["部門/科系", "部門", "科系"]),
        new(RosterField.UnitClass, ["單位/班級", "單位", "班級"]),
        new(RosterField.NativePlace, ["籍貫"]),
        new(RosterField.OccupationalAccidentBarcode, ["職災申報條碼", "職災條碼"]),
        new(RosterField.WorkArea, ["執行區域", "院區", "區域"]),
        new(RosterField.LabNo, ["檢驗號", "檢驗編號"]),
        new(RosterField.DisplayPassword, ["顯示密碼", "密碼"]),
        new(RosterField.SequenceNo, ["序號", "流水號"]),
        new(RosterField.ReserveDate, ["名單預約日期"]),
        new(RosterField.EnvironmentTest, ["環境檢測"]),
        new(RosterField.EnvironmentTestDescription, ["環境檢測描述", "環境檢測說明"]),
        new(RosterField.CheckNo, ["檢查號", "健檢號"])
    ];

    private static readonly IReadOnlyDictionary<string, RosterField> HeaderLookup = BuildHeaderLookup();

    private static readonly RosterField[] RequiredHeaderFields =
    [
        RosterField.IdNo,
        RosterField.Name,
        RosterField.CheckDate
    ];

    public static IReadOnlyList<GroupRosterMemberViewModel> Read(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".xlsx" => ReadXlsx(stream),
            ".csv" => ReadCsv(stream),
            _ => throw new InvalidOperationException("只支援 Excel (.xlsx) 或 CSV (.csv) 名單格式。")
        };
    }

    private static IReadOnlyList<GroupRosterMemberViewModel> ReadXlsx(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = GetFirstWorksheetEntry(archive)
            ?? throw new InvalidOperationException("找不到 Excel 第一個工作表。");

        using var sheetStream = sheetEntry.Open();
        var sheetDocument = XDocument.Load(sheetStream);
        XNamespace worksheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<WorksheetRow>();

        foreach (var rowElement in sheetDocument.Descendants(worksheetNamespace + "row"))
        {
            var rowNumber = ParseInt(rowElement.Attribute("r")?.Value, rows.Count + 1);
            var values = new List<string>();

            foreach (var cellElement in rowElement.Elements(worksheetNamespace + "c"))
            {
                var columnIndex = GetColumnIndex(cellElement.Attribute("r")?.Value);

                if (columnIndex < 1)
                {
                    continue;
                }

                SetCellValue(values, columnIndex, ReadCellValue(cellElement, worksheetNamespace, sharedStrings));
            }

            rows.Add(new WorksheetRow(rowNumber, values.ToArray()));
        }

        return BuildMembers(rows);
    }

    private static IReadOnlyList<GroupRosterMemberViewModel> ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<WorksheetRow>();
        var rowNumber = 1;

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine() ?? "";
            rows.Add(new WorksheetRow(rowNumber, ParseCsvLine(line).ToArray()));
            rowNumber += 1;
        }

        return BuildMembers(rows);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");

        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace worksheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        return document
            .Descendants(worksheetNamespace + "si")
            .Select(sharedString => string.Concat(sharedString.Descendants(worksheetNamespace + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static ZipArchiveEntry? GetFirstWorksheetEntry(ZipArchive archive)
    {
        XNamespace workbookNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relationshipEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

        if (workbookEntry is null || relationshipEntry is null)
        {
            return archive.GetEntry("xl/worksheets/sheet1.xml");
        }

        using var workbookStream = workbookEntry.Open();
        using var relationshipStream = relationshipEntry.Open();
        var workbookDocument = XDocument.Load(workbookStream);
        var relationshipDocument = XDocument.Load(relationshipStream);
        var firstSheetId = workbookDocument
            .Descendants(workbookNamespace + "sheet")
            .FirstOrDefault()
            ?.Attribute(relationshipNamespace + "id")
            ?.Value;

        if (string.IsNullOrWhiteSpace(firstSheetId))
        {
            return archive.GetEntry("xl/worksheets/sheet1.xml");
        }

        var target = relationshipDocument
            .Descendants(packageRelationshipNamespace + "Relationship")
            .FirstOrDefault(relationship => relationship.Attribute("Id")?.Value == firstSheetId)
            ?.Attribute("Target")
            ?.Value
            ?.Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(target))
        {
            return archive.GetEntry("xl/worksheets/sheet1.xml");
        }

        var sheetPath = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : "xl/" + target.TrimStart('/');

        return archive.GetEntry(sheetPath);
    }

    private static string ReadCellValue(
        XElement cellElement,
        XNamespace worksheetNamespace,
        IReadOnlyList<string> sharedStrings)
    {
        var cellType = cellElement.Attribute("t")?.Value ?? "";

        if (cellType == "inlineStr")
        {
            return TrimCellText(string.Concat(cellElement.Descendants(worksheetNamespace + "t").Select(text => text.Value)));
        }

        var rawValue = cellElement.Element(worksheetNamespace + "v")?.Value ?? "";

        if (cellType == "s" && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex))
        {
            return sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count
                ? TrimCellText(sharedStrings[sharedStringIndex])
                : "";
        }

        if (cellType == "b")
        {
            return rawValue == "1" ? "是" : "否";
        }

        return TrimCellText(rawValue);
    }

    private static IReadOnlyList<GroupRosterMemberViewModel> BuildMembers(IEnumerable<WorksheetRow> rows)
    {
        var rowArray = rows.ToArray();
        var firstContentRow = rowArray.FirstOrDefault(row => !IsBlankRow(row.Values));

        if (firstContentRow is null)
        {
            return [];
        }

        var headerMatch = FindHeaderRow(rowArray);
        var headerRow = headerMatch?.Row ?? firstContentRow;
        var fieldIndexes = headerMatch?.HeaderMap ?? LegacyFieldIndexes;

        return rowArray
            .Where(row => row.RowNumber > headerRow.RowNumber)
            .Where(row => !IsBlankRow(row.Values))
            .Select(row => BuildMember(row, fieldIndexes))
            .ToArray();
    }

    private static HeaderRowMatch? FindHeaderRow(IReadOnlyList<WorksheetRow> rows)
    {
        HeaderRowMatch? bestMatch = null;

        foreach (var row in rows.Where(row => !IsBlankRow(row.Values)).Take(HeaderScanRowLimit))
        {
            var headerMap = BuildHeaderMap(row.Values);

            if (!IsReliableHeaderMap(headerMap))
            {
                continue;
            }

            var requiredMatchCount = CountRequiredHeaderFields(headerMap);
            var score = headerMap.Count + (requiredMatchCount * 2);

            if (bestMatch is null || score > bestMatch.Score)
            {
                bestMatch = new HeaderRowMatch(row, headerMap, score);
            }
        }

        return bestMatch;
    }

    private static bool IsReliableHeaderMap(IReadOnlyDictionary<RosterField, int> headerMap) =>
        headerMap.Count >= MinimumHeaderMatchCount
        && CountRequiredHeaderFields(headerMap) >= 2;

    private static int CountRequiredHeaderFields(IReadOnlyDictionary<RosterField, int> headerMap) =>
        RequiredHeaderFields.Count(headerMap.ContainsKey);

    private static GroupRosterMemberViewModel BuildMember(
        WorksheetRow row,
        IReadOnlyDictionary<RosterField, int> fieldIndexes)
    {
        var idNo = ValueFor(row.Values, fieldIndexes, RosterField.IdNo);
        var name = ValueFor(row.Values, fieldIndexes, RosterField.Name);
        var sex = ValueFor(row.Values, fieldIndexes, RosterField.Sex);
        var staffNo = ValueFor(row.Values, fieldIndexes, RosterField.StaffNo);
        var companyBranch = ValueFor(row.Values, fieldIndexes, RosterField.CompanyBranch);
        var department = ValueFor(row.Values, fieldIndexes, RosterField.Department);
        var unitClass = ValueFor(row.Values, fieldIndexes, RosterField.UnitClass);
        var jobTitleSeatNo = ValueFor(row.Values, fieldIndexes, RosterField.JobTitleSeatNo);
        var birthday = DateValueFor(row.Values, fieldIndexes, RosterField.Birthday);
        var phone = ValueFor(row.Values, fieldIndexes, RosterField.Phone);
        var mobile = ValueFor(row.Values, fieldIndexes, RosterField.Mobile);
        var address = ValueFor(row.Values, fieldIndexes, RosterField.Address);
        var email = ValueFor(row.Values, fieldIndexes, RosterField.Email);
        var checkDate = DateValueFor(row.Values, fieldIndexes, RosterField.CheckDate);
        var isDependent = ValueFor(row.Values, fieldIndexes, RosterField.IsDependent);
        var dependentEmployeeId = ValueFor(row.Values, fieldIndexes, RosterField.DependentEmployeeId);
        var chartNo = ValueFor(row.Values, fieldIndexes, RosterField.ChartNo);
        var nativePlace = ValueFor(row.Values, fieldIndexes, RosterField.NativePlace);
        var note = ValueFor(row.Values, fieldIndexes, RosterField.Note);
        var occupationalAccidentBarcode = ValueFor(row.Values, fieldIndexes, RosterField.OccupationalAccidentBarcode);
        var workArea = ValueFor(row.Values, fieldIndexes, RosterField.WorkArea);
        var labNo = ValueFor(row.Values, fieldIndexes, RosterField.LabNo);
        var displayPassword = ValueFor(row.Values, fieldIndexes, RosterField.DisplayPassword);
        var sequenceNo = ValueFor(row.Values, fieldIndexes, RosterField.SequenceNo);
        var reserveDate = DateValueFor(row.Values, fieldIndexes, RosterField.ReserveDate);
        var environmentTest = ValueFor(row.Values, fieldIndexes, RosterField.EnvironmentTest);
        var environmentTestDescription = ValueFor(row.Values, fieldIndexes, RosterField.EnvironmentTestDescription);
        var checkNo = ValueFor(row.Values, fieldIndexes, RosterField.CheckNo);
        var publicPackageName = ValueFor(row.Values, fieldIndexes, RosterField.PublicPackageName);
        var selfPayAddOns = ValueFor(row.Values, fieldIndexes, RosterField.SelfPayAddOns);
        var validation = ResolveValidation(
            idNo,
            name,
            sex,
            phone,
            mobile,
            email,
            checkDate,
            isDependent,
            dependentEmployeeId,
            environmentTest,
            environmentTestDescription,
            publicPackageName);

        return new GroupRosterMemberViewModel
        {
            RowNumber = row.RowNumber,
            IdNo = idNo,
            Name = name,
            Sex = sex,
            StaffNo = staffNo,
            CompanyBranch = companyBranch,
            Department = department,
            UnitClass = unitClass,
            JobTitleSeatNo = jobTitleSeatNo,
            Birthday = birthday,
            Phone = phone,
            Mobile = mobile,
            Address = address,
            Email = email,
            CheckDate = checkDate,
            IsDependent = isDependent,
            DependentEmployeeId = dependentEmployeeId,
            ChartNo = chartNo,
            NativePlace = nativePlace,
            Note = note,
            OccupationalAccidentBarcode = occupationalAccidentBarcode,
            WorkArea = workArea,
            LabNo = labNo,
            DisplayPassword = displayPassword,
            SequenceNo = sequenceNo,
            ReserveDate = reserveDate,
            EnvironmentTest = environmentTest,
            EnvironmentTestDescription = environmentTestDescription,
            CheckNo = checkNo,
            PublicPackageName = publicPackageName,
            SelfPayAddOns = selfPayAddOns,
            Status = validation.Status,
            Tone = validation.Tone,
            ValidationMessage = validation.Message
        };
    }

    private static (string Status, string Tone, string Message) ResolveValidation(
        string idNo,
        string name,
        string sex,
        string phone,
        string mobile,
        string email,
        string checkDate,
        string isDependent,
        string dependentEmployeeId,
        string environmentTest,
        string environmentTestDescription,
        string publicPackageName)
    {
        var errors = new List<string>();
        AddRequiredError(errors, idNo, "身分證號");
        AddRequiredError(errors, name, "姓名");
        AddRequiredError(errors, sex, "性別");
        AddRequiredError(errors, checkDate, "預約日期");
        AddRequiredError(errors, publicPackageName, "健檢專案 / 方案");

        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(mobile))
        {
            errors.Add("聯絡電話或手機");
        }

        if (errors.Count > 0)
        {
            return ("格式錯誤", "error", string.Join("、", errors) + "必填");
        }

        var pendingItems = new List<string>();

        if (IsYes(isDependent) && string.IsNullOrWhiteSpace(dependentEmployeeId))
        {
            pendingItems.Add("眷屬缺少員工ID");
        }

        if (IsYes(environmentTest) && string.IsNullOrWhiteSpace(environmentTestDescription))
        {
            pendingItems.Add("環境檢測缺少描述");
        }

        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@', StringComparison.Ordinal))
        {
            pendingItems.Add("E-MAIL格式需確認");
        }

        if (pendingItems.Count > 0)
        {
            return ("待確認", "pending", string.Join("、", pendingItems));
        }

        return ("匯入成功", "success", "");
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index += 1)
        {
            var current = line[index];

            if (current == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index += 1;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (current == ',' && !inQuotes)
            {
                values.Add(TrimCellText(field.ToString()));
                field.Clear();
                continue;
            }

            field.Append(current);
        }

        values.Add(TrimCellText(field.ToString()));
        return values;
    }

    private static void AddRequiredError(ICollection<string> errors, string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(label);
        }
    }

    private static IReadOnlyDictionary<string, RosterField> BuildHeaderLookup()
    {
        var lookup = new Dictionary<string, RosterField>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in RosterColumns)
        {
            foreach (var alias in column.Aliases)
            {
                var normalizedAlias = NormalizeHeader(alias);

                if (!string.IsNullOrWhiteSpace(normalizedAlias) && !lookup.ContainsKey(normalizedAlias))
                {
                    lookup[normalizedAlias] = column.Field;
                }
            }
        }

        return lookup;
    }

    private static IReadOnlyDictionary<RosterField, int> BuildHeaderMap(IReadOnlyList<string> headers)
    {
        var headerMap = new Dictionary<RosterField, int>();

        for (var index = 0; index < headers.Count; index += 1)
        {
            var field = FindRosterField(headers[index]);

            if (field.HasValue && !headerMap.ContainsKey(field.Value))
            {
                headerMap[field.Value] = index;
            }
        }

        return headerMap;
    }

    private static RosterField? FindRosterField(string header)
    {
        var normalizedHeader = NormalizeHeader(header);

        if (string.IsNullOrWhiteSpace(normalizedHeader))
        {
            return null;
        }

        if (HeaderLookup.TryGetValue(normalizedHeader, out var exactField))
        {
            return exactField;
        }

        foreach (var column in RosterColumns)
        {
            if (column.Aliases.Any(alias => IsSimilarHeader(normalizedHeader, NormalizeHeader(alias))))
            {
                return column.Field;
            }
        }

        return null;
    }

    private static bool IsSimilarHeader(string normalizedHeader, string normalizedAlias) =>
        normalizedAlias.Length >= 3
        && (normalizedHeader.Contains(normalizedAlias, StringComparison.OrdinalIgnoreCase)
            || normalizedAlias.Contains(normalizedHeader, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeHeader(string? value)
    {
        var normalizedValue = (value ?? "")
            .Normalize(NormalizationForm.FormKC)
            .Trim()
            .ToUpperInvariant();
        var builder = new StringBuilder(normalizedValue.Length);

        foreach (var character in normalizedValue)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static bool IsYes(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized is "是" or "Y" or "YES" or "TRUE" or "1";
    }

    private static string ValueAt(IReadOnlyList<string> values, int index) =>
        index >= 0 && index < values.Count
            ? TrimCellText(values[index])
            : "";

    private static string ValueFor(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<RosterField, int> fieldIndexes,
        RosterField field) =>
        fieldIndexes.TryGetValue(field, out var index)
            ? ValueAt(values, index)
            : "";

    private static string DateValueFor(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<RosterField, int> fieldIndexes,
        RosterField field) =>
        FormatExcelDate(ValueFor(values, fieldIndexes, field));

    private static void SetCellValue(List<string> values, int columnIndex, string value)
    {
        while (values.Count < columnIndex)
        {
            values.Add("");
        }

        values[columnIndex - 1] = value;
    }

    private static bool IsBlankRow(IReadOnlyList<string> values) =>
        values.All(string.IsNullOrWhiteSpace);

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return 0;
        }

        var columnIndex = 0;

        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            columnIndex = columnIndex * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }

        return columnIndex;
    }

    private static string FormatExcelDate(string rawValue)
    {
        var trimmedValue = TrimCellText(rawValue);

        if (string.IsNullOrWhiteSpace(trimmedValue))
        {
            return "";
        }

        if (double.TryParse(trimmedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var serialDate))
        {
            try
            {
                return DateTime.FromOADate(serialDate).ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            }
            catch (ArgumentException)
            {
                return trimmedValue;
            }
        }

        return DateTime.TryParse(trimmedValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
            : trimmedValue;
    }

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string TrimCellText(string? value) =>
        (value ?? "").Trim();

    private sealed record WorksheetRow(int RowNumber, IReadOnlyList<string> Values);

    private sealed record HeaderRowMatch(
        WorksheetRow Row,
        IReadOnlyDictionary<RosterField, int> HeaderMap,
        int Score);

    private sealed record RosterColumn(RosterField Field, string[] Aliases);

    private enum RosterField
    {
        IdNo,
        ChartNo,
        Name,
        Sex,
        Birthday,
        Phone,
        Mobile,
        Address,
        Email,
        CheckDate,
        IsDependent,
        DependentEmployeeId,
        Note,
        PublicPackageName,
        SelfPayAddOns,
        CompanyBranch,
        JobTitleSeatNo,
        StaffNo,
        Department,
        UnitClass,
        NativePlace,
        OccupationalAccidentBarcode,
        WorkArea,
        LabNo,
        DisplayPassword,
        SequenceNo,
        ReserveDate,
        EnvironmentTest,
        EnvironmentTestDescription,
        CheckNo
    }
}
