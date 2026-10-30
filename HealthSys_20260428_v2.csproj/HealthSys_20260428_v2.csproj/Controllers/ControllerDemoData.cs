using HealthSys_20260428_v2.csproj.Models;
using HealthSys_20260428_v2.csproj.Data;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HealthSys_20260428_v2.csproj.Controllers;

// All demonstration records live in the Controller layer. Existing controllers map
// these tables into their ViewModels and pass those models to Views as before.
// SQL is only a fixture identifier here; no connection or SQL execution takes place.
// Edits are shared across requests and discarded when the application restarts.
internal sealed class ControllerDemoData
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, List<Dictionary<string, object?>>> Tables = Seed();
    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] fields) =>
        fields.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    private static object? Unwrap(object? value) => value is OracleParameterValue p ? p.Value : value is DBNull ? null : value;
    private static string Text(object? value) => Convert.ToString(Unwrap(value), CultureInfo.InvariantCulture)?.Trim() ?? "";
    private static string Value(Dictionary<string, object?> row, string key) => row.TryGetValue(key, out var v) ? Text(v) : "";

    private static Dictionary<string, List<Dictionary<string, object?>>> Seed()
    {
        var data = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, params Dictionary<string, object?>[] rows) => data[name] = rows.ToList();
        Add("HealthRESERVEAREATBL", Row(("AREA_CODE", "A"), ("AREA_NAME", "健檢一區"), ("STATUS", "Y")),
            Row(("AREA_CODE", "B"), ("AREA_NAME", "健檢二區"), ("STATUS", "Y")));
        Add("HealthPHRASENOTETBL", Row(("CATEGORY", "檢查提醒"), ("PHRASE_CONTENT", "此為示範資料，檢查前請依通知準備。")),
            Row(("CATEGORY", "服務備註"), ("PHRASE_CONTENT", "示範受檢者需要專人引導。")));
        Add("FlowListTBL", Row(("NO", "F01"), ("NAME", "一般健檢流程")), Row(("NO", "F02"), ("NAME", "進階健檢流程")));
        Add("GENDCTTYPETBL", Row(("CHDCTTYPE", "00"), ("CHDCTTYPENAME", "一般自費"), ("DCTTYPE_DISPLAY", "[00] 一般自費")),
            Row(("CHDCTTYPE", "01"), ("CHDCTTYPENAME", "示範企業"), ("DCTTYPE_DISPLAY", "[01] 示範企業")));
        Add("GENUSERPROFILE1", Row(("CHUSERID", "D001"), ("CHUSERNAME", "示範醫師甲"), ("DOCTOREMP", "(D001) 示範醫師甲")),
            Row(("CHUSERID", "D002"), ("CHUSERNAME", "示範醫師乙"), ("DOCTOREMP", "(D002) 示範醫師乙")));
        Add("ROLE_MASTER", Row(("ROLE_ID", "ADMIN"), ("ROLE_NAME", "系統管理員"), ("ROLE_DESC", "示範管理角色"), ("IS_ACTIVE", 1)),
            Row(("ROLE_ID", "COUNTER"), ("ROLE_NAME", "櫃檯人員"), ("ROLE_DESC", "預約與報到"), ("IS_ACTIVE", 1)),
            Row(("ROLE_ID", "DOCTOR"), ("ROLE_NAME", "醫師"), ("ROLE_DESC", "檢查報告"), ("IS_ACTIVE", 1)));
        Add("ACCOUNT", Row(("H_EMPNO", "DEMO001"), ("H_EMPNANE", "示範管理員"), ("H_DPTNAME", "健檢中心"), ("H_EMPPOSITION", "管理員"), ("STATUS", "active"), ("ROLE_CODE", "ADMIN"), ("CDATE", DateTime.Today)),
            Row(("H_EMPNO", "DEMO002"), ("H_EMPNANE", "示範櫃檯"), ("H_DPTNAME", "健檢中心"), ("H_EMPPOSITION", "專員"), ("STATUS", "active"), ("ROLE_CODE", "COUNTER"), ("CDATE", DateTime.Today)));
        Add("SYSTEM_FUNCTION", Row(("FUNCTION_ID", "APPOINTMENT"), ("PARENT_ID", ""), ("FUNCTION_NAME", "預約管理"), ("SORT_NO", 1), ("CAN_VIEW", 1), ("CAN_REVIEW", 1)),
            Row(("FUNCTION_ID", "APPOINTMENT_LIST"), ("PARENT_ID", "APPOINTMENT"), ("FUNCTION_NAME", "預約清單"), ("SORT_NO", 2), ("CAN_VIEW", 1), ("CAN_REVIEW", 1)),
            Row(("FUNCTION_ID", "REPORTS"), ("PARENT_ID", ""), ("FUNCTION_NAME", "檢查報告"), ("SORT_NO", 3), ("CAN_VIEW", 1), ("CAN_REVIEW", 1)));
        Add("ROLE_PERMISSION");
        Add("CheckItem", Row(("CHECKNO", "C001"), ("NAME", "身高體重"), ("ENAME", "Height and weight"), ("CHECKCATEGORY", "基本體格檢查"), ("ITEMCATEGORY", "體格"), ("REPORTNAME", "身高體重"), ("FORMAT", "數值"), ("UNIT", "cm/kg")),
            Row(("CHECKNO", "C002"), ("NAME", "血糖"), ("ENAME", "Glucose"), ("CHECKCATEGORY", "血液檢查"), ("ITEMCATEGORY", "生化"), ("REPORTNAME", "空腹血糖"), ("FORMAT", "數值"), ("UNIT", "mg/dL")),
            Row(("CHECKNO", "C003"), ("NAME", "胸部X光"), ("ENAME", "Chest X-ray"), ("CHECKCATEGORY", "影像檢查"), ("ITEMCATEGORY", "放射"), ("REPORTNAME", "胸部X光"), ("FORMAT", "文字"), ("UNIT", "")));
        Add("SpecialCategoryTBL", Row(("CATEGORYNO", "S01"), ("SPECIALITEMNO", "S01"), ("NAME", "健康問卷"), ("PUBLICEXPENSE", 0), ("FIRSTCHAR", "健")),
            Row(("CATEGORYNO", "S02"), ("SPECIALITEMNO", "S02"), ("NAME", "營養諮詢"), ("PUBLICEXPENSE", 0), ("FIRSTCHAR", "營")));
        for (var i = 3; i <= 6; i++)
            data["HealthRESERVEAREATBL"].Add(Row(("AREA_CODE", ((char)('A' + i - 1)).ToString()), ("AREA_NAME", $"健檢示範{i}區"), ("STATUS", "Y")));
        for (var i = 3; i <= 8; i++)
        {
            data["FlowListTBL"].Add(Row(("NO", $"F{i:D2}"), ("NAME", $"示範健檢流程{i}")));
            data["GENUSERPROFILE1"].Add(Row(("CHUSERID", $"D{i:D3}"), ("CHUSERNAME", $"示範醫師{i}"), ("DOCTOREMP", $"(D{i:D3}) 示範醫師{i}")));
        }
        for (var i = 3; i <= 12; i++)
            data["HealthPHRASENOTETBL"].Add(Row(("CATEGORY", i % 2 == 0 ? "檢查提醒" : "服務備註"),
                ("PHRASE_CONTENT", $"示範提醒{i}：" + new[] { "請依通知時間報到。", "如需更改預約請聯絡櫃檯。", "檢查流程請參閱現場指引。", "報告完成後將另行通知。", "可向服務人員確認餐點與停車安排。" }[i % 5])));
        data["ROLE_MASTER"].Add(Row(("ROLE_ID", "FINANCE"), ("ROLE_NAME", "帳務人員"), ("ROLE_DESC", "示範費用與帳目管理"), ("IS_ACTIVE", 1)));
        data["ROLE_MASTER"].Add(Row(("ROLE_ID", "EXAM"), ("ROLE_NAME", "檢查人員"), ("ROLE_DESC", "示範檢查流程管理"), ("IS_ACTIVE", 1)));
        (string Code, string Name, string Department)[] extraRoles =
        [
            ("NURSE", "護理人員", "護理組"), ("LAB", "醫檢人員", "檢驗組"), ("RADIOLOGY", "放射人員", "影像組"),
            ("NUTRITION", "營養師", "營養組"), ("COORDINATOR", "健檢排程人員", "排程組"), ("REPORT_REVIEW", "報告覆核人員", "報告組"),
            ("CASE_MANAGER", "個案管理師", "個案管理組"), ("CUSTOMER_SERVICE", "客服人員", "客服組"), ("PROJECT_MANAGER", "專案管理人員", "專案組"),
            ("AUDITOR", "稽核人員", "稽核組"), ("BILLING_MANAGER", "帳務主管", "帳務組"), ("SUPERVISOR", "健檢主管", "健檢中心"),
            ("READONLY", "唯讀查詢人員", "行政組")
        ];
        foreach (var role in extraRoles)
            data["ROLE_MASTER"].Add(Row(("ROLE_ID", role.Code), ("ROLE_NAME", role.Name), ("ROLE_DESC", $"示範{role.Name}權限"), ("IS_ACTIVE", 1)));
        (string Code, string Name)[] modules = [("CHECKIN", "報到作業"), ("BASIC_DATA", "基本資料"), ("CUSTOMERS", "公司管理"), ("PROJECTS", "專案管理"),
            ("MEDICAL_ORDERS", "健檢醫令"), ("PACKAGES", "套餐管理"), ("STATISTICS", "帳目查詢"), ("SETTINGS", "權限與人員")];
        foreach (var module in modules)
        {
            var sort = data["SYSTEM_FUNCTION"].Count + 1;
            data["SYSTEM_FUNCTION"].Add(Row(("FUNCTION_ID", module.Code), ("PARENT_ID", ""), ("FUNCTION_NAME", module.Name), ("SORT_NO", sort)));
            data["SYSTEM_FUNCTION"].Add(Row(("FUNCTION_ID", module.Code + "_LIST"), ("PARENT_ID", module.Code), ("FUNCTION_NAME", module.Name + "查詢"), ("SORT_NO", sort + 1)));
            data["SYSTEM_FUNCTION"].Add(Row(("FUNCTION_ID", module.Code + "_EDIT"), ("PARENT_ID", module.Code), ("FUNCTION_NAME", module.Name + "維護"), ("SORT_NO", sort + 2)));
        }
        foreach (var function in new[] { ("APPOINTMENT_EDIT", "APPOINTMENT", "預約維護"), ("REPORT_LIST", "REPORTS", "檢查報告查詢"), ("REPORT_REVIEW", "REPORTS", "檢查報告覆核") })
            data["SYSTEM_FUNCTION"].Add(Row(("FUNCTION_ID", function.Item1), ("PARENT_ID", function.Item2), ("FUNCTION_NAME", function.Item3), ("SORT_NO", data["SYSTEM_FUNCTION"].Count + 1)));
        foreach (var role in data["ROLE_MASTER"])
        {
            var code = Value(role, "ROLE_ID");
            string[] allowed = code switch
            {
                "FINANCE" or "BILLING_MANAGER" => ["STATISTICS", "CUSTOMERS"],
                "DOCTOR" => ["REPORTS", "CHECKIN", "APPOINTMENT"],
                "NURSE" or "EXAM" or "LAB" or "RADIOLOGY" => ["REPORTS", "CHECKIN"],
                "NUTRITION" or "REPORT_REVIEW" => ["REPORTS"],
                "PROJECT_MANAGER" => ["PROJECTS", "CUSTOMERS", "PACKAGES", "MEDICAL_ORDERS"],
                "AUDITOR" => ["STATISTICS", "REPORTS", "SETTINGS"],
                _ => ["APPOINTMENT", "CHECKIN", "CUSTOMERS"]
            };
            foreach (var function in data["SYSTEM_FUNCTION"])
            {
                var module = string.IsNullOrEmpty(Value(function, "PARENT_ID")) ? Value(function, "FUNCTION_ID") : Value(function, "PARENT_ID");
                var canView = code is "ADMIN" or "SUPERVISOR" or "READONLY" || allowed.Contains(module);
                data["ROLE_PERMISSION"].Add(Row(("ROLE_ID", code), ("FUNCTION_ID", function["FUNCTION_ID"]), ("CAN_VIEW", canView ? 1 : 0),
                    ("CAN_REVIEW", canView && code is not ("READONLY" or "AUDITOR" or "CUSTOMER_SERVICE") ? 1 : 0)));
            }
        }
        for (var i = 3; i <= 150; i++)
        {
            var role = data["ROLE_MASTER"][(i - 1) % data["ROLE_MASTER"].Count];
            var roleCode = Value(role, "ROLE_ID");
            var department = extraRoles.FirstOrDefault(r => r.Code == roleCode).Department ?? "健檢中心";
            data["ACCOUNT"].Add(Row(("H_EMPNO", $"DEMO{i:D3}"), ("H_EMPNANE", $"示範員工{i:D3}"), ("H_DPTNAME", department),
                ("H_EMPPOSITION", i % 9 == 0 ? "主管" : i % 4 == 0 ? "組長" : "專員"), ("STATUS", i % 7 == 0 ? "inactive" : "active"),
                ("ROLE_CODE", i % 4 == 0 && roleCode != "READONLY" ? roleCode + ",READONLY" : roleCode), ("CDATE", DateTime.Today.AddDays(-i))));
        }
        (string Name, string English, string Category, string Unit)[] extraChecks =
        [
            ("血壓", "Blood pressure", "基本體格檢查", "mmHg"), ("腰圍", "Waist circumference", "基本體格檢查", "cm"),
            ("視力", "Visual acuity", "基本體格檢查", ""), ("聽力", "Hearing test", "基本體格檢查", "dB"),
            ("身體質量指數", "BMI", "基本體格檢查", "kg/m2"), ("白血球", "WBC", "血液檢查", "10^3/uL"),
            ("紅血球", "RBC", "血液檢查", "10^6/uL"), ("血色素", "Hemoglobin", "血液檢查", "g/dL"),
            ("血小板", "Platelet", "血液檢查", "10^3/uL"), ("糖化血色素", "HbA1c", "血液檢查", "%"),
            ("總膽固醇", "Total cholesterol", "血液檢查", "mg/dL"), ("三酸甘油脂", "Triglycerides", "血液檢查", "mg/dL"),
            ("高密度膽固醇", "HDL", "血液檢查", "mg/dL"), ("低密度膽固醇", "LDL", "血液檢查", "mg/dL"),
            ("肝功能AST", "AST", "生化檢查", "U/L"), ("肝功能ALT", "ALT", "生化檢查", "U/L"),
            ("肌酸酐", "Creatinine", "生化檢查", "mg/dL"), ("尿酸", "Uric acid", "生化檢查", "mg/dL"),
            ("腎絲球過濾率", "eGFR", "生化檢查", "mL/min"), ("尿蛋白", "Urine protein", "尿液檢查", ""),
            ("尿糖", "Urine glucose", "尿液檢查", ""), ("尿潛血", "Urine occult blood", "尿液檢查", ""),
            ("腹部超音波", "Abdominal ultrasound", "影像檢查", ""), ("甲狀腺超音波", "Thyroid ultrasound", "影像檢查", ""),
            ("乳房超音波", "Breast ultrasound", "影像檢查", ""), ("心電圖", "ECG", "生理檢查", ""),
            ("肺功能", "Pulmonary function", "生理檢查", "L"), ("骨質密度", "Bone density", "生理檢查", ""),
            ("營養評估", "Nutrition assessment", "健康諮詢", ""), ("健康諮詢", "Health consultation", "健康諮詢", "")
        ];
        for (var i = 0; i < extraChecks.Length; i++)
        {
            var item = extraChecks[i];
            data["CheckItem"].Add(Row(("CHECKNO", $"C{i + 4:D3}"), ("NAME", item.Name), ("ENAME", item.English), ("CHECKCATEGORY", item.Category),
                ("ITEMCATEGORY", item.Category), ("REPORTNAME", item.Name), ("FORMAT", string.IsNullOrEmpty(item.Unit) ? "文字" : "數值"), ("UNIT", item.Unit)));
        }
        (string Category, string[] Names)[] additionalCategories =
        [
            ("肝膽功能", ["鹼性磷酸酶", "總膽紅素", "直接膽紅素", "白蛋白", "總蛋白"]),
            ("腎臟功能", ["尿素氮", "尿白蛋白", "尿白蛋白肌酸酐比值", "胱抑素C", "尿比重"]),
            ("血脂評估", ["非高密度膽固醇", "載脂蛋白A1", "載脂蛋白B", "脂蛋白a", "膽固醇比值"]),
            ("糖尿病評估", ["空腹胰島素", "飯後血糖", "胰島素阻抗", "C胜肽", "果糖胺"]),
            ("甲狀腺功能", ["甲狀腺刺激素", "游離甲狀腺素", "三碘甲狀腺素", "甲狀腺球蛋白抗體", "甲狀腺過氧化酶抗體"]),
            ("心血管評估", ["高敏感度C反應蛋白", "同半胱胺酸", "心臟超音波", "頸動脈超音波", "動脈硬化評估"]),
            ("腫瘤標記", ["甲型胎兒蛋白", "癌胚抗原", "攝護腺特異抗原", "CA125", "CA19-9"]),
            ("肝炎篩檢", ["B型肝炎表面抗原", "B型肝炎表面抗體", "B型肝炎核心抗體", "C型肝炎抗體", "A型肝炎抗體"]),
            ("感染篩檢", ["梅毒篩檢", "HIV抗原抗體", "結核菌檢測", "幽門螺旋桿菌抗原", "麻疹抗體"]),
            ("免疫與過敏", ["免疫球蛋白E", "類風濕因子", "抗核抗體", "補體C3", "補體C4"]),
            ("電解質檢查", ["血鈉", "血鉀", "血氯", "血鈣", "血鎂"]),
            ("糞便檢查", ["糞便潛血", "糞便常規", "糞便寄生蟲", "糞便白血球", "糞便脂肪"]),
            ("腸胃檢查", ["胃鏡", "大腸鏡", "胃蛋白酶原", "肝臟纖維化評估", "胃泌素"]),
            ("婦科檢查", ["子宮頸抹片", "人類乳突病毒檢測", "婦科超音波", "乳房攝影", "婦科諮詢"]),
            ("眼科檢查", ["眼壓", "眼底攝影", "色覺", "裂隙燈檢查", "眼科諮詢"]),
            ("耳鼻喉檢查", ["純音聽力", "鼓室圖", "鼻咽內視鏡", "耳鏡檢查", "耳鼻喉諮詢"]),
            ("肺部評估", ["低劑量肺部電腦斷層", "尖峰呼氣流量", "血氧飽和度", "肺部超音波", "呼吸道評估"])
        ];
        foreach (var category in additionalCategories)
            foreach (var name in category.Names)
            {
                var code = $"C{data["CheckItem"].Count + 1:D3}";
                data["CheckItem"].Add(Row(("CHECKNO", code), ("NAME", name), ("ENAME", $"Demo examination {code}"),
                    ("CHECKCATEGORY", category.Category), ("ITEMCATEGORY", category.Category), ("REPORTNAME", name),
                    ("FORMAT", category.Category == "電解質檢查" ? "數值" : "文字"), ("UNIT", category.Category == "電解質檢查" ? "mmol/L" : "")));
            }
        Add("HealthCustomerList"); Add("HealthProjectList"); Add("CheckProjectScheme"); Add("CheckProjectSpecialItemTbl");
        Add("CheckGlobalScheme", Row(("PROJECTNO", "DEMO01"), ("PROJECTNAME", "示範標準方案")));
        for (var i = 1; i <= 24; i++)
        {
            var no = i.ToString("D4"); var project = $"DEMO{i:D2}"; var company = $"示範企業{i}";
            data["HealthCustomerList"].Add(Row(("CUSTOMERNO", no), ("CUSTOMERNAME", company), ("CustUnit", $"[{no}] {company}"),
                ("ADDRESS", "示範市測試路1號"), ("TEL", "02-0000-0000"), ("FAX", "02-0000-0001"), ("EMAIL", $"demo{i}@example.invalid"),
                ("CUSTOMERNOTE", "虛構公司，僅供展示"), ("ENGLISHNAME", $"Demo Company {i}"), ("UNIFORMNO", "00000000"),
                ("CONTACTPERSON", "示範聯絡人"), ("BUSINESSREGADDRESS", "示範市測試路1號"), ("INDUSTRYCATEGORY", "服務業"),
                ("BUSINESSUNITNAME", company), ("BUSINESSUNITADDRESS", "示範市測試路1號"), ("ACCOUNTINGUNIT", "00")));
            data["HealthProjectList"].Add(Row(("PROJECTNO", project), ("PROJECTNAME", $"菁英示範健檢方案{i}"), ("CUSTOMERNO", no),
                ("SELFCOSTNO", "00"), ("YEAR", DateTime.Today.Year), ("CHECKYEAR", DateTime.Today.Year), ("CHECKDATE", DateTime.Today),
                ("CREATEDATE", DateTime.Today.AddDays(-7)), ("EDITDATE", DateTime.Today), ("ENDDATE", DateTime.Today.AddYears(1)),
                ("NOTE", "示範專案"), ("EXECAREA", "A"), ("FLOWNAME", "一般健檢流程"), ("QUESTIONNAIRENAME", "健康問卷"),
                ("STATUS", "啟用"), ("ISPASTHISTORY", "Y"), ("ISORALCHECK", "Y"), ("ISONLINEQUESTION", "Y"), ("COMPAREVAL", "0")));
            data["CheckProjectScheme"].Add(Row(("PROJECTNO", project), ("SCHEMENO", "1"), ("LISITEMNAME", "294-DEMO"),
                ("NAME", "示範標準套餐"), ("NOTE", "示範套餐"), ("PUBLICEXPENSE", 3500), ("CHDCTTYPE", "00"), ("FIXITEMS", "C001,C002,C003"), ("SCHEMECODE", "DEMO")));
            data["CheckProjectScheme"].Add(Row(("PROJECTNO", project), ("SCHEMENO", "2"), ("LISITEMNAME", "294-CARDIO"),
                ("NAME", "示範心血管套餐"), ("NOTE", "示範進階套餐"), ("PUBLICEXPENSE", 6500), ("CHDCTTYPE", "00"), ("FIXITEMS", "C004,C014,C015,C016,C029"), ("SCHEMECODE", $"CARDIO{i:D2}")));
            data["CheckProjectScheme"].Add(Row(("PROJECTNO", project), ("SCHEMENO", "3"), ("LISITEMNAME", "294-METAB"),
                ("NAME", "示範代謝套餐"), ("NOTE", "示範代謝評估"), ("PUBLICEXPENSE", 4800), ("CHDCTTYPE", "01"), ("FIXITEMS", "C002,C013,C014,C015,C020,C021"), ("SCHEMECODE", $"METAB{i:D2}")));
            data["CheckProjectSpecialItemTbl"].Add(Row(("PROJECTNO", project), ("SPECIALITEMNO", "S01"), ("NAME", "健康問卷"), ("PUBLICEXPENSE", 0)));
        }
        Add("HealthCheckupList"); Add("FEMHCheckupList"); Add("HealthCheckupPatient"); Add("HealthCheckupNOTE");
        Add("HealthCheckupReserInfo"); Add("HealthCheckupPayment"); Add("HealthCheckupSetInfo"); Add("HealthCheckupOtherSet"); Add("HealthCheckupQuestInfo");
        string[] names = ["示範王小明", "示範陳美玲", "示範林大華", "示範張雅婷", "示範李志強", "示範黃怡君"];
        string[] statuses = ["1", "4", "5", "6", "2", "0"];
        for (var i = 0; i < 120; i++)
        {
            var projectIndex = i < 6 ? i % 3 + 1 : i % 24 + 1;
            var barcode = $"DEMO{i + 1:D6}"; var project = $"DEMO{projectIndex:D2}";
            var reservationDate = i < 60 ? DateTime.Today : DateTime.Today.AddDays(i % 31 - 15);
            var birthday = new DateTime(i < 6 ? 1980 + i : 1960 + i % 45, 5, 10);
            var age = DateTime.Today.Year - birthday.Year - (birthday.AddYears(DateTime.Today.Year - birthday.Year) > DateTime.Today ? 1 : 0);
            var area = data["HealthRESERVEAREATBL"][i < 6 ? i % 2 : i % 6];
            var person = Row(("BARCODESTR", barcode), ("PROJECTNO", project), ("CUSTOMERNO", $"{projectIndex:D4}"), ("PATNO", $"99000{i + 1:D3}"),
                ("PATIENTNAME", i < names.Length ? names[i] : $"示範受檢者{i + 1:D3}"), ("IDNUMBER", $"DEMO{i + 1:D6}"), ("GENDER", i % 2 == 0 ? "男" : "女"),
                ("BIRTHDAY", birthday), ("AGE", age), ("MOBILE", $"090000{i + 1:D4}"),
                ("TEL", "02-0000-0000"), ("EMAIL", $"patient{i + 1}@example.invalid"), ("ISRESERVE", statuses[i % statuses.Length]),
                ("RESERVEDATE", reservationDate), ("RESERVETIME", $"{8 + i % 24 / 4:D2}:{i % 4 * 15:D2}"), ("WORKAREA", area["AREA_NAME"]),
                ("CHECKINDATE", i % 6 is 2 or 3 ? reservationDate.ToString("yyyy/MM/dd") : ""), ("CHECKINTIME", i % 6 is 2 or 3 ? "08:15" : ""),
                ("REGTIME", "1"), ("DOCTOREMPNO", "D001"), ("DOCTORNAME", "示範醫師甲"), ("ROOMNO", "DEMO01"),
                ("CATEGORY", "團體"), ("SELFCOSTNO", "00"), ("CREATEDAT", DateTime.Today), ("UPDATEDAT", DateTime.Today),
                ("HAS_SELECTED_APPOINTMENT_ITEM", "1"), ("HAS_MISSING_ACCOUNTING_UNIT", "0"), ("CONTACTNAME", "示範家屬"),
                ("CONTACTTEL", "0900000000"), ("CONTACTRELATION", "家人"), ("MAILADDRESS", "示範市測試路1號"),
                ("SENDINGADDRESS", "示範市測試路1號"), ("LIVEADDRESS", "示範市測試路1號"), ("ISSAMEADDRESS", "Y"), ("IS_VIP", "N"), ("IS_FAMILY", "N"));
            person["FLOW_COMPLETED_STEPS"] = statuses[i % statuses.Length] switch { "5" when i % 12 == 8 => 6, "5" => 1, "6" => 2 + i % 3, _ => 0 };
            person["FLOW_STARTED"] = statuses[i % statuses.Length] is "5" or "6" ? "1" : "0";
            data["HealthCheckupList"].Add(person);
            data["HealthCheckupPatient"].Add(new(person, StringComparer.OrdinalIgnoreCase));
            Dictionary<string, object?> Detail(params (string Key, object? Value)[] fields)
            {
                var row = Row(fields); row["BARCODESTR"] = barcode; row["PROJECTNO"] = project; return row;
            }
            data["FEMHCheckupList"].Add(Detail(("CHOP1DATE", $"{reservationDate.Year - 1911:D3}{reservationDate:MMdd}"),
                ("CHOP1TIME", "1"), ("CHOP1ROOM", "DEMO01"), ("INTOP1NO", i + 1), ("INTOP1ORDSEQ", 1), ("CHOP0DC", "0"), ("CHOP4ORDNO", "294-DEMO")));
            data["HealthCheckupNOTE"].Add(Detail(("FAMILYNOTE", "示範家族史"), ("NOTICENOTE", "此為假資料"), ("CURRENTNOTE", "示範備註"), ("PERMANENTNOTE", "示範資料")));
            data["HealthCheckupReserInfo"].Add(Detail(("RESERVE_AREA", Value(person, "WORKAREA")), ("RESERVE_DATE", DateTime.Today), ("RESERVE_TIME", "08:00"), ("PARKING", "N"), ("LAXATIVE", "N"), ("MEAL", "一般餐")));
            data["HealthCheckupPayment"].Add(Detail(("PACKAGE_AMOUNT", 3500), ("EXTRA_AMOUNT", 0), ("REMIT_AMOUNT", 0), ("DISCOUNT_AMOUNT", 0),
                ("ACCOUNT_AMOUNT", 3500), ("RECEIVED_AMOUNT", 3500), ("PAYMENT_DATE", DateTime.Today), ("PAYMENT_METHOD", "現金"), ("NOTE", "示範付款")));
            data["HealthCheckupSetInfo"].Add(Detail(("ITEMNO", "1"), ("LISITEMNAME", "294-DEMO"), ("ORDTOT", 1), ("SETNAME", "示範標準套餐"), ("AMOUNT", 3500), ("CHDCTTYPE", "00")));
            data["HealthCheckupOtherSet"].Add(Detail(("ITEMNO", "1"), ("SETCODE", "294-EXTRA"), ("SETNAME", "示範加選套餐"), ("AMOUNT", 500), ("CHDCTTYPE", "00")));
            data["HealthCheckupQuestInfo"].Add(Detail(("ITEMNO", "1"), ("QUESTIONNAIRE", "健康問卷"), ("AMOUNT", 0)));
        }
        Add("GenOrdBasicTbl", Row(("CHORDNO", "294-DEMO"), ("CHORDENAME", "示範標準套餐"), ("CHORDUNIT", "套"), ("SELF_PAY_AMOUNT", 3500), ("PRICE_DATE", DateTime.Today), ("ORDER_COUNT", 3)),
            Row(("CHORDNO", "294-EXTRA"), ("CHORDENAME", "示範加選套餐"), ("CHORDUNIT", "套"), ("SELF_PAY_AMOUNT", 500), ("PRICE_DATE", DateTime.Today), ("ORDER_COUNT", 1)));
        data["GenOrdBasicTbl"].Add(Row(("CHORDNO", "294-CARDIO"), ("CHORDENAME", "示範心血管套餐"), ("CHORDUNIT", "套"), ("SELF_PAY_AMOUNT", 6500), ("PRICE_DATE", DateTime.Today), ("ORDER_COUNT", 5)));
        data["GenOrdBasicTbl"].Add(Row(("CHORDNO", "294-METAB"), ("CHORDENAME", "示範代謝套餐"), ("CHORDUNIT", "套"), ("SELF_PAY_AMOUNT", 4800), ("PRICE_DATE", DateTime.Today), ("ORDER_COUNT", 6)));
        foreach (var item in data["CheckItem"])
            data["GenOrdBasicTbl"].Add(Row(("CHORDNO", item["CHECKNO"]), ("CHORDENAME", item["NAME"]), ("CHORDUNIT", "次"),
                ("SELF_PAY_AMOUNT", 200 + data["GenOrdBasicTbl"].Count * 50), ("PRICE_DATE", DateTime.Today), ("ORDER_COUNT", 1)));
        Add("GenMultiOrderTbl", data["CheckItem"].Take(3).Select(item => Row(("CHPRICENO", "294-DEMO"), ("CHPRICENOKID", item["CHECKNO"]), ("CHORDEXTNAME", item["NAME"]))).ToArray());
        foreach (var suite in new[] { (Code: "294-CARDIO", Items: new[] { "C004", "C014", "C015", "C016", "C029" }), (Code: "294-METAB", Items: new[] { "C002", "C013", "C014", "C015", "C020", "C021" }) })
            foreach (var code in suite.Items)
                data["GenMultiOrderTbl"].Add(Row(("CHPRICENO", suite.Code), ("CHPRICENOKID", code), ("CHORDEXTNAME", data["CheckItem"].First(item => Value(item, "CHECKNO") == code)["NAME"])));
        Add("OpdOrdTbl", Row(("SUITENAME", "示範標準套餐"), ("TOTAL", 3500), ("RECEIVABLE", 3500), ("DISCOUNT", 0), ("BOOKED", 3500), ("ARREARS", 0),
            ("ORDSEQ", "1"), ("CHOP4ORDNO", "C002"), ("CHOP4ORDNAME", "示範血糖檢查"), ("INTOP1ORDSEQ", "2"), ("CHOP4GREQNO", "DEMO-G001"), ("CHOP4REQNO", "DEMO-R001")));
        Add("RISWORKLISTDATAS", Row(("HISGREQNO", "DEMO-G001"), ("HISREQNO", "DEMO-R001"), ("ACCNO", "DEMO-IMG001"), ("REPORT_STATUS_CODE", "RPTVS"),
            ("REPORT_STATUS_NAME", "正式報告"), ("REPORTID", "DEMO-REPORT001"), ("MODIFYDR", "示範醫師甲"), ("MODIFYDATE", DateTime.Today)));
        Add("RISTEXTDATAS", Row(("ID", "1"), ("REPORTID", "DEMO-REPORT001"), ("ITEMNAME", "1"), ("LINENO", 1), ("TEXTVALUE", "示範影像報告：無明顯異常。此資料不供醫療用途。")));
        Add("ADMRQTRCPSWTBL", Row(("CHSTAT", "60"), ("CHSTAT_NAME", "報告確認"), ("CHMODNM", "示範醫師甲"), ("CHMODDTM", DateTime.Today)));
        Add("ADMRESULTTEXTSLISTBL", Row(("CHTXT", "示範特殊檢查報告：檢查已完成，此為虛構資料。")));
        Add("EKGReportTbl", Row(("AUTOSTATEMENT_NEW", "示範心電圖報告：正常竇性心律。"), ("REPORTSTATUS", "2"), ("LASTMODIFYDATE", DateTime.Today), ("OWNERUSERID", "示範醫師甲")));
        Add("AdmResultRpTbl", Row(("CHSTAT", "60"), ("CHSTAT_NAME", "報告確認"), ("CHRPSEQ", "1"), ("CHORDNO", "C002"), ("CHHEAD", "血糖"),
            ("CHNL", "70"), ("CHNH", "100"), ("CHVAL", "92"), ("VCHCORDNM", "空腹血糖"), ("CHMDDTM", DateTime.Today), ("CHRCPDTM", DateTime.Today),
            ("CHUNIT", "mg/dL"), ("CHITEMSEQ", "1"), ("CHLREQNO", "DEMO-L001"), ("LAB_NAME", "血糖 空腹血糖"), ("CHOP4GREQNO", "DEMO-G001"), ("CHOP4REQNO", "DEMO-R001")));
        (string Code, string Result, string Low, string High)[] labValues =
            [("C009", "6.2", "4", "10"), ("C011", "14.1", "12", "17"), ("C012", "256", "150", "400"),
             ("C013", "5.4", "4", "5.6"), ("C014", "185", "0", "200"), ("C015", "110", "0", "150"),
             ("C018", "24", "0", "40"), ("C020", "0.9", "0.6", "1.3")];
        for (var i = 0; i < labValues.Length; i++)
        {
            var sample = labValues[i]; var item = data["CheckItem"].First(r => Value(r, "CHECKNO") == sample.Code);
            data["AdmResultRpTbl"].Add(Row(("CHSTAT", "60"), ("CHSTAT_NAME", "報告確認"), ("CHRPSEQ", (i + 2).ToString()), ("CHORDNO", sample.Code),
                ("CHHEAD", item["NAME"]), ("CHNL", sample.Low), ("CHNH", sample.High), ("CHVAL", sample.Result), ("VCHCORDNM", item["ENAME"]),
                ("CHMDDTM", DateTime.Today), ("CHRCPDTM", DateTime.Today), ("CHUNIT", item["UNIT"]), ("CHITEMSEQ", (i + 2).ToString()),
                ("CHLREQNO", "DEMO-L001"), ("LAB_NAME", item["NAME"]), ("CHOP4GREQNO", "DEMO-G001"), ("CHOP4REQNO", "DEMO-R001")));
        }
        var potassium = data["CheckItem"].First(r => Value(r, "NAME") == "血鉀");
        data["AdmResultRpTbl"].Add(Row(("CHSTAT", "60"), ("CHSTAT_NAME", "報告確認"), ("CHRPSEQ", "10"), ("CHORDNO", potassium["CHECKNO"]),
            ("CHHEAD", "血鉀"), ("CHNL", "3.5"), ("CHNH", "5.1"), ("CHVAL", "4.2"), ("VCHCORDNM", "Potassium"),
            ("CHMDDTM", DateTime.Today), ("CHRCPDTM", DateTime.Today), ("CHUNIT", "mmol/L"), ("CHITEMSEQ", "10"),
            ("CHLREQNO", "DEMO-L001"), ("LAB_NAME", "血鉀"), ("CHOP4GREQNO", "DEMO-G001"), ("CHOP4REQNO", "DEMO-R001")));
        Add("DemoAccountSuites"); Add("DemoAccountOrders"); Add("DemoLabResults");
        string[] accountNames = ["基礎健檢", "心血管評估", "代謝檢查", "肝腎功能", "影像檢查", "腸胃篩檢", "健康諮詢", "加選檢查"];
        foreach (var registration in data["FEMHCheckupList"])
        {
            var number = Convert.ToInt32(registration["INTOP1NO"], CultureInfo.InvariantCulture);
            Dictionary<string, object?> RegistrationRow(params (string Key, object? Value)[] fields)
            {
                var row = Row(fields);
                foreach (var key in new[] { "BARCODESTR", "CHOP1DATE", "CHOP1TIME", "CHOP1ROOM", "INTOP1NO" }) row[key] = registration[key];
                return row;
            }
            for (var suite = 0; suite < 6 + number % 3; suite++)
            {
                var total = 1200 + suite * 650 + number * 35;
                var discount = (number + suite) % 3 == 0 ? total / 10 : (number + suite) % 3 == 1 ? total / 20 : 0;
                var booked = (number + suite) % 4 == 0 ? total / 3 : (number + suite) % 4 == 1 ? total / 2 : 0;
                var arrears = (number + suite) % 5 == 0 ? total / 5 : 0;
                data["DemoAccountSuites"].Add(RegistrationRow(("SUITENAME", $"示範{accountNames[suite]}組套"), ("ORDSEQ", (suite + 1).ToString()),
                    ("TOTAL", total), ("RECEIVABLE", total - discount - booked - arrears), ("DISCOUNT", discount), ("BOOKED", booked), ("ARREARS", arrears)));
                for (var child = 0; child < 3 + suite % 3; child++)
                {
                    var item = data["CheckItem"][(number + suite * 5 + child) % data["CheckItem"].Count];
                    data["DemoAccountOrders"].Add(RegistrationRow(("CHOP4ORDNAME", item["NAME"]), ("CHOP4ORDNO", item["CHECKNO"]), ("INTOP1ORDSEQ", (suite + 1).ToString())));
                }
            }
            foreach (var template in data["AdmResultRpTbl"])
            {
                var row = RegistrationRow(template.Select(p => (p.Key, p.Value)).ToArray());
                row["IS_CRITICAL"] = "0"; row["IS_ABNORMAL"] = "0";
                // Explicit fictional flags for six demo examinees, never a medical threshold rule.
                if (number is 1 or 2 or 3 or 7 or 8 or 9)
                {
                    var value = Value(row, "CHORDNO") switch { "C002" => "420", "C011" => "6.1", _ => Value(row, "CHORDNO") == Value(potassium, "CHECKNO") ? "6.8" : "" };
                    if (value.Length > 0) { row["CHVAL"] = value; row["IS_CRITICAL"] = "1"; row["IS_ABNORMAL"] = "1"; }
                }
                else if (number % 10 == 0 && Value(row, "CHORDNO") == "C002") { row["CHVAL"] = "145"; row["IS_ABNORMAL"] = "1"; }
                data["DemoLabResults"].Add(row);
            }
        }
        return data;
    }

    public DataTable GETDataTable(string sql, string dbconstr = "", bool TestConnection = false) => GETDataTable(sql, new Dictionary<string, object?>());
    public DataTable GETDbGenDataTable(string sql, IReadOnlyDictionary<string, object?> parameters, int commandTimeoutSeconds = 0, CancellationToken cancellationToken = default) =>
        GETDataTable(sql, parameters, cancellationToken: cancellationToken);

    public DataTable GETDataTable(string sql, IReadOnlyDictionary<string, object?> parameters, string dbconstr = "", bool TestConnection = false,
        int commandTimeoutSeconds = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (Gate)
        {
            var query = sql.ToUpperInvariant();
            var source = Source(query);
            var rows = Tables.TryGetValue(source, out var records) ? records.Select(r => new Dictionary<string, object?>(r, StringComparer.OrdinalIgnoreCase)).ToList()
                : throw new InvalidOperationException($"尚未設定示範資料：{source}");
            if (query.Contains("WHERE 1 = 0")) rows.Clear();
            if (source == "HealthCheckupList" || source == "CheckProjectScheme" || source == "HealthProjectList")
            {
                foreach (var row in rows)
                {
                    Merge(row, "HealthProjectList", "PROJECTNO"); Merge(row, "HealthCustomerList", "CUSTOMERNO");
                    if (source == "HealthCheckupList")
                    {
                        Merge(row, "FEMHCheckupList", "BARCODESTR");
                        row["RESERVE_STATUS"] = Value(row, "ISRESERVE");
                        row["STATUS_NAME"] = new DashboardExaminee { RawStatus = Value(row, "ISRESERVE") }.Status;
                        if (query.Contains("AS RESERVE_STATUS"))
                        {
                            var labs = Tables["DemoLabResults"].Where(r => Value(r, "BARCODESTR") == Value(row, "BARCODESTR"));
                            row["CRITICAL_COUNT"] = labs.Count(r => Value(r, "IS_CRITICAL") == "1");
                            row["ABNORMAL_COUNT"] = labs.Count(r => Value(r, "IS_ABNORMAL") == "1");
                        }
                    }
                }
            }
            if (query.Contains("預估人數"))
            {
                rows = Tables["HealthProjectList"].Select(r => new Dictionary<string, object?>(r, StringComparer.OrdinalIgnoreCase)).ToList();
                foreach (var row in rows)
                {
                    Merge(row, "HealthCustomerList", "CUSTOMERNO");
                    var people = Tables["HealthCheckupList"].Where(p => Value(p, "PROJECTNO") == Value(row, "PROJECTNO")).ToArray();
                    row["預估人數"] = people.Length;
                    foreach (var (col, status) in new[] { ("預約取消", "0"), ("預約人數", "1"), ("佔名額", "2"), ("已掛號", "4") })
                        row[col] = people.Count(p => Value(p, "ISRESERVE") == status);
                }
            }
            if (source == "HealthCheckupPatient")
                foreach (var row in rows)
                    foreach (var (alias, field) in new[] { ("CHMRNO", "PATNO"), ("病人姓名", "PATIENTNAME"), ("病人身份證", "IDNUMBER"), ("病人性別", "GENDER"), ("年齡", "AGE"), ("病人生日", "BIRTHDAY"), ("CHMRCELLULAR", "MOBILE"), ("CHTELH", "TEL") }) row[alias] = row.GetValueOrDefault(field) is DateTime d ? d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) : row.GetValueOrDefault(field);
            if (query.Contains("SYSTIMESTAMP")) return AsTable([Row(("DT_MS", DateTime.Now.ToString("yyyyMMddHHmmssfff")))]);
            rows = Filter(rows, query, parameters);
            if (query.Contains("AS ITEMCOUNT")) return AsTable([Row(("ITEMCOUNT", rows.Count))]);
            if (source == "ROLE_MASTER" && query.Contains("IS_ACTIVE = 1")) rows = rows.Where(r => Value(r, "IS_ACTIVE") == "1").ToList();
            if (source == "SYSTEM_FUNCTION")
            {
                var role = parameters.TryGetValue("ROLE_ID", out var code) ? Text(code) : "ADMIN";
                foreach (var row in rows)
                {
                    var permission = Tables["ROLE_PERMISSION"].FirstOrDefault(p => Value(p, "ROLE_ID") == role && Value(p, "FUNCTION_ID") == Value(row, "FUNCTION_ID"));
                    row["CAN_VIEW"] = permission?.GetValueOrDefault("CAN_VIEW") ?? (role == "ADMIN" ? 1 : 0);
                    row["CAN_REVIEW"] = permission?.GetValueOrDefault("CAN_REVIEW") ?? (role == "ADMIN" ? 1 : 0);
                }
            }
            if (query.Contains("SELECT DISTINCT CHECKCATEGORY")) rows = rows.DistinctBy(r => Value(r, "CHECKCATEGORY")).ToList();
            if (query.Contains("SELECT DISTINCT ITEMCATEGORY")) rows = rows.DistinctBy(r => Value(r, "ITEMCATEGORY")).ToList();
            if (parameters.ContainsKey("PAGE_SIZE"))
            {
                var count = rows.Count; var size = Convert.ToInt32(parameters["PAGE_SIZE"]);
                var page = Math.Clamp(Convert.ToInt32(parameters["REQUESTED_PAGE"]), 1, Math.Max(1, (int)Math.Ceiling((double)count / size)));
                rows = rows.Skip((page - 1) * size).Take(size).ToList();
                foreach (var row in rows) { row["TOTAL_COUNT"] = count; row["RESULT_PAGE"] = page; }
            }
            return AsTable(rows);
        }
    }

    private static string Source(string query)
    {
        // Prefer the outer query's domain over tables mentioned by joins/subqueries.
        if (query.Contains("SYSTIMESTAMP")) return "HealthCheckupList";
        if (query.Contains("ADMRESULTRPTBL")) return "DemoLabResults";
        if (query.Contains("AS SUITENAME")) return "DemoAccountSuites";
        if (query.Contains("SELECT CHOP4ORDNAME, CHOP4ORDNO")) return "DemoAccountOrders";
        if (query.Contains("RISWORKLISTDATAS")) return "RISWORKLISTDATAS";
        if (query.Contains("RISTEXTDATAS")) return "RISTEXTDATAS";
        if (query.Contains("ADMRQTRCPSWTBL")) return "ADMRQTRCPSWTBL";
        if (query.Contains("ADMRESULTTEXTSLISTBL")) return "ADMRESULTTEXTSLISTBL";
        if (query.Contains("EKGREPORTTBL")) return "EKGReportTbl";
        if (query.Contains("FROM SYSTEM_FUNCTION")) return "SYSTEM_FUNCTION";
        if (query.Contains("預估人數")) return "HealthProjectList";
        if (query.Contains("FROM HEALTHCHECKUPPATIENT") || query.Contains("OPDMRBASICTBL")) return "HealthCheckupPatient";
        if (query.Contains("CHECKUPLIST.*") || query.Contains("FROM HEALTHCHECKUPLIST H") || query.Contains("AS RESERVE_STATUS")) return "HealthCheckupList";
        if (query.Contains("FROM CHECKPROJECTSCHEME")) return "CheckProjectScheme";
        if (query.Contains("FROM CHECKPROJECTSPECIALITEMTBL")) return "CheckProjectSpecialItemTbl";
        if (query.Contains("FROM CHECKITEM")) return "CheckItem";
        if (query.Contains("GENMULTIORDERTBL") && !query.Contains("SELF_PAY_AMOUNT")) return "GenMultiOrderTbl";
        if (query.Contains("GENORDBASICTBL")) return "GenOrdBasicTbl";
        if (query.Contains("FROM HEALTHPROJECTLIST")) return "HealthProjectList";
        if (query.Contains("FROM GUID.GENUSERPROFILE1")) return "ACCOUNT";
        var match = Regex.Match(query, @"\bFROM\s+([A-Z_][A-Z_0-9]*)");
        return match.Success ? match.Groups[1].Value : "UNKNOWN";
    }

    private static void Merge(Dictionary<string, object?> row, string table, string key)
    {
        var related = Tables[table].FirstOrDefault(r => Value(r, key) == Value(row, key));
        if (related != null) foreach (var pair in related) row.TryAdd(pair.Key, pair.Value);
    }

    private static List<Dictionary<string, object?>> Filter(List<Dictionary<string, object?>> rows, string sql, IReadOnlyDictionary<string, object?> args)
    {
        // Apply registration keys to account/lab records so each examinee has their own details.
        foreach (var key in new[] { "CHOP1DATE", "CHOP1TIME", "CHOP1ROOM", "INTOP1NO", "BARCODESTR", "PROJECTNO", "CUSTOMERNO", "ROLE_ID", "CHPRICENO", "SELFCOSTNO", "ISRESERVE", "RESERVE_STATUS", "RESERVESTATUS", "WORKAREA", "RESERVATION_AREA", "ORDER_CODE", "ORDER_NAME", "PROJECT_NAME", "PROJECT_NO", "PACKAGE_NAME", "SCHEME_CODE", "CUSTOMER_NAME" })
        {
            if (!args.TryGetValue(key, out var raw) || Text(raw).Trim('%') is not { Length: > 0 } wanted) continue;
            var col = key switch { "SELFCOSTNO" => "CHDCTTYPE", "RESERVE_STATUS" or "RESERVESTATUS" => "ISRESERVE", "RESERVATION_AREA" => "WORKAREA", "ORDER_CODE" => "CHORDNO", "ORDER_NAME" => "CHORDENAME", "PROJECT_NAME" => "PROJECTNAME", "PROJECT_NO" => "PROJECTNO", "PACKAGE_NAME" => "NAME", "SCHEME_CODE" => "SCHEMECODE", "CUSTOMER_NAME" => "CUSTOMERNAME", _ => key };
            if (col == "WORKAREA") wanted = Tables["HealthRESERVEAREATBL"].Where(r => Value(r, "AREA_CODE") == wanted).Select(r => Value(r, "AREA_NAME")).FirstOrDefault() ?? wanted;
            if (!rows.Any(r => r.ContainsKey(col))) continue;
            rows = rows.Where(r => raw is string s && s.Contains('%') ? Value(r, col).Contains(wanted, StringComparison.OrdinalIgnoreCase) : Value(r, col).Equals(wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (args.TryGetValue("KEYWORD", out var keyword) && Text(keyword).Trim('%') is { Length: > 0 } term)
            rows = rows.Where(r => r.Values.Any(v => Text(v).Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (Match m in Regex.Matches(sql, @"\b(?:[A-Z]+\.)?(CUSTOMERNO|PROJECTNO|CHECKNO|PATNO|IDNUMBER|PATIENTNAME|MOBILE|CHECKCATEGORY)\s*(=|LIKE)\s*'((?:[^']|'')*)'"))
        {
            var field = m.Groups[1].Value; var termValue = m.Groups[3].Value.Replace("''", "'").Trim('%');
            if (rows.Any(r => r.ContainsKey(field))) rows = rows.Where(r => m.Groups[2].Value == "LIKE" ? Value(r, field).Contains(termValue, StringComparison.OrdinalIgnoreCase) : Value(r, field).Equals(termValue, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        foreach (var key in new[] { "START_DATE", "END_DATE", "END_DATE_EXCLUSIVE", "RESERVEDATE_START", "RESERVEDATE_END", "CHECK_DATE_START", "CHECK_DATE_END", "QUERY_START_DATE", "QUERY_END_DATE" })
        {
            var dateColumn = key.StartsWith("CHECK_DATE", StringComparison.Ordinal) || key.StartsWith("QUERY_", StringComparison.Ordinal)
                ? "CHECKDATE" : rows.Any(r => r.ContainsKey("RESERVEDATE")) ? "RESERVEDATE" : "CHECKDATE";
            if (!args.TryGetValue(key, out var raw) || Unwrap(raw) is not DateTime date || !rows.Any(r => r.ContainsKey(dateColumn))) continue;
            var end = key.Contains("END"); var exclusive = key.Contains("EXCLUSIVE") || (key == "RESERVEDATE_END" && sql.Contains("< :RESERVEDATE_END"));
            rows = rows.Where(r => !DateTime.TryParse(Value(r, dateColumn), CultureInfo.InvariantCulture, out var d) || (end ? exclusive ? d < date : d.Date <= date.Date : d.Date >= date.Date)).ToList();
        }
        if (sql.Contains("ISRESERVE IN ('4', '5')") || sql.Contains("TRIM(ISRESERVE) IN ('4', '5')") || sql.Contains("TRIM(CHECKUPLIST.ISRESERVE) IN ('4', '5')")) rows = rows.Where(r => Value(r, "ISRESERVE") is "4" or "5").ToList();
        if (sql.Contains("TRIM(H.ISRESERVE) NOT IN ('1', '0', '2')")) rows = rows.Where(r => Value(r, "ISRESERVE") is "4" or "5" or "6").ToList();
        if (sql.Contains("TRIM(ISRESERVE) NOT IN ('0', '2')")) rows = rows.Where(r => Value(r, "ISRESERVE") is not ("0" or "2")).ToList();
        foreach (Match m in Regex.Matches(sql, @"UPPER\((?:TRIM\()?([A-Z]+)\)?\)\s+LIKE\s+'%((?:[^']|'')*)%'"))
        {
            var field = m.Groups[1].Value; var searchTerm = m.Groups[2].Value.Replace("''", "'");
            if (field == "CHECKCATEGORY") rows = rows.Where(r => Value(r, field).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            else if (field == "CHECKNO") rows = rows.Where(r => new[] { "CHECKNO", "NAME", "ENAME", "ITEMCATEGORY", "REPORTNAME" }.Any(k => Value(r, k).Contains(searchTerm, StringComparison.OrdinalIgnoreCase))).ToList();
        }
        var inList = Regex.Match(sql, @"UPPER\(TRIM\(CHECKNO\)\)\s+IN\s*\(([^)]+)\)");
        if (inList.Success)
        {
            var codes = Regex.Matches(inList.Groups[1].Value, "'([^']+)'").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            rows = rows.Where(r => codes.Contains(Value(r, "CHECKNO"))).ToList();
        }
        if (args.TryGetValue("ORDER_INITIAL", out var initial)) rows = rows.Where(r => Value(r, "CHORDNO").StartsWith(Text(initial), StringComparison.OrdinalIgnoreCase)).ToList();
        if (sql.Contains("LIKE '294-%'")) rows = rows.Where(r => Value(r, "CHORDNO").StartsWith("294-", StringComparison.OrdinalIgnoreCase)).ToList();
        if (sql.Contains("'^[0-9]'")) rows = rows.Where(r => char.IsAsciiDigit(Value(r, "CHORDNO").FirstOrDefault())).ToList();
        return rows;
    }

    private static DataTable AsTable(IEnumerable<Dictionary<string, object?>> records)
    {
        var rows = records.ToArray(); var table = new DataTable { Locale = CultureInfo.InvariantCulture };
        foreach (var column in rows.SelectMany(r => r.Keys).Distinct(StringComparer.OrdinalIgnoreCase)) table.Columns.Add(column, typeof(object));
        foreach (var record in rows)
        {
            var row = table.NewRow(); foreach (var pair in record) row[pair.Key] = pair.Value is DateTime d ? d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) : pair.Value ?? DBNull.Value; table.Rows.Add(row);
        }
        return table;
    }

    public int ExecuteNonQuery(string sql) => ExecuteNonQuery(sql, new Dictionary<string, object?>());
    public int ExecuteNonQuery(string sql, string dbconstr = "", bool TestConnection = true) => ExecuteNonQuery(sql);
    public int ExecuteNonQuery(string sql, IReadOnlyDictionary<string, object?> parameters, string dbconstr = "", bool TestConnection = true)
    {
        lock (Gate) return Write(sql, parameters);
    }
    public int[] ExecuteNonQueryTransaction(IReadOnlyList<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> commands, string dbconstr = "", bool TestConnection = true) =>
        ExecuteNonQueryTransaction(commands.Select(c => new OracleTransactionCommand(c.Sql, c.Parameters)).ToArray(), dbconstr, TestConnection);
    public int[] ExecuteNonQueryTransaction(IReadOnlyList<OracleTransactionCommand> commands, string dbconstr = "", bool TestConnection = true)
    {
        lock (Gate)
        {
            var backup = Tables.ToDictionary(p => p.Key, p => p.Value.Select(r => new Dictionary<string, object?>(r, StringComparer.OrdinalIgnoreCase)).ToList(), StringComparer.OrdinalIgnoreCase);
            try
            {
                return commands.Select(c => { var count = Write(c.Sql, c.Parameters); if (count == 0 && !c.AllowZeroAffectedRows) throw new InvalidOperationException("找不到要更新的示範資料。"); return count; }).ToArray();
            }
            catch { Tables.Clear(); foreach (var pair in backup) Tables.Add(pair.Key, pair.Value); throw; }
        }
    }

    private static int Write(string sql, IReadOnlyDictionary<string, object?> args)
    {
        var statement = sql.Trim().ToUpperInvariant();
        var match = Regex.Match(statement, @"\b(?:INSERT\s+INTO|UPDATE|DELETE(?:\s+FROM)?|MERGE\s+INTO)\s+([A-Z_][A-Z_0-9]*)");
        if (!match.Success || !Tables.TryGetValue(match.Groups[1].Value, out var records)) throw new InvalidOperationException("尚未設定此示範資料的儲存方式。");
        var fields = args.ToDictionary(p => p.Key, p => Unwrap(p.Value), StringComparer.OrdinalIgnoreCase);
        if (fields.Remove("EMP_NO", out var emp)) fields["H_EMPNO"] = emp;
        if (statement.StartsWith("INSERT") && statement.Contains("INTO ROLE_PERMISSION"))
        {
            foreach (var function in Tables["SYSTEM_FUNCTION"]) records.Add(Row(("ROLE_ID", fields.GetValueOrDefault("ROLE_ID")), ("FUNCTION_ID", function["FUNCTION_ID"]), ("CAN_VIEW", 0), ("CAN_REVIEW", 0)));
            return Tables["SYSTEM_FUNCTION"].Count;
        }
        if (statement.StartsWith("INSERT"))
        {
            if (statement.Contains("INTO ROLE_MASTER")) fields["IS_ACTIVE"] = 1;
            records.Add(fields); return 1;
        }
        var where = statement.Contains("WHERE") ? statement[(statement.IndexOf("WHERE", StringComparison.Ordinal) + 5)..] : statement;
        var predicates = Regex.Matches(where, @"\b([A-Z_][A-Z_0-9]*)\s*=\s*:([A-Z_][A-Z_0-9]*)")
            .Where(m => args.ContainsKey(m.Groups[2].Value)).Select(m => (Column: m.Groups[1].Value, Parameter: m.Groups[2].Value)).ToList();
        if (statement.StartsWith("MERGE"))
        {
            string[] mergeKeys = match.Groups[1].Value switch
            {
                "HEALTHCHECKUPPATIENT" => ["PATNO"],
                "FEMHCHECKUPLIST" => ["BARCODESTR", "PROJECTNO", "INTOP1NO"],
                "ROLE_PERMISSION" => ["ROLE_ID", "FUNCTION_ID"],
                "HEALTHCHECKUPNOTE" or "HEALTHCHECKUPRESERINFO" or "HEALTHCHECKUPPAYMENT" => ["BARCODESTR", "PROJECTNO"],
                _ => throw new InvalidOperationException("尚未設定此示範資料的更新索引。")
            };
            predicates = mergeKeys.Select(k => (Column: k, Parameter: k)).ToList();
            if (match.Groups[1].Value == "FEMHCHECKUPLIST") fields["CHOP0DC"] = "0";
        }
        var matches = records.Where(r => predicates.All(p => Value(r, p.Column) == Text(args.GetValueOrDefault(p.Parameter)))).ToList();
        var expectedStatus = Regex.Match(where, @"(?:TRIM\()?ISRESERVE\)?\s*=\s*'([^']*)'");
        if (expectedStatus.Success) matches = matches.Where(r => Value(r, "ISRESERVE") == expectedStatus.Groups[1].Value).ToList();
        foreach (Match literal in Regex.Matches(where, @"\b(CUSTOMERNO|H_EMPNO|BARCODESTR)\s*=\s*'([^']*)'")) matches = matches.Where(r => Value(r, literal.Groups[1].Value) == literal.Groups[2].Value).ToList();
        if (statement.StartsWith("DELETE")) { foreach (var row in matches) records.Remove(row); return matches.Count; }
        if (statement.StartsWith("MERGE") && matches.Count == 0) { records.Add(fields); return 1; }
        foreach (var row in matches)
        {
            foreach (var field in fields) row[field.Key] = field.Value;
            var assignments = statement.Split("WHERE", 2)[0];
            foreach (Match assignment in Regex.Matches(assignments, @"\b([A-Z_][A-Z_0-9]*)\s*=\s*:([A-Z_][A-Z_0-9]*)"))
                if (args.TryGetValue(assignment.Groups[2].Value, out var value)) row[assignment.Groups[1].Value] = Unwrap(value);
            foreach (Match literal in Regex.Matches(assignments, @"\b(ISRESERVE|CHOP0DC)\s*=\s*(?:'([^']*)'|([0-9]+))")) row[literal.Groups[1].Value] = literal.Groups[2].Success ? literal.Groups[2].Value : literal.Groups[3].Value;
            if (statement.Contains("CHECKINTIME = SYSDATE")) { row["CHECKINTIME"] = DateTime.Now.ToString("HH:mm"); row["CHECKINDATE"] = DateTime.Today.ToString("yyyy/MM/dd"); }
            if (statement.Contains("CHECKINTIME = NULL")) { row["CHECKINTIME"] = ""; row["CHECKINDATE"] = ""; }
        }
        return matches.Count;
    }
}
